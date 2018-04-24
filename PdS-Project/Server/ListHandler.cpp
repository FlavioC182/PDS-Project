#include "ListHandler.hpp"
#define MAXSTR 100
#define MAXEXT 10
#define pair std::pair<DWORD, ApplicationItem>
#define SHIFT 1
#define CTRL 2
#define ALT 4

/*
Funzione che viene richiamata per ogni finestra rilevata da EnumWindow() (vedi dopo)
Se la finestra non è visibile o se il suo nome è vuoto, ritorna subito;
altrimenti crea una struttura ApplicationItem con i parametri della finestra
e la inserisce nella lista passata come parametro. Ciò viene fatto per ogni finestra (applicazione) aperta
*/

BOOL CALLBACK MyWindowProc(__in HWND hwnd, __in LPARAM lparam) {
	
	/* Finestra non visibile */
	if (!IsWindowVisible(hwnd)) {
		return TRUE;
	}

	/* Ottenimento del pid del processo e verifica che esso sia stato già inserito nella lista delle applicazioni */

	DWORD procID;
	GetWindowThreadProcessId(hwnd, &procID);		// ottenimento del pid

	// se find() = end() significa che la pair con key "proc" (il pid) non è presente nella lista
	if (((std::map<DWORD, ApplicationItem>*) lparam)->find(procID) != ((std::map<DWORD, ApplicationItem>*)lparam)->end())
		return TRUE;

	/* Arrivati qui significa che il processo non è presente nella lista 
	* Si vuole ottenere l'handle del processo tramite il pID ottenuto, ottenendo i giusti permessi per poter ottenere le informazioni sul nome
	*/
	
	HANDLE process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, procID);
	if (process == NULL)
		return TRUE;
	
	TCHAR* file_name = new TCHAR[MAXSTR];
	DWORD maxstr = MAXSTR;
	/* la funzione QueryFullProcessImageName prende l'handle del process, e estrae il path del processo, salvandolo in file_name, riuscendoci grazie ai "diritti" definiti con OpenProcess */

	if (QueryFullProcessImageName(process, 0, file_name, &maxstr) == 0 || maxstr >= MAXSTR) {
		CloseHandle(process);
		delete[] file_name;
		return TRUE;
	}

	/* Dopo essere riusciti ad estrarre il path dell'applicazione, posso aggiungerla alla lista */
	ApplicationItem app;

	TCHAR* buff = new TCHAR[MAXSTR + 1];
	TCHAR* ext = new TCHAR[MAXEXT + 1];

	/* Prendiamo dal nome completo del file il nome dell'eseguibile
	*  gli altri due parametri sono a NULL e 0 perché non servono quelle informazioni
	*  vedendo la documentazione, in buff ci finisce il nome del file senza estensione, in ext ci finisce l'estensione
	*  se torna 0 ha avuto successo
	*/

	if (splitpath(file_name, NULL, 0, NULL, 0, buff, MAXSTR, ext, MAXEXT) != 0) {
		delete[] buff;
		delete[] ext;
		delete[] file_name;
		CloseHandle(process);
		return TRUE;
	}

	/* il campo Name dell'appplicazione lo costruisco come nome file + estensione */
	app.Name = buff;
	app.Name += ext;
	app.Exec_name = file_name;

	((std::map<DWORD, ApplicationItem>*)lparam)->insert(pair(procID, app));
	delete[] buff; delete[] ext; delete[] file_name;
	CloseHandle(process);

	return TRUE;
}

/*
Creazione della lista da zero, semplicemente richiamando la funzione EnumWindows()
Alla funzione viene passata la callback e la lista
*/

void ListHandler::buildList(std::map<DWORD,ApplicationItem>& ApplicationList) {

	ApplicationList.clear();

	/* Per ogni applicazione in foreground eseguiamo la MyWindowsProc passando la lista delle app
	*  Enumera tutte le top-level windows sullo schermo passando l'handle ad ogni window, a turno, ad una application-defined callback function.
	*  EnumWindows continua finché l'ultima top-level window non viene enumerata o se la callback function ritorna FALSE (per questo restituisce true la func mywind).
	*/
	if (!EnumWindows(MyWindowProc, (LPARAM)&ApplicationList))
		throw std::runtime_error("Fallimento nella enumerazione delle Windows");
}

