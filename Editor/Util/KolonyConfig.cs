using System;
using System.Collections.Generic;

namespace Kub.Util
{
    [Serializable]
    public class KolonyConfig
    {
        public static string kolonyId = "1";

        [Serializable]
        public class AssemblyLocation
        {
            public string name;
            public string npmPackageName;
            public string location;
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
        public class ProviderConfig
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
            public string location;
            public ProviderCredentials credentials;
            public DriverDefinition providerDriver;
            public List<DriverDefinition> drivers; 
        }

        public string npmEntitlementToken = "1";

        public List<AssemblyLocation> dependencies;
        public List<KubConfig> kubs;
        public List<ProviderConfig> providers;

        //#region TEST CONFIGS
        ///// <summary>
        ///// Key = KubName
        ///// </summary>
        //public static List<KubConfig> test_kubs = new()
        //{
        //    new()
        //    {
        //        name = KubName.Auth.ToString(),
        //        location = "https://github.com/Cubes-Library/KubAuth.git",
        //        providerNames = new()
        //        {
        //            WellKnownProviders.BrainCloud.ToString()
        //        }
        //    },
        //    new()
        //    {
        //        name = KubName.Analytics.ToString(),
        //        location = "https://github.com/Cubes-Library/KubAnalytics.git",
        //        providerNames = new()
        //        {
        //            WellKnownProviders.BrainCloud.ToString()
        //        }
        //    },
        //    //new()
        //    //{
        //    //    name = KubName.Chat.ToString(),
        //    //    location = "https://github.com/Cubes-Library/KubChat.git",
        //    //    providerNames = new()
        //    //    {
        //    //        WellKnownProviders.BrainCloud.ToString()
        //    //    }
        //    //},
        //    new()
        //    {
        //        name = KubName.Leaderboard.ToString(),
        //        location = "https://github.com/Cubes-Library/KubLeaderboard.git",
        //        providerNames = new()
        //        {
        //            WellKnownProviders.BrainCloud.ToString()
        //        }
        //    },
        //    new()
        //    {
        //        name = KubName.Mail.ToString(),
        //        location = "https://github.com/Cubes-Library/KubMail.git",
        //        providerNames = new()
        //        {
        //            WellKnownProviders.BrainCloud.ToString()
        //        }
        //    },
        //    new()
        //    {
        //        name = KubName.Persona.ToString(),
        //        location = "https://github.com/Cubes-Library/KubPersona.git",
        //        providerNames = new()
        //        {
        //            WellKnownProviders.BrainCloud.ToString()
        //        }
        //    }
        //};

        ///// <summary>
        ///// Key = ProviderName
        ///// </summary>
        //public static List<ProviderConfig> test_providers = new()
        //{
        //    new ProviderConfig
        //    {
        //        name = WellKnownProviders.BrainCloud.ToString(),
        //        location = "https://github.com/getbraincloud/braincloud-unity-package.git",
        //        credentials = new()
        //        {
        //            serverUrl = "https://api.braincloudservers.com/dispatcherv2",
        //            secret = "e2f1acf6-2f43-4611-8149-72bfc77a424d",
        //            appId = "14848",
        //            version = "1.0.0"
        //        },
        //        providerDriver = new()
        //        {
        //            name = WellKnownProviders.BrainCloud.ToString(),
        //            location = "https://github.com/Cubes-Library/ProviderBrainCloud.git",
        //            assemblyName = "kub.driver.braincloud",
        //            className = "Kub.Providers.BrainCloud_Provider"
        //        },
        //        drivers = new()
        //        {
        //            new()
        //            {
        //                name = KubName.Auth.ToString(),
        //                //location = "https://github.com/Cubes-Library/KubAuth.git",
        //                assemblyName = "kub.auth.driver.braincloud",
        //                className = "Kub.Auth.Driver.AuthDriver_BrainCloud"
        //            },
        //            new()
        //            {
        //                name = KubName.Analytics.ToString(),
        //                //location = "https://github.com/Cubes-Library/KubAnalytics.git",
        //                assemblyName = "kub.analytics.driver.braincloud",
        //                className = "Kub.Analytics.Driver.AnalyticsDriver_BrainCloud"
        //            },
        //            new()
        //            {
        //                name = KubName.Chat.ToString(),
        //                //location = "https://github.com/Cubes-Library/KubChat.git",
        //                assemblyName = "kub.chat.driver.braincloud",
        //                className = "Kub.Chat.Driver.ChatDriver_BrainCloud"
        //            },
        //            new()
        //            {
        //                name = KubName.Leaderboard.ToString(),
        //                //location = "https://github.com/Cubes-Library/KubLeaderboard.git",
        //                assemblyName = "kub.leaderboard.driver.braincloud",
        //                className = "Kub.Leaderboard.Driver.LeaderboardDriver_BrainCloud"
        //            },
        //            new()
        //            {
        //                name = KubName.Mail.ToString(),
        //                //location = "https://github.com/Cubes-Library/KubMail.git",
        //                assemblyName = "kub.mail.driver.braincloud",
        //                className = "Kub.Mail.Driver.MailDriver_BrainCloud"
        //            },
        //            new()
        //            {
        //                name = KubName.Persona.ToString(),
        //                //location = "https://github.com/Cubes-Library/KubPersona.git",
        //                assemblyName = "kub.persona.driver.braincloud",
        //                className = "Kub.Persona.Driver.PersonaDriver_BrainCloud"
        //            },
        //        }
        //    }
        //};
        //#endregion
    }
}
