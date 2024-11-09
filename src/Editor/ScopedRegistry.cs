using Kub.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Kub.Kolinizer
{
    /// <summary>
    /// Utility class to work with Unity Scoped Registries.
    /// * Sets up authentication in upmconfig.toml.
    /// * Sets up scoped registry in /Packages/manifest.json.
    /// </summary>
    public class ScopedRegistry
    {
        private const string _PackageManifestPath = "Packages/manifest.json";
        private const string _NpmBaseURL = "https://npm.cloudsmith.io/kubs/repo";
        private const string _ScopedRegistryName = "Kubs for Unity";
        private string _upmConfigPath;
        private string UpmConfigFilePath => _upmConfigPath ??= GetUpmConfigPath();

        #region Load / Save Package manifest.json
        private static void SaveManifest(JObject manifest)
        {
            File.WriteAllText(_PackageManifestPath, manifest.ToString());
        }

        private static JObject LoadManifest()
        {
            if (!File.Exists(_PackageManifestPath))
            {
                Log.E($"Unable to load {_PackageManifestPath}");
                return null;
            }

            string json = File.ReadAllText(_PackageManifestPath);
            return JObject.Parse(json);
        }
        #endregion

        /// <summary>
        /// Add the client token to the global upmconfig.toml file.
        /// This allows authentication to the Kubs private scoped registry.
        /// </summary>
        /// <param name="clientNpmEntitlementToken">Clients entitlement token from CloudSmith account</param>
        public bool UpdateUpmConfig(string clientNpmEntitlementToken)
        {
            if (string.IsNullOrWhiteSpace(UpmConfigFilePath))
            {
                return false;
            }

            if (!File.Exists(UpmConfigFilePath))
            {
                return CreateUpmConfig(clientNpmEntitlementToken);
            }

            // Search for Kubs NPM entry in the existing upmconfig.toml
            //
            string[] upmLines = File.ReadAllLines(UpmConfigFilePath);
            for (int i = 0; i < upmLines.Length; i++)
            {
                if (upmLines[i].Contains(_NpmBaseURL))
                {
                    // Found Kub NPM Scoped Registry - Replace with latest EntitlementToken
                    //
                    upmLines[i + 1] = $"token = \"{clientNpmEntitlementToken}\"";

                    File.WriteAllLines(UpmConfigFilePath, upmLines);
                    return true;
                }
            }

            // Kubs NPM Scoped Registry Not Found - Append it
            //
            File.AppendAllLines(UpmConfigFilePath, 
                UpmConfig_KubsNPMAuthEntry(clientNpmEntitlementToken));

            return true;
        }        

        private static string[] UpmConfig_KubsNPMAuthEntry(string clientNpmEntitlementToken)
        {
            return new string[]
            {
                $"[npmAuth.\"{_NpmBaseURL}\"]",
                $"token = \"{clientNpmEntitlementToken}\""
            };

        }

        private string GetUpmConfigPath()
        {
            string fileName = "upmconfig.toml";
            string tomlPath = string.Empty;

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    tomlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Unity", "Editor", fileName);
                    break;
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.OSXEditor:
                    tomlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Preferences", "Unity", fileName);
                    break;
                default:
                    Log.E($"Unsupported Application.platform: {Application.platform}");
                    break;
            }
            Log.D($"UPM Config path: {tomlPath}");

            // Check environment variable UPM_USER_CONFIG_FILE for full file path override.
            var envPath = Environment.GetEnvironmentVariable("UPM_USER_CONFIG_FILE", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                tomlPath = envPath;
                Log.D($"UPM Config path override from environment UPM_USER_CONFIG_FILE: {tomlPath}");
            }

            return tomlPath;
        }

        private bool CreateUpmConfig(string clientNpmEntitlementToken)
        {
            string path = GetUpmConfigPath();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            File.WriteAllLines(path, 
                UpmConfig_KubsNPMAuthEntry(clientNpmEntitlementToken));
            return true;
        }

        public bool AddScopedRegistryAuth(KolonyConfig kolonyConfig)
        {
            string upmPath = GetUpmConfigPath();
            if (string.IsNullOrWhiteSpace(upmPath))
            {
                return false;
            }

            return ScopedRegistry.AddKubNpmRegistry(kolonyConfig);
        }

        /// <summary>
        /// Parse KolonyConfig for all NPM package names and return that list.
        /// </summary>
        /// <param name="kolonyConfig"></param>
        /// <returns>JArray of all npm package names, or null if error</returns>
        private static JArray GetKubScopes(KolonyConfig kolonyConfig)
        {
            List<string> scopeList = new(100);

            foreach(var kub in kolonyConfig.kubs)
            {
                scopeList.Add(kub.packageName);
            }
            foreach(var provider in kolonyConfig.providers)
            {
                scopeList.Add(provider.providerDriver.packageName);

                foreach(var driver in provider.drivers)
                {
                    scopeList.Add(driver.packageName);
                }
            }            

            return new JArray(scopeList);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scopeName"></param>
        /// <returns>true if successful, false if error occured</returns>
        public static bool AddKubNpmRegistry(KolonyConfig kolonyConfig)
        {
            JObject manifest = LoadManifest();

            if (manifest == null)
            {
                return false;
            }

            JArray scopedRegistries = manifest["scopedRegistries"] as JArray;

            if (scopedRegistries == null)
            {
                scopedRegistries = new JArray();
                manifest["scopedRegistries"] = scopedRegistries;
            }

            JArray scopes = GetKubScopes(kolonyConfig);
            if (scopes == null)
            {
                Log.E("Unable to load Kub PackageNames from KolonyConfig.json");
                return false;
            }

            var kubUnityRegistry = new JObject
            {
                ["name"] = _ScopedRegistryName,
                ["url"] = _NpmBaseURL,
                ["scopes"] = scopes
            };

            // Remove existing registries if they exist
            scopedRegistries.Remove(scopedRegistries.FirstOrDefault(r => (string)r["name"] == _ScopedRegistryName));

            // Add new registries
            scopedRegistries.Add(kubUnityRegistry);

            SaveManifest(manifest);
            return true;
        }
    }
}