/*
* Funzione principale della classe ListHandler, eseguita dal thread che gestisce la lista.
* Fino a che il programma non viene terminato, viene richiesta una nuova lista di applicazioni ogni refreshTime millisecondi; 
* questa lista viene confrontata con quella del ListManager per determinare i programmi nuovi e quelli terminati, per
* poi sostituire la vecchia lista. I dati poi devono essere inviati al client.
*/

void ListHandler::UpdateAppList() {
	
	std::map<DWORD, ApplicationItem> newList;
	DWORD newForeground = 0;
	int count = 0;

	/* il ciclo viene interrotto quando il client chiude la connessione */
	
	while (socket.getStatus() == true) {
		count++;
		buildList(newList);		//lista temporanea

		/* Creazione della strutture delle modifiche da inviare al Client */
		
		for each(pair app in newList) {
			std::map<DWORD, ApplicationItem>::iterator i = applicationsList.find(app.first);
			if (i != applicationsList.end()) {
				//L'applicazione esiste già nella lista applicationsList, percui la cancello (non sarà una modifica da inviare)
				applicationsList.erase(i);
			}
			else {
				/* In caso contrario, significa che c'è una nuova applicazione che prima non era presente, percui bisogna aggiungere la modifica di tipo add */
				Change	c(app.first,app.second);
				changeList.push_back(c);
				count = 0;
			}
		}

		/* A questo punto si aggiungono le le modifiche di tipo remove per tutte le applicazioni terminate 
		* (non trovate nella lista nuova, e che sono state lasciate in applicationsList nel for each precedente)
		*/

		for each(pair app in applicationsList) {
			Change c(rem, app.first);
			changeList.push_back(c);
			count = 0;
		}

		/* Memorizzo la nuova lista */
		applicationsList.swap(newList);

		/* vedo se è cambiata l'applicazione col focus
		*  la funzione GetForegroundWindow() prende (restituisce) l'HANDLE della window in foreground
		*  la funzione getwindowthreadprocessID mi salva dentro newForeground il pid del HANDLE della window in foreground
		*/

		GetWindowThreadProcessId(GetForegroundWindow(), &newForeground);
		if (newForeground != focusedApplication) {				// focusedApplication: PID della window in foreground(see Application.hpp)
			focusedApplication = newForeground;
			Change c(chf, focusedApplication);
			changeList.push_back(c);
			count = 0;
		}

		if (count == 10) {
			count = 0;
			Change c(heartbeat, 0);
			changeList.push_back(c);
		}

		/* invio modifiche al client */
		sendToClient();

		/* il thread è messo in pausa per tot millisecondi */
		std::this_thread::sleep_for(std::chrono::microseconds(refreshTime));
	}

}

void ListHandler::setRefreshTime(unsigned long time) {
	if (time > 500 && time < 10000) {
		refreshTime = time;
	}
}

/* Invio della lista al client */

