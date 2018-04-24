using System;
using System.Windows;
using System.Windows.Threading;

namespace Client
{
    static class ExceptionHandler
    {
        static public void ReceiveConnectionError(ServerTabManagement Item)
        {
            MessageBox.Show("Errore di connessione.", "Attenzione!", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (Item.ServerTab.MainWndw.ServerTabs.Count == 1)
                Item.ServerTab.MainWndw.Error = true;
            Item.ServerTab.MainWndw.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() =>
            {
                Item.ServerTab.MainWndw.CloseTab(Item.ServerTab);
            }));
        }

        static public void MemoryError(MainWindow main)
        {
            MessageBox.Show("Errore di memoria.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            main.Error = true;
            main.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { Application.Current.Shutdown(); }));
        }

        static public void MemoryError(uint attempt, MainWindow main)
        {
            if (attempt > 1)
            {
                attempt--;
                System.GC.Collect();
            }
            else
            {
                MessageBox.Show("Errore irreversibile di memoria.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                main.Error = true;
                main.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { Application.Current.Shutdown(); }));
            }
        }

        static public void SendError(ServerTabManagement Item)
        {
            MessageBoxResult res = MessageBox.Show("Errore durante l'invio del comando. Chiudere la connessione?", "Attenzione!", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
                Item.ServerTab.MainWndw.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { Item.ServerTab.MainWndw.CloseTab(Item.ServerTab); }));
        }

        static public void ConnectionError()
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MainWindow)
                {
                    MainWindow w = window as MainWindow;
                    ExceptionHandler.ReceiveConnectionError(w.ServerTabs[w.ServerTabs.Count - 1].ServerTab);
                    break;
                }
            }
        }
    }
}