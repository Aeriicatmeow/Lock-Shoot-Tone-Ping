using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.DedicatedServer.Commands;
using NuclearOption.Networking;
using Unity.Audio;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Lock_Shoot_Tone_Ping;


[BepInPlugin("com.Aeriicatmeow.LockToneShootPing", " Lock Shoot Tone Ping", "1.1.0")]
public class Plugin : BaseUnityPlugin
{
    public static Plugin I { get; private set; }
    internal static new ManualLogSource Logger;

    private const string FileModName = "LockShootTonePing";
    private bool IsSetupCorrectly = false;

    private int TicksSinceJustifiedExistence = 0;



    private Harmony Inj_Harmony;
    private AudioHandler Audio;

    private ConfigEntry<int> CFG_Volume_Percent;

    //Sound Classifications by lock type
    private ConfigEntry<string>[] CFG_NEZ_Sound;
    private ConfigEntry<string>[] CFG_SHOOT_Sound;
    private ConfigEntry<string>[] CFG_LOCKING_Sound;
    private ConfigEntry<bool>[] CFG_UsingPitchDistanceScaling;
    private ConfigEntry<bool>[] CFG_PlayOnceOnStatusChanged;

    //General
    private ConfigEntry<bool> CFG_Enabled;
    private ConfigEntry<bool> CFG_PlayWhenNoTargetSelect;
    private ConfigEntry<bool> CFG_PlayWhenNoAmmo;
    private ConfigEntry<bool> CFG_PlayWhenGearDown;
    private ConfigEntry<bool> CFG_prioritizeNEZSound;

    //Sound classification by weapon type
    private ConfigEntry<bool> CFG_EnableSARH;
    private ConfigEntry<int> CFG_SARH_set;

    private ConfigEntry<bool> CFG_EnableIR;
    private ConfigEntry<int> CFG_IR_set;

    private ConfigEntry<bool> CFG_EnableOptical;
    private ConfigEntry<int> CFG_Optical_set;

    private ConfigEntry<bool> CFG_EnableLaser;
    private ConfigEntry<int> CFG_Laser_set;

    private ConfigEntry<bool> CFG_EnableBomb;
    private ConfigEntry<int> CFG_Bomb_set;

    private ConfigEntry<bool> CFG_EnableGun;
    private ConfigEntry<int> CFG_Gun_set;

    private ConfigEntry<bool> CFG_EnableJammer;
    private ConfigEntry<int> CFG_Jammer_set;

    private ConfigEntry<bool> CFG_EnableMedusaLaser;
    private ConfigEntry<int> CFG_MedusaLaser_set;

    //Misc Sounds
    private ConfigEntry<string> CFG_NoAmmoSound;
    private ConfigEntry<bool> CFG_NoAmmoSwitchEnabled;
    private bool HasAmmo;
    private string CurrentWeaponStationName;
    private char CurrentState; //L = Locking, S = Shoot, N = Nez
    private int PrevtargetNumber;

    private ConfigEntry<float> CFG_ScalePitchEnd;

    //Local Audio stores
    private AudioClip[] Aud_NEZ;
    private AudioClip[] Aud_SHOOT;
    private AudioClip[] Aud_LOCKING;
    private AudioClip Aud_NoAmmo;

    //Expandable to ensure the user can brick the mod probably
    //Acc ill prob limit it to like 10 cos this mod only covers 5 classes of weapons so 10 seems to be exhaustive
    //private ConfigEntry<int> CFG_AudioClipNumber;
    private const int AudioClipNumber = 10;

    //PACK HELL Stuff

    private Dictionary<string, ConfigEntryBase> BigConfigDictionary;//yes, this is extremely poor programming.
    private ConfigEntry<string> CFG_PackSelected;
    string CurrentSelectedPack;
    private ExternalPackHandler PackHandler;


    private AcceptableValueList<string> AllAudioNames;
    //Hey, note to future me. if things start breaking, switch the reference for nuclear option from the game folder to a local one.

