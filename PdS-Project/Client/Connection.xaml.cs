using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Finestra di avvio: permette di impostare i parametri della connessione come IP e Porta del server
    /// </summary>
    public partial class Connection : Window
    {
        /// <summary>
        /// IAsyncResult è un'interfaccia utilizzata da classi che contengono metodi asincroni.
        /// La classe Connection contiene metodi asincroni perchè deve gestire la connessione con un server.
        /// </summary>
        private IAsyncResult connectionResult;

        /// <summary>
        /// TcpClient fornisce le funzioni basiche per instaurare una connessione TCP
        /// </summary>
        private TcpClient client;

        /// <summary>
        /// Classe utilizzata per passare informazioni relative al client alla callback di connessione
        /// </summary>
        private class ClientProperties
        {
            public TcpClient client { get; set; }
            public string address { get; set; }
        }

        /// <summary>
        /// Costruttore della classe connection. Imposta semplicemente il focus sul primo box relativo all'indirizzo IP da inserire.
        /// </summary>
        public Connection()
        {
            InitializeComponent();
            txtAddress1.Focus();
        }

        /// <summary>
        /// Funzione invocata dall'evento di inserimento testo.
        /// Impedisce di inserire nelle textbox relativi ad IP e Porta elementi che non siano numeri
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"> Evento di composizione di testo </param>
        public void IsAllowedCharacter(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[0-9]+");

            // Se il carattere inserito è diverso da un numero (Non si ha match con la regular expression)
            if (regex.IsMatch(e.Text) == false)
                e.Handled = true;
        }

        /// <summary>
        /// Funzione invocata quando una textbox acquisisce il focus.
        /// Seleziona tutto il contenuto presente, permettendo una più veloce modifica di IP e Porta.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"> Evento di acquisizione focus della textbox </param>
        private void SelectAll (object sender, RoutedEventArgs e)
        {
            if (e.Source.GetType() == typeof(TextBox))
            {
                ((TextBox)e.Source).SelectAll();
            }
        }

        /// <summary>
        /// Funzione richiamata di fronte all'evento di perdita del focus dalla textbox.
        /// Imposta come valore di default 0, se non è stato inserito nulla.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"> Evento di perdita focus della textbox</param>
        private void DefaultValue (object sender, RoutedEventArgs e)
        {
            if (e.Source is TextBox)
            {
                TextBox source = e.Source as TextBox;

                if (source.Text == "")
                    source.Text = "0";
            }
        }

        /// <summary>
        /// Funzione che tenta di instaurare la connessione. Riavvia l'interfaccia di connessione se la connessione fallisce.
        /// In caso di connessione riuscita, crea la Window principale (MainWindow) se non già esistente
        /// Se già esistente invece, perchè già si è collegati ad un altro server, crea un nuovo tab
        /// </summary>
        /// <param name="result"> Struttura contenente il risultato delle operazioni asincrone </param>
        private void ConnectionRequest (IAsyncResult result)
        {
            ClientProperties properties = result.AsyncState as ClientProperties;

            // Nel caso in cui la connessione fallisce

            if ((properties == null) || (!properties.client.Connected))
            {
                MessageBox.Show("Impossibile stabilire una connessione");

                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
               {
                   // Abilitazione dei vari componenti dell'interfaccia
                   ConnectButton.IsEnabled = true;
                   txtAddress1.IsEnabled = txtAddress2.IsEnabled = txtAddress3.IsEnabled = txtAddress4.IsEnabled = true;
                   txtPort.IsEnabled = true;
                   this.Cursor = Cursors.Arrow;
               }));

                return;
            }

            NetworkStream stream = properties.client.GetStream();

            // Imposto il tempo in cui il client si mette in attesa di ricevere dati
            stream.ReadTimeout = 5000;

            // Se la connessione ha avuto successo, bisogna verificare se esiste già una MainWindow
            //  - Se esiste, significa che non bisogna crearne una nuova, ma bisogna solo aggiungere un tab
            //  - Se non esiste, significa che bisogna crearne una nuova

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
            {
                // Caso in cui MainWindow esiste già
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is MainWindow)
                    {
                        MainWindow w = window as MainWindow;
                        w.NewTab(properties.client, stream, properties.address);
                        w.ActiveConnections.Add(properties.address);
                        this.Close();
                        return;
                    }
                }

                // Caso in cui MainWindows non esiste: creazione di una nuova MainWindow
                MainWindow main = new MainWindow(properties.client, stream, properties.address);
                main.ActiveConnections.Add(properties.address);
                this.Close();
                main.Show();
            }));

            try
            {
                properties.client.EndConnect(result);
            }
            catch (SocketException) 
            {
                // In caso di errore sul socket: chiusura del nuovo tab
                ExceptionHandler.ConnectionError();
            }
            catch (ObjectDisposedException)
            {
                // In caso di chiusura del socket: chiusura del tab
                ExceptionHandler.ConnectionError();
            }
        }

        /// <summary>
        /// Funzione invocata dall'evento di pressione del tasto Connetti.
        /// Cerca di instaurare una connessione con il server i cui parametri sono indicati nei campi della finestra.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Evento di pressione del tasto "Connetti" </param>
        private void C_ConnectButton(object sender, RoutedEventArgs e)
        {
            string address = txtAddress1.Text + "." + txtAddress2.Text + "." + txtAddress3.Text + "." + txtAddress4.Text;

            if (address =="0.0.0.0")
            {
                MessageBox.Show("Impossibile connettersi all'host specificato", "Attenzione!", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Int32 port;
            try
            {
                // Conversione del testo della porta in Int32 e gestione delle eventuali eccezioni
                port = Convert.ToInt32(txtPort.Text);
            }
            catch (FormatException)
            {
                port = 2000;
            }
            catch (OverflowException)
            {
                port = 2000;
            }

            // Verifica dell'eventuale esistenza di un tab connesso allo stesso indirizzo
            foreach (Window w in System.Windows.Application.Current.Windows)
            {
                if (w is MainWindow)
                {
                    MainWindow m = w as MainWindow;

                    if (m.ActiveConnections.Contains(address))
                    {
                        MessageBox.Show("Il server del quale si è inserito l'indirizzo IP è già collegato ");
                        return;
                    }
                }
            }
            // Nel caso in cui l'indirizzo indicato non sia relativo ad alcun server già connesso, si procede normalmente
            Console.WriteLine("Connessione verso: {0} - {1}", address, port);

            try
            {
                client = new TcpClient();
                ClientProperties properties = new ClientProperties();
                properties.client = client;
                properties.address = address;

                // Invia una richiesta di connessione asincrona: il client non si blocca in attesa del risultato
                // La callback specificata come parametro viene lanciata quando l'operazione di connessione è completa
                this.connectionResult = client.BeginConnect(address, port, new AsyncCallback(ConnectionRequest), properties);

                // Disabilitazione dell'interfaccia per evitare richieste simultanee che non sono gestibili
                ConnectButton.IsEnabled = false;
                txtAddress1.IsEnabled = txtAddress2.IsEnabled = txtAddress3.IsEnabled = txtAddress4.IsEnabled = false;
                txtPort.IsEnabled = false;
                this.Cursor = Cursors.AppStarting; // Cursore relativo ad un'app appena lanciata
            }
            catch (SecurityException)
            {
                MessageBox.Show("Accesso negato: non hai i permessi necessari", "Attenzione!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (ObjectDisposedException)
            {
                MessageBox.Show("Errore di connessione", "Attenzione!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("Numero di porta non valido. I valori ammessi sono [1 - 65536]", "Attenzione!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (SocketException)
            {
                MessageBox.Show("Impossibile stabilire una connessione", "Attenzione!", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
