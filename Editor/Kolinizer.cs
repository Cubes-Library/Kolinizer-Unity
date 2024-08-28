using System;
using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Kub.Util;
using System.Threading.Tasks;
using System.IO;

namespace Kub.Kolinizer
{
    public class Kolinizer : EditorWindow
    {
        #region Constant Variables
        private const string welcomeName = "Kub Kolonizer";
        private const string installationName = "Kolonizer Installation";
        private const string errorName = "Kolonizer Error";
        //private const string _placeholderKolonyId = "Enter Your Kolony ID...";
        private const string runtimePath = "?path=/Runtime/";
        private const string runtimeBasePath = "?path=/Runtime/Base";

        private const string Logo_PKG_PATH = "Packages/kub.kolinizer.unity/Editor/kub_logo.png";
        private const string Logo_DEBUG_PATH = "Assets/Kub/SrcDelivery/Editor/kub_logo.png";

        private const string GuiSkin_PKG_PATH = "Packages/kub.kolinizer.unity/Editor/Unity/GUI/KolonizerSkin.guiskin";
        private const string GuiSkin_DEBUG_PATH = "Assets/Kub/SrcDelivery/Editor/Unity/GUI/KolonizerSkin.guiskin";

        #endregion

        #region Static Variables
        private static string windowName = welcomeName;
        public static bool IsRunning { get; private set; } = false;
        #endregion

        #region Private GUI Variables
        private enum State
        {
            Welcome,
            Installation,
            Error
        }

        private State state = State.Welcome;
        private GUISkin skin = null;
        private float previousContentHeight;
        private bool showDetailedResults = false;
        private float minWidth;
        private float minHeight;
        private float maxWidth;
        private float maxHeight;
        #endregion

        #region Private Kolonizer Variables
        private SavedKolonyID _savedKolonyId = null;
        private KolonyConfig _LocalKolonyConfig;
        private KolonyConfig _WebKolonyConfig;
        private AddRequest addRequest;
        private RemoveRequest removeRequest;
        private ListRequest listRequest;
        private List<(string name, string location)> manifestPackages = new();
        private List<string> storedPackages = new();
        private List<string> storedProviders = new();
        private List<string> removePackagesList = new();
        private List<string> installedPackages = new();
        private List<(string name, string location)> providers = new();
        private Dictionary<string,string> upm_pkg_names = new();
        private readonly AsyncAutoResetEvent _autoResetEvent = new();
        private int num_deps = 0;
        private int num_kubs = 0;
        private int num_drivers = 0;
        private int num_added = 0;
        private int num_installed = 0;
        private int num_removed = 0;
        private int num_error = 0;
        private int package_installed = 0;
        private int num_providers = 0;
        private int num_Total => num_kubs + num_drivers + num_providers;
        private bool InstallComplete = false;
        private string errorTitle = string.Empty;
        private string resultLog = string.Empty;
        private string currentPackageName = string.Empty;
        #endregion

        #region Public Variables
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
                    return string.Empty;//_placeholderKolonyId;
                }
                return _savedKolonyId.KolonyID; 
            } 
            private set 
            {
                //if (value == _placeholderKolonyId)
                //{
                //    return;
                //}
                //if (value.Contains(_placeholderKolonyId))
                //{
                //    value = value.Replace(_placeholderKolonyId, string.Empty);
                //}
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
        #endregion

        #region Menu Items
        [MenuItem("Kub/Kub Kolonizer")]
        public static void ShowWindow()
        {
            if (IsRunning) return;

            GetWindow<Kolinizer>(windowName);
        }
        #endregion

        [ExecuteInEditMode]
        private void OnEnable()
        {
            string assetPath = GuiSkin_PKG_PATH;

            if (!File.Exists(assetPath))
            {
                assetPath = GuiSkin_DEBUG_PATH;
            }

            if (File.Exists(assetPath))
            {
                skin = (GUISkin)AssetDatabase.LoadAssetAtPath(assetPath, typeof(GUISkin));
            }
            else
            {
                Debug.LogWarning($"GFX not found at location: {assetPath}");
            }
        }

        #region GUI Methods

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
                case State.Welcome:
                    windowName = welcomeName;
                    DrawWelcome();
                    break;
                case State.Installation:
                    windowName = installationName;
                    DrawInstallation();
                    break;
                case State.Error:
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

            for (int i = 0; i < pix.Length; i++)pix[i] = col;

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

                var txtAsset = (TextAsset)AssetDatabase.LoadAssetAtPath("Assets/Resources/Kubs/KolonyConfig.asset", typeof(TextAsset));
                string buttonName = string.Empty;

                if (txtAsset != null)
                    buttonName = "Refresh";
                else
                    buttonName = "Install";

                if (GUILayout.Button(buttonName, buttonStyle))
                {
                    if (!string.IsNullOrEmpty(KolonyId) )//&& KolonyId != _placeholderKolonyId)
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
                state = State.Error;
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
            if (InstallComplete)
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
            else if (num_added > 0)
            {
                GUILayout.BeginHorizontal();
                GUIStyle myStyleTitleWorking = new GUIStyle(GUI.skin.label);
                myStyleTitleWorking.fontSize = 16;
                myStyleTitleWorking.normal.textColor = Color.yellow;

                float progress = 1f;
                float installedCnt = num_installed + num_added;
                if (num_Total > 0)
                {
                    progress = installedCnt / num_Total;
                }

                string progressText = "Installing: " + installedCnt + "/" + num_Total + "\n" + "(" + currentPackageName + ")";

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

            if (num_error > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Errors: {num_error}. Please check the Unity Debug log.", myStyleResult);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();
            }

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
                GUILayout.Label($"{num_kubs}", myStyleResultValue);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Drivers Installed
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Drivers:", myStyleResult);
                GUILayout.Label($"{num_drivers}", myStyleResultValue);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Providers Installed
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Providers:", myStyleResult);
                GUILayout.Label($"{num_providers}", myStyleResultValue);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Dependencies Installed
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Dependencies:", myStyleResult);
                GUILayout.Label($"{num_deps}", myStyleResultValue);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                // Already Installed Packages
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Already Installed:", myStyleResultValueGoodLeft);
                GUILayout.Label($" {num_installed}", myStyleResultValueGoodRight);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Newly Installed Packages
                GUILayout.BeginHorizontal();
                GUILayout.Label($"New Packages:", myStyleResultValueGoodLeft);
                GUILayout.Label($" {num_added}", myStyleResultValueGoodRight);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                // Packages Removed
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Removals:", myStyleResultValueGoodLeft);
                GUILayout.Label($" {num_removed}", myStyleResultValueGoodRight);
                minHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                EditorGUI.indentLevel--;
            }

            if (InstallComplete)
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
            string assetPath = Logo_PKG_PATH;

            Texture2D cover;
            if (!File.Exists(assetPath))
            {
                assetPath = Logo_DEBUG_PATH;
                if (!File.Exists(assetPath))
                {
                    Debug.LogWarning($"Can't find logo: {assetPath}");
                    return;
                }
            }
            
            cover = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

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

        #region Running Kub Bootstrapper
        internal sealed class AsyncAutoResetEvent
        {
            private readonly Task s_completed = Task.FromResult(true);
            private readonly Queue<TaskCompletionSource<bool>> _waits = new Queue<TaskCompletionSource<bool>>();
            private bool _signaled;

            public Task WaitAsync()
            {
                lock (_waits)
                {
                    if (_signaled)
                    {
                        _signaled = false;
                        return s_completed;
                    }
                    else
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        _waits.Enqueue(tcs);
                        return tcs.Task;
                    }
                }
            }

            public void Set()
            {
                TaskCompletionSource<bool> toRelease = null;

                lock (_waits)
                {
                    if (_waits.Count > 0)
                    {
                        toRelease = _waits.Dequeue();
                    }
                    else if (!_signaled)
                    {
                        _signaled = true;
                    }
                }

                toRelease?.SetResult(true);
            }
        }

        #region Methods
        private void Clear()
        {
            num_deps = 0;
            num_kubs = 0;
            num_drivers = 0;
            num_added = 0;
            num_installed = 0;
            num_removed = 0;
            num_error = 0;
            package_installed = 0;
            num_providers = 0;
            errorTitle = string.Empty;
            resultLog = string.Empty;
            currentPackageName = string.Empty;
        }

        void SaveKolonyConfig(string json)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Kubs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Kubs");

            // Debug version of the KolonyConfig. -chuck
            AssetDatabase.DeleteAsset("Assets/Resources/Kubs/KolonyConfig.json");                       

            AssetDatabase.CreateAsset(new TextAsset(json), "Assets/Resources/Kubs/KolonyConfig.asset");
        }

        private async Task GetInstalledPackages()
        {
            installedPackages.Clear();
            upm_pkg_names.Clear();

            listRequest = Client.List(offlineMode: true);
            
            EditorApplication.update += OnProgressUpdate_UPM_GetPackageList;

            await _autoResetEvent.WaitAsync();
        }

        private void OnProgressUpdate_UPM_GetPackageList()
        {
            if (!listRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnProgressUpdate_UPM_GetPackageList;

            if (listRequest.Status != StatusCode.Success)
            {
                errorTitle = "Package Cannot Be Installed";
                resultLog = listRequest.Error.message;
                state = State.Error;
                this.Repaint();
            }
            else
            {
                foreach (UnityEditor.PackageManager.PackageInfo package in listRequest.Result)
                {
                    string fullName = package.packageId;
                    string[] splitName = fullName.Split('@');

                    installedPackages.Add(splitName[1]);

                    if (!upm_pkg_names.ContainsKey(splitName[1]))
                    {
                        upm_pkg_names.Add(splitName[1], splitName[0]);
                    }
                    else
                    {
                        //Debug.Log($"Dup pkg {package}");
                    }
                }
                Debug.Log($"UPM Client.List() found {installedPackages.Count} Unity packages installed");
            }

            _autoResetEvent.Set();
        }

        private KolonyConfig LoadLocalKolonyConfig()
        {
            var txtAsset = (TextAsset)AssetDatabase.LoadAssetAtPath("Assets/Resources/Kubs/KolonyConfig.asset", typeof(TextAsset));
            if (txtAsset != null)
            {
                return JsonConvert.DeserializeObject<KolonyConfig>(txtAsset.text);
            }
            return null;
        }

        private async Task<KolonyConfig> GetKolonyConfigFromWeb()
        {
            // Create a UnityWebRequest object
            UnityWebRequest webRequest = UnityWebRequest.Get("https://getkolony-cplqdkn7ya-uc.a.run.app");

            // Set headers
            webRequest.SetRequestHeader("key", KolonyId);

            // Send the request asynchronously
            await webRequest.SendWebRequest();

            // Check if there was an error
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(webRequest.error);
                errorTitle = "Download Failed";
                resultLog = "Kolony Config could not be downloaded. Please check your internet connection and try again.";
                state = State.Error;
                this.Repaint();
            }

            // Check if downloadHandler is null
            if (webRequest.downloadHandler.text == null)
            {
                errorTitle = "Kolony Config Cannot Be Loaded";
                resultLog = "Kolony Config is not valid or does not exist.";
                state = State.Error;
                this.Repaint();
            }

            // Return the response data
            return JsonConvert.DeserializeObject<KolonyConfig>(webRequest.downloadHandler.text);
        }

        private void SetStoredPackages(KolonyConfig kolonyConfig)
        {
            removePackagesList.Clear();
            storedPackages.Clear();
            storedProviders.Clear();

            if (kolonyConfig == null)
            {
                return;
            }

            foreach (var dependency in kolonyConfig.dependencies)
            {
                storedPackages.Add(dependency.location);
                removePackagesList.Add(dependency.location);
            }

            // Then install the providers
            foreach (var provider in kolonyConfig.providers)
            {
                if (!string.IsNullOrEmpty(provider.location))
                {
	                storedPackages.Add(provider.location);
  	                removePackagesList.Add(provider.location);
                }

                if (provider.providerDriver != null)
                {
                    storedProviders.Add(provider.providerDriver.location + runtimeBasePath);
                    removePackagesList.Add(provider.providerDriver.location + runtimeBasePath);
                }

                if (provider.drivers.Count > 0)
                {
                    foreach (var driver in provider.drivers)
                    {
                        storedProviders.Add(provider.providerDriver.location + runtimePath + driver.name);
                        removePackagesList.Add(provider.providerDriver.location + runtimePath + driver.name);
                    }
                }
            }

            // And finally the kubs
            foreach (var kub in kolonyConfig.kubs)
            {
                storedPackages.Add(kub.location);
                removePackagesList.Add(kub.location);
            }
        }

        private void SetManifestPackages(KolonyConfig manifestKolonyConfig)
        {
            num_deps = num_drivers = num_kubs = 0;

            // First install the pre-requisites
            manifestPackages.Clear();
            providers.Clear();

            // Add the dependencies
            if (manifestKolonyConfig.dependencies.Count > 0)
            {
                foreach (var dependency in manifestKolonyConfig.dependencies)
                {
                    var addedDependency = (dependency.name + " Dependency", dependency.location);
                    manifestPackages.Add(addedDependency);

                    if (removePackagesList.Contains(dependency.location))
                    {
                        removePackagesList.Remove(dependency.location);
                    }

                    num_deps++;
                }
            }

            // Add the providers
            if (manifestKolonyConfig.providers.Count > 0)
            {
                foreach (var provider in manifestKolonyConfig.providers)
                {
                    if (!string.IsNullOrEmpty(provider.location))
                    {
                        var addedProvider = (provider.name + "Provider", provider.location);
                        manifestPackages.Add(addedProvider);
                        removePackagesList.Remove(provider.location);
                    }

                    num_drivers++; // Count +1 for Provider Base
                    num_providers++;

                    if (installedPackages.Contains(provider.providerDriver.location + runtimeBasePath))
                    {
                        storedProviders.Remove(provider.providerDriver.location + runtimeBasePath);
                        removePackagesList.Remove(provider.providerDriver.location + runtimeBasePath);
                        num_installed++;
                    }
                    else
                    {
                        var addedProvider = (provider.providerDriver.name + " Provider", provider.providerDriver.location + runtimeBasePath);
                        providers.Add(addedProvider);
                        removePackagesList.Remove(provider.providerDriver.location + runtimeBasePath);
                    }

                    if (provider.drivers.Count > 0)
                    {
                        foreach (var driver in provider.drivers)
                        {
                            num_drivers++;

                            if (installedPackages.Contains(provider.providerDriver.location + runtimePath + driver.name))
                            {
                                storedProviders.Remove(provider.providerDriver.location + runtimePath + driver.name);
                                removePackagesList.Remove(provider.providerDriver.location + runtimePath + driver.name);
                                num_installed++;
                            }
                            else
                            {
                                var addedProvider = (provider.providerDriver.name + " Provider: " + driver.name, provider.providerDriver.location + runtimePath + driver.name);
                                providers.Add(addedProvider);
                                removePackagesList.Remove(provider.providerDriver.location + runtimePath + driver.name);
                            }
                        }
                    }
                }
            }

            // Add the kubs
            if (manifestKolonyConfig.kubs.Count > 0)
            {
                foreach (var kub in manifestKolonyConfig.kubs)
                {
                    var addedKub = (kub.name + " Kub", kub.location);
                    manifestPackages.Add(addedKub);

                    num_kubs++;

                    if (removePackagesList.Contains(kub.location))
                    {
                        removePackagesList.Remove(kub.location);
                    }
                }
            }
        }

        public async Task UpdateKolony()
        {
            Debug.Log("UpdateKolony...");

            Clear();
            
            state = State.Installation;
            this.Repaint();
            await GetInstalledPackages();

            // Read the manifest and create a list of uninstalled packages
            num_removed = num_installed = num_error = num_added = 0;

            _LocalKolonyConfig = LoadLocalKolonyConfig();

            // Check Kolony ID
            _WebKolonyConfig = await GetKolonyConfigFromWeb();

            SaveKolonyConfig(JsonConvert.SerializeObject(_WebKolonyConfig));

            // Kolony ID & Kolony Config Asset are valid - Reset installProgressState.errorTitle and installProgressState.resultLog
            errorTitle = string.Empty;
            resultLog = string.Empty;

            // Download kolony packages (stored and manifest)
            SetStoredPackages(_LocalKolonyConfig);
            SetManifestPackages(_WebKolonyConfig);

            Debug.Log("removePackagesList.Count: " + removePackagesList.Count);

            // Remove drivers from removePackagesList
            await RemovePackages(removePackagesList);

            // Install drivers from manifestPackages
            await InstallPackages(manifestPackages);

            // Install drivers from providers
            await InstallPackages(providers);

            InstallComplete = true;
            Debug.Log("All packages have been downloaded, removed, and installed");
        }

        private async Task RemovePackages(List<string> packageList)
        {
            if (packageList.Count > 0)
            {
                foreach (var package in packageList)
                {
                    removeRequest = Client.Remove(upm_pkg_names[package]);

                    EditorApplication.update += OnProgressUpdate_UPM_RemovePackages;

                    await _autoResetEvent.WaitAsync();

                    state = State.Installation;
                    this.Repaint();
                }
            }

            Debug.Log($"RemovePackageList Client.List() had {num_removed} packages removed");
        }

        private void OnProgressUpdate_UPM_RemovePackages()
        {
            if (!removeRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnProgressUpdate_UPM_RemovePackages;

            if (removeRequest.Status != StatusCode.Success)
            {
                errorTitle = "Package Cannot Be Removed";
                resultLog = removeRequest.Error.message;
                state = State.Error;
                this.Repaint();
            }
            else
            {
                num_removed++;
            }

            _autoResetEvent.Set();
        }

        private async Task InstallPackages(List<(string name, string location)> packageList)
        {
            if (packageList.Count > 0)
            {
                foreach (var package in packageList)
                {
                    if (!installedPackages.Contains(package.location))
                    {
                        // Package is not installed
                        addRequest = Client.Add(package.location);

                        EditorApplication.update += OnProgressUpdate_UPM_AddPackages;

                        await _autoResetEvent.WaitAsync();
                    }
                    else
                    {
                        // Package is already installed
                        num_installed++;
                    }

                    currentPackageName = package.name;
                    state = State.Installation;
                    this.Repaint();
                }

                package_installed++;
            }

            Debug.Log($"InstallPackageList Client.List() had {num_added} added / {num_error} errors / {num_installed} already installed / {package_installed} packages installed");
        }

        private void OnProgressUpdate_UPM_AddPackages()
        {
            if (!addRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnProgressUpdate_UPM_AddPackages;

            if (addRequest.Status != StatusCode.Success)
            {
                num_error++;
                errorTitle = "Package Cannot Be Added";
                resultLog = addRequest.Error.message;
                state = State.Error;
                this.Repaint();
            }
            else
            {
                num_added++;
            }

            _autoResetEvent.Set();
        }
        #endregion
        #endregion
    }
}