void ListHandler::sendToClient() {
	
	if (changeList.empty())
		return;

	char* send_buf = nullptr;
	int length = 0;
	bool noIcon = false;
	int nOfChange = changeList.size();

	try {
		for each(Change c in changeList) {

			/* tipo di modifica + pid :sono le info da inviare sempre per tutti i tipi di modifica */
			
			send_buf =c.getSerializedChangeType(length);	//see Change.cpp
			if (send_buf != nullptr) {
				socket.sendData(send_buf, length);
				length = 0;
				free(send_buf);
			}

			/* Modifica ADD: solo se è un Modification di tipo add il getSerializedName restituisce un valore diverso da nullptr 
			* La modifica di tipo add prevede molto più lavoro, in quanto bisogna inviare il nome dell'applicazione e l'icona
			*/
			send_buf = c.getSerializedName(length);
			if (send_buf != nullptr) {
				u_long length_net = htonl(u_long(length));				// see SocketStream.cpp (traduzione in formato per la network)
				socket.sendData(((char*) &length_net), sizeof(int));		// invio dimensione (lunghezza) del nome dell'applicazione aggiunta
				socket.sendData(send_buf,length);						// invio del nome dell'applicazione
				length = 0;
				free(send_buf);

				/* invio dell'icona */
				send_buf = c.getSerializedIcon(length);
				if (send_buf != nullptr) {
					u_long length_net = htonl(u_long(length));			// see SocketStream.cpp (traduzione in formato per la network)
					socket.sendData(((char*) &length_net), sizeof(int));	// invio dimensione dell'icona dell'applicazione aggiunta
					socket.sendData(send_buf, length);					// invio dell'icona
					length = 0;
					free(send_buf);
				}
				else {													// Non è stato allocato nessun buffer => no free in caso di eccezione in sendData.
					length = 0;
					noIcon = true;
					socket.sendData(((char*)&length), sizeof(int));
				}
			}

		}

		/* al termine dell'invio cancello la lista */
		changeList.clear();
	}
	catch (std::overflow_error& e) {
		std::wcerr << e.what() << std::endl;
		// la memcpy_s fallisce dentro getSerializedName
		// forziamo la chiusura della connessione
		socket.closeConnection();
		changeList.clear();			// la lista è disponibile per altre connessioni
		socket.setStatus(false);	// termina il metodo UpdateAppList
	}
	catch (socket_exception) {

	}
	catch (std::exception& e) {
		std::wcerr << e.what() << std::endl;

		/* si rilasciano eventuali risorse quando la send fallisce */
		if (!noIcon)
			free(send_buf);

		changeList.clear();			// lista disponibile per altre connessioni

		socket.setStatus(false);	// durante l'invio c'è stato un errore, il ciclo dentro UpdateAppList termina.	
	}
}

/* metodo gestito da un thread secondario (sganciato dal ThreadManager nella funzione ServerManagement)
*  si occupa di attendere i comandi del client, li decifra, e li invia all'applicazione in foreground come input
*/