    #region Mod Setup
    private void Awake()
    {

        I = this;
        // Plugin startup logic
        Logger = base.Logger;

        int[] AudioSetLimit = new int[10];
        for (int i = 0; i < AudioSetLimit.Length; i++)
        {
            AudioSetLimit[i] = i + 1;
        }

        //CFG_AudioClipNumber = Config.Bind("General", "Number Of Total Audio Sets", 3, new ConfigDescription("You get a maximun of 10 in total. Which should be enough. If you want more, ask me and ill increase the limit as all i need to do is change a number on my end", new AcceptableValueList<int>(AudioSetLimit)));
        //AudioClipNumber = CFG_AudioClipNumber.Value;

        EstablishAudioCFGArray();
        EstablishGeneralCFG();
        EstablishWeaponSpecificCFG();

        //Locating of the DLL and creation of the audio directory/filestructure.


        string Root = Path.GetDirectoryName(Info.Location);
        IsSetupCorrectly = VerifyCriticalFileStructure(Root, Info.Location);
        //if (IsSetupCorrectly)
        //{
        //    VerifyNonCriticalFileStructure(Root);
        //}



        //So what happens is that all audio is loaded into memory. Select tones (selected by the user) are then played at relevant times. those being:
        //Target Aquired, not free to shoot / Not Advisable
        //Target Aquired, Advisable to shoot. Not Ideal (more of the thing for MMRs and so on as you want to shoot them at short range but you can, if you choose to, shoot them at long range)
        //Target Aquired, Shooting is advisable. Conditions are nominal.



        Audio = new AudioHandler(gameObject, CFG_Volume_Percent, Root + "\\Audio");

        PackHandler = new ExternalPackHandler(Root, gameObject, CFG_Volume_Percent, Audio);

        EstablishAudioSetsCFG();
        HasAmmo = true;

        Logger.LogInfo("loading audio into memory");
        LoadAudioClipsIntoMemory(true);

        Logger.LogInfo("Loading Harmony Patches");
        Inj_Harmony = new Harmony($"com.Aeriicatmeow.{FileModName}");
        Inj_Harmony.PatchAll();

        TicksSinceJustifiedExistence = 0;

        Logger.LogInfo("Generating ConfigDictionary");
        BigConfigDictionary = DefineConfigDictionary();

        EstablishPackHandlingCFG();

        Logger.LogInfo($"Plugin {FileModName} is loaded!");

        //WriteConfigsToExternalFile(Root + "\\Audio\\"+ExternalPackHandler.ConfigFileName);

    }
    private void initialiseAllConfigsOnly()
    {
        EstablishAudioCFGArray();
        EstablishGeneralCFG();
        EstablishWeaponSpecificCFG();

        EstablishAudioSetsCFG();
        BigConfigDictionary = DefineConfigDictionary();
        EstablishPackHandlingCFG();

        LoadAudioClipsIntoMemory(true);
    }
    private void EstablishAudioSetsCFG()
    {
        
        Logger.LogInfo("Establishing AudioSets Configs");

        string[] AudioNames = Audio.CreateArrayOfAudioNames();

        foreach (string s in AudioNames)
        {
            Logger.LogInfo(s);
        }
        //AudioNames.Append(":null:");
        CFG_NoAmmoSwitchEnabled = Config.Bind("Misc", "Play NoAmmo On Weapon Switch", false, "If Enabled, The no Ammo tone will also play when switching to a weapon with no ammo. If disabled, sound will play once when your current weapon runs out of ammo");
        if (AudioNames.Length > 0)
        {
            CFG_NoAmmoSound = Config.Bind("Misc", "NoAmmo", AudioNames[0], new ConfigDescription("What Audio Tones do you want to use when you are out of ammo?", new AcceptableValueList<string>(AudioNames)));
        }
        else
        {
            CFG_NoAmmoSound = Config.Bind("Misc", "NoAmmo", "", "What Audio Tones do you want to use? (None found Yet)");
        }

        CFG_ScalePitchEnd = Config.Bind("Misc", "(Distance) PitchScale", 0.5f, new ConfigDescription("How many octives higher (or lower if negetive) do you want the shoot tone to be at minimun range on any sets that have Pitch Scale With Distance Enabled. Note: a value of zero will yield no change in pitch", new AcceptableValueRange<float>(-3f,3f)));

        Logger.LogInfo("Entering 2nd Half of AudioSets Config");
        for (int i = 0; i < AudioClipNumber; i++)
        {
            SetupAlarmCFG(ref CFG_NEZ_Sound[i], "target in NEZ", "1) NezSound", "Set " + i, AudioNames);
            SetupAlarmCFG(ref CFG_SHOOT_Sound[i], "it is viable to shoot", "2) ShootSound", "Set " + i, AudioNames);
            SetupAlarmCFG(ref CFG_LOCKING_Sound[i], "it is not advisable to shoot", "3) LockingSound", "Set " + i, AudioNames);
            CFG_UsingPitchDistanceScaling[i] = Config.Bind("Set " + i + " Sounds", "4) Enable Distance Pitch Scaling", false, "If Enabled, the NEZ sound will be replaced by the Shoot Sound of variable pitch. The pitch depends on the relevant config in the Misc Section");
            CFG_PlayOnceOnStatusChanged[i] = Config.Bind("Set " + i + " Sounds", "5) PlayAudioOnceWhenLockStatusChanged", false, "If Enabled, Selected audio will play once when the status of the lock has changed (e.g. from Shoot to NEZ). If disabled then audio will play continuously. Please note that this is not compatible with pitch scaling for obvious reasons");
        }


    }
    private void EstablishAudioCFGArray()
    {
        Logger.LogInfo("Establishing Audio Config Arrays");
        Aud_NEZ = new AudioClip[AudioClipNumber];
        Aud_SHOOT = new AudioClip[AudioClipNumber];
        Aud_LOCKING = new AudioClip[AudioClipNumber];

        CFG_NEZ_Sound = new ConfigEntry<string>[AudioClipNumber];
        CFG_SHOOT_Sound = new ConfigEntry<string>[AudioClipNumber];
        CFG_LOCKING_Sound = new ConfigEntry<string>[AudioClipNumber];
        CFG_UsingPitchDistanceScaling = new ConfigEntry<bool>[AudioClipNumber];
        CFG_PlayOnceOnStatusChanged = new ConfigEntry<bool>[AudioClipNumber];
    }
    private void EstablishGeneralCFG()
    {
        Logger.LogInfo("Establishing General Configs");
        CFG_Volume_Percent = Config.Bind("General", "GeneralVolume", 100, new ConfigDescription("How Loud you want to be told when to shoot", new AcceptableValueRange<int>(0, 200)));
        CFG_Enabled = Config.Bind("General", "ModEnabled", true, "Do you want to enable this mod?");
        CFG_PlayWhenNoTargetSelect = Config.Bind("General", "PlayWhenNoTargets", false, "Do you want the no lock sound to be played when you have no target selected?");
        CFG_PlayWhenNoAmmo = Config.Bind("General", "PlayWhenNoAmmo", false, "Do you want Sound to be played even if you have no Ammo");
        CFG_PlayWhenGearDown = Config.Bind("General", "PlayWhenGearDown", false, "If Enabled, Lock tones continue to play when your landing gear is extended");
        CFG_prioritizeNEZSound = Config.Bind("General", "PrioritizeNEZ", false, "If ticked, if a missile does not have a NEZ defined, the NEZ sound will play to shoot instead of the shoot sound");
    }
    private void EstablishWeaponSpecificCFG()
    {
        Logger.LogInfo("Establishing Weapon Specific Configs");

        int[] SetNumbers = new int[AudioClipNumber];
        for(int i = 0; i < AudioClipNumber; i++)
        {
            SetNumbers[i] = i;
        }
        //SARH
        CFG_EnableSARH = Config.Bind("SARH/ARM/ARAD", "Radar_Enabled", true, "If Enabled, Lock Tone Sounds will play when using SARHs/ARHs/ARADs");
        CFG_SARH_set = Config.Bind("SARH/ARM/ARAD", "RadarSet", 0, new ConfigDescription("What Audio set do you want to use for SARHs/ARHs/ARADs", new AcceptableValueList<int>(SetNumbers)));

         //IR
        CFG_EnableIR = Config.Bind("IR", "IR_Enabled", true, "If Enabled, Lock Tone Sounds will play when using IR Missiles");
        CFG_IR_set = Config.Bind("IR", "IR_Set", 0, new ConfigDescription("What Audio set do you want to use for IR Missiles", new AcceptableValueList<int>(SetNumbers)));

        //Optical

        CFG_EnableOptical = Config.Bind("Optical", "Optical_Enabled", true, "If Enabled, Lock Tone Sounds will play when using Optical Missiles");
        CFG_Optical_set = Config.Bind("Optical", "Optical_Set", 0, new ConfigDescription("What Audio set do you want to use for Optical Missiles", new AcceptableValueList<int>(SetNumbers)));

        //Laser
        CFG_EnableLaser = Config.Bind("Laser", "Laser_Enabled", true, "If Enabled, Lock Tone Sounds will play when using Laser Missiles");
        CFG_Laser_set = Config.Bind("Laser", "Laser_Set", 0, new ConfigDescription("What Audio set do you want to use for Laser Missiles", new AcceptableValueList<int>(SetNumbers)));

        //Bombs
        CFG_EnableBomb = Config.Bind("Gravity Bombs", "Bombs_Enabled", true, "If Enabled, Lock Tone Sounds will play when using Gravity Bombs");
        CFG_Bomb_set = Config.Bind("Gravity Bombs", "Bombs_Set", 0, new ConfigDescription("What Audio set do you want to use for Gravity Bombs", new AcceptableValueList<int>(SetNumbers)));

        //Guns
        CFG_EnableGun = Config.Bind("Guns", "Guns_Enabled", true, "If Enabled, Lock Tone Sounds will play when using Guns");
        CFG_Gun_set = Config.Bind("Guns", "Guns_Set", 0, new ConfigDescription("What Audio set do you want to use for Guns", new AcceptableValueList<int>(SetNumbers)));

        //Jammer
        CFG_EnableJammer = Config.Bind("Medusa", "JammerPods_Enabled", true, "If Enabled, Lock Tone Sounds will play when using Jamming Pods");
        CFG_Jammer_set = Config.Bind("Medusa", "JammerPods_Set", 0, new ConfigDescription("What Audio set do you want to use for Jamming Pods", new AcceptableValueList<int>(SetNumbers)));

        //High Power Laser
        CFG_EnableMedusaLaser = Config.Bind("Medusa", "HighPowerLaser_Enabled", true, "If Enabled, Lock Tone Sounds will play when using HighPowerLaser");
        CFG_MedusaLaser_set = Config.Bind("Medusa", "HighPowerLaser_Set", 0, new ConfigDescription("What Audio set do you want to use for HighPowerLaser", new AcceptableValueList<int>(SetNumbers)));
    }

