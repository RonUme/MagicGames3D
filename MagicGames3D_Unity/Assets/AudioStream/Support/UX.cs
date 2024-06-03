// (c) 2022-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using UnityEngine;

namespace AudioStreamSupport
{
    /// <summary>
    /// common (mainly demo) Gui/UX helpers
    /// </summary>
    public static class UX
    {
        // ========================================================================================================================================
        #region OnGUI styles
        static GUIStyle _guiStyleLabelSmall = GUIStyle.none;
        public static int fontSizeBase = 0;
        public static GUIStyle guiStyleLabelSmall
        {
            get
            {
                if (UX._guiStyleLabelSmall == GUIStyle.none)
                {
                    UX._guiStyleLabelSmall = new GUIStyle(GUI.skin.GetStyle("Label"));
                    UX._guiStyleLabelSmall.fontSize = 8 + UX.fontSizeBase;
                    UX._guiStyleLabelSmall.margin = new RectOffset(0, 0, 0, 0);
                }
                return UX._guiStyleLabelSmall;
            }
            private set { UX._guiStyleLabelSmall = value; }
        }
        static GUIStyle _guiStyleLabelMiddle = GUIStyle.none;
        public static GUIStyle guiStyleLabelMiddle
        {
            get
            {
                if (UX._guiStyleLabelMiddle == GUIStyle.none)
                {
                    UX._guiStyleLabelMiddle = new GUIStyle(GUI.skin.GetStyle("Label"));
                    UX._guiStyleLabelMiddle.fontSize = 10 + UX.fontSizeBase;
                    UX._guiStyleLabelMiddle.margin = new RectOffset(0, 0, 0, 0);
                }
                return UX._guiStyleLabelMiddle;
            }
            private set { UX._guiStyleLabelMiddle = value; }
        }
        static GUIStyle _guiStyleLabelNormal = GUIStyle.none;
        public static GUIStyle guiStyleLabelNormal
        {
            get
            {
                if (UX._guiStyleLabelNormal == GUIStyle.none)
                {
                    UX._guiStyleLabelNormal = new GUIStyle(GUI.skin.GetStyle("Label"));
                    UX._guiStyleLabelNormal.fontSize = 12 + UX.fontSizeBase;
                    UX._guiStyleLabelNormal.margin = new RectOffset(0, 0, 0, 0);
                }
                return UX._guiStyleLabelNormal;
            }
            private set { UX._guiStyleLabelNormal = value; }
        }
        static GUIStyle _guiStyleButtonNormal = GUIStyle.none;
        public static GUIStyle guiStyleButtonNormal
        {
            get
            {
                if (UX._guiStyleButtonNormal == GUIStyle.none)
                {
                    UX._guiStyleButtonNormal = new GUIStyle(GUI.skin.GetStyle("Button"));
                    UX._guiStyleButtonNormal.fontSize = 14 + UX.fontSizeBase;
                    UX._guiStyleButtonNormal.margin = new RectOffset(5, 5, 5, 5);
                }
                return UX._guiStyleButtonNormal;
            }
            private set { UX._guiStyleButtonNormal = value; }
        }
        public static void ResetStyles()
        {
            UX.guiStyleButtonNormal =
                UX.guiStyleLabelMiddle =
                UX.guiStyleLabelNormal =
                UX.guiStyleLabelSmall =
                GUIStyle.none;
        }
        #endregion
        // ========================================================================================================================================
        #region OnGUI
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullVersion"></param>
        public static void OnGUI_Header(string fullVersion)
        {
            GUILayout.Label("", UX.guiStyleLabelSmall); // statusbar on mobile overlay
            GUILayout.Label("", UX.guiStyleLabelSmall);
            GUILayout.Label(fullVersion, UX.guiStyleLabelMiddle);
            GUILayout.Label(RuntimeBuildInformation.Instance.buildString, UX.guiStyleLabelMiddle);
            GUILayout.Label(RuntimeBuildInformation.Instance.defaultOutputProperties, UX.guiStyleLabelMiddle);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="audioTexture_OutputData"></param>
        /// <param name="audioTexture_SpectrumData"></param>
        public static void OnGUI_AudioTextures(AudioTexture_OutputData audioTexture_OutputData, AudioTexture_SpectrumData audioTexture_SpectrumData)
        {
            if (audioTexture_OutputData && audioTexture_OutputData.outputTexture)
                GUI.DrawTexture(new Rect(0
                                    , (Screen.height / 2)
                                    , Screen.width
                                    , audioTexture_OutputData.outputTexture.height
                                    )
                , audioTexture_OutputData.outputTexture
                , ScaleMode.StretchToFill
                );

            if (audioTexture_SpectrumData && audioTexture_SpectrumData.outputTexture)
                GUI.DrawTexture(new Rect(0
                                    , (Screen.height / 2) + (audioTexture_OutputData && audioTexture_OutputData.outputTexture ? audioTexture_OutputData.outputTexture.height : 0)
                                    , Screen.width
                                    , audioTexture_SpectrumData.outputTexture.height
                                    )
                , audioTexture_SpectrumData.outputTexture
                , ScaleMode.StretchToFill
                );
        }
        /// <summary>
        /// 
        /// </summary>
        public static void OnGUI_DownloadCache()
        {
            var di = FileSystem.DirectoryInfo(RuntimeSettings.downloadCachePath);
            GUILayout.Label(string.Format("[Clear/view download cache directory at {0}; current size: {1} b, files: {2}]", RuntimeSettings.downloadCachePath, di.Item1, di.Item2), UX.guiStyleLabelNormal);

            using (new GUILayout.HorizontalScope())
            {
                if (!Application.isMobilePlatform && !Application.isConsolePlatform)
                    if (GUILayout.Button("Open download cache folder", UX.guiStyleButtonNormal))
                    {
                        Application.OpenURL(RuntimeSettings.downloadCachePath);
                    }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public static void OnGUI_TemporaryCache()
        {
            var di = FileSystem.DirectoryInfo(RuntimeSettings.temporaryDirectoryPath);
            GUILayout.Label(string.Format("[Clear/view temporary/decoded samples directory at {0}; current size: {1} b, files: {2}]", RuntimeSettings.temporaryDirectoryPath, di.Item1, di.Item2), UX.guiStyleLabelNormal);

            using (new GUILayout.HorizontalScope())
            {
                if (!Application.isMobilePlatform && !Application.isConsolePlatform)
                    if (GUILayout.Button("Open temp dir. folder", UX.guiStyleButtonNormal))
                    {
                        Application.OpenURL(RuntimeSettings.temporaryDirectoryPath);
                    }

                if (GUILayout.Button("Clear temp dir. folder", UX.guiStyleButtonNormal))
                {
                    foreach (var fp in System.IO.Directory.GetFiles(RuntimeSettings.temporaryDirectoryPath))
                    {
                        System.IO.File.Delete(fp);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public static void OnGUI_Logfile()
        {
            // https://docs.unity3d.com/Manual/LogFiles.html

            var log_path = "";
            var log_path_env = "";
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    GUILayout.Label(@"For log use logcat");
                    break;
                case RuntimePlatform.IPhonePlayer:
                    GUILayout.Label(@"For log see Xcode's Ogranizer Console");
                    break;
                case RuntimePlatform.LinuxPlayer:
                    log_path_env = string.Format("~/.config/unity3d/{0}/{1}/Player.log", Application.companyName, Application.productName);
                    log_path = Environment.ExpandEnvironmentVariables(log_path_env);
                    break;
                case RuntimePlatform.OSXPlayer:
                    GUILayout.Label(@"(also in Console.app)");
                    log_path_env = string.Format("~/Library/Logs/{0}/{1}/Player.log", Application.companyName, Application.productName);
                    log_path = Environment.ExpandEnvironmentVariables(log_path_env);
                    break;
                case RuntimePlatform.WSAPlayerARM:
                case RuntimePlatform.WSAPlayerX64:
                case RuntimePlatform.WSAPlayerX86:
                    log_path_env = string.Format("%USERPROFILE%\\AppData\\Local\\Packages\\{0}\\TempState\\UnityPlayer.log", Application.productName);
                    log_path = Environment.ExpandEnvironmentVariables(log_path_env);
                    break;
                case RuntimePlatform.WebGLPlayer:
                    GUILayout.Label(@"For log see browser's JS console");
                    break;
                case RuntimePlatform.WindowsPlayer:
                    log_path_env = string.Format("%USERPROFILE%\\AppData\\LocalLow\\{0}\\{1}\\Player.log", Application.companyName, Application.productName);
                    log_path = Environment.ExpandEnvironmentVariables(log_path_env);
                    break;
            }

            // open player log button
            if (!string.IsNullOrWhiteSpace(log_path))
                if (GUILayout.Button("Open player log", UX.guiStyleButtonNormal))
                    Application.OpenURL(log_path);
        }
        /// <summary>
        /// 
        /// </summary>
        public static void OnGUI_Fontsize()
        {
            GUILayout.Label("Font size / zoom: ", UX.guiStyleLabelNormal);

            using (new GUILayout.HorizontalScope())
            {
                var sz = UX.fontSizeBase;

                using (new GUILayout.HorizontalScope())
                {
                    sz = (int)GUILayout.HorizontalSlider(sz, -1, 10);
                    GUILayout.Label(string.Format("{0}", sz));
                }

                if (sz != UX.fontSizeBase)
                {
                    UX.ResetStyles();
                    UX.fontSizeBase = sz;
                }
            }
        }
        #endregion
    }
}