void CommandsFromClient(ListHandler* listHandler, SocketStream* s) {
	
	char buffer[1 + sizeof(int)];		// 1 byte per i modificatori e 4 byte per il messaggio key inviato (che è di tipo ulong)
	INPUT input[8];						// al più 4 pressioni + 4 rilasci di tasti (3 modificatori e un key).
	int nOfInput = 0;

	/* Modificatori da gestire */
	INPUT CtrlDown, ShiftDown, AltDown, CtrlUp, ShiftUp, AltUp;
	INPUT KeyDown, KeyUp;

	CtrlDown.type = ShiftDown.type = AltDown.type = KeyDown.type = INPUT_KEYBOARD;						//	evento INPUT_KEYBOARD
	CtrlUp.type = ShiftUp.type = AltUp.type = KeyUp.type = INPUT_KEYBOARD;

	ShiftUp.ki.dwFlags = CtrlUp.ki.dwFlags = AltUp.ki.dwFlags = KeyUp.ki.dwFlags = KEYEVENTF_KEYUP;		// evento: il key viene rilasciato
	ShiftDown.ki.dwFlags = CtrlDown.ki.dwFlags = AltDown.ki.dwFlags = KeyDown.ki.dwFlags = 0;			// evento: il key viene premuto

	ShiftUp.ki.time = CtrlUp.ki.time = AltUp.ki.time = KeyUp.ki.time = 0 ;								// timestamp: il sistema usa il proprio timestamp
	ShiftDown.ki.time = CtrlDown.ki.time = AltDown.ki.time = KeyDown.ki.time = 0;		

	ShiftUp.ki.dwExtraInfo = CtrlUp.ki.dwExtraInfo = AltUp.ki.dwExtraInfo = KeyUp.ki.dwExtraInfo = 0;	// non ci sono info addizionali
	ShiftDown.ki.dwExtraInfo = CtrlDown.ki.dwExtraInfo = AltDown.ki.dwExtraInfo = KeyDown.ki.dwExtraInfo = 0;

	ShiftDown.ki.wVk = ShiftUp.ki.wVk = VK_SHIFT;														// associazione con il proprio key modificatore
	CtrlDown.ki.wVk = CtrlUp.ki.wVk = VK_CONTROL;
	AltDown.ki.wVk = AltUp.ki.wVk = VK_MENU;

	try {
		/* rimaniamo in attesa dei comandi finché la connessione non viene chiusa */
		while (s->receiveData(buffer, 1 + sizeof(int)) != 0) {
			char modifier = buffer[0];				// lettura il primo byte dal buffer che rappresenta la concatenazione di uno o più modificatori
			int key = ntohl(*((u_long*)&buffer[1]));// lettura tasto premuto
			std::wcout << "Input dal client: " << key << ", modifier: " << (u_short)modifier << std::endl;

			nOfInput = 0;
			/* in input salviamo i modificatori premuti (ricevuti dal client) */
			if ((modifier & SHIFT) != 0)
				input[nOfInput++] = ShiftDown;
			if ((modifier & CTRL) != 0)
				input[nOfInput++] = CtrlDown;
			if ((modifier & ALT) != 0)
				input[nOfInput++] = AltDown;

			/* concateniamo sia la pressione sia il rilascio del tasto */
			KeyDown.ki.wVk = KeyUp.ki.wVk = key;	// l'associazione è fatta ad hoc in base al tasto ricevuto dal client. 
			input[nOfInput++] = KeyDown;
			input[nOfInput++] = KeyUp;

			/* concateniamo i rilasci dei modificatori eventualmente premuti */
			if ((modifier & SHIFT) != 0)
				input[nOfInput++] = ShiftUp;
			if ((modifier & CTRL) != 0)
				input[nOfInput++] = CtrlUp;
			if ((modifier & ALT) != 0)
				input[nOfInput++] = AltUp;

			/* funzione che invia direttamente all'app in foreground un vettore con i modificatori selezionati */
			int res = SendInput(nOfInput, input, sizeof(INPUT));
		}	
	}
	catch (std::exception& e) {
		std::wcerr << "Client close connection: " << e.what() << std::endl;
	}
	s->setStatus(false);
}

/* Funzione principale, entry point del thread ThreadManager, che si occupa della gestione della lista dell'applicazioni in esecuzione, ovvero:
* 1. generazione ed aggiornamento della lista delle applicazioni attive.
* 2. invio al Client degli aggiornamenti (modifiche) delle applicazioni attive.
* 3. ascolto e ricezione dei comandi inviati dal Client (CommandsFromClient: tramite la generazione di un thread secondario).
*/

void serverManagementList(SocketStream& socket, std::atomic_bool& continua) {

	try {
		/* finché continua è a true il server rimane attivo in comunicazione con il Client o attesa di esso */
		
		while (continua) {
			
			socket.waitingForConnection();		// server in attesa di connessione con il client
			
			ListHandler listHandler(socket);	// creazione dell'istanza listHandler che gestirà lista delle applicazioni
			
			std::thread ThreadListener(CommandsFromClient, &listHandler, &socket);		//thread secondario che gestisce i messaggi in ricezione dal client

			std::wcout << "Inizio del servizio Client" << std::endl;

			listHandler.UpdateAppList();		//il ThreadManager si occupa di questo metodo finché non termina la connessione

			std::wcout << "Fine della routine del servizio Client" << std::endl;

			ThreadListener.join();				// si attende la terminazione del thread ThreadListener (che terminerà non ricevendo più dati dal Client)

			socket.closeConnection();
		}

	}
	catch (socket_exception) {
		PostQuitMessage(-10);
	}
	catch (std::exception& e) {
		std::cerr << e.what() << std::endl;
	}
}
