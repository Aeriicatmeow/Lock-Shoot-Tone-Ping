using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.DedicatedServer.Commands;
using Unity.Audio;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Lock_Shoot_Tone_Ping;


[BepInPlugin("com.Aeriicatmeow.LockToneShootPing"," Lock Shoot Tone Ping", "1.0.0")]
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

        
    //Hey, note to future me. if things start breaking, switch the reference for nuclear option from the game folder to a local one.
    private void Awake()
    {

        I = this;
        // Plugin startup logic
        Logger = base.Logger;

        int[] AudioSetLimit = new int[10];
        for(int i = 0; i < AudioSetLimit.Length; i++)
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
        IsSetupCorrectly = VerifyFileStructure(Root, Info.Location);



        //So what happens is that all audio is loaded into memory. Select tones (selected by the user) are then played at relevant times. those being:
        //Target Aquired, not free to shoot / Not Advisable
        //Target Aquired, Advisable to shoot. Not Ideal (more of the thing for MMRs and so on as you want to shoot them at short range but you can, if you choose to, shoot them at long range)
        //Target Aquired, Shooting is advisable. Conditions are nominal.

        Audio = new AudioHandler(gameObject, CFG_Volume_Percent, Root+"\\Audio");


        EstablishAudioSetsCFG();
        HasAmmo = true;

        Logger.LogInfo("loading audio into memory");
        LoadAudioClipsIntoMemory(true);

        Logger.LogInfo("Loading Harmony Patches");
        Inj_Harmony = new Harmony($"com.Aeriicatmeow.{FileModName}");
        Inj_Harmony.PatchAll();

        TicksSinceJustifiedExistence = 0;

        Logger.LogInfo($"Plugin {FileModName} is loaded!");
        
    }
    private void EstablishAudioSetsCFG()
    {
        Logger.LogInfo("Establishing AudioSets Configs");

        string[] AudioNames = Audio.CreateArrayOfAudioNames();
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
    }
    private void EstablishGeneralCFG()
    {
        Logger.LogInfo("Establishing General Configs");
        CFG_Volume_Percent = Config.Bind("General", "GeneralVolume", 100, new ConfigDescription("How Loud you want to be told when to shoot", new AcceptableValueRange<int>(0, 200)));
        CFG_Enabled = Config.Bind("General", "ModEnabled", true, "Do you want to enable this mod?");
        CFG_PlayWhenNoTargetSelect = Config.Bind("General", "PlayWhenNoTargts", false, "Do you want the no lock sound to be played when you have no target selected?");
        CFG_PlayWhenNoAmmo = Config.Bind("General", "PlayWhenNoTargts", false, "Do you want Sound to be played even if you have no Ammo");
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
    private void Update()
    {
        
        LoadAudioClipsIntoMemory();
        TicksSinceJustifiedExistence++;
        //Logger.LogInfo("Ticks Since Justified: " + TicksSinceJustifiedExistence);
        if (TicksSinceJustifiedExistence > 3)
        {
            StopAudio();
        }
    }
    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        Audio.Stop();
        //LoadAudioClipsIntoMemory(true);
    }
    private void SetupAlarmCFG(ref ConfigEntry<string> M_Config, string Hint, string Key, string category,string[] AudioNames)
    {
        if (AudioNames.Length > 0)
        {
            M_Config = Config.Bind(category+" Sounds", Key, AudioNames[0], new ConfigDescription($"What Audio Tones do you want to use when using {Hint}?", new AcceptableValueList<string>(AudioNames)));
        }
        else
        {
            M_Config = Config.Bind(category+" Sounds", Key, "", "What Audio Tones do you want to use? (None found Yet)");
        }
    }
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
                if (CheckIfReloadIsNeeded(CFG_SHOOT_Sound[i] ,Aud_SHOOT[i]))
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
        if(AC == null & CFG.Value != AudioHandler.NoAudio)
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
    public void ResolveLockAudio(bool ClearToShoot, float NEZ, float TargetDistance, float MaxRange, float MinRange,bool TargetsSelected, bool SufficientAmmo, bool GearDown, WeaponStation WeaponStation)
    {
        if (CFG_Enabled.Value)
        {
            //Logger.LogInfo("Resolving through method 1");
            //Logger.LogInfo("Harmony Patch Recieved | CTS: " + ClearToShoot + " | NEZ: " + NEZ + " | TD: " + TargetDistance);
            int SetToPlayFrom = -1;
            if ((TargetsSelected || CFG_PlayWhenNoTargetSelect.Value) & (SufficientAmmo || CFG_PlayWhenNoAmmo.Value) & CheckIfWeaponTypeEnabled(WeaponStation, ref SetToPlayFrom) & (!GearDown || CFG_PlayWhenGearDown.Value))
            {
                TicksSinceJustifiedExistence = 0;
                if (ClearToShoot)
                {

                    if (ResolveIfNEZSound(NEZ, TargetDistance))
                    {
                        if (CFG_UsingPitchDistanceScaling[SetToPlayFrom].Value)
                        {

                            Audio.PlayAudio(Aud_SHOOT[SetToPlayFrom], false, ResolveAudioPitchFromDistance(TargetDistance, NEZ, MinRange));
                        }
                        else
                        {
                            //Logger.LogInfo("PLAY NEZ");
                            Audio.PlayAudio(Aud_NEZ[SetToPlayFrom]);
                        }

                    }
                    else
                    {
                        //Logger.LogInfo("PLAY SHOOT");
                        Audio.PlayAudio(Aud_SHOOT[SetToPlayFrom]);
                    }

                }
                else
                {
                    //Logger.LogInfo("PLAY LOCK");
                    Audio.PlayAudio(Aud_LOCKING[SetToPlayFrom]);
                }
            }
            else
            {
                Audio.Stop();
            }
            ResolveAudioLockPost(SufficientAmmo, WeaponStation);
        }
    }
    public void ResolveLockAudio(bool ClearToShoot, float TargetDistance, float MaxRange, float MinRange,bool TargetsSelected, bool SufficientAmmo, bool GearDown, WeaponStation WeaponStation, bool DisableDistanceScale = false)
    {
        if (CFG_Enabled.Value)
        {
            int SetToPlayFrom = -1;
            //Logger.LogInfo("Resolving through method 2");
            if ((TargetsSelected || CFG_PlayWhenNoTargetSelect.Value) & (SufficientAmmo || CFG_PlayWhenNoAmmo.Value) & CheckIfWeaponTypeEnabled(WeaponStation, ref SetToPlayFrom) & (!GearDown || CFG_PlayWhenGearDown.Value))
            {
                TicksSinceJustifiedExistence = 0;
                if (ClearToShoot)
                {
                    if (CFG_UsingPitchDistanceScaling[SetToPlayFrom].Value & !DisableDistanceScale)
                    {
                        Audio.SetPitch(ResolveAudioPitchFromDistance(TargetDistance, MaxRange, MinRange));
                    }
                    else
                    {
                        Audio.ResetPitch();
                    }
                    if (CFG_prioritizeNEZSound.Value)
                    {
                        //Logger.LogInfo("PLAY NEZ");
                        Audio.PlayAudio(Aud_NEZ[SetToPlayFrom]);
                    }
                    else
                    {
                        //Logger.LogInfo("PLAY SHOOT");
                        Audio.PlayAudio(Aud_SHOOT[SetToPlayFrom]);
                    }

                }
                else
                {
                    //Logger.LogInfo("PLAY LOCK");
                    Audio.PlayAudio(Aud_LOCKING[SetToPlayFrom]);
                }
            }
            else
            {
                Audio.Stop();
            }
            ResolveAudioLockPost(SufficientAmmo, WeaponStation);
        }
        
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

    public void Log(LogLevel LogLevel, object Data)
    {
        Logger.Log(LogLevel, Data);
    }


    private static bool VerifyFileStructure(string Root, string CurrentDLLPath)
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
            return true;
        }
        return false;
    }
    
    public void StopAudio()
    {
        Audio.Stop();
    }
    private void OnDestroy()
    {
        if (!IsSetupCorrectly)
        {
            File.Delete(Info.Location);
        }
    }
    
   
}

