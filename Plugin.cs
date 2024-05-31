using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JesterSymphony.Patches;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
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

        internal List<LinkedSongs> LinkedSongs { get; set; } = new List<LinkedSongs>();

        internal string ExecutingPath { get => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }


        private void Awake()
        {
            // Plugin startup logic
            Config = new(base.Config);

            rand = new System.Random(0);
            Instance = this;
            LoggerInstance = Logger;
            
            Logger.LogMessage("Loading Audio Files");

            //Paths.PluginPath + "/Sk737-JesterSymphony/Screaming"
            if (Directory.Exists(ExecutingPath + "/Screaming"))
            {
                var files = Directory.GetFiles(ExecutingPath + "/Screaming").OrderBy(f => f);
                screamingClips = files.ToArray();
            }
            //Paths.PluginPath + "/Sk737-JesterSymphony/Windup"
            if (Directory.Exists(ExecutingPath + "/Windup"))
            {
                var files = Directory.GetFiles(ExecutingPath + "/Windup").OrderBy(f => f);
                windupClips = files.ToArray();
            }
            //Paths.PluginPath + "/Sk737-JesterSymphony/Popup"
            if (Directory.Exists(ExecutingPath + "/Popup"))
            {
                var files = Directory.GetFiles(ExecutingPath + "/Popup").OrderBy(f => f);
                popUpClips = files.ToArray();
            }
            GetLinkedSongs();
            LoadNewClips();
            
            Logger.LogMessage("Patching");
            harmony.PatchAll(typeof(JesterAIPatch));
            harmony.PatchAll(typeof(RoundManagerPatch));
            harmony.PatchAll(typeof(Plugin));
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }


        public async void LoadNewClips()
        {
            int selectedScreaming = rand.Next(Config.IncludeDefaultScreaming.Value ? -1 : 0, screamingClips.Length);
            int selectedWindup = rand.Next(Config.IncludeDefaultWindup.Value ? -1 : 0, windupClips.Length);
            int selectedPopup = rand.Next(Config.IncludeDefaultPopUp.Value ? -1 : 0, popUpClips.Length);

            if (selectedWindup >= 0 && windupClips.Length > 0)
            {
                int linkIndex = FindWindupInLinked(Path.GetFileName(windupClips[selectedWindup]));
                if (linkIndex >= 0)
                {
                    linkIndex = FindSongInScreaming(LinkedSongs[linkIndex].ScreamingName);
                    if (linkIndex >= 0)
                    {
                        selectedScreaming = linkIndex;
                        Logger.LogMessage("Found Link!");
                    }

                }
                windup = await LoadClip(windupClips[selectedWindup]);
            }
            else
                windup = null;
            
            if (selectedScreaming >= 0 && screamingClips.Length > 0)
                screaming = await LoadClip(screamingClips[selectedScreaming]);
            else
                screaming = null;
            
            if (selectedPopup >= 0 && popUpClips.Length > 0)
                popUp = await LoadClip(popUpClips[selectedPopup]);
            else
                popUp = null;

        }

        public int FindWindupInLinked(string name)
        {
            for (int i = 0; i < LinkedSongs.Count; i++)
            {
                if (Path.GetFileName(LinkedSongs[i].WindupName) == name)
                    return i;
            }
            return -1;
        }

        public int FindSongInScreaming(string name)
        {
            for(int i = 0;i < screamingClips.Length;i++)
            {
                if (Path.GetFileName(screamingClips[i]) == name)
                    return i;
            }
            return -1;
        }

        public void GetLinkedSongs()
        {
            try
            {
                if (!File.Exists(ExecutingPath + "/linkedSongs.json"))
                {
                    Logger.LogMessage("No linkfile found!");
                    return;
                }
                string json = File.ReadAllText(ExecutingPath + "/linkedSongs.json");
                LinkedSongs = JsonConvert.DeserializeObject<List<LinkedSongs>>(json);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }

        public AudioType GetAudioType(string extension)
        {
            switch (extension)
            {
                default:
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".mp3":
                    return AudioType.MPEG;
                case ".wav":
                    return AudioType.WAV;
            }
        }

        public async Task<AudioClip> LoadClip(string path)
        {
            AudioType type = GetAudioType(Path.GetExtension(path));
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(path, type);
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

    public struct LinkedSongs
    {
        public string WindupName { get; set; }
        public string ScreamingName { get; set; }
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