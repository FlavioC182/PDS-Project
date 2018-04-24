#include "resource.h"
#include "ListHandler.hpp"
#define PORT 2000
//per debugging della memoria
#define _CRTDBG_MAP_ALLOC 
#include <stdlib.h> 
#include <crtdbg.h>
//per wcout in main di prova
#include <fcntl.h>
#include <io.h>
#include <stdarg.h>

/*
* Server con 3 thread: uno per l'interfaccia, uno per l'invio della lista e l'altro per gestire i comandi
*/

/*
* In linea generale, per creare una applicazione desktop si fa uso della procedura Main, detta WinMain, che funge da entry point.
* Oltre alla funzione WinMain, ogni applicazione desktop di Windows deve avere anche una funzione routine di finestra.
* Questa funzione viene in genere denominata WndProc, ma nel nostro caso WindowProcedure. Questa funzione deve essere definita per determinare
* come l'applicazione risponderà ai vari eventi. Si occupa quindi di gestire i molti messaggi ricevuti da un'applicazione dal sistema operativo.
* In sostanza permette al S.O. WINDOWS di comunicare con la nostra applicazione.
* Ad esempio, in un'applicazione con una finestra di dialogo con un pulsante OK, quando l'utente fa clic sul pulsante, il sistema operativo
* invia all'applicazione un messaggio per segnalare che è stato fatto clic sul pulsante.WndProc è responsabile della risposta a questo evento.
*/

HWND Hwnd;
HMENU Hmenu;
NOTIFYICONDATA NotifyIconData;
TCHAR Advise[64] = TEXT("Server: In Esecuzione");
TCHAR ClassName[] = TEXT("Server");
TCHAR Message[] = TEXT("Il Server è eseguito in background.\n Per terminare l'esecuzione fare click sull'icona nella ToolBar Area");
LRESULT CALLBACK WindowProc(HWND, UINT, WPARAM, LPARAM);
int InitNotifyIconData();

/*Parametri della WinMain:
	* HINSTANCE hThisInstance: è l'handle all'istanza di applicazione, dove un'istanza di applicazione, non è altro che una singola esecuzione 
	  della nostra applicazione (duale al concetto di oggetto e classe). Infatti, creare una applicazione è equivalente a creare un'istanza di essa
	* L'instanza viene usata da Windows come un riferimento alla nostra applicazione, per gestire gli eventi, il processamento di messaggi, 
	* e altro ancora. (Contiene la window procedure per la classe).
	* hPrevInstance: sempre NULL.
	* lpszArgument (detta anche lpCmdLine): is a pointer string that is used to hold any command - line arguments that may have been specified 
	  when the application began.For example, if the user opened the Run application and typed myapp.exe myparameter 1, then lpCmdLine would 
	  be myparameter 1.
	* nCmdShow - is the parameter that determines how your application's window will be displayed once it begins executing.

	* All'interno del WinMain devono essere effettuati tipicamente 4 steps:
		1.	Window-class setup
		2.	Window-class registration
		3.	Window Creation
		4.	Message loop with event handler
*/

