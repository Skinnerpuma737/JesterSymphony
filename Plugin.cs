using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JesterSymphony.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace JesterSymphony
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal AudioClip screaming;
        internal AudioClip windup;
        internal AudioClip popUp;


        private string[] screamingClips;
        private string[] windupClips;
        private string[] popUpClips;
        internal System.Random rand;
        internal static ManualLogSource LoggerInstance;

        private Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        public static new Config Config { get; internal set; }

        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            // Plugin startup logic
            Config = new(base.Config);

            rand = new System.Random(0);
            Instance = this;
            LoggerInstance = Logger;
            
            Logger.LogMessage("Loading Audio Files");
            if (Directory.Exists(Paths.PluginPath + "/Sk737-JesterSymphony/Screaming"))
            {
                var files = Directory.GetFiles(Paths.PluginPath + "/Sk737-JesterSymphony/Screaming").OrderBy(f => f);
                screamingClips = files.ToArray();
            }
            if (Directory.Exists(Paths.PluginPath + "/Sk737-JesterSymphony/Windup"))
            {
                var files = Directory.GetFiles(Paths.PluginPath + "/Sk737-JesterSymphony/Windup").OrderBy(f => f);
                windupClips = files.ToArray();
            }
            if (Directory.Exists(Paths.PluginPath + "/Sk737-JesterSymphony/Popup"))
            {
                var files = Directory.GetFiles(Paths.PluginPath + "/Sk737-JesterSymphony/Popup").OrderBy(f => f);
                popUpClips = files.ToArray();
            }
            LoadNewClips();
            
            Logger.LogMessage("Patching");
            harmony.PatchAll(typeof(JesterAIPatch));
            harmony.PatchAll(typeof(RoundManagerPatch));
            harmony.PatchAll(typeof(Plugin));
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }


        public async void LoadNewClips()
        {
            int selectionIndex = rand.Next(Config.IncludeDefaultScreaming.Value ? -1 : 0, screamingClips.Length);
            if (selectionIndex >= 0 && screamingClips.Length > 0)
                screaming = await LoadClip(screamingClips[selectionIndex]);
            else
                screaming = null;

            selectionIndex = rand.Next(Config.IncludeDefaultWindup.Value ? -1 : 0, windupClips.Length);
            if (selectionIndex >= 0 && windupClips.Length > 0)
                windup = await LoadClip(windupClips[selectionIndex]);
            else
                windup = null;
            
            selectionIndex = rand.Next(Config.IncludeDefaultPopUp.Value ? -1 : 0, popUpClips.Length);
            if (selectionIndex >= 0 && popUpClips.Length > 0)
                popUp = await LoadClip(popUpClips[selectionIndex]);
            else
                popUp = null;

        }

        public async Task<AudioClip> LoadClip(string path)
        {
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.OGGVORBIS);
            try
            {
                request.SendWebRequest();
                while (!request.isDone) { await Task.Delay(50); }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogError($"Failed to load file:{path}, + {request.error}");
                    return null;
                }
                else
                {
                    
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                    Logger.LogMessage($"Loaded file: {path}");
                    return clip;
                }
            }
            finally
            {
                request?.Dispose();
            }
        }
    }

    public class Config
    {
        public static ConfigEntry<bool> IncludeDefaultWindup;
        public static ConfigEntry<bool> IncludeDefaultScreaming;
        public static ConfigEntry<bool> IncludeDefaultPopUp;

        public Config(ConfigFile config)
        {
            IncludeDefaultWindup = config.Bind(
                "General",
                "IncludeDefaultWindup",
                true,
                "Allows the randomiser to pick the games windup sound"
                );
            IncludeDefaultScreaming = config.Bind(
                "General",
                "IncludeDefaultScreaming",
                true,
                "Allows the randomiser to pick the games screaming sound"
                );
            IncludeDefaultPopUp = config.Bind(
                "General",
                "IncludeDefaultPopUp",
                true,
                "Allows the randomiser to pick the games PopUp sound"
                );
        }
    }
}

namespace JesterSymphony.Patches
{

    [HarmonyPatch(typeof(RoundManager))]
    internal class RoundManagerPatch
    {
        [HarmonyPatch("InitializeRandomNumberGenerators")]
        [HarmonyPostfix]
        public static void SeedPatch(ref RoundManager __instance)
        {
            Plugin.LoggerInstance.LogInfo("Loading new audio files!");
            Plugin.LoggerInstance.LogInfo($"Initializing random with seed {__instance.playersManager.randomMapSeed}");
            Plugin.Instance.rand = new System.Random(__instance.playersManager.randomMapSeed);
            Plugin.Instance.LoadNewClips();
        }
    }

    [HarmonyPatch(typeof(JesterAI))]
    internal class JesterAIPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPrefix]
        public static void JesterPatch(ref AudioClip ___screamingSFX, ref AudioClip ___popGoesTheWeaselTheme, ref AudioClip ___popUpSFX)
        {
            if (Plugin.Instance.screaming != null)
                ___screamingSFX = Plugin.Instance.screaming;
            if (Plugin.Instance.windup != null)
                ___popGoesTheWeaselTheme = Plugin.Instance.windup;
            if (Plugin.Instance.popUp != null)
                ___popUpSFX = Plugin.Instance.popUp;
        }
    }

    
}