    private void EstablishPackHandlingCFG()
    {
        Logger.LogInfo("Generating Pack Configs");
        Logger.LogInfo(PackHandler.GetNumberOfLoadedPacks());
        CFG_PackSelected = Config.Bind("General", "SelectedPack", ExternalPackHandler.DefaultNotated,new ConfigDescription("What External Packs do you want to load?", new AcceptableValueList<string>(PackHandler.GeneratePackNamesArray())));
        //CurrentSelectedPack = CFG_PackSelected.Value;
        Logger.LogInfo("2nd Part");
        UpdateActivePack();
        
    }
    //private void Update()
    //{
    //    if(AudioClipNumber != CFG_AudioClipNumber.Value)
    //    {
    //        Logger.LogError("Mismatch between AudioSetNumber. Reloading Configs");

    //        EstablishAudioCFGArray();
    //        EstablishGeneralCFG();
    //        EstablishWeaponSpecificCFG();
    //        EstablishAudioSetsCFG();
    //    }
    //}
    private void SetupAlarmCFG(ref ConfigEntry<string> M_Config, string Hint, string Key, string category, string[] AudioNames)
    {
        if (AudioNames.Length > 0)
        {
            M_Config = Config.Bind(category + " Sounds", Key, AudioNames[0], new ConfigDescription($"What Audio Tones do you want to use when using {Hint}?", new AcceptableValueList<string>(AudioNames)));
        }
        else
        {
            M_Config = Config.Bind(category + " Sounds", Key, "", "What Audio Tones do you want to use? (None found Yet)");
        }
    }
    #region AudioHandlingInThisScript
    private void LoadAudioClipsIntoMemory(bool OnStartUp = false)
    {
        //Logger.LogInfo("Request to reload audio files received. NEZ: "+ CFG_NEZ_Sound.Value + " SHOOT "+CFG_SHOOT_Sound.Value + " LOCK: "+CFG_LOCKING_Sound.Value);
        if (OnStartUp)
        {
            //as aud_X will not actually have a value yet.
            Logger.LogInfo("Request to reload all audio.");
            for (int i = 0; i < AudioClipNumber; i++)
            {
                Aud_NEZ[i] = Audio.SimpleSearchForAudio(CFG_NEZ_Sound[i].Value);
                Aud_SHOOT[i] = Audio.SimpleSearchForAudio(CFG_SHOOT_Sound[i].Value);
                Aud_LOCKING[i] = Audio.SimpleSearchForAudio(CFG_LOCKING_Sound[i].Value);
            }
            Aud_NoAmmo = Audio.SimpleSearchForAudio(CFG_NoAmmoSound.Value);
        }
        else
        {
            for (int i = 0; i < AudioClipNumber; i++)
            {
                //Logger.LogInfo("Interation: " + i);
                if (CheckIfReloadIsNeeded(CFG_NEZ_Sound[i], Aud_NEZ[i]))
                {
                    if (CFG_NEZ_Sound[i].Value != AudioHandler.NoAudio)
                    {
                        Logger.LogDebug("Mismatch for Audio (NEZ). Updating");
                        Aud_NEZ[i] = Audio.SimpleSearchForAudio(CFG_NEZ_Sound[i].Value);
                    }
                    else
                    {
                        Aud_NEZ[i] = null;
                    }
                }
                if (CheckIfReloadIsNeeded(CFG_SHOOT_Sound[i], Aud_SHOOT[i]))
                {
                    if (CFG_SHOOT_Sound[i].Value != AudioHandler.NoAudio)
                    {
                        Logger.LogDebug("Mismatch for Audio (SHOOT). Updating");
                        Aud_SHOOT[i] = Audio.SimpleSearchForAudio(CFG_SHOOT_Sound[i].Value);
                    }
                    else
                    {
                        Aud_SHOOT[i] = null;
                    }
                }
                if (CheckIfReloadIsNeeded(CFG_LOCKING_Sound[i], Aud_LOCKING[i]))
                {
                    if (CFG_LOCKING_Sound[i].Value != AudioHandler.NoAudio)
                    {
                        Logger.LogDebug("Mismatch for Audio (LOCKING). Updating");
                        Aud_LOCKING[i] = Audio.SimpleSearchForAudio(CFG_LOCKING_Sound[i].Value);
                    }
                    else
                    {
                        Aud_LOCKING[i] = null;
                    }
                }
            }
            if (CheckIfReloadIsNeeded(CFG_NoAmmoSound, Aud_NoAmmo))
            {
                Logger.LogDebug("Mismatch for Audio (NoAmmo). Updating");
                Aud_NoAmmo = Audio.SimpleSearchForAudio(CFG_NoAmmoSound.Value);
            }
        }
        //Logger.LogInfo("load (or reload) complete");
    }
    private bool CheckIfReloadIsNeeded(ConfigEntry<string> CFG, AudioClip AC)
    {
        if (AC == null & CFG.Value != AudioHandler.NoAudio)
        {
            return true;
        }
        else
        {
            if (AC != null)
            {
                if (CFG.Value != AC.name)
                {
                    return true;
                }
            }
        }
        return false;
    }
    #endregion
    #region FileHandling
    private static bool VerifyCriticalFileStructure(string Root, string CurrentDLLPath)
    {
        Regex LastInPath = new Regex(@"^.*[\\]([^\\]*$)");


        if (LastInPath.Match(Root).Groups[1].Value != FileModName)
        {
            Logger.LogError($"Correct file structure missing. Specifically the {FileModName} Mod Folder.");
            if (Directory.Exists($"{Root}\\{FileModName}"))
            {
                Logger.LogWarning("Mod folder does exist. Please move DLL file to this folder for mod to work.");
            }
            else
            {
                Logger.LogError("Mod Folder Not Found. Generating New one");
                Directory.CreateDirectory($"{Root}\\{FileModName}\\Audio");
                Directory.CreateDirectory($"{Root}\\{FileModName}\\Packs");
            }
            Logger.LogInfo("Restart of Nuclear Option is required to ensure the mod works properly");
            try
            {
                File.Copy(CurrentDLLPath, $"{Root}\\{FileModName}\\Lock_Shoot_Tone_Ping.dll");
                Logger.LogInfo(CurrentDLLPath + " -> " + $"{Root}\\{FileModName}\\Lock_Shoot_Tone_Ping.dll");
            }
            catch
            {
                Logger.LogError("Could not copy dll");
            }
        }
        else
        {
            if (Directory.Exists($"{Root}\\Audio"))
            {
                Logger.LogMessage("File Structure Verifed");

            }
            else
            {
                Logger.LogWarning("Audio Folder Not Found. Generating New one");
                Directory.CreateDirectory($"{Root}\\Audio");
                Logger.LogMessage("File Structure has been corrected. Should be functional now.");
            }
            if (!Directory.Exists($"{Root}\\Packs"))
            {
                Logger.LogWarning("External Packs Folder does not exist. Generating replacement");
                Directory.CreateDirectory($"{Root}\\Packs");
            }

            return true;
        }
        return false;
    }

