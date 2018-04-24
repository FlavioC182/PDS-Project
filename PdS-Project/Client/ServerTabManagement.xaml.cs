using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Client
{
    /// <summary>
    /// Classe che contiene la logica per la gestione e la visualizzazione grafica di un tab relativo ad un singolo server
    /// Estende la classe UserControl 
    /// </summary>
    public partial class ServerTabManagement : UserControl
    {
        /// <summary>
        /// Proprietà che incapsula il tab relativo al server associato al ServerTabManagement
        /// </summary>
        public DynamicTabItem ServerTab { get; private set; }

        /// <summary>
        /// Socket connesso
        /// </summary>
        private TcpClient _socket;

        /// <summary>
        /// Stream (lettura e scrittura)
        /// </summary>
        private NetworkStream _stream;

        /// <summary>
        /// Struttura che mantiene il timestamp della creazione del ServerTab
        /// </summary>
        private DateTime ServerTabBirth;

        /// <summary>
        /// Struttura che mantiene il timestamp dell'ultimo aggiornamento della percentuale
        /// </summary>
        private DateTime LastPercentageUpdate;

        /// <summary>
        /// Timer che scandisce ogni quanto tempo bisogna aggiornare la percentuale
        /// </summary>
        private System.Timers.Timer PercentageRefreshTimer;

        /// <summary>
        /// Lista delle applicazioni attive sul server relativo a questo tab
        /// </summary>
        public ObservableCollection<AppItem> Applications { get; }

        /// <summary>
        /// Funzione da assegnare al thread Listener (vedi sotto)
        /// </summary>
        private SocketListener Listener;

        /// <summary>
        /// Thread che verrà lanciato e messo in attesa di informazioni dal server
        /// </summary>
        private Thread ListenerThread;

        /// <summary>
        /// Proprietà che incapsula le informazioni relative al socket
        /// </summary>
        public TcpClient Connection
        {
            get { return _socket; }
            set { _socket = value; }
        }

        /// <summary>
        /// Proprietà che incapsula le informazioni relative allo stream (lettura o scrittura)
        /// </summary>
        public NetworkStream Stream
        {
            get { return _stream; }
            set { _stream = value; }
        }

        /// <summary>
        /// Costruttore della classe ServerTabManagement
        /// </summary>
        /// <param name="DTI"> Tab Item del server al quale è associato l'oggetto corrente </param>
        public ServerTabManagement(DynamicTabItem DTI)
        {
            InitializeComponent();

            ServerTab = DTI;

            // Ogni ServerTab si iscrive all'evento di chiusura della mainwindow
            ServerTab.MainWndw.ClosingEvent += ServerTabClose;

            // Si impostano i timestamp iniziali all'istante corrente
            ServerTabBirth = LastPercentageUpdate = DateTime.Now;

            // La percentuale viene aggiornata ogni 1000 ms
            PercentageRefreshTimer = new System.Timers.Timer(1000);

            // Impostiamo il timer come ricorsivo: al termine dei 1000 ms riparte da zero e ricomincia
            PercentageRefreshTimer.AutoReset = true;

            // Allo scadere del timer, si lancia la funzione di aggiornamento della percentuale (PercentageRefresh)
            PercentageRefreshTimer.Elapsed += (obj, e) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => { PercentageRefresh(); }));
            };

            // Inizializzazione della lista di applicazioni (inizialmente vuota)
            Applications = new ObservableCollection<AppItem>();

            // Associazione della lista delle applicazioni all'elemento WPF Applist
            Applist.ItemsSource = Applications;

            // Abilitazione dell'accesso alla lista da parte di più thread
            BindingOperations.EnableCollectionSynchronization(Applications, Applications);
        }

        /// <summary>
        /// Funzione che inizia la raccolta delle informazioni dal server
        /// </summary>
        public void StartServerDataExchange()
        {
            uint attempt = 2;

            while (attempt != 0)
            {
                try
                {
                    Listener = new SocketListener(this);

                    // Thread secondario che si pone in attesa di informazioni dal server
                    ListenerThread = new Thread(Listener.SocketThreadListen);

                    Console.WriteLine("Main thread: Call Start, to start ThreadFcn.");

                    // Mettiamo il thread in background
                    ListenerThread.IsBackground = true;
                    ListenerThread.Start();

                    // Avvio del timer di refresh della percentuale
                    PercentageRefreshTimer.Start();

                    Console.WriteLine("Main thread: Call Join(), to wait until ThreadFcn ends.");

                    attempt = 0;
                }
                catch(OutOfMemoryException)
                {
                    ExceptionHandler.MemoryError(attempt, this.ServerTab.MainWndw);
                }

            }


        }

        /// <summary>
        /// Ferma l'ascolto dal server e disalloca le risorse precedentemente allocate per la comunicazione.
        /// </summary>
        public void ServerTabClose()
        {
            // Chiudo il socket in attesa
            Listener.Stop();

            try
            {
                // Disabilita il socket sia in ingresso che uscita (Both)
                Connection.Client.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
                MessageBox.Show("Errore di connessione.", "Attenzione!", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (ObjectDisposedException)
            {
                // Indica che il socket è già stato chiuso
                MessageBox.Show("Tentativo di chiudere un socket già chiuso");
            }
            finally
            {
                ListenerThread.Join();
            }
        }

        /// <summary>
        /// Funzione che modifica la vista della lista delle applicazioni, inquadrando la nuova applicazione aggiunta.
        /// Viene lanciata dall'evento di aggiunta di un'applicazione.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"> Evento scatenante </param>
        public void ApplistRerrangeView(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                Applist.ScrollIntoView(Applist.Items[Applist.Items.Count - 1]);
        }

        /// <summary>
        /// Metodo richiamato all'atto della modifica del cambio di focus o allo scadere del timer
        /// Aggiorna le percentuali di tempo di focus delle diverse applicazioni
        /// </summary>
        public void PercentageRefresh()
        {
            // Accediamo in mutua esclusione alla lista delle applicazioni
            lock (Applications)
            {
                TimeSpan lastUpdate = DateTime.Now - LastPercentageUpdate;
                TimeSpan totalExecutionTime = DateTime.Now - ServerTabBirth;

                foreach (AppItem a in Applications)
                {
                    if (a.HasFocus)
                        a.ExecutionTime += lastUpdate;
                    try
                    {
                        a.Percentage = (int)(a.ExecutionTime.TotalMilliseconds / totalExecutionTime.TotalMilliseconds * 100);
                    }
                    catch (DivideByZeroException)
                    {
                        a.Percentage = 0;
                    }
                }

                LastPercentageUpdate = DateTime.Now;
            }
        }
    }
}
