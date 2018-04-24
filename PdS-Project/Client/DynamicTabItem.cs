using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Client
{
    /// <summary>
    /// Classe che estende la classe TabItem di WPF per permettere la modifica dinamica del titolo e
    /// mostrare le proprietà del server sottostante
    /// </summary>
    public class DynamicTabItem : TabItem, INotifyPropertyChanged
    {
        /// <summary>
        /// Delegato che indica l'aggiornamento di una certa proprietà (titolo)
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Riferimento alla main window
        /// </summary>
        public MainWindow MainWndw { get; private set; }
        
        /// <summary>
        /// Costruttore della classe DynamicTabItem
        /// </summary>
        /// <param name="w">Window che deve essere impostata come quella di riferimento per l'oggetto DynamicTabItem </param>
        public DynamicTabItem (MainWindow w) { MainWndw = w; }

        /// <summary>
        /// Proprietà che incapsula la stringa che indica l'host remoto
        /// </summary>
        public string RemoteHost { get; set; }

        /// <summary>
        /// Proprietà che incapsula la stringa che indica il nome dell'app in foreground (focus)
        /// </summary>
        public String ForegroundApp { get; set; }

        /// <summary>
        /// Proprietà che incapsula un riferimento al tab del server XAML
        /// </summary>
        public ServerTabManagement ServerTab { get; set; }

        /// <summary>
        /// Funzione invocata all'atto della modifica dell'header. Invoca il delegato PropertyChanged per notificare la modifica
        /// all'interfaccia
        /// </summary>
        /// <param name="property"> Riferimento alla proprietà che è stata modificata </param>
        private void NotifyPropertyUpdate ([CallerMemberName] String property = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }

        /// <summary>
        /// Proprietà che permette di notificare la variazione dell'header all'interfaccia, ed aggiornare il titolo
        /// </summary>
        public object DynamicHeader
        {
            get { return Header; }
            set
            {
                Header = value;
                NotifyPropertyUpdate("Header");
            }
        }
    }
}
