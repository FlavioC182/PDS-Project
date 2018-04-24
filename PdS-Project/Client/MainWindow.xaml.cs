using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Client
{
    /// <summary>
    /// Handler che gestisce la chiusura della finestra
    /// </summary>
    public delegate void CloseHandler();

    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// Lista di tab dei Server
        /// </summary>
        public ObservableCollection<DynamicTabItem> ServerTabs { get; }

        /// <summary>
        /// Proprietà che incapsula l'icona di default
        /// </summary>
        public BitmapFrame DefaultIcon { get; set; }

        /// <summary>
        /// Proprietà che incapsula la lista delle applicazioni in foreground (per tutti i server)
        /// </summary>
        public ObservableCollection<ForegroundApp> ForegroundApps {get;}

        /// <summary>
        /// Proprietà che incapsula la lista degli indirizzi IP dei server connessi
        /// </summary>
        public List <string> ActiveConnections { get; set; }

        /// <summary>
        /// Proprietà utile a forzare la chiusura della finestra in caso di errore
        /// </summary>
        public bool Error { get; set; } = false;

        /// <summary>
        /// Evento di chiusura della MainWindow
        /// </summary>
        public event CloseHandler ClosingEvent;

        /// <summary>
        /// Costruttore della classe MainWindow
        /// Crea anche un primo tab di default
        /// </summary>
        /// <param name="client">Informazioni del socket</param>
        /// <param name="stream">Informazioni sullo stream</param>
        /// <param name="address">Indirizzo del server al quale collegarsi</param>
        public MainWindow(TcpClient client, NetworkStream stream, String address)
        {
            InitializeComponent();

            // Icona di default, presa dalla cartella Resources dell'applicazione
            DefaultIcon = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/default.ico"));

            ActiveConnections = new List<string>();

            ForegroundApps = new ObservableCollection<ForegroundApp>();
            ForegroundAppsBox.ItemsSource = ForegroundApps;

            // Permette a più thread di accedere alla lista di app in foreground, e blocca la lista stessa all'accesso
            BindingOperations.EnableCollectionSynchronization(ForegroundApps, ForegroundApps);

            ServerTabs = new ObservableCollection<DynamicTabItem>();

            NewTab(client, stream, address);

            ServerTabControl.DataContext = ServerTabs;
        }

        /// <summary>
        /// Funzione invocata dall'evento ClosingEvent. Chiude la finestra ed i tab aperti in essa.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseWindow (object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!Error)
            {
                MessageBoxResult res = MessageBox.Show("Sei sicuro di voler terminare l'applicazione? \n Tutte le connessioni istaurate andranno perse.");

                switch (res)
                {
                    case MessageBoxResult.Yes:
                        ClosingEvent();
                        break;

                    case MessageBoxResult.No:
                        e.Cancel = true;                        //Evento rientrato
                        break;
                }

            }

            // In caso di errore, chiudi la finestra
            else
                ClosingEvent();
        }

        /// <summary>
        /// Funzione che chiude un singolo tab
        /// </summary>
        /// <param name="tab">Riferimento al tab da chiudere</param>
        public void CloseTab (DynamicTabItem tab)
        {
            // Caso in cui il tab da chiudere sia l'ultimo attivo: chiusura dell'intera finestra
            if (ServerTabs.Count == 1)
            {
                this.Close();
                return;
            }

            ServerTabManagement servertab = tab.ServerTab;

            // In caso di chiusura della main window, la funzione di chiusura di questo tab non andrà più eseguita
            ClosingEvent -= servertab.ServerTabClose;

            // Chiusura del tab
            servertab.ServerTabClose();

            // Rimozione di questo tab dalla lista dei tab dei server
            ServerTabs.Remove(tab);

            // Rimozione dei questa connessione dalla lista di connessioni attive
            ActiveConnections.Remove(tab.RemoteHost);

            if (ServerTabs.Count == 1)
                ForegroundAppsBox.IsEnabled = false;
        }

        /// <summary>
        /// Funzione che crea un nuovo tab
        /// </summary>
        /// <param name="client"></param>
        /// <param name="stream"></param>
        /// <param name="address"></param>
        public void NewTab (TcpClient client, NetworkStream stream, String address)
        {
            DynamicTabItem tab = new DynamicTabItem(this);
            ServerTabManagement s = new ServerTabManagement(tab);
            tab.ServerTab = s;
            
            // Il titolo del nuovo tab ed il suo host remoto corrispondono all'indirizzo del server
            tab.DynamicHeader = tab.RemoteHost = address;

            if (address.StartsWith("127."))
                tab.DynamicHeader = "Loopback";
            else
                Dns.BeginGetHostEntry(address, new AsyncCallback((IAsyncResult ar) =>
                {
                    try
                    {
                        string host = Dns.EndGetHostEntry(ar).HostName;
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                        {
                            tab.DynamicHeader = host;
                        }));
                    }
                    catch (SocketException)
                    {
                        Console.WriteLine("Server {0}: Nessun host trovato", address);
                    }
                }), null);

            s.Connection = client;
            s.Stream = stream;
            s.StartServerDataExchange();
            tab.Content = s;
            
            // Aggiunta del nuovo tab alla lista
            ServerTabs.Add(tab);

            // Evidenziazione dell'ultimo tab creato
            ServerTabControl.SelectedIndex = ServerTabs.Count - 1;

            if (ServerTabs.Count > 1)
                ForegroundAppsBox.IsEnabled = true;
        }
        /// <summary>
        /// Funzione associata al tasto disconnetti. Chiude il tab relativo a server da cui vi si sta disconnettendo.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void F_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            ServerTabManagement s = ServerTabControl.SelectedContent as ServerTabManagement;

            if (s != null)
                CloseTab(s.ServerTab);
        }

        /// <summary>
        /// Funzione associata al tasto connetti. Apre il menu di connessione.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void F_Connect_Click(object sender, RoutedEventArgs e)
        {
            Connection C = new Connection();
            C.Show();
        }

        /// <summary>
        /// Funzione associata al tasto Esci. Chiude la finestra.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void F_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