int WINAPI WinMain(HINSTANCE hThisInstance, HINSTANCE hPrevInstance, LPSTR lpszArgument, int nCmdShow) {
	
	//The _crtDbgFlag flag consists of five bit fields that control how memory allocations on the debug version of the heap are tracked, 
	// verified, reported, and dumped. The bit fields of the flag are set using the _CrtSetDbgFlag function

	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF); //per debug di memoria
	_setmode(_fileno(stdout), _O_U16TEXT); //per evitare problemi in wcout

	MSG message;	//messaggio ricevuto dall'applicazione
	
	/* Nella funzione WinMain bisogna creare una struttura della classe della finestra di tipo WNDCLASSEX.
	* Questa struttura contiene informazioni sulla finestra, ad esempio l'icona dell'applicazione, il colore di sfondo della finestra,
	* il nome da visualizzare nella barra del titolo, il nome della funzione della routine della finestra e così via.
	*/
	WNDCLASSEX WndClx;	//classe dell'applicazione

	WndClx.hInstance = hThisInstance;	//handle dell'istanza che contiene la window procedure per la classe
	WndClx.lpszClassName = ClassName;
	WndClx.lpfnWndProc = WindowProc;   //Procedura per la gestione dei messaggi: si setta con il puntatore della WindowProcedure che gestisce la finestra
	WndClx.style = CS_DBLCLKS;
	WndClx.cbSize = sizeof(WNDCLASSEX);

	/* Setting dell'icona (standard) della finestra: */
	WndClx.hIcon = LoadIcon(GetModuleHandle(NULL),MAKEINTRESOURCE(IDI_ICON1));
	if (WndClx.hIcon == NULL)
		return -1;

	/* Setting dell'icona small */
	WndClx.hIconSm = LoadIcon(GetModuleHandle(NULL), MAKEINTRESOURCE(IDI_ICON1));
	if (WndClx.hIconSm == NULL)
		return -1;

	/* Setting Cursore di default */
	WndClx.hCursor = LoadCursor(NULL,IDC_ARROW);
	if (WndClx.hCursor == NULL)
		return -1;

	/* Setting finestra */
	WndClx.lpszMenuName = NULL;
	WndClx.cbClsExtra = 0;
	WndClx.cbWndExtra = 0;
	WndClx.hbrBackground = (HBRUSH)(CreateSolidBrush(RGB(255,255,255))); //colore finestra di bianco

	/* Registrazione della classe della nostra applicazione l'interno del S.O. */
	if (!RegisterClassEx(&WndClx))
		return -1;

	/* Dopo il setup della window class, si crea la window */

	Hwnd = CreateWindowEx(0,
		ClassName,				// Nome della finestra
		ClassName,				// Titolo
		WS_OVERLAPPEDWINDOW,	// Stile di default
		CW_USEDEFAULT,			// Posizione di default
		CW_USEDEFAULT,
		100,					// Dimensione 100x100
		100,
		HWND_DESKTOP,			// Collocata sul desktop
		NULL,
		hThisInstance,			// Mantenuta da questo programma
		NULL);

	if (Hwnd == NULL)
		return -1;

	/* Inizializzazione dell'icona nella tray area */

	if (InitNotifyIconData() != 0)
		return -1;
	
	/* Dopo essere stata inizializzata la NotifyIconData, viene usata dalla Shell_NotifyIcon per inviare messaggi alla Notification Area */
	
	if (!Shell_NotifyIcon(NIM_ADD, &NotifyIconData))
		return -1;

	/* Visualizzazione del box di dialogo alla partenza dell'applicazione */
	MessageBox(Hwnd, Message, ClassName, MB_OK | MB_ICONINFORMATION);

	std::atomic_bool continua = true;		//finché rimane a true, il server rimane in comunicazione o attesa del client

	
	
	try {
		SocketStream socket(PORT);

		/* Creazione del thread che gestisce le funzionalità del Server */
		std::thread ThreadManager(serverManagementList, std::ref(socket), std::ref(continua)); //thread che gestisce la funzione "serverManagementList" di ListHandler.cpp che si occupa della gestione della lista
		
		/* Loop per estrarre i messaggi dalla coda. Se non ci sono messaggi si blocca.
		*  Termina il loop se riceve un messaggio di QUIT.
		*/

		BOOL bReturn;
		while ((bReturn = GetMessage(&message, NULL, 0, 0)) != 0) {
			if (bReturn == -1)
				break;
			TranslateMessage(&message);
			DispatchMessage(&message); //richiama la CALLBACK WindowProc passandole il contenuto del messaggio
		}

		/* Usciti dal loop, sono concluse le operazioni da fare, quindi si chiude l'applicazione Server */

		if (message.wParam == -10) {
			continua = false;	//si imposta la variabile booleana a false così nella funzione func gestita da otherthread si potrà uscire dal while
			ThreadManager.join();
			throw socket_exception("Socket in secondary thread failed");
		}

		if (socket.getStatus() == true) {
			socket.closeConnection();
			socket.setStatus(false);
			continua = false;	//si imposta la variabile booleana a false così nella funzione func gestita da otherthread si potrà uscire dal while
								//attendo che il thread finisca l'esecuzione
			ThreadManager.join();
		}
		else
			//si permette al thread di operare indipendentemente dal thread principale
			ThreadManager.detach();
	}
	catch (socket_exception& e) {
		MessageBox(Hwnd, TEXT("Errore del socket"), ClassName, MB_OK | MB_ICONERROR);
		WSACleanup();
		return -1;
	}
	catch (std::system_error) {
		MessageBox(Hwnd, TEXT("Impossibile creare un nuovo thread"), ClassName, MB_OK | MB_ICONERROR);
		WSACleanup();
		return -1;
	}

	WSACleanup();					// liberazione delle risorse Winsock
	return message.wParam;
}