    private static void VerifyNonCriticalFileStructure(string Root)
    {
        //to be ran after the critical file structure script
        if (!Directory.Exists($"{Root}\\Packs"))
        {
            Logger.LogWarning("External Packs Folder does not exist. Generating replacement");
            Directory.CreateDirectory($"{Root}\\Packs");
        }

    }
    #endregion
    #endregion


    #region LockAudioLogic
    private void Update()
    {

        LoadAudioClipsIntoMemory();
        TicksSinceJustifiedExistence++;
        //Logger.LogInfo("Ticks Since Justified: " + TicksSinceJustifiedExistence);
        if (TicksSinceJustifiedExistence > 3)
        {
            StopAudio();
        }
        UpdateActivePack();
    }
    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        Audio.Stop();
        //LoadAudioClipsIntoMemory(true);
    }
    private bool ResolveIfNEZSound(float NEZ, float TargetDistance)
    {
        if (TargetDistance < NEZ)
        {
            if(TargetDistance == 0)//i.e. weapon does not have a defined NEZ
            {
                if (CFG_prioritizeNEZSound.Value)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }

        }
        else
        {
            return false;
        }
    }
    public void ResolveLockAudio(bool ClearToShoot, float NEZ, float TargetDistance, float MaxRange, float MinRange,int TargetNumber, bool SufficientAmmo, bool GearDown, WeaponStation WeaponStation)
    {
        if (CFG_Enabled.Value)
        {
            //Logger.LogInfo("Resolving through method 1");
            //Logger.LogInfo("Harmony Patch Recieved | CTS: " + ClearToShoot + " | NEZ: " + NEZ + " | TD: " + TargetDistance);
            int SetToPlayFrom = -1;
            if ((TargetNumber > 0 || CFG_PlayWhenNoTargetSelect.Value) & (SufficientAmmo || CFG_PlayWhenNoAmmo.Value) & CheckIfWeaponTypeEnabled(WeaponStation, ref SetToPlayFrom) & (!GearDown || CFG_PlayWhenGearDown.Value))
            {
                TicksSinceJustifiedExistence = 0;
                if (ClearToShoot)
                {

                    if (ResolveIfNEZSound(NEZ, TargetDistance))
                    {
                        if (CFG_UsingPitchDistanceScaling[SetToPlayFrom].Value)
                        {
                            //Audio.ResetPitch();
                            //Audio.SetPitch(ResolveAudioPitchFromDistance(TargetDistance, NEZ, MinRange));
                            InternalRequestToPlayLockStateAudio(Aud_SHOOT[SetToPlayFrom], 'N', SetToPlayFrom, WeaponStation, TargetNumber,ResolveAudioPitchFromDistance(TargetDistance, NEZ, MinRange));
                        }
                        else
                        {
                            //Logger.LogInfo("PLAY NEZ");
                            //Audio.ResetPitch();
                            InternalRequestToPlayLockStateAudio(Aud_NEZ[SetToPlayFrom], 'N', SetToPlayFrom, WeaponStation, TargetNumber);
                        }
                        
                    }
                    else
                    {
                        //Logger.LogInfo("PLAY SHOOT");
                        InternalRequestToPlayLockStateAudio(Aud_SHOOT[SetToPlayFrom], 'S', SetToPlayFrom, WeaponStation, TargetNumber);
                        CurrentState = 'S';
                    }

                }
                else
                {
                    //Logger.LogInfo("PLAY LOCK");
                    InternalRequestToPlayLockStateAudio(Aud_LOCKING[SetToPlayFrom], 'L', SetToPlayFrom, WeaponStation, TargetNumber);
                    CurrentState = 'L';
                }
            }
            else
            {
                Audio.Stop();
            }
            ResolveAudioLockPost(SufficientAmmo, WeaponStation);
        }
        PrevtargetNumber = TargetNumber;
    }
    public void ResolveLockAudio(bool ClearToShoot, float TargetDistance, float MaxRange, float MinRange,int TargetNumber, bool SufficientAmmo, bool GearDown, WeaponStation WeaponStation, bool DisableDistanceScale = false)
    {
        if (CFG_Enabled.Value)
        {
            int SetToPlayFrom = -1;
            //Logger.LogInfo("Resolving through method 2");
            if ((TargetNumber > 0 || CFG_PlayWhenNoTargetSelect.Value) & (SufficientAmmo || CFG_PlayWhenNoAmmo.Value) & CheckIfWeaponTypeEnabled(WeaponStation, ref SetToPlayFrom) & (!GearDown || CFG_PlayWhenGearDown.Value))
            {
                TicksSinceJustifiedExistence = 0;
                if (ClearToShoot)
                {
                    if (CFG_UsingPitchDistanceScaling[SetToPlayFrom].Value & !DisableDistanceScale)
                    {
                        Audio.ResetPitch();
                        Audio.SetPitch(ResolveAudioPitchFromDistance(TargetDistance, MaxRange, MinRange));
                    }
                    else
                    {
                        Audio.ResetPitch();
                    }
                    if (CFG_prioritizeNEZSound.Value)
                    {
                        //Logger.LogInfo("PLAY NEZ");
                        InternalRequestToPlayLockStateAudio(Aud_NEZ[SetToPlayFrom], 'S', SetToPlayFrom, WeaponStation, TargetNumber);
                        
                    }
                    else
                    {
                        //Logger.LogInfo("PLAY SHOOT");
                        InternalRequestToPlayLockStateAudio(Aud_SHOOT[SetToPlayFrom], 'S', SetToPlayFrom, WeaponStation, TargetNumber);
                        
                    }

                }
                else
                {
                    //Logger.LogInfo("PLAY LOCK");
                    InternalRequestToPlayLockStateAudio(Aud_LOCKING[SetToPlayFrom], 'L', SetToPlayFrom, WeaponStation, TargetNumber);
                    
                }
            }
            else
            {
                Audio.Stop();
            }
            ResolveAudioLockPost(SufficientAmmo, WeaponStation);
        }
        PrevtargetNumber = TargetNumber;
        
    }

    private void InternalRequestToPlayLockStateAudio(AudioClip SourceAudio, char Code, int Set, WeaponStation WeaponStation, int CurrentTargetCount,float pitch = 1)//only on the calls for lock state audio. not for anything else
    {
        if(
            ((Code != CurrentState || WeaponStation.WeaponInfo.weaponName !=  CurrentWeaponStationName || PrevtargetNumber != CurrentTargetCount) & CFG_PlayOnceOnStatusChanged[Set].Value) 
            
            || !CFG_PlayOnceOnStatusChanged[Set].Value)
        {
            Audio.PlayAudio(SourceAudio, CFG_PlayOnceOnStatusChanged[Set].Value, pitch);
        }
        CurrentState = Code;
        
        
    }
    private float ResolveAudioPitchFromDistance(float TargetDistance, float MaxRange, float MinRange)
    {
        float Multi = MathF.Abs((TargetDistance - MinRange) / (MaxRange - MinRange) - 1);
        float Pitch = MathF.Pow(2, Multi * CFG_ScalePitchEnd.Value);//default pitch in unit is 1.
        return Pitch;
        //Audio.SetPitch(Pitch);
    }
    private void ResolveAudioLockPost(bool SufficientAmmo, WeaponStation WeaponStation)
    {
        if((HasAmmo & !SufficientAmmo & WeaponStation.WeaponInfo.weaponName == CurrentWeaponStationName) || 
            (!SufficientAmmo & WeaponStation.WeaponInfo.weaponName != CurrentWeaponStationName & CFG_NoAmmoSwitchEnabled.Value))
        {
            Audio.PlayAudio(Aud_NoAmmo, true);
        }
        HasAmmo = SufficientAmmo;
        CurrentWeaponStationName = WeaponStation.WeaponInfo.weaponName;
    }

    private bool CheckIfWeaponTypeEnabled(WeaponStation WeaponStation, ref int SetToPlayFrom)
        //0 - SARH
        //1 - IR
        //2 - Optical
        //3 - Laser
        //4 - Bomb
        //5 - Gun
        //6 - Jammer
        //7 - Medusa Laser
    {
        if (SetToPlayFrom < 0 || SetToPlayFrom > 5) {
            WeaponInfo Info = WeaponStation.WeaponInfo;
            if (Info.bomb || Info.glideBomb)
            {
                SetToPlayFrom = 4;
            }
            else if (Info.gun)
            {
                SetToPlayFrom = 5;
            }
            else if (Info.laserGuided)
            {
                SetToPlayFrom = 3;
            }
            else if (Info.jammer)
            {
                SetToPlayFrom = 6;
                //Yes, No Radar Jammer Noise
                //ok: update: jammers now have an option for noise ig.
                //this is more for completionist sake than acc utility. you can bet im turning this off after ive tested it.
            }
            else
            {
                if (new Regex("laser").Match(Info.shortName.ToLower()).Success)
                {
                    SetToPlayFrom = 7;
                    //this indicates medusa death laser

                }
                else
                {
                    MissileSeeker SeekingType = Info.weaponPrefab.GetComponent(typeof(MissileSeeker)) as MissileSeeker;
                    if (SeekingType == null)
                    {
                        SetToPlayFrom = -1;
                        //hopefully this is such a niche that it never gets tirggered
                    }
                    else
                    {
                        SetToPlayFrom = (SeekingType.GetSeekerType()) switch
                        {
                            "IR" => 1,
                            "ARAD" => 0,
                            "ARH" => 0,
                            "SARH" => 0,
                            "Optical" => 2,
                            "INS / Opt." => 2,
                            _ => -1
                        };
                    }
                }
                
            }
        }
        bool ReturnBool = SetToPlayFrom switch
        {
            0 => CFG_EnableSARH.Value,
            1 => CFG_EnableIR.Value,
            2 => CFG_EnableOptical.Value,
            3 => CFG_EnableLaser.Value,
            4 => CFG_EnableBomb.Value,
            5 => CFG_EnableGun.Value,
            6 => CFG_EnableJammer.Value,
            7 => CFG_EnableMedusaLaser.Value,
            _ => false
        };
        SetToPlayFrom = SetToPlayFrom switch
        {
            0 => CFG_SARH_set.Value,
            1 => CFG_IR_set.Value,
            2 => CFG_Optical_set.Value,
            3 => CFG_Laser_set.Value,
            4 => CFG_Bomb_set.Value,
            5 => CFG_Gun_set.Value,
            6 => CFG_Jammer_set.Value,
            7 => CFG_MedusaLaser_set.Value,
            _ => -1
        };
        return ReturnBool;
        
    }


    #endregion

    #region misc
    public void StopAudio()
    {
        Audio.Stop();
    }
    public void StopAudioAndRatchetUpJustification()
    {
        StopAudio();
        TicksSinceJustifiedExistence = int.MaxValue / 2;
    }
    private void OnDestroy()
    {
        if (!IsSetupCorrectly)
        {
            File.Delete(Info.Location);
        }
        else if(CFG_PackSelected.Value == ExternalPackHandler.DefaultNotated)
        {
            WriteConfigsToExternalFile(PackHandler.GetDefaultConfigPath());
        }
    }
    public void Log(LogLevel LogLevel, object Data)
    {
        Logger.Log(LogLevel, Data);
    }
    public string GetFileModName() => FileModName;
    #endregion
    #region ExternalPackHandling

    private Dictionary<string,ConfigEntryBase> DefineConfigDictionary()
    {
        Dictionary<string, ConfigEntryBase> ReturnDictionary = new Dictionary<string, ConfigEntryBase>();
        //string text = "#START#\n";
        //General
        //text += "#GENERAL#\n";
        //Logger.LogInfo("Dictionary. General");
        AppendDictionary(CFG_PlayWhenNoTargetSelect, ref ReturnDictionary);
        AppendDictionary(CFG_PlayWhenNoAmmo, ref ReturnDictionary);
        AppendDictionary(CFG_PlayWhenGearDown, ref ReturnDictionary);
        AppendDictionary(CFG_prioritizeNEZSound, ref ReturnDictionary);

        //Weapons
        //Logger.LogInfo("Dictionary. Weapons");
        //RADAR
        //Logger.LogInfo("Dictionary. Radar");
        AppendDictionary(CFG_SARH_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableSARH, ref ReturnDictionary);

        //IR
        //Logger.LogInfo("Dictionary. IR");
        AppendDictionary(CFG_IR_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableIR, ref ReturnDictionary);

        //Optical
        //Logger.LogInfo("Dictionary. Optical");
        AppendDictionary(CFG_Optical_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableOptical, ref ReturnDictionary);

        //Laser
        //Logger.LogInfo("Dictionary. Laser");
        AppendDictionary(CFG_Laser_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableLaser, ref ReturnDictionary);

        //Bomb
        //Logger.LogInfo("Dictionary. Bomb");
        AppendDictionary(CFG_Bomb_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableBomb, ref ReturnDictionary);

        //Gun
        //Logger.LogInfo("Dictionary. Gun");
        AppendDictionary(CFG_Gun_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableGun, ref ReturnDictionary);

        //Jammer
        //Logger.LogInfo("Dictionary. Jammer");
        AppendDictionary(CFG_Jammer_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableJammer, ref ReturnDictionary);

        //MedusaLaser
        //Logger.LogInfo("Dictionary. MedusaLaser");
        AppendDictionary(CFG_MedusaLaser_set, ref ReturnDictionary);
        AppendDictionary(CFG_EnableMedusaLaser, ref ReturnDictionary);

        //Misc
        //Logger.LogInfo("Dictionary. Misc");
        AppendDictionary(CFG_NoAmmoSound, ref ReturnDictionary);
        AppendDictionary(CFG_NoAmmoSwitchEnabled, ref ReturnDictionary);
        AppendDictionary(CFG_ScalePitchEnd, ref ReturnDictionary);

        //The Sets
        //Logger.LogInfo("Dictionary. Sets");
        for (int i = 0; i < AudioClipNumber; i++)
        {
            //Logger.LogInfo("Dictionary. "+i);
            AppendDictionary(CFG_NEZ_Sound[i], ref ReturnDictionary);
            AppendDictionary(CFG_SHOOT_Sound[i], ref ReturnDictionary);
            AppendDictionary(CFG_LOCKING_Sound[i], ref ReturnDictionary);
            AppendDictionary(CFG_UsingPitchDistanceScaling[i], ref ReturnDictionary);
        }

        return ReturnDictionary;
    }
    private void AppendDictionary<T>(ConfigEntry<T> Config, ref Dictionary<string, ConfigEntryBase> Dictionary)
    {
        //Logger.LogInfo("Processing" + Config.Definition);
        //try
        //{
            Dictionary.Add(Config.Definition.ToString(), Config);
        //}
        //catch(Exception Value)
        //{
        //    Logger.LogFatal(Value+"\n"+Config.Definition.ToString());
            
        //}
    }

    private void AppendStringWithRelevantConfig<T>(ConfigEntry<T> Config, ref string ReturnString)
    {
        ReturnString += Config.Definition + "#" + Config.Value +"\n";
    }
    private void WriteConfigsToExternalFile(string Path)
    {
        //string text = "";
        //foreach(string s in BigConfigDictionary.Keys)
        //{
        //    text += s + "#" + BigConfigDictionary[s].BoxedValue  +"\n";
        //}
        //File.WriteAllText(Path, text);
        File.WriteAllLines(Path, GetCurrentConfig());
    }
    private string[] GetCurrentConfig()
    {
        int index = 0;
        string[] CFG = new string[BigConfigDictionary.Count];
        foreach (string s in BigConfigDictionary.Keys)
        {
            CFG[index] = s + "#" + BigConfigDictionary[s].BoxedValue;
            index++;
        }
        return CFG;
    }
    private void LoadConfigsFromText(string[] Lines)//this doesnt work for the arrays of configs. how odd.
    {
        Regex ConfigComb = new Regex(@"^(.+)\#(.+)$");
        Regex NumberOnlyCheck = new Regex(@"^[\d|\.]+$");
        foreach(string s in Lines)
        {
            if (ConfigComb.Match(s).Success)
            {
                string[] data = s.Split('#');
                //[0] is the config field, [1] is the value;
                ConfigEntryBase SpecificConfig = BigConfigDictionary[data[0]];
                //Logger.LogInfo("Processing " + SpecificConfig.Definition);
                //Logger.LogInfo("OLD:" + SpecificConfig.BoxedValue);
                if (SpecificConfig.GetType() == typeof(ConfigEntry<bool>))
                {
                    SpecificConfig.BoxedValue = data[1] switch
                    {
                        "True" => true,
                        "False" => false,
                        _ => false
                    };
                    //Logger.LogInfo("This is boolean");

                }
                else if(SpecificConfig.GetType() == typeof(ConfigEntry<string>))
                {
                    SpecificConfig.BoxedValue = data[1];
                    //Logger.LogInfo("replacing " + SpecificConfig.Definition + " with " + data[1]);

                }
                else if (SpecificConfig.GetType() == typeof(ConfigEntry<int>))
                {
                    if (NumberOnlyCheck.Match(data[1]).Success)
                    {
                        SpecificConfig.BoxedValue = System.Convert.ToInt32(data[1]);

                        //Logger.LogInfo("replacing " + SpecificConfig.Definition + " with " + data[1]);
                    }
                    else
                    {
                        Plugin.I.Log(LogLevel.Error, "Integer expected. value could not be parsed to integer. value: " + data[1]);
                    }
                }
                else if(SpecificConfig.GetType() == typeof(ConfigEntry<float>))
                {
                    if (NumberOnlyCheck.Match(data[1]).Success)
                    {
                        SpecificConfig.BoxedValue = float.Parse(data[1]);

                        //Logger.LogInfo("replacing " + SpecificConfig.Definition + " with " + data[1]);
                    }
                    else
                    {
                        Plugin.I.Log(LogLevel.Error, "Float expected. value could not be parsed to Float. value: " + data[1]);
                    }
                }
                else
                {
                    Plugin.I.Log(LogLevel.Error,"Unknown config. " + SpecificConfig.GetType());
                }

                //Logger.LogInfo("NEW:" + SpecificConfig.BoxedValue);

            }
            
        }
    }


                    //    try
                    //{
                    //    //Haha. big vulnerability go brrr.
                    //                                        //well ig this does technically paint over the cracks.
                    //                                        //this mod was never meant to have packs. 
                    //                                        //if I had intended to implement this feature from the start i would have set out its architecture that way.
                    //                                        //but alas, i misjudged what the people want. and the people want packs. and so I shall deliver.
                    //                                        //I hope I dont succumb to the same pit falls as my predessor.
                    //                                        //should that happen, ill just make the whole thing from the ground up. 
                    //                                        //This has been my first mod and it has been a wonderful learning experience. 


                    //    //I love learning so much.
                    //}
                    //catch (Exception Exception)
                    //{

                    //    Logger.LogFatal("ERROR LOADING CONFIG FILE\n" + Exception);
                    ////}

