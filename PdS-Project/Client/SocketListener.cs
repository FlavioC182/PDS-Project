using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Client
{
    /// <summary>
    /// Classe che incapsula lo stream di lettura dei dati inviati dal server
    /// </summary>
    public class SocketListener
    {
        // Importazione delle librerie Windows utili ad interpretare l'icona inviata dal server
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static bool DestroyIcon(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static IntPtr CreateIconFromResourceEx(IntPtr buffer, uint size, int isIcon, uint dwVer, int cx, int cy, uint flags);

        private volatile bool stop = false;
        private NetworkStream Stream;
        private ServerTabManagement Item;

        /// <summary>
        /// Costruttore della classe SocketListener
        /// </summary>
        /// <param name="s"></param>
        public SocketListener(ServerTabManagement s)
        {
            Item = s;
            Stream = s.Stream;
        }

        /// <summary>
        /// Funzione utilizzata per interrompere il ciclo di lettura del thread sullo stream
        /// </summary>
        public void Stop()
        {
            stop = true;
        }

        /// <summary>
        /// Funzione eseguita da un thread in background: riceve dati dal socket
        /// </summary>
        public void SocketThreadListen()
        {
            int n = 0;

            try
            {
                Byte[] readBuffer = new Byte[1024];

                while (!stop)
                {
                    Console.WriteLine("In attesa di ricevere dati dal server...");

                    // Ricezione del tipo di modifica effettuata
                    n = Stream.Read(readBuffer, 0, sizeof(ushort));

                    if (!readSuccessful(n, sizeof(ushort)))
                        return;

                    // Conversione del buffer nell'ordine dei byte dell'host (Precedentemente era in ordine di rete)
                    ushort conv_mod = BitConverter.ToUInt16(readBuffer, 0);
                    int ModificationType = IPAddress.NetworkToHostOrder((short)conv_mod);
                    Console.WriteLine("Tipo della modifica: {0}", ModificationType);

                    // Ricezione del PID del processo. E' una DWORD che ha dimensioni pari ad uint
                    n = Stream.Read(readBuffer, 0, sizeof(uint));

                    if (!readSuccessful(n, sizeof(uint)))
                        return;

                    uint PID = BitConverter.ToUInt32(readBuffer, 0);

                    Console.WriteLine("PID: {0}", PID);

                    // Switch sul tipo di modifica
                    switch (ModificationType)
                    {
                        // CASO 0: Aggiunta di una nuova applicazione
                        case 0:

                            // Lettura della lunghezza del nome dell'applicazione
                            n = Stream.Read(readBuffer, 0, sizeof(int));

                            if (!readSuccessful(n, sizeof(uint)))
                                return;

                            // Conversione della lunghezza del nome in ordine dell'host
                            int conv_length = BitConverter.ToInt32(readBuffer, 0);
                            Console.WriteLine("Lunghezza convertita: {0}", conv_length);
                            int NameLength = IPAddress.NetworkToHostOrder(conv_length);
                            Console.WriteLine("Lunghezza nome: {0}", NameLength);

                            Byte[] NameBuffer = new Byte[NameLength];

                            String AppName = String.Empty;

                            // Lettura del nome dell'applicazione
                            n = Stream.Read(NameBuffer, 0, NameLength);

                            if (!readSuccessful(n, NameLength))
                                return;

                            try
                            {
                                // Conversione in stringa
                                AppName = System.Text.UnicodeEncoding.Unicode.GetString(NameBuffer);
                                AppName = AppName.Replace("\0", String.Empty);
                            }
                            catch (ArgumentException)
                            {
                                AppName = "Nessun nome";
                            }

                            Console.WriteLine("Nome dell'applicazione: {0}", AppName);

                            // Lettura della lunghezza dell'icona

                            n = Stream.Read(readBuffer, 0, sizeof(int));

                            if (!readSuccessful(n, sizeof(uint)))
                                return;

                            AppItem app = new AppItem(Item.ServerTab.MainWndw.DefaultIcon);
                            app.PID = PID;
                            app.Name = AppName;

                            int conv_icon = BitConverter.ToInt32(readBuffer, 0);
                            int IconLength = IPAddress.HostToNetworkOrder(conv_icon);
                            Console.WriteLine("Lunghezza dell'icona: {0}", IconLength);

                            // Se la dimensione è valida la si sostituisce a quella di default
                            if (IconLength != 0 && IconLength < 1048576)
                            {
                                Console.WriteLine("Icona valida trovata");

                                // Lettura dell'icona dallo stream in blocchi da 1024 byte
                                Byte[] BufferIcon = new Byte[IconLength];

                                int TotalRead = 0;
                                int ToRead = 1024;

                                while (TotalRead != IconLength)
                                {
                                    if (ToRead > IconLength - TotalRead)
                                        ToRead = IconLength - TotalRead;

                                    n = Stream.Read(BufferIcon, TotalRead, ToRead);

                                    if (n == 0)
                                    {
                                        Console.WriteLine("Connessione persa durante la lettura dell'icona");
                                        return;
                                    }

                                    TotalRead += n;

                                }

                                if (TotalRead != IconLength)
                                {
                                    Console.WriteLine("Si è verificato un errore durante la lettura dell'icona");
                                    Item.ServerTab.MainWndw.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                                   {
                                       Item.ServerTab.MainWndw.CloseTab(Item.ServerTab);
                                   }));
                                }

                                unsafe
                                {
                                    fixed (byte* buffer = &BufferIcon[0])
                                    {
                                        IntPtr Hicon = CreateIconFromResourceEx((IntPtr)buffer, (uint)IconLength, 1, 0x00030000, 48, 48, 0);

                                        if (Hicon != null)
                                        {
                                            BitmapFrame bitmap = BitmapFrame.Create(Imaging.CreateBitmapSourceFromHIcon(Hicon, new Int32Rect(0, 0, 48, 48), BitmapSizeOptions.FromEmptyOptions()));
                                            if (bitmap.CanFreeze)
                                            {
                                                bitmap.Freeze();
                                                app.Icon = bitmap;
                                            }

                                            DestroyIcon(Hicon);
                                        }
                                    }
                                }
                            }


                            // Aggiunta di una nuova applicazione e notifica del cambiamento nella lista
                            Item.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() =>
                           {
                               lock (Item.Applications)
                               {
                                   Item.Applications.Add(app);
                               }
                           }));

                            Item.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() =>
                            {
                                Item.ApplistRerrangeView(Item.Applist, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, app));
                            }));

                            break;

                        // Caso 1: rimozione di un'applicazione
                        case 1:
                            Console.WriteLine("Modifica: Rimozione");

                            // Rimozione dell'applicazione dalla lista
                            Monitor.Enter(Item.Applications);
                            foreach (AppItem appItem in Item.Applications)
                            {
                                if (appItem.PID == PID)
                                {
                                    Console.WriteLine("Rimozione applicazione: {0}", appItem.Name);
                                    Monitor.Exit(Item.Applications);
                                    this.Item.Dispatcher.Invoke(DispatcherPriority.Send,
                                        new Action(() => { lock (Item.Applications) { this.Item.Applications.Remove(appItem); } }));
                                    Monitor.Enter(Item.Applications);
                                    break;
                                }
                            }
                            Monitor.Exit(Item.Applications);
                            break;

                        // Caso 3: cambio di focus
                        case 2:
                            Console.WriteLine("Modifica: Change Focus");

                            // Pulizia della selezione precedente
                            this.Item.ServerTab.MainWndw.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { this.Item.Applist.SelectedItem = null; }));


                            // Applicazione che perde il focus
                            this.Item.ServerTab.MainWndw.Dispatcher.Invoke(DispatcherPriority.Send,
                                        new Action(() =>
                                        {
                                            // Aggiornamento lista app in foreground
                                            int index = this.Item.ServerTab.MainWndw.ForegroundApps.IndexOf(new ForegroundApp(Item.ServerTab.ForegroundApp, 0));
                                            if (index != -1)
                                            {
                                                if (--this.Item.ServerTab.MainWndw.ForegroundApps[index].Count <= 0)
                                                    this.Item.ServerTab.MainWndw.ForegroundApps.RemoveAt(index);
                                            }
                                        }));
                            // Ricerca delle applicazioni coinvolte nel cambiamento
                            Monitor.Enter(Item.Applications);
                            foreach (AppItem appItem in Item.Applications)
                            {
                                // Applicazione che guadagna il focus
                                if (appItem.PID == PID)
                                {
                                    Console.WriteLine("Pid: {0} - applicazione: {1}", PID, appItem.Name);
                                    Monitor.Exit(Item.Applications);
                                    this.Item.ServerTab.MainWndw.Dispatcher.Invoke(DispatcherPriority.Send,
                                        new Action(() =>
                                        {
                                            lock (Item.Applications)
                                            {
                                                // Evidenziazione elemento nella tab
                                                appItem.HasFocus = true;
                                                this.Item.Applist.SelectedItem = appItem;
                                                this.Item.ServerTab.ForegroundApp = appItem.Name;
                                                // Aggiornamento lista delle app in foreground
                                                int index = this.Item.ServerTab.MainWndw.ForegroundApps.IndexOf(new ForegroundApp(appItem.Name, 0));
                                                if (index != -1)
                                                    this.Item.ServerTab.MainWndw.ForegroundApps[index].Count++;
                                                else
                                                {
                                                    ForegroundApp newapp = new ForegroundApp(appItem.Name, 1);
                                                    this.Item.ServerTab.MainWndw.ForegroundApps.Add(newapp);
                                                    if (!this.Item.ServerTab.MainWndw.ForegroundAppsBox.IsEnabled)
                                                        this.Item.ServerTab.MainWndw.ForegroundAppsBox.SelectedItem = newapp;
                                                }
                                            }
                                        }));
                                    Monitor.Enter(Item.Applications);
                                }
                                else if (appItem.HasFocus)
                                    appItem.HasFocus = false;
                            }
                            Monitor.Exit(Item.Applications);
                            // Aggiornamento delle percentuali
                            Item.Dispatcher.Invoke(DispatcherPriority.Send,
                                             new Action(() => { Item.PercentageRefresh(); }));
                            break;

                        case 3:
                            break;
                        default:
                            Console.WriteLine("Modifica sconosciuta");
                            break;
                    }
                }
                Console.WriteLine("Thread - terminata ricezione dati dal server");
            }
            catch (NullReferenceException)
            {
                ExceptionHandler.ReceiveConnectionError(Item);
            }
            catch (IOException)
            {
                ExceptionHandler.ReceiveConnectionError(Item);
            }
            catch (ObjectDisposedException)
            {
                ExceptionHandler.ReceiveConnectionError(Item);
            }
            catch (ArgumentOutOfRangeException)
            {
                ExceptionHandler.ReceiveConnectionError(Item);
            }
            catch (OutOfMemoryException)
            {
                ExceptionHandler.MemoryError(Item.ServerTab.MainWndw);
            }
        }


        /// <summary>
        /// Metodo per verificare la corretta lettura dal server.
        /// </summary>
        /// <param name="byteread">Numero di byte letti</param>
        /// <param name="bytetoread">Numero di byte che bisognava leggere</param>
        /// <returns></returns>
        private bool readSuccessful(int byteread, int bytetoread)
        {
            if (byteread == 0)
            {
                Console.WriteLine("Connessione interrotta durante la lettura");
                return false;
            }

            else if (byteread != bytetoread)
            {
                Console.WriteLine("La lettura non ha avuto successo");
                MessageBox.Show("Server" + Item.ServerTab.Header as String + ": Connessione interrotta");
                Item.ServerTab.MainWndw.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
               {
                   Item.ServerTab.MainWndw.CloseTab(Item.ServerTab);
               }));

                return false;

            }

            return true;
        }
    }// Class closing bracket





}// Namespace closing bracket
