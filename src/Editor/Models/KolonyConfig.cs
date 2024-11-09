using System;
using System.Collections.Generic;

namespace Kub.Util
{
    [Serializable]
    public class KolonyConfig
    {
        [Serializable]
        public class AssemblyLocation
        {
            public string packageName; // NPM package name
            public string gitUrl; // Null unless stored via Git
        }

        [Serializable]
        public class KubConfig : AssemblyLocation
        {
            public List<string> providerNames;
        }

        /// <summary>
        /// Provider credentials
        /// </summary>
        [Serializable]
        public class ProviderConfig : AssemblyLocation
        {
            [Serializable]
            public class DriverDefinition : AssemblyLocation
            {
                public string assemblyName;
                public string className;
            }

            [Serializable]
            public class ProviderCredentials
            {
                public string serverUrl;
                public string secret;
                public string appId;
                public string version;
                /// <summary>
                /// Optional, use if provider has special credentials needs
                /// </summary>
                public string customJson;
            }

            public string name;
            public ProviderCredentials credentials;
            public DriverDefinition providerDriver;
            public List<DriverDefinition> drivers;
        }        

        public string kolonyId = "1";
        public string npmEntitlementToken = "1";

        //public List<AssemblyLocation> dependencies;
        public List<KubConfig> kubs;
        public List<ProviderConfig> providers;
    }
}