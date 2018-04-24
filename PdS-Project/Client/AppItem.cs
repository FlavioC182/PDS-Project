using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Client
{
    /// <summary>
    /// Classe che incapsula le informazioni relative alle singole applicazioni che compaiono nella lista.
    /// </summary>
    public class AppItem : INotifyPropertyChanged
    {
        /// <summary>
        /// Numero che mantiene la percentuale di focus avuto dall'applicazione corrente
        /// </summary>
        private int _percentage = 0;

        /// <summary>
        /// Stato dell'applicazione (In esecuzione, In foreground)
        /// </summary>
        private String _status = "In esecuzione";

        /// <summary>
        /// Indica se l'applicazione ha il focus o meno
        /// </summary>
        private bool _hasFocus = false;

        /// <summary>
        /// Proprietà che incapsula il nome dell'applicazione
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// Proprietà che incapsula l'icona dell'applicazione
        /// </summary>
        public ImageSource Icon { get; set; }

        /// <summary>
        /// Proprietà che incapsula il PID dell'applicazione
        /// </summary>
        public uint PID { get; set; } = 0;

        /// <summary>
        /// Proprietà che incapsula il tempo di esecuzione dell'applicazione
        /// </summary>
        public TimeSpan ExecutionTime { get; set; } = new TimeSpan(0);

        /// <summary>
        /// Proprietà che incapsula la percentuale di focus dell'applicazione e ne notifica eventuali variazioni all'interfaccia
        /// </summary>
        public int Percentage
        {
            get { return _percentage; }
            set
            {
                if (value != _percentage)
                {
                    _percentage = value;
                    NotifyPropertyUpdate();
                }
            }
        }

        /// <summary>
        /// Proprietà che incapsula lo stato dell'applicazione e ne notifica eventuali variazioni all'interfaccia
        /// </summary>
        public String Status
        {
            get { return _status; }
            private set
            {
                if (value!=_status)
                {
                    _status = value;
                    NotifyPropertyUpdate();
                }
            }
        }

        /// <summary>
        /// Proprietà che incapsula lo stato dell'applicazione
        /// </summary>
        public bool HasFocus
        {
            get { return _hasFocus; }
            set
            {
                if (value != _hasFocus)
                {
                    _hasFocus = value;

                    if (_hasFocus)
                        Status = "In foreground";
                    else
                        Status = "In esecuzione";
                }
            }
        }

        /// <summary>
        /// Costruttore della classe AppItem
        /// </summary>
        /// <param name="default
        /// 
        /// n"> Icona di default </param>
        public AppItem (ImageSource defaultIcon)
        {
            Name = "Applicazione di default";
            Icon = defaultIcon;
        }

        /// <summary>
        /// Delegato che traccia l'aggiornamento di una proprietà
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;


        /// <summary>
        /// Metodo invocato all'atto della variazione della percentuale o dello stato dell'applicazione
        /// Invoca l'evento PropertyUpdated per notificare la modifica all'interfaccia
        /// </summary>
        /// <param name="property"> Nome della proprietà che ha subito una variazione</param>
        private void NotifyPropertyUpdate ([CallerMemberName] String property = "")
        {
            // Se c'è stato un aggiornamento di una proprietà
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }




    }
}
