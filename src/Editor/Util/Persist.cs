using UnityEngine;
using Newtonsoft.Json;

namespace Kub.Util
{
    /// <summary>
    /// Persist a serializable data class to Unity PlayerPrefs.
    /// </summary>
    /// <typeparam name="Tclass">A serializable class</typeparam>
    /// <remarks>
    /// Converts a class to a JSON string and stores it using the PlayerPrefs api.
    /// No definitive size limit as this is platform dependant.
    /// This class will warn in the console if saving over 1MB.
    /// </remarks>
    /// <example>
    /// [Serializable]
    /// public class SavedLogin : Persist<SavedLogin>
    /// {
    ///    public string Email;
    ///    public string Password; // todo saved in obfuscated form
    ///
    ///    protected override string PREF_NAME { get; set; } = "EventStream.SavedLogin";
    /// }
    /// </example>         
    [AddComponentMenu("Kub/Storage/Persist")]
    public abstract class Persist<Tclass>
    where Tclass : class, new()
    {
        protected abstract string PREF_NAME { get; set; }

        public void Load()
        {
            LoadSelf(PREF_NAME);
        }

        public void Save()
        {
            Save(PREF_NAME);
        }

        public void Clear()
        {
            ClearSavedData(PREF_NAME);
        }

        public static Tclass Load(string prefName)
        {
            string json = PlayerPrefs.GetString(prefName, null);
            if (string.IsNullOrEmpty(json))
            {
                return new Tclass();
            }
            return JsonConvert.DeserializeObject<Tclass>(json); // JsonUtility.FromJson<Tclass>(json);
        }

        public static void Save(Tclass state, string prefName)
        {
            string json = JsonConvert.SerializeObject(state);// JsonUtility.ToJson(state);
            if (json.Length > 1000000)
            {
                Debug.LogWarning("Trying to persist over 1MB");
            }
            PlayerPrefs.SetString(prefName, json);
        }
        public static void ClearSavedData(string prefName)
        {
            Tclass freshClass = new Tclass();
            Save(freshClass, prefName);
        }

        public void LoadSelf(string prefName)
        {
            string json = PlayerPrefs.GetString(prefName, null);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            JsonConvert.PopulateObject(json, this);  //JsonUtility.FromJsonOverwrite(json, this);
        }

        public void Save(string prefName)
        {
            Save(state: this as Tclass, prefName);
        }
    }
}
