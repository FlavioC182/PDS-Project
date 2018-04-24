#pragma comment(lib,"Ws2_32.lib")
#include "Change.hpp"

/*	Costruttori del tipo di modifica. I parametri sono il tipo di modifica e il pID del processo applicativo.
*	L'idea è di definire un costruttore ad hoc per il tipo "add", perché esso riceverà anche l'ApplicationItem.	
*/

Change::Change(changeType t, DWORD id) : changeT(t), pID(id) {
	if (changeT == add)
		throw std::invalid_argument("Costruttore sbagliato per la modifica di add!");
}

/* Costruttore add */
Change::Change(DWORD id, ApplicationItem a) : changeT(add), pID(id), app(a) {};

/* Funzione che si occupa della serializzazione del tipo di modifica (changeType) e del pID.
*  Viene usata per ogni tipo di modifica da inviare (al contrario delle successive due funzioni che riguardano solo la modifica add).
*/

char* Change::getSerializedChangeType(int& length) {
	length = dimShort + dimWord;		// dimensione di un u_short (per memorizzare il changeType) + quella di un DWORD (per il pID)
										//	N.B. la dimensione di un enum è tipicamente di un integer
	char* buffer = (char*) malloc(length);
	if (buffer == NULL)
		throw std::bad_alloc();

	/*	htons converte un u_short in una sequenza di bit ordinata per comunicazione su rete TCP/IP. 
	*	In particolare converte da little Endian a Big Endian, che è l'ordine usato per comunicazione su rete.
	*	In questo caso viene trasformato in una stringa di bit l'identificatore della modifica (changeT)
	*	E viene memorizzato nel buffer, che riceve il cast, passando da char* a u_short*
	*/
	*(u_short*)buffer = htons(u_short(changeT));
	
	//	Aggiungiamo al buffer il PID del processo relativo, utilizzando come displacement dimShort, che permette
	//	di "skippare" la parte iniziale contenente il changeType, appena inserito.
	*(PDWORD)(buffer + dimShort) = pID;

	return buffer;
}

/* Funzione per serializzare il nome dell'applicazione in esecuzione.
*  Per modifiche diverse da add, questa funzione non viene usata.
*/

char* Change::getSerializedName(int& length) {
	if (changeT != add)
		return nullptr;

	// Impostiamo la lunghezza pari alla dimensione del nome dell'applicazione + terminatore (Moltiplicato per la dimensione di wchar_t)
	length = (app.Name.size() + 1) * sizeof(wchar_t);
	char* buffer = (char*) malloc(length);
	if (buffer == NULL)
		throw std::bad_alloc();

	// Si copia il nome dell'applicazione nel buffer tramite il metodo memcpy_s per poterlo poi inviare su rete.
	if (memcpy_s(buffer, length, app.Name.c_str(), length) != 0) {
		free(buffer);
		throw std::overflow_error("Errore: non si puo' copiare il nome dell'applicazione");
	}
	return buffer;
}


/*	Funzione che serializza l'icona per renderla adatta all'invio sulla rete. 
*	Deve essere lanciata solo per operazioni di ADD, in quanto per operazioni di modifica non è necessario serializzare nuovamente l'icona,
*	che sarà già stata serializzata ed inviata (ed ormai memorizzata dal client) in precedenza.
*/

