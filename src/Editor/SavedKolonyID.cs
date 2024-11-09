using Kub.Util;
using System;

namespace Kub.Kolinizer
{
    /// <summary>
    /// Class to persist the last entered Kolony ID.
    /// </summary>
    [Serializable]
    public class SavedKolonyID : Persist<SavedKolonyID>
    {
        /// <summary>
        /// Key to store under
        /// </summary>
        protected override string PREF_NAME { get; set; } = "Kub.KolonyID";

        /// <summary>
        /// The Kolony ID as entered by developer
        /// </summary>
        public string KolonyID;
    }
}