/*WindowProc è la funzione per la gestione dei messaggi di sistema.
 * LRESULT è il tipo usato da Windows per dichiarare un Long integer.
 * CALLBACK è la calling convention (tipo WINAPI) per indicare le funzioni chiamate da Windows.
 * La Window Procedure è un function pointer, che ci permette di chiamarla quando vogliamo perché l'indirizzo della funzione 
   sarà assegnato ad un puntatore subito dopo la creazione della window class (nel WinMain -> WINDOWCLASSEX wncl).

 * Parametri:
 * hwnd - Only important if you have several windows of the same class open
   at one time. This is used to determine which window hwnd pointed to before
   deciding on an action.
 * message - The actual message identifier that WndProc will be handling (event verified).
 * wParam and lParam - Extensions of the message parameter. Used to give more information and point to specifics that message cannot
   convey on its own
* 
*/

LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
	
	/* Switch per la gestione dei messaggi */
	switch (message) {

	case WM_CREATE:	// Creazione del menu dell'icona

		ShowWindow(Hwnd, SW_HIDE);	//teniamo nascosta la finestra
		Hmenu = CreatePopupMenu();	//creiamo il contenuto del menu/* Switch per la gestione dei messaggi */

	/* Il contenuto del menu è l'opzione exit di tipo string. Identifichiamo il click su questa opzione
	*  grazie al messaggio ID_TRAY_EXIT che abbiamo definito nel file resource.h. Viene fatto l'append nel menù
	*/
		if (!AppendMenu(Hmenu, MF_STRING, ID_TRAY_EXIT, TEXT("Exit"))) {
			MessageBox(Hwnd, TEXT("Impossibile caricare l'applicazione"), ClassName, MB_OK | MB_ICONERROR);
			Shell_NotifyIcon(NIM_DELETE, &NotifyIconData);	//eliminiamo l'icona dalla tray area
			PostQuitMessage(-1);		//terminiamo l'applicazione con codice di errore
			return 0;
		}
		break;

	case WM_SYSCOMMAND: //L'utente clicca un comando nel Control Menù oppure preme tasti come "Minimize", "Restore", "Close" etc..

	/* Per ottenere il risultato corretto dal parametro di WM_SYSCOMMAND, bisogna porre gli ultimi			
	   quattro bit a zero con una maschera, perchè sono usati dal sistema operativo */

		switch (wParam & 0xFFF0) {
		// Se la finestra dovesse comparire, premendo minimizza o chiudi la finestra viene fatta scomparire. Gli altri casi non vengono gestiti
		case SC_MINIMIZE:
		case SC_CLOSE:
			ShowWindow(Hwnd, SW_HIDE);
			return 0;
			break;
		}
		break;

	case WM_SYSICON: {	//definito in resource.h (VM_USER -> spazio dedicato ai messaggi privati che possono essere definiti ad hoc)

		/* Messaggio da parte dell'applicazione nella tray area: c'è stato un evento
		* Entriamo in questo "case" quando c'è un qualsiasi evento nella tray area.
		*/

		//Verifichiamo che l'evento interessa l'icona della nostra app
		if (wParam == ID_TRAY_APP_ICON)
			SetForegroundWindow(Hwnd);	//mettiamo la nostra applicazione in foreground

										//Il menu compare sia al click del tasto sinistro sia al click del tasto destro.
		if (lParam == WM_RBUTTONDOWN || lParam == WM_LBUTTONDOWN) {

			// Ottieni posizione corrente del mouse
			POINT curPoint;
			GetCursorPos(&curPoint);
			SetForegroundWindow(Hwnd);

			// Ottieni elemento del menu che è stato cliccato
			UINT clicked = TrackPopupMenu(Hmenu, TPM_RETURNCMD | TPM_NONOTIFY, curPoint.x, curPoint.y, 0, hwnd, NULL);

			SendMessage(hwnd, WM_NULL, 0, 0);	// Invio messaggio per far sparire il menu

			if (clicked == ID_TRAY_EXIT) {	// Se è stato cliccato Exit, elimina l'icona e invia messaggio di quit
				Shell_NotifyIcon(NIM_DELETE, &NotifyIconData);
				PostQuitMessage(0);	//terminiamo l'applicazione con codice di uscita 0
			}
		}
	}
		break;

	case WM_NCHITTEST: {	// Cattura eventuali click nella finestra dell'applicazione e non li gestiamo

		UINT uHitTest = DefWindowProc(hwnd, WM_NCHITTEST, wParam, lParam);
		if (uHitTest == HTCLIENT)
			return HTCAPTION;
		else
			return uHitTest;
		}
			break;

	case WM_CLOSE:		//Click sul Close Button

		ShowWindow(Hwnd, SW_HIDE);	//nascondiamo la finestra, non si deve chiudere l'applicazione da qui
		return 0;
		break;

	case WM_DESTROY:

		PostQuitMessage(0);	//quando ad esempio dal task manager terminiamo l'applicazione
		break;

	}

	return DefWindowProc(hwnd, message, wParam, lParam);	//procedura che gestisce tutti i messaggi che non abbiamo gestito noi: ad esempio mostrare il menu al click.
}

