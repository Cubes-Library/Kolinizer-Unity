using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using Newtonsoft.Json;
using Kub.Util;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace Kub.Kolinizer
{
    /// <summary>
    /// Unity package installer for Kubs
    /// </summary>
    public class Kolinizer : EditorWindow
    {
        #region Constants
        private const string welcomeName = "Kub Kolonizer";
        private const string installationName = "Kolonizer Installation";
        private const string errorName = "Kolonizer Error";

        //Git path on URL.  Save this comment. -chuck
        //private const string runtimeBasePath = "?path=/Runtime/Base";

        private const string Logo_PKG_PATH = "Packages/kub.kolinizer.unity/src/Editor/Kub_Logo.png";
        private const string Logo_DEBUG_PATH = "Assets/Kub/Kolinizer/UnityBuild/src/Editor/Kub_Logo.png";

        private const string GuiSkin_PKG_PATH = "Packages/kub.kolinizer.unity/src/Editor/GUI/KolonizerSkin.guiskin";
        private const string GuiSkin_DEBUG_PATH = "Assets/Kub/Kolinizer/UnityBuild/src/Editor/GUI/KolonizerSkin.guiskin";

        private const string _KolonyConfigPath = "Assets/Resources/Kubs/KolonyConfig.asset";

        /// <summary>
        /// REST URL to get KolonyConfig.  Internally grabs from firebase.
        /// </summary>
        private const string Endpoint_GetKolony = "https://getkolony-cplqdkn7ya-uc.a.run.app";

        #endregion

        #region Static Variables
        private static string windowName = welcomeName;

        /// <summary>
        /// TODO: store in editor pref
        /// </summary>
        public static bool IsKolinizerRunning { get; private set; } = false;
        /// <summary>
        /// TODO: store in editor pref
        /// </summary>
        public static bool IsRefreshEntitlementTokenRunning { get; private set; } = false;
        #endregion

        #region Private GUI Variables
        private enum KolinizerState
        {
            Welcome,
            Installation,
            InstallComplete,
            Error
        }

        private KolinizerState state = KolinizerState.Welcome;


        private GUISkin skin = null;
        private bool showDetailedResults = false;

        private float minWidth;
        private float minHeight;
        private float maxWidth;
        private float maxHeight;
        #endregion

        #region Private Kolonizer Variables
        private ScopedRegistry _scopedRegistry;
        private SavedKolonyID _savedKolonyId;
        //private KolonyConfig _localKolonyConfig;    // Previously Downloaded    
        private KolonyConfig _webKolonyConfig;      // Latest Get
        
        private class PackageStatus
        {
            public List<string> PreviouslyInstalled = new(100);
            public List<string> ToInstall = new(100);
            public List<string> ToRemove = new(100);

            public AddAndRemoveRequest UPM_AddAndRemoveRequest;
            public AddRequest UPM_AddRequest;
            public RemoveRequest UPM_RemoveRequest;
            public ListRequest UPM_ListRequest;
        }
        private PackageStatus _pkgs;
        
        private AsyncAutoResetEvent _autoResetEvent;
        
        private class InstallationCounters
        {
            public int num_deps;
            public int num_kubs;
            public int num_drivers;
            public int num_providers;

            public int num_added;
            public int num_removed;
            public int num_installed;

            public int num_error;
            public int package_installed;
            
            public int num_Total => num_kubs + num_drivers + num_providers;

            public bool InstallComplete;

            public void Clear()
            {
                num_deps = 0;
                num_kubs = 0;
                num_drivers = 0;
                num_providers = 0;

                num_added = 0;
                num_removed = 0;
                num_installed = 0;
                num_error = 0;

                package_installed = 0;

                InstallComplete = false;
            }
        }
        private InstallationCounters _installCount;        
        
        private string errorTitle;
        private string resultLog;
        
        private string currentPackageName = string.Empty;
        #endregion

        public string KolonyId
        {
            get
            {
                if (_savedKolonyId == null)
                {
                    _savedKolonyId = new SavedKolonyID();
                    _savedKolonyId.Load();
                }
                if (string.IsNullOrEmpty(_savedKolonyId.KolonyID))
                {
                    return string.Empty;
                }
                return _savedKolonyId.KolonyID;
            }
            private set
            {
                if (_savedKolonyId == null)
                {
                    _savedKolonyId = new SavedKolonyID();
                    _savedKolonyId.Load();
                }
                if (_savedKolonyId.KolonyID == value)
                {
                    return;
                }
                _savedKolonyId.KolonyID = value;
            }
        }

        #region Menu Items
        [MenuItem("Kub/Kolonizer")]
        public static void ShowWindow()
        {
            // TODO: Domain Refresh will reset this static var.
            // Must use an editor pref to monitor state.
            // Also must set to false when closing the window.
            if (IsKolinizerRunning)
            {
                return;
            }
            IsKolinizerRunning = true;

            GetWindow<Kolinizer>(windowName);
        }
        private void OnDestroy()
        {
            IsKolinizerRunning = false;
        }
        /// <summary>
        /// TODO:  Should be called from a tab on the main UI and not a separate entity from menu. -chuck
        /// 
        /// flow:
        /// * reg - kolonyconfig must already be downloaded (it has the kolonyID and Entitlement token
        /// * optional - could re-get the kolonyconfig with the kolonyid stored in the current config
        /// * reg - add or modify .toml
        /// </summary>
        [MenuItem("Kub/Refresh Entitilement Token")]
        public static void UpdateUPMConfigTOML()
        {
            // TODO: Domain Refresh will reset this static var.
            // Must use an editor pref to monitor state.
            if (IsRefreshEntitlementTokenRunning)
            {
                return;
            }
            IsRefreshEntitlementTokenRunning = true;

            // TODO: The work. -chuck

            IsRefreshEntitlementTokenRunning = false;
        }
        #endregion

        #region GUI Methods
        [ExecuteInEditMode]
        private void OnEnable()
        {
            string assetPath = GuiSkin_DEBUG_PATH;

            if (!File.Exists(assetPath))
            {
                assetPath = GuiSkin_PKG_PATH;
            }

            skin = (GUISkin)AssetDatabase.LoadAssetAtPath(assetPath, typeof(GUISkin));
        }

        [ExecuteInEditMode]
        private void OnGUI()
        {
            // Set Skin if Found
            if (skin != null)
            {
                GUI.skin = skin;
            }

            // Display Kolonizer Image
            DrawKolonizerImage();

            // Draw Window Based on State
            switch (state)
            {
                case KolinizerState.Welcome:
                    windowName = welcomeName;
                    DrawWelcome();
                    break;
                case KolinizerState.InstallComplete:
                case KolinizerState.Installation:
                    windowName = installationName;
                    DrawInstallation();
                    break;
                case KolinizerState.Error:
                    windowName = errorName;
                    DrawError();
                    break;
            }

            // Set Window Sizing
            this.maxSize = new Vector2(maxWidth, maxHeight);
            this.minSize = new Vector2(minWidth, minHeight);

            // Reset Skin
            GUI.skin = null;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++) pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        private void DrawCustomProgressBar(Rect rect, float progress)
        {
            GUILayout.BeginVertical();
            GUIStyle progressBarStyle = new GUIStyle(GUI.skin.box);
            progressBarStyle.normal.background = MakeTex(1, 1, new Color(0.243f, 0.612f, 0.440f, 1f)); // #3EBD71 - Green to match Kub green in cover image

            GUIStyle backgroundBarStyle = new GUIStyle(GUI.skin.box);
            backgroundBarStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 1f));
            backgroundBarStyle.border = new RectOffset(2, 2, 2, 2);

            Rect progressRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
            Rect backgroundRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            progressRect.position = new Vector2((this.position.width / 2) - (backgroundRect.width / 2), progressRect.position.y);
            backgroundRect.position = progressRect.position;

            GUI.Box(backgroundRect, GUIContent.none, backgroundBarStyle);
            GUI.Box(progressRect, GUIContent.none, progressBarStyle);
            GUILayout.EndVertical();
        }

        private void DrawWelcome()
        {
            try
            {
                GUILayout.BeginVertical();

                // Breaker Line
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Kolony ID Prompt
                GUILayout.BeginHorizontal();
                GUIStyle myStylePrompt = new GUIStyle(GUI.skin.label);

                GUILayout.Label("Please enter your Kolony ID", myStylePrompt);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Breaker Line
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Kolony ID Text Field
                GUILayout.BeginHorizontal();
                GUIStyle myStyleTextField = new GUIStyle(GUI.skin.textField);
                myStyleTextField.normal.background = MakeTex(1, 1, Color.black);

                KolonyId = GUILayout.TextField(KolonyId, 100, myStyleTextField);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Breaker Line
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Button - Download and Install Kolony
                GUILayout.BeginHorizontal();
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.normal.background = MakeTex(1, 1, new Color(0.243f, 0.612f, 0.440f, 1f)); // #3EBD71 - Green to match Kub green in cover image
                buttonStyle.active.background = MakeTex(1, 1, new Color(0.216f, 0.440f, 0.333f, 1f)); // #379077 - Darker green than Kub green in cover image

                var txtAsset = (TextAsset)AssetDatabase.LoadAssetAtPath(_KolonyConfigPath, typeof(TextAsset));
                string buttonName = string.Empty;

                if (txtAsset != null)
                    buttonName = "Refresh";
                else
                    buttonName = "Install";

                if (GUILayout.Button(buttonName, buttonStyle))
                {
                    if (!string.IsNullOrEmpty(KolonyId))
                    {
                        _savedKolonyId.Save();

                        _ = UpdateKolony();
                    }
                }

                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Breaker Lines
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            catch
            {
                state = KolinizerState.Error;
                this.Repaint();
            }
        }

        private void DrawInstallation()
        {
            GUILayout.BeginVertical();

            // Breaker Line
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            // All Packages Installed Notice
            if (_installCount.InstallComplete)
            {
                GUILayout.BeginHorizontal();

                GUIStyle myStyleTitleSuccess = new GUIStyle(GUI.skin.label);
                myStyleTitleSuccess.fontSize = 18;
                myStyleTitleSuccess.normal.textColor = Color.green;

                GUILayout.Label("All Packages Installed", myStyleTitleSuccess);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();
            }

            // Installation In Progress
            else if (_installCount.num_added > 0)
            {
                GUILayout.BeginHorizontal();
                GUIStyle myStyleTitleWorking = new GUIStyle(GUI.skin.label);
                myStyleTitleWorking.fontSize = 16;
                myStyleTitleWorking.normal.textColor = Color.yellow;

                float progress = 1f;
                float installedCnt = _installCount.num_installed + _installCount.num_added;
                if (_installCount.num_Total > 0)
                {
                    progress = installedCnt / _installCount.num_Total;
                }

                string progressText = "Installing: " + installedCnt + "/" + _installCount.num_Total + "\n" + "(" + currentPackageName + ")";

                GUILayout.Label(progressText, myStyleTitleWorking);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                Rect progressRect = GUILayoutUtility.GetRect(100, 20);
                DrawCustomProgressBar(progressRect, progress);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();
            }

            // Accordion for detailed results
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            GUIStyle myStyleResult = new GUIStyle(GUI.skin.label);
            myStyleResult.alignment = TextAnchor.MiddleLeft;

            if (_installCount.num_error > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Errors: {_installCount.num_error}. Please check the Unity Debug log.", myStyleResult);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();
            }

            // TODO: make this a toggled value. -chuck
            showDetailedResults = true;

            if (showDetailedResults)
            {
                // GUI Styles
                myStyleResult.margin = new RectOffset(15, 15, 0, 0);
                myStyleResult.padding = new RectOffset(15, 15, 0, 0);

                GUIStyle myStyleResultValue = new GUIStyle(GUI.skin.label);
                myStyleResultValue.alignment = TextAnchor.MiddleRight;
                myStyleResultValue.margin = new RectOffset(15, 15, 0, 0);
                myStyleResultValue.padding = new RectOffset(15, 15, 0, 0);

                GUIStyle myStyleResultValueGoodLeft = new GUIStyle(GUI.skin.label);
                myStyleResultValueGoodLeft.alignment = TextAnchor.MiddleLeft;
                myStyleResultValueGoodLeft.normal.textColor = Color.green;
                myStyleResultValueGoodLeft.hover.textColor = Color.green;
                myStyleResultValueGoodLeft.margin = new RectOffset(15, 15, 0, 0);
                myStyleResultValueGoodLeft.padding = new RectOffset(15, 15, 0, 0);

                GUIStyle myStyleResultValueGoodRight = new GUIStyle(GUI.skin.label);
                myStyleResultValueGoodRight.alignment = TextAnchor.MiddleRight;
                myStyleResultValueGoodRight.normal.textColor = Color.green;
                myStyleResultValueGoodRight.hover.textColor = Color.green;
                myStyleResultValueGoodRight.margin = new RectOffset(15, 15, 0, 0);
                myStyleResultValueGoodRight.padding = new RectOffset(15, 15, 0, 0);

                GUIStyle myStyleResultValueErrorLeft = new GUIStyle(GUI.skin.label);
                myStyleResultValueErrorLeft.alignment = TextAnchor.MiddleLeft;
                myStyleResultValueErrorLeft.normal.textColor = Color.red;
                myStyleResultValueErrorLeft.hover.textColor = Color.red;
                myStyleResultValueErrorLeft.margin = new RectOffset(15, 15, 0, 0);
                myStyleResultValueErrorLeft.padding = new RectOffset(15, 15, 0, 0);

                GUIStyle myStyleResultValueErrorRight = new GUIStyle(GUI.skin.label);
                myStyleResultValueErrorRight.alignment = TextAnchor.MiddleRight;
                myStyleResultValueErrorRight.normal.textColor = Color.red;
                myStyleResultValueErrorRight.hover.textColor = Color.red;
                myStyleResultValueErrorRight.margin = new RectOffset(15, 15, 0, 0);
                myStyleResultValueErrorRight.padding = new RectOffset(15, 15, 0, 0);

                GUIStyle myStyleTitleError = new GUIStyle(GUI.skin.label);
                myStyleTitleError.fontSize = 18;
                myStyleTitleError.normal.textColor = Color.red;
                myStyleTitleError.hover.textColor = Color.red;

                GUILayout.Space(10);

                // Kubs Installed
                EditorGUI.indentLevel++;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Kubs:", myStyleResult);
                GUILayout.Label($"{_installCount.num_kubs}", myStyleResultValue);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Drivers Installed
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Drivers:", myStyleResult);
                GUILayout.Label($"{_installCount.num_drivers}", myStyleResultValue);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Providers Installed.  These don't have a package, confusing to add here. -chuck
                //GUILayout.BeginHorizontal();
                //GUILayout.Label($"Providers:", myStyleResult);
                //GUILayout.Label($"{_installCount.num_providers}", myStyleResultValue);
                //minHeight += GUILayoutUtility.GetLastRect().height;
                //GUILayout.EndHorizontal();

                // Dependencies Installed
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Dependencies:", myStyleResult);
                GUILayout.Label($"{_installCount.num_deps}", myStyleResultValue);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                // Already Installed Packages
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Already Installed:", myStyleResultValueGoodLeft);
                GUILayout.Label($" {_installCount.num_installed}", myStyleResultValueGoodRight);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Newly Installed Packages
                GUILayout.BeginHorizontal();
                GUILayout.Label($"New Packages:", myStyleResultValueGoodLeft);
                GUILayout.Label($" {_installCount.num_added}", myStyleResultValueGoodRight);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Packages Removed
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Removals:", myStyleResultValueGoodLeft);
                GUILayout.Label($" {_installCount.num_removed}", myStyleResultValueGoodRight);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                EditorGUI.indentLevel--;
            }

            if (_installCount.InstallComplete)
            {
                // Button - Close Window
                GUILayout.BeginHorizontal();
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.normal.background = MakeTex(1, 1, new Color(0.243f, 0.612f, 0.440f, 1f)); // #3EBD71 - Green to match Kub green in cover image
                buttonStyle.active.background = MakeTex(1, 1, new Color(0.216f, 0.440f, 0.333f, 1f)); // #379077 - Darker green than Kub green in cover image

                string buttonName = "Close";

                if (GUILayout.Button(buttonName, buttonStyle))
                {
                    this.Close();
                    errorTitle = string.Empty;
                    resultLog = string.Empty;
                }

                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();
            }

            // Breaker Line
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawError()
        {
            GUILayout.BeginVertical();

            // Breaker Line
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            // Error Title
            GUILayout.BeginHorizontal();
            GUIStyle myStyleErrorTitle = new GUIStyle(GUI.skin.label);
            myStyleErrorTitle.fontSize = 18;
            myStyleErrorTitle.fontStyle = FontStyle.Bold;

            GUILayout.Label(errorTitle, myStyleErrorTitle);
            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            // Breaker Line
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            // Error Message
            GUILayout.BeginHorizontal();
            GUIStyle myStyleError = new GUIStyle(GUI.skin.label);

            GUILayout.Label(resultLog, myStyleError);
            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            // Button - Close Window
            GUILayout.BeginHorizontal();
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = MakeTex(1, 1, new Color(0.243f, 0.612f, 0.440f, 1f)); // #3EBD71 - Green to match Kub green in cover image
            buttonStyle.active.background = MakeTex(1, 1, new Color(0.216f, 0.440f, 0.333f, 1f)); // #379077 - Darker green than Kub green in cover image

            string buttonName = "Close";

            if (GUILayout.Button(buttonName, buttonStyle))
            {
                this.Close();
                errorTitle = string.Empty;
                resultLog = string.Empty;
            }

            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            // Breaker Lines
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            minHeight += GUILayoutUtility.GetLastRect().height;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawKolonizerImage()
        {
            string assetPath = Logo_DEBUG_PATH;

            if (!File.Exists(assetPath))
            {
                assetPath = Logo_PKG_PATH;
            }

            Texture2D cover = (Texture2D)AssetDatabase.LoadAssetAtPath(assetPath, typeof(Texture2D));

            float imageHeight = cover.height;
            float imageWidth = cover.width;

            maxHeight = imageHeight + (minHeight - (imageHeight / 2));
            maxWidth = imageWidth - 150;

            minHeight = 50;
            minWidth = (imageWidth / 2);

            Rect rectCoverImage = GUILayoutUtility.GetRect(imageHeight, imageWidth, GUILayout.MinWidth(minWidth), GUILayout.MinHeight(minHeight), GUILayout.MaxWidth(imageWidth), GUILayout.MaxHeight(imageHeight));
            GUI.DrawTexture(rectCoverImage, cover, ScaleMode.ScaleToFit);

            minHeight += imageHeight / 2;
        }
        #endregion

        #region Running Kub Kolonizer

        private void ClearPackageStats()
        {
            _installCount = new();
            _scopedRegistry = new();
            _pkgs = new();
            _autoResetEvent = new();

            errorTitle = string.Empty;
            resultLog = string.Empty;
            currentPackageName = string.Empty;
        }

        private void SetState(KolinizerState kolinizerState, string errTitle, string result)
        {
            if (!string.IsNullOrWhiteSpace(errTitle))
            {
                Log.E(errTitle);
                Log.E($"\t{result}");
                errorTitle = errTitle;
                resultLog = result;
            }
            state = kolinizerState;
            this.Repaint();
        }

        void SaveKolonyConfig(string json)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Kubs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Kubs");

            // Remove .json debug version if exist
            //
            string jsonPath = _KolonyConfigPath.Replace(".asset", ".json");
            AssetDatabase.DeleteAsset(jsonPath);

            AssetDatabase.CreateAsset(new TextAsset(json), _KolonyConfigPath);
        }
               
        private async Task<KolonyConfig> GetKolonyConfigFromWeb()
        {
            Log.D("KolonyConfig Downloading...");

            UnityWebRequest webRequest = UnityWebRequest.Get(Endpoint_GetKolony);
            webRequest.SetRequestHeader("key", KolonyId);
            await webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                SetState(KolinizerState.Error,
                    errTitle: "Download Failed",
                    result: webRequest.error);
            }

            if (webRequest.downloadHandler.text == null)
            {
                SetState(KolinizerState.Error,
                    errTitle: "Kolony Config Cannot Be DownLoaded",
                    result: "Kolony Config was null");
            }

            Log.D("KolonyConfig Download success");

            return JsonConvert.DeserializeObject<KolonyConfig>(webRequest.downloadHandler.text);
        }

        private void DeterminePackagesToAddOrRemove(KolonyConfig kolonyConfig)
        {
            // Gather packages to be installed
            //
            foreach (var kub in kolonyConfig.kubs)
            {
                if (AddPkgToInstall(kub))
                {
                    _installCount.num_kubs++;
                }
            }

            foreach (var provider in kolonyConfig.providers)
            {
                _installCount.num_providers++;

                // Check if provider had a required library package
                if (provider.gitUrl != null || provider.packageName != null)
                {
                    if (AddPkgToInstall(provider))
                    {
                        _installCount.num_deps++;
                    }
                }

                // Base driver
                if (provider.providerDriver != null)
                {
                    if (AddPkgToInstall(provider.providerDriver))
                    {
                        _installCount.num_drivers++;
                    }
                }

                if (provider.drivers.Count > 0)
                {
                    foreach (var driver in provider.drivers)
                    {
                        if (AddPkgToInstall(driver))
                        {
                            _installCount.num_drivers++;
                        }
                    }
                }
            }

            // Check for packages to remove
            foreach(var pkgName in _pkgs.PreviouslyInstalled)
            {
                if (!_pkgs.ToInstall.Contains(pkgName))
                {
                    _pkgs.ToRemove.Add(pkgName);
                }
            }
        }


        /// <summary>
        /// Adds pkg to ToInstall list if not already in list and pkg location is set.
        /// </summary>
        /// <param name="pkg">AssemblyLocation</param>
        /// <returns>true if pkg added to ToInstall list</returns>
        private bool AddPkgToInstall(KolonyConfig.AssemblyLocation pkg)
        {
            if (!string.IsNullOrEmpty(pkg.gitUrl))
            {
                if (_pkgs.ToInstall.Contains(pkg.gitUrl))
                {
                    Log.W($"Dup pkg GitURL entry for {pkg.gitUrl}");
                }
                else
                {
                    _pkgs.ToInstall.Add(pkg.gitUrl);
                    return true;
                }
            }
            else if (!string.IsNullOrEmpty(pkg.packageName))
            {
                if (_pkgs.ToInstall.Contains(pkg.packageName))
                {
                    Log.W($"Dup pkg PackageName entry for {pkg.packageName}");
                }
                else
                {
                    _pkgs.ToInstall.Add(pkg.packageName);
                    return true;
                }
            }
            else
            {
                Log.E($"Pkg missing repo location");
            }
            return false;
        }       

        public async Task UpdateKolony()
        {
            Log.D("UpdateKolony...");

            ClearPackageStats();

            SetState(KolinizerState.Installation, null, null);

            await GetInstalledPackages();

            //_LocalKolonyConfig = LoadLocalKolonyConfig();

            // Check Kolony ID
            _webKolonyConfig = await GetKolonyConfigFromWeb();

            SaveKolonyConfig(JsonConvert.SerializeObject(_webKolonyConfig));

            if (string.IsNullOrEmpty(_webKolonyConfig.npmEntitlementToken))
            {
                SetState(KolinizerState.Error,
                    errTitle: "KolonyConfig missing Entitlement Token",
                    result: _pkgs.UPM_RemoveRequest.Error.message);

                return;
            }

            _scopedRegistry.UpdateUpmConfig(_webKolonyConfig.npmEntitlementToken);
            
            if (!_scopedRegistry.AddScopedRegistryAuth(_webKolonyConfig))
            {
                SetState(KolinizerState.Error,
                    errTitle: "Problem adding scoped registry",
                    result: "see console logs");

                return;
            }

            DeterminePackagesToAddOrRemove(_webKolonyConfig);

            if (_pkgs.ToInstall.Count > 0)
            {
                Log.D($"AddAndRemovePackages installing: {_pkgs.ToInstall.Count} removing {_pkgs.ToRemove.Count}");

                await AddAndRemovePackages();

                SetState(KolinizerState.InstallComplete, null, null);

                Log.D($"AddAndRemovePackages had: {_installCount.num_installed} installs and {_installCount.num_removed} removals");
            }
            else if (_pkgs.ToRemove.Count > 0)
            {
                Log.D($"RemovePackages removing {_pkgs.ToRemove.Count}");

                await RemovePackages(_pkgs.ToRemove);
            }
            else
            {
                Log.D("Kubs already up to date");
            }

            _installCount.InstallComplete = true;
            //SetState(KolinizerState.InstallComplete, null, null);

            Log.D("Install Completed");

            AssetDatabase.Refresh();
            Client.Resolve();

        }

        #endregion

        #region UPM Package Handling
        /// <summary>
        /// Read the manifest and create a list of uninstalled packages
        /// </summary>
        /// <returns>Task</returns>
        private async Task GetInstalledPackages()
        {
            _pkgs.PreviouslyInstalled.Clear();

            _pkgs.UPM_ListRequest = Client.List(offlineMode: true);

            EditorApplication.update += OnProgressUpdate_UPM_GetPackageList;

            await _autoResetEvent.WaitAsync();
        }

        private void OnProgressUpdate_UPM_GetPackageList()
        {
            if (!_pkgs.UPM_ListRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnProgressUpdate_UPM_GetPackageList;

            if (_pkgs.UPM_ListRequest.Status != StatusCode.Success)
            {
                SetState(
                    KolinizerState.Error,
                    errTitle: "Package Cannot Be Installed",
                    result: _pkgs.UPM_ListRequest.Error.message);
            }
            else
            {
                foreach (UnityEditor.PackageManager.PackageInfo package in _pkgs.UPM_ListRequest.Result)
                {
                    // TODO: make this a const reserved word list. -chuck
                    if (!package.packageId.Contains("kub", System.StringComparison.InvariantCultureIgnoreCase))//All kubs/drivers begin with "kub."
                    {
                        if (!package.packageId.Contains("braincloud", System.StringComparison.InvariantCultureIgnoreCase))//Known provider libs
                        {
                            continue;
                        }
                    }

                    if (_pkgs.PreviouslyInstalled.Contains(package.name))
                    {
                        Log.W($"UPM found duplicate package name: {package.name}");
                        continue;
                    }

                    if (package.packageId.EndsWith(".git"))
                    {
                        if (package.packageId.Contains("Kolinizer"))
                        {
                            continue;  // don't include self (the installer). -chuck
                        }
                        try
                        {
                            // Add the full git string, removing prepended package name.
                            string gitStr = package.packageId.Replace(package.name + "@", string.Empty);
                            _pkgs.PreviouslyInstalled.Add(gitStr);
                        }
                        catch(System.Exception err) {
                            Log.E(err.ToString());
                        }

                    }
                    else  // else NPM
                    {
                        _pkgs.PreviouslyInstalled.Add(package.name);
                    }

                    Log.D($"\tUPM found installed kub: {package.name}");

                    //string[] splitName = package.packageId.Split('@');
                    //var packageName = splitName[0];
                    //var packageURL = splitName[1];
                }
                Log.D($"UPM found {_pkgs.PreviouslyInstalled.Count} Kub related Unity packages installed");
            }

            _autoResetEvent.Set();
        }

        private async Task RemovePackages(List<string> packageList)
        {
            if (packageList.Count > 0)
            {
                foreach (var packageName in packageList)
                {
                    _pkgs.UPM_RemoveRequest = Client.Remove(packageName);

                    EditorApplication.update += OnProgressUpdate_UPM_RemovePackages;

                    await _autoResetEvent.WaitAsync();

                    state = KolinizerState.Installation;
                    this.Repaint();
                }
            }

            Log.D($"RemovePackageList Client.List() had {_installCount.num_removed} packages removed");
        }

        private void OnProgressUpdate_UPM_RemovePackages()
        {
            if (!_pkgs.UPM_RemoveRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnProgressUpdate_UPM_RemovePackages;

            if (_pkgs.UPM_RemoveRequest.Status != StatusCode.Success)
            {
                SetState(KolinizerState.Error,
                    errTitle: "Package Cannot Be Removed",
                    result: _pkgs.UPM_RemoveRequest.Error.message);
            }
            else
            {
                _installCount.num_removed++;
            }

            _autoResetEvent.Set();
        }

        private async Task AddAndRemovePackages()
        {
            _pkgs.UPM_AddAndRemoveRequest = Client.AddAndRemove(_pkgs.ToInstall.ToArray(), _pkgs.ToRemove.ToArray());

            EditorApplication.update += OnProgressUpdate_UPM_AddAndRemovePackages;

            await _autoResetEvent.WaitAsync();
        }

        private void OnProgressUpdate_UPM_AddAndRemovePackages()
        {
            if (!_pkgs.UPM_AddAndRemoveRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnProgressUpdate_UPM_AddAndRemovePackages;

            if (_pkgs.UPM_AddAndRemoveRequest.Status != StatusCode.Success)
            {
                _installCount.num_error++;

                SetState(KolinizerState.Error,
                    errTitle: "Trouble, Trouble, Trouble",
                    result: _pkgs.UPM_AddAndRemoveRequest.Error.message);
            }
            else
            {
                _installCount.num_installed = _pkgs.PreviouslyInstalled.Count - _pkgs.ToRemove.Count;
                _installCount.num_added = _pkgs.ToInstall.Count - _installCount.num_installed;
                _installCount.num_removed = _pkgs.ToRemove.Count;
            }

            _autoResetEvent.Set();
        }
        #endregion


#if NotUsed
        private KolonyConfig LoadLocalKolonyConfig()
        {
            var txtAsset = (TextAsset)AssetDatabase.LoadAssetAtPath(_KolonyConfigPath, typeof(TextAsset));
            if (txtAsset != null)
            {
                return JsonConvert.DeserializeObject<KolonyConfig>(txtAsset.text);
            }
            return null;
        }

        private async Task InstallPackages(List<string> packageList)
        {
            if (packageList.Count > 0)
            {
                foreach (var package in packageList)
                {
                    if (!_pkgs.PreviouslyInstalled.Contains(package))
                    {
                        // Package is not installed
                        _pkgs.UPM_AddRequest = Client.Add(package);

                        EditorApplication.update += OnProgressUpdate_UPM_AddPackages;

                        await _autoResetEvent.WaitAsync();
                    }
                    else
                    {
                        // Package is already installed
                        _installCount.num_installed++;
                    }

                    currentPackageName = package;
                    state = KolinizerState.Installation;
                    this.Repaint();
                }

                _installCount.package_installed++;
            }

            Log.D($"InstallPackages");
            Log.D($"\tadded: {_installCount.num_added}");
            Log.D($"\terrors {_installCount.num_error}");
            Log.D($"\talready installed {_installCount.num_installed}");
            Log.D($"\tpackages installed {_installCount.package_installed}");
        }

        private void OnProgressUpdate_UPM_AddPackages()
        {
            if (!_pkgs.UPM_AddRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnProgressUpdate_UPM_AddPackages;

            if (_pkgs.UPM_AddRequest.Status != StatusCode.Success)
            {
                _installCount.num_error++;

                SetState(KolinizerState.Error,
                    errTitle: "Package Cannot Be Added",
                    result: _pkgs.UPM_AddRequest.Error.message);
            }
            else
            {
                _installCount.num_added++;
            }

            _autoResetEvent.Set();
        }
#endif


    }
}