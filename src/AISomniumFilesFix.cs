using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace AISomniumFilesFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class AISFFix : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        public static ConfigEntry<bool> CustomResolution;
        public static ConfigEntry<int> DesiredResolutionX;
        public static ConfigEntry<int> DesiredResolutionY;
        public static ConfigEntry<bool> Fullscreen;
        public static ConfigEntry<bool> UIFix;
        public static ConfigEntry<bool> SomniumFix;

        private void Awake()
        {
            Log = Logger;

            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            CustomResolution = Config.Bind("Set Custom Resolution",
                                "CustomResolution",
                                true,
                                "Enable the usage of a custom resolution.");

            DesiredResolutionX = Config.Bind("Set Custom Resolution",
                                "ResolutionWidth",
                                (int)Display.main.systemWidth, // Set default to display width so we don't leave an unsupported resolution as default
                                "Set desired resolution width.");

            DesiredResolutionY = Config.Bind("Set Custom Resolution",
                                "ResolutionHeight",
                                (int)Display.main.systemHeight, // Set default to display height so we don't leave an unsupported resolution as default
                                "Set desired resolution height.");

            Fullscreen = Config.Bind("Set Custom Resolution",
                                "Fullscreen",
                                 true,
                                "Set to true for fullscreen or false for windowed.");

            UIFix = Config.Bind("Fixes",
                                "UIFix",
                                true,
                                "Fixes UI issues at ultrawide/wider.");

            SomniumFix = Config.Bind("Fixes",
                                "SomniumFix",
                                true,
                                "Caps FPS at 90 in Somnium scenes to mitigate visual glitches at high framerates.");

            SceneManager.sceneLoaded += OnSceneLoaded;

            float NewAspectRatio = (float)AISFFix.DesiredResolutionX.Value / AISFFix.DesiredResolutionY.Value;

            if (CustomResolution.Value) { Harmony.CreateAndPatchAll(typeof(CustomResolution)); }
            if (NewAspectRatio > 1.8 && UIFix.Value && CustomResolution.Value) { Harmony.CreateAndPatchAll(typeof(UIFix)); }

            Screen.SetResolution(DesiredResolutionX.Value, DesiredResolutionY.Value, Fullscreen.Value);
        }

        private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1) // Run the fix every time a scene is loaded
        {
            float DefaultAspectRatio = (float)1920 / 1080;
            float NewAspectRatio = (float)AISFFix.DesiredResolutionX.Value / AISFFix.DesiredResolutionY.Value;
            float AspectMultiplier = NewAspectRatio / DefaultAspectRatio;

            Scene scene = SceneManager.GetActiveScene();
            AISFFix.Log.LogInfo($"Scene loaded = {scene.name}");

            // Somnium
            if (scene.name == "Somnium")
            {
                if (SomniumFix.Value)
                {
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 90;
                    AISFFix.Log.LogInfo($"Scene = {scene.name}. Target FPS = {Application.targetFrameRate}, Vsync = {QualitySettings.vSyncCount}");
                }

                if (UIFix.Value) // Scale gameplay relevant UI to full screen
                {
                    GameObject RightWindow2 = GameObject.Find("$Root/Canvas/ScreenScaler/RightWindow");
                    RightWindow2.transform.localScale = new Vector3(1 * AspectMultiplier, 1f, 1f);
                    GameObject Letterbox2 = GameObject.Find("$Root/Canvas (1)/ScreenScaler/UIOff5");
                    Letterbox2.transform.localScale = new Vector3(1 * AspectMultiplier, 1f, 1f);
                    GameObject FilterBlur2 = GameObject.Find("$Root/Canvas (1)/ScreenScaler/FilterBlur");
                    FilterBlur2.transform.localScale = new Vector3(1 * AspectMultiplier, 1f, 1f);
                }
            }   
            else
            {
                if (SomniumFix.Value)
                {
                    QualitySettings.vSyncCount = 1;
                    Application.targetFrameRate = 0;
                    AISFFix.Log.LogInfo($"Scene = {scene.name}. Target FPS = {Application.targetFrameRate}, Vsync = {QualitySettings.vSyncCount}");
                } 
            }

            // Investigation
            if (scene.name == "Investigation")
            {
                if (UIFix.Value) // Scale gameplay relevant UI to full screen
                {
                    GameObject RightWindow1 = GameObject.Find("$Root/UICanvas/ScreenScaler/Windows/RightWindow");
                    RightWindow1.transform.localScale = new Vector3(1 * AspectMultiplier, 1f, 1f);
                    GameObject Letterbox1 = GameObject.Find("$Root/FrontCanvas/ScreenScaler/UIOff3");
                    Letterbox1.transform.localScale = new Vector3(1 * AspectMultiplier, 1f, 1f);
                    GameObject FilterBlur1 = GameObject.Find("$Root/UICanvas/ScreenScaler/FilterBlur");
                    FilterBlur1.transform.localScale = new Vector3(1 * AspectMultiplier, 1f, 1f);
                }
            }
        }
    }

    [HarmonyPatch]
    public class CustomResolution
    {
        // Patch camera rect to be full at all times
        [HarmonyPatch(typeof(Game.CameraScaler), nameof(Game.CameraScaler.Update))]
        [HarmonyPrefix]
        public static bool CameraScaler(Game.CameraScaler __instance)
        {
            Game.Settings instance = Game.Settings.instance;
            if (__instance.camera_.targetTexture == null && instance != null)
            {
                __instance.camera_.rect = new Rect(0f, 0f, 1f, 1f);
            }
            return false;
        }
    }

    public class UIFix
    {
        // Set screen match mode when object has CanvasScaler enabled
        [HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
        [HarmonyPostfix]
        public static void SetScreenMatchMode(CanvasScaler __instance)
        {
            __instance.m_ScreenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        // Disable RectMask2D from being enabled.
        [HarmonyPatch(typeof(RectMask2D), "OnEnable")]
        [HarmonyPrefix]
        public static bool SetRectMask2D(RectMask2D __instance)
        {
            __instance.enabled = false;
            return false;
        }

        // Fix video aspect ratio
        [HarmonyPatch(typeof(Game.VideoHelper), "Play")]
        [HarmonyPostfix]
        public static void FixVideoAspectRatio(Game.VideoHelper __instance)
        {
            __instance.videoPlayer.aspectRatio = UnityEngine.Video.VideoAspectRatio.FitVertically;
            AISFFix.Log.LogInfo("Video aspect ratio set to FitVertically.");
        }
    }
}