/* Setup delle informazioni per l'icona */

int InitNotifyIconData()
{
	memset(&NotifyIconData, 0, sizeof(NOTIFYICONDATA));

	//The size of this structure, in bytes:
	NotifyIconData.cbSize = sizeof(NOTIFYICONDATA);
	//A handle to the window that receives notifications associated with an icon in the notification area:
	NotifyIconData.hWnd = Hwnd;	 // dove Hwnd è la finestra creata nel WinMain
								 //The application-defined identifier of the taskbar icon:
	
	NotifyIconData.uID = ID_TRAY_APP_ICON; //definizione dell'ID (usato poi nella WindowProc)

	NotifyIconData.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;

	/* An application-defined message identifier. The system uses this identifier to send notification messages to the window 
	* identified in hWnd.These notification messages are sent when a mouse event or hover occurs in the bounding rectangle of the icon, 
	* when the icon is selected or activated with the keyboard, or when those actions occur in the balloon notification:
	*/

	NotifyIconData.uCallbackMessage = WM_SYSICON;	//binding con la costante definita ad hoc, che si usa nella WindowProcedure() quando c'è un evento sull'icona
	
	if ((NotifyIconData.hIcon = LoadIcon(GetModuleHandle(NULL), MAKEINTRESOURCE(IDI_ICON1))) == NULL)
		return -1;
	wcsncpy_s(NotifyIconData.szTip, Advise, sizeof(Advise));	//assegnamo il messaggio da visualizzare al passaggio del mouse sull'icona
	return 0;
}
