#pragma once

#include <string>
#include <exception>
#include <Windows.h>


#define dimShort sizeof(u_short)
#define dimWord sizeof(DWORD)


/* Struct che contiene le inforamzioni su un'applicazione */
struct ApplicationItem {
	std::wstring Name;		//Nome dell'applicazione
	std::wstring Exec_name;
};

	//Tipo di modifica alla lista
	enum changeType { add, rem, chf, heartbeat };

	/* la classe che rappresenta una modifica alla lista */
	class Change {
	private:
		changeType changeT;
		DWORD pID;
		ApplicationItem app;

	public:
		Change(changeType t, DWORD id);         // Costruttore di modifica change_focus o remove
		Change(DWORD id, ApplicationItem a);	// Costruttore modifica add
		char * getSerializedChangeType(int& length);
		char * getSerializedName(int& length);
		char * getSerializedIcon(int& length);
	};