private void UpdateActivePack()
    {
        if (CurrentSelectedPack != CFG_PackSelected.Value)
        {
            PackAudioHandler CurrentAudioPack = PackHandler.GetPackAudioHandlerFromName(CurrentSelectedPack);
            if (!CurrentAudioPack.IsNull)
            {
                Logger.LogInfo("Saving Current PackConfig");
                WriteConfigsToExternalFile(CurrentAudioPack.GetConfigPath());
                

            }
            else
            {
                Logger.LogError("Current Pack could not be saved .Pack is not defined. If this is triggered during mod load up, disregard.");
            }
            Logger.LogInfo("Loading Pack" + CFG_PackSelected.Value);
            CurrentAudioPack = PackHandler.GetPackAudioHandlerFromName(CFG_PackSelected.Value);
            if (!CurrentAudioPack.IsNull)
            {
                Logger.LogInfo("Loading " + CFG_PackSelected.Value);
                try
                {
                    CurrentSelectedPack = CFG_PackSelected.Value;//This must go here to stop infitite recursion.

                    StopAudioAndRatchetUpJustification();
                    Audio = CurrentAudioPack.AudioHandler;


                    LoadConfigsFromText(CurrentAudioPack.Configs);
                    Logger.LogInfo("Pack Loaded");

                    Logger.LogInfo("Reloading Mod (includes clearing config");
                    //try
                    //{
                    //    ReEstablishAudioSetsCFG();//this is meant to reload all acceptable values lists. we shall see if it acc does
                    //}
                    //catch(Exception e)
                    //{
                    //    Logger.LogFatal(e);
                    //}

                    //Logger.LogInfo("loading audio into memory");
                    //LoadAudioClipsIntoMemory(true);

                    Config.Clear();
                    initialiseAllConfigsOnly();


                }
                catch(Exception EXP)
                {
                    Logger.LogFatal(EXP);
                }

            }
            else
            {
                Logger.LogError("Could not Load Pack. Pack is not defined.");
            }

            //Logger.LogInfo("Reloading Mod");
            //EstablishAudioSetsCFG();

            //Logger.LogInfo("loading audio into memory");
            //LoadAudioClipsIntoMemory(true);
            //CurrentSelectedPack = CFG_PackSelected.Value;
        }

    }

    private void ReEstablishAudioSetsCFG()
    {
        Logger.LogInfo("Establishing AudioSets Configs");

        string[] AudioNames = Audio.CreateArrayOfAudioNames();

        Logger.LogInfo("audio pack contains");
        foreach (string s in Audio.CreateArrayOfAudioNames())
        {
            Logger.LogInfo(s);
        }
        Logger.LogInfo("END OF LIST");
        //AudioNames.Append(":null:");
        CFG_NoAmmoSwitchEnabled = Config.Bind("Misc", "Play NoAmmo On Weapon Switch", false, "If Enabled, The no Ammo tone will also play when switching to a weapon with no ammo. If disabled, sound will play once when your current weapon runs out of ammo");
        if (AudioNames.Length > 0)
        {
            CFG_NoAmmoSound = Config.Bind("Misc", "NoAmmo", AudioNames[0], new ConfigDescription("FUCK YOU?", new AcceptableValueList<string>(AudioNames)));
            //Logger.LogFatal(CFG_NoAmmoSound.Description.Description);
        }
        else
        {
            CFG_NoAmmoSound = Config.Bind("Misc", "NoAmmo", "", "What Audio Tones do you want to use? (None found Yet)");
        }

        CFG_ScalePitchEnd = Config.Bind("Misc", "(Distance) PitchScale", 0.5f, new ConfigDescription("How many octives higher (or lower if negetive) do you want the shoot tone to be at minimun range on any sets that have Pitch Scale With Distance Enabled. Note: a value of zero will yield no change in pitch", new AcceptableValueRange<float>(-3f, 3f)));

        Logger.LogInfo("Entering 2nd Half of AudioSets Config");
        for (int i = 0; i < AudioClipNumber; i++)
        {
            SetupAlarmCFG(ref CFG_NEZ_Sound[i], "target in NEZ", "1) NezSound", "Set " + i, AudioNames);
            SetupAlarmCFG(ref CFG_SHOOT_Sound[i], "it is viable to shoot", "2) ShootSound", "Set " + i, AudioNames);
            SetupAlarmCFG(ref CFG_LOCKING_Sound[i], "it is not advisable to shoot", "3) LockingSound", "Set " + i, AudioNames);
            CFG_UsingPitchDistanceScaling[i] = Config.Bind("Set " + i + " Sounds", "4) Enable Distance Pitch Scaling", false, "If Enabled, the NEZ sound will be replaced by the Shoot Sound of variable pitch. The pitch depends on the relevant config in the Misc Section");
            CFG_PlayOnceOnStatusChanged[i] = Config.Bind("Set " + i + " Sounds", "5) PlayAudioOnceWhenLockStatusChanged", false, "If Enabled, Selected audio will play once when the status of the lock has changed (e.g. from Shoot to NEZ). If disabled then audio will play continuously. Please note that this is not compatible with pitch scaling for obvious reasons");
        }
    }
    private void ChangeAcceptableValuesStringLIST(string[] NewAcceptableValues)
    {
        //Remember to update this set.
        
    }
    private void SetAcceptableValues(ConfigEntry<string> CFG, string[] AcceptableValues)
    {
        //Assume the defualt is still item zero.
        if(AcceptableValues.Length <= 0)
        {
            CFG.Value = AudioHandler.NoAudio;
        }
        else
        {
            if (!AcceptableValues.Contains(CFG.Value))
            {
                CFG.Value = AcceptableValues[0];
            }
            //CFG.Description = new ConfigDescription(CFG.Description.Description, new AcceptableValueList<string>(AcceptableValues));
        }
    }
    #endregion



    //private void WriteConfigsToExternalFile(string Path)
    //{
    //    string text = "#START#\n";
    //    //General
    //    text += "#GENERAL#\n";

    //    AppendStringWithRelevantConfig(CFG_PlayWhenNoTargetSelect, ref text);
    //    AppendStringWithRelevantConfig(CFG_PlayWhenNoAmmo, ref text);
    //    AppendStringWithRelevantConfig(CFG_PlayWhenGearDown, ref text);
    //    AppendStringWithRelevantConfig(CFG_prioritizeNEZSound, ref text);

    //    //Weapons
    //    text += "#WEAPONS#\n";

    //    //RADAR
    //    AppendStringWithRelevantConfig(CFG_SARH_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableSARH, ref text);

    //    //IR
    //    AppendStringWithRelevantConfig(CFG_IR_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableIR, ref text);

    //    //Optical
    //    AppendStringWithRelevantConfig(CFG_Optical_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableOptical, ref text);

    //    //Laser
    //    AppendStringWithRelevantConfig(CFG_Laser_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableLaser, ref text);

    //    //Bomb
    //    AppendStringWithRelevantConfig(CFG_Bomb_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableBomb, ref text);

    //    //Gun
    //    AppendStringWithRelevantConfig(CFG_Gun_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableGun, ref text);

    //    //Jammer
    //    AppendStringWithRelevantConfig(CFG_Jammer_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableJammer, ref text);

    //    //MedusaLaser
    //    AppendStringWithRelevantConfig(CFG_MedusaLaser_set, ref text);
    //    AppendStringWithRelevantConfig(CFG_EnableMedusaLaser, ref text);

    //    //Misc
    //    text += "#MISC#\n";
    //    AppendStringWithRelevantConfig(CFG_NoAmmoSound, ref text);
    //    AppendStringWithRelevantConfig(CFG_NoAmmoSwitchEnabled, ref text);
    //    AppendStringWithRelevantConfig(CFG_ScalePitchEnd, ref text);

    //    //The Sets
    //    text += "#SETS#\n";
    //    for (int i = 0; i < AudioClipNumber; i++)
    //    {
    //        text += "#SET " + i + "#\n";
    //        AppendStringWithRelevantConfig(CFG_NEZ_Sound[i], ref text);
    //        AppendStringWithRelevantConfig(CFG_SHOOT_Sound[i], ref text);
    //        AppendStringWithRelevantConfig(CFG_LOCKING_Sound[i], ref text);
    //    }
    //    text += "#END#";

    //    File.WriteAllText(Path, text);
    //}
}