char* Change::getSerializedIcon(int& length) {

	if (changeT != add)
		return nullptr;

	HRSRC resource = NULL;
	LPTSTR groupIconName = NULL;

	/*	La funzione LoadLibraryEx ci permette di caricare l'eseguibile/libreria (in generale il modulo) dell'applicazione nello spazio di memoria
	*	del nostro processo in formato binario per recuperare l'icona.
	*	In particolare il parametro LOAD_LIBRARY_AS_DATAFILE indica che il sistema "mappa" il file dentro lo spazio di indirizzamento virtuale 
	*	del processo chiamante come se in esso ci fosse un data file.
	*/

	HMODULE hExe = LoadLibraryEx(app.Exec_name.c_str(), NULL, LOAD_LIBRARY_AS_DATAFILE);
	if (hExe != NULL) {
		// La funzione EnumResourceNames enumera (scansiona una per volta) tutte le risorse di un certo tipo specificato come parametro.
		// Nel nostro caso, vengono enumerate le icone (RT_GROUP_ICON) che sono la risorsa che vogliamo estrarre e serializzare.
		// Il primo parametro è l'handle al modulo (libreria) in cui si deve cercare (se NULL, si considera il processo corrente).
		// Il terzo parametro è un puntatore alla funzione di call back che deve essere chiamata ogni volta per enumerare le risorse.
		// (in maniera più formale "A pointer to the callback function to be called for each enumerated resource name or ID").
		// In questo caso facciamo uso di una funzione lambda che restituisce un Bool.
		// Il quarto parametro è passato alla funzione di Callback (la lambda).
		// La funzione di callback in generale prende come parametri di hModule, lpszType e lParam i parametri specificati nella EnumResourceName (hExe, RT_GROUP_ICON, groupIconName).
		// per ogni risorsa di tipo RT_GROUP_ICON

		EnumResourceNames(hExe, RT_GROUP_ICON, [](HMODULE hModule, LPCTSTR lpszType, LPTSTR lpszName, LONG_PTR lparam)-> BOOL {
			/* si memorizza la prima risorsa disponibile */
			if (lpszName != NULL) {
				LPTSTR* name = (LPTSTR*)lparam;	// name sarà il puntatore ad lparam, che non è altro che groupIconName passato per riferimento
				*name = lpszName;				// in questo modo, deferenziando name, e assegnando il valore lpszName, groupIconName assumerà quel valore
				// appena la troviamo interrompiamo la funzione EnumResourceNames.
				// infatti, similmente alla EnumWindows del ListManage, la funzione di callback viene lanciata finché quest'ultima non ritorna false. 
				// (oppure finché non viene enumerata l'ultima risorsa del tipo specificato).
				return FALSE;
			}
			return TRUE;
		}, (LONG_PTR)&groupIconName);

		/* in questo modo, con EnumResourceNames, abbiamo estratto il nome della risorsa (salvato in groupIconName), informazione che ci servirà per estrarre la posizione */

		// FindResource definisce la posizione di una risorsa di un determinato tipo (RT_GROUP_ICON), con un determinato nome (groupIconName)
		// in un determinato modulo exe (hExe).
		resource = FindResource(hExe, groupIconName, RT_GROUP_ICON);
	}
	/* Se non riusciamo a caricare la libreria o non troviamo un gruppo_icona valido verrà caricata sul client l'icona di default */
	if (hExe == NULL || resource == NULL) {
			FreeLibrary(hExe);
			return nullptr;
	}

	/* Impostiamo length pari alla dimensione della risorsa alla locazione res (icona) */
	length = SizeofResource(hExe, resource);
	if (length == 0) {
		FreeLibrary(hExe);
		return nullptr;
	}

	// Carichiamo la risorsa, ovvero creiamo un puntatore ad essa
	// LoadResource: Riceve un handle che può essere usata (in combinazione con il metodo lockResource) per ottenere un puntatore al primo
	// byte di una specifica resource in memoria.

	HGLOBAL resourcePtr = LoadResource(hExe, resource);
	if (resourcePtr == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}
	
	// Prendiamo un puntatore alla risorsa indicata da resourceptr (il grouppo_icona)
	// La risorsa non viene bloccata, ma viene solo acquisito un puntatore ad essa
	LPVOID icon = LockResource(resourcePtr);
	if (icon == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	// Cerchiamo l'ID dal gruppo_icona di un'icona di queste dimensioni
	// LookupIconIdFromDirectoryEx: Cerca un'icona che si adatta meglio al display del device corrente
	//  - (PBYTE) icon: L'icona o la directory
	//  - TRUE: Indica che si sta cercando un'icona (FALSE indica un cursore)
	//  - 48, 48: Dimensioni desiderate dell'icona
	//  - LR_DEFAULTCOLOR: Flag che indica che il colore scelto è quello di default

	int idIcon = LookupIconIdFromDirectoryEx((PBYTE) icon, TRUE, 48, 48, LR_DEFAULTCOLOR);
	if (idIcon == 0) {
		FreeLibrary(hExe);
		return nullptr;
	}

	/* Ora che abbiamo l'ID dell'icona e non più il gruppo_icona in generale, si può passare all'estrazione di essa */
	
	// Restituisce un puntatore alla risorsa icona con quell'ID (Appena ottenuto)
	// FindResource usa la MAKEINTRESOURCE macro con l'identifier idIcon (ottenuto da Lookup..) per localizzare la risorsa presente nel modulo.
		
	resource = FindResource(hExe, MAKEINTRESOURCE(idIcon), RT_ICON);
	if (hExe == NULL || resource == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	/* Come prima si estrae la dimensione della risorsa (questa volta l'icona) */
	length = SizeofResource(hExe, resource);
	if (length == 0) {
		FreeLibrary(hExe);
		return nullptr;
	}
	
	resourcePtr = LoadResource(hExe, resource);
	if (resourcePtr == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	icon = LockResource(resourcePtr);
	if (icon == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	/* Dopo aver ottenuto il puntatore all'icona corretta, si alloca il buffer per poterla inviare sulla rete */
	char* buffer = (char*)malloc(length);
	if (buffer == NULL) {
		FreeLibrary(hExe);
		throw std::bad_alloc();
	}

	/* Copio il contenuto dell'icona nel buffer, per poterlo inviare in rete */
	if (memcpy_s(buffer, length, icon, length) != 0) {
		FreeLibrary(hExe);
		free(buffer);
		return nullptr;
	}

	FreeLibrary(hExe);

	//restituiamo i byte dell'icona
	return buffer;
}
