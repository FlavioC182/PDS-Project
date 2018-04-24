using System;


namespace Client
{
    /// <summary>
    /// Classe che mantiene le informazioni sulle app in foreground
    /// </summary>
    public class ForegroundApp
    {
        /// <summary>
        /// Nome dell'applicazione
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// Numero di server che hanno l'app in foreground
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Costruttore della classe ForegroundApp
        /// </summary>
        /// <param name="name">Nome della nuova applicazione</param>
        /// <param name="count">Numero di server che hanno la nuova applicazione in foreground</param>
        public ForegroundApp (string name, int count)
        {
            Name = name;
            Count = count;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            ForegroundApp p = obj as ForegroundApp;

            if ((object)p == null)
                return false;

            if (Name == p.Name)
                return true;

            //Caso di default
            return false;
        }
    }
}
