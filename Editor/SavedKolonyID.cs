using Kub.Util;
using System;

namespace Kub.Kolinizer
{
    [Serializable]
    public class SavedKolonyID : Persist<SavedKolonyID>
    {
        protected override string PREF_NAME { get; set; } = "Kub.KolonyID";
        public string KolonyID;
    }
}
