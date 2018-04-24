using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Client
{
    [Flags]
    public enum mod_code: byte { none=0, shift=1, ctrl=2, alt=4}

    /// <summary>
    /// Definizione della componente della classe MainWindow in grado di gestire la pressione dei tasti
    /// </summary>
    public partial class MainWindow : Window
    {
        private mod_code modifier = mod_code.none;

        private bool capturing = false;

        /// <summary>
        /// Funzione invocata dall'evento KeyPressed.
        /// Cattura e registra i modificatori premuti ed invia i tasti premuti.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientKeyPressed (object sender, KeyEventArgs e)
        {
            Key key;
            if (e.Key == Key.System)
                key = e.SystemKey;
            else
                key = e.Key;

            switch (key)
            {
                case Key.LeftShift:
                case Key.RightShift:
                    modifier = modifier | mod_code.shift;
                    Shift.Fill = new SolidColorBrush(Colors.Blue);
                    e.Handled = true;
                    break;

                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifier = modifier | mod_code.ctrl;
                    Ctrl.Fill = new SolidColorBrush(Colors.Blue);
                    e.Handled = true;
                    break;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifier = modifier | mod_code.alt;
                    Alt.Fill = new SolidColorBrush(Colors.Blue);
                    e.Handled = true;
                    break;

                default:
                    break;
            } // Switch closing bracket
            
            // Nel caso in cui il tasto premuto non sia un modificatore
            if (e.Handled == false)
            {
                // Preparazione dei dati da inviare
                int conv_key = KeyInterop.VirtualKeyFromKey(key);
                byte[] buffer = new byte[1 + sizeof(int)];          // Struttura che conterrà (Modificatori + tasto)
                buffer[0] = (byte)modifier;
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder(conv_key)).CopyTo(buffer, 1);

                // Recupero l'applicazione che è in focus dall'elemento selezionato nella combobox

                ForegroundApp appinfocus = ForegroundAppsBox.SelectedItem as ForegroundApp;
                if (appinfocus == null)
                {
                    e.Handled = true;
                    return;
                }

                // Se c'è almeno un app in focus, cerchiamo il tab o server a cui appartiene 
                foreach (DynamicTabItem tab in ServerTabs)
                {
                    if (tab.ForegroundApp == appinfocus.Name)
                    {
                        ServerTabManagement s = tab.Content as ServerTabManagement;
                        if (s != null)
                            try
                            {
                                s.Stream.BeginWrite(buffer, 0, 1 + sizeof(int), new AsyncCallback(SendToServer), s);
                            }
                            catch (IOException)
                            {
                                ExceptionHandler.SendError(s);
                            }
                    }
                }

                e.Handled = true;
            }

        } // ClientKeyPressed closing bracket


        /// <summary>
        /// Metodo che gestisce la terminazione dell'invio di dati al server
        /// </summary>
        /// <param name="a"></param>
        private void SendToServer (IAsyncResult a)
        {
            ServerTabManagement s = (ServerTabManagement)a.AsyncState;

            try
            {
                s.Stream.EndWrite(a);
            }
            catch (IOException)
            {
                ExceptionHandler.SendError(s);
            }
        }

        /// <summary>
        /// Funzione richiamata all'accorrere dell'evento di rilascio del tasto KeyReleased
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientKeyReleased (object sender, KeyEventArgs e)
        {
            Key key;

            if (e.Key == Key.System)
                key = e.SystemKey;
            else
                key = e.Key;

            switch (key)
            {
                case Key.LeftShift:
                case Key.RightShift:
                    modifier = modifier & ~mod_code.shift;
                    Shift.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifier = modifier & ~mod_code.ctrl;
                    Ctrl.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifier = modifier & ~mod_code.alt;
                    Alt.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;
                default:
                    break;
            }
        } // ClientKeyReleased closing bracket

        /// <summary>
        /// Funzione richiamata all'atto dell'attivazione della checkbox dell'Invio comandi
        /// Aggiunge agli eventi di pressione e rilascio tasti le funzioni relative
        /// Abilita la cattura e l'invio di tasti e modificatori
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendCommands_Checked (object sender, RoutedEventArgs e)
        {
            Shift.Fill = new SolidColorBrush(Colors.White);
            Ctrl.Fill = new SolidColorBrush(Colors.White);
            Alt.Fill = new SolidColorBrush(Colors.White);

            modifier = mod_code.none;

            this.PreviewKeyDown += ClientKeyPressed;
            this.PreviewKeyUp += ClientKeyReleased;

            capturing = true;
        }

        /// <summary>
        /// Funzione richiamata all'atto della disattivazione della checkbox dell'Invio comandi
        /// Rimuove agli eventi di pressione e rilascio tasti le funzioni relative
        /// Disabilita la cattura di modificatori e tasti
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendCommands_Unchecked (object sender, RoutedEventArgs e)
        {
            this.PreviewKeyDown -= ClientKeyPressed;
            this.PreviewKeyUp -= ClientKeyReleased;
            capturing = false;
        }
             
    }
}
