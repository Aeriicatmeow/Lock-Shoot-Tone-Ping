using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Lock_Shoot_Tone_Ping
{
    internal class ExternalPackHandler
    {
        public const string ConfigFileName = "Configs.txt";
        public const string DefaultNotated = ":[Default]:";
        private string DefaultConfigPath;

        private List<PackAudioHandler> AudioHandlersForDifferentPacks;
        public ExternalPackHandler(string Root, GameObject Host, ConfigEntry<int> Volume_Percent, AudioHandler DefaultAudioHandler)
        {

            AudioHandlersForDifferentPacks = new List<PackAudioHandler>();

            Plugin.I.Log(LogLevel.Info, "Generating External Pack Handler");
            string FileModName = Plugin.I.GetFileModName();
            string PRoot = $"{Root}\\Packs";
            if (!Directory.Exists(Root))
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Error, "External packs Folder Not Found [In External Pack Handler]. Replacement cannot therefore be generated");
                return;
            }

            Regex LastInPath = new Regex(@"^.*[\\]([^\\]*$)");


            
            //In reference to specifically ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
            //Is this a shit way of doing it?
            //Most certainly.
            //But as ive made the audio handler file mostly discrete, this is also the easiest way of doing this.
            //If the people demand more stuff to do with packs, redo this part properly please.

            foreach (string Path in Directory.GetDirectories(PRoot))
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Info, "Analysing pack at: " + Path);
                if (!File.Exists(Path + "\\" + ConfigFileName))
                {
                    Plugin.I.Log(BepInEx.Logging.LogLevel.Error, "Config File Not Found. Pack Is not suitable for use. Rejecting Pack");
                }
                else
                {

                    AudioHandlersForDifferentPacks.Add(new PackAudioHandler(
                        new AudioHandler(Host, Volume_Percent, Path),
                        LastInPath.Match(Path).Groups[1].Value,
                        File.ReadAllLines(Path + "\\" + ConfigFileName),
                        Path));
                }
            }

            DefaultConfigPath = Root + "\\Audio\\" + ConfigFileName;

            Plugin.Logger.LogInfo(DefaultConfigPath);

            AddDefaultAudioHandler(DefaultAudioHandler, Root + "\\Audio\\");
            Plugin.I.Log(LogLevel.Info, "External Pack Handler Generated");
        }
        
        private AudioHandler GenerateAudioHandlerForPack(string Path, GameObject Host, ConfigEntry<int> Volume_Percent)
        {
            string[] AudioFiles = Directory.GetFiles(Path);

            if (AudioFiles.Length < 1)
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Error, "No Audio Found In This Pack. Pack is not suitable for use. Rejecting Pack");
                return null;
            }

            Plugin.I.Log(BepInEx.Logging.LogLevel.Info, "Pack has been found to be suitable");

            return new AudioHandler(Host, Volume_Percent, Path);
        }
        public string GetDefaultConfigPath() => DefaultConfigPath;
        public void AddDefaultAudioHandler(AudioHandler DefaultAudioHandler, string DefaultPath)//to add the default audio handler as it will always be audiohandler 0
        {
            AudioHandlersForDifferentPacks.Insert(0, new PackAudioHandler(DefaultAudioHandler,DefaultNotated,File.ReadAllLines(DefaultConfigPath), DefaultPath));
        }
        public string[] GeneratePackNamesArray()
        {
            
            string[] Names = new string[AudioHandlersForDifferentPacks.Count];
            for(int i = 0; i < Names.Length; i++)
            {
                Names[i] = AudioHandlersForDifferentPacks[i].Name;
            }
            return Names;
        }
        public PackAudioHandler GetPackAudioHandlerFromName(string Name)
        {
            foreach(PackAudioHandler PAH in AudioHandlersForDifferentPacks)
            {
                if(PAH.Name == Name)
                {
                    return PAH;
                }
            }

            return new PackAudioHandler();
        }
        public int GetNumberOfLoadedPacks()
        {
            int i = 0;
            try
            {
                 i = AudioHandlersForDifferentPacks.Count;
            }
            catch(Exception EXP)
            {
                Plugin.I.Log(BepInEx.Logging.LogLevel.Fatal, EXP);
            }
            return i;
        }
    }
    internal struct PackAudioHandler
    {
        public AudioHandler AudioHandler;
        public string Name;
        public string[] Configs;
        public string Path;
        public bool IsNull;
        public PackAudioHandler(AudioHandler AudioHandler, string PackName, string[] PackConfigs, string Path)
        {
            this.AudioHandler = AudioHandler;
            Name = PackName;
            Configs = PackConfigs;
            this.Path = Path;
            IsNull = false;
        }
        public PackAudioHandler()
        {
            IsNull = true;
        }
        public string GetConfigPath() => Path + "\\" + ExternalPackHandler.ConfigFileName;
    }
}
