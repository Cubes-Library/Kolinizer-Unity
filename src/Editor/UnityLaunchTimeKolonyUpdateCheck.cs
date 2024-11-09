using UnityEditor;
using UnityEngine;

namespace Kub.Kolinizer
{
    public class UnityLaunchTimeKolonyUpdateCheck : AssetPostprocessor
    {
        //[ExecuteInEditMode]
        //[UnityEditor.Callbacks.DidReloadScripts]
        //private static void CreateAssetWhenReady()
        //{
        //    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        //    {
        //        EditorApplication.delayCall += displayMenu;
        //        return;
        //    }

        //    EditorApplication.delayCall += displayMenu;
        //}

        [ExecuteInEditMode]
        public static void displayMenu()
        {
            if (!SessionState.GetBool("KubCheckerRun", false))
            {
                SessionState.SetBool("KubCheckerRun", true);                                

                if (EditorPrefs.GetInt("RunKubCheckAtLaunch") == 1)
                {
                    Kolinizer.ShowWindow();
                }
            }
        }
    }

    [InitializeOnLoad]
    public class UnityLaunchCheck
    {
        private static void ShowWindow()
        {
            if (EditorPrefs.GetInt("RunKubCheckAtLaunch") == 1)
            {
                UnityLaunchTimeKolonyUpdateCheck.displayMenu();
            }
        }
    }
}