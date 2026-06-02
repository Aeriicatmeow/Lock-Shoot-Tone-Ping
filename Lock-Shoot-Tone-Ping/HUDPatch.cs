using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;
namespace Lock_Shoot_Tone_Ping
{
    [HarmonyPatch(typeof(HUDMissileState), nameof(HUDMissileState.UpdateWeaponDisplay))]
    internal static class MissileHUDPatch
    {
        static void Postfix(WeaponStation ___weaponStation, bool ___allRequirementsMet, float ___noEscapeRange, float ___maxTargetDist, float ___maxRange, float ___minRange,Aircraft aircraft, List<Unit> targetList)
        {
            //Plugin.I.Log(BepInEx.Logging.LogLevel.Message, "Fired Through Missile HUD");
            
            //Plugin.I.Log(BepInEx.Logging.LogLevel.Message, "MISSILE TYPE " + ___weaponStation.WeaponInfo.shortName);

            Plugin.I.ResolveLockAudio(___allRequirementsMet, ___noEscapeRange, ___maxTargetDist, ___maxRange, ___minRange, targetList.Count > 0, ___weaponStation.Ammo >0, ___weaponStation);

        }
    }

    [HarmonyPatch(typeof(HUDLaserGuidedState), nameof(HUDLaserGuidedState.UpdateWeaponDisplay))]
    internal static class LaserHUDPatch
    {
        static void Postfix(WeaponStation ___weaponStation, bool ___allRequirementsMet, float ___targetDist, float ___maxRange, float ___minRange, Aircraft aircraft, List<Unit> targetList)
        {
            //Plugin.I.Log(BepInEx.Logging.LogLevel.Message, "Fired Through Laser HUD");
            Plugin.I.ResolveLockAudio(___allRequirementsMet, ___targetDist, ___maxRange, ___minRange,targetList.Count > 0, ___weaponStation.Ammo > 0, ___weaponStation);
        }
    }
    [HarmonyPatch(typeof(HUDBombingState), nameof(HUDBombingState.UpdateWeaponDisplay))]
    internal static class BombHUDPatch
    {
        static void Postfix(WeaponStation ___weaponStation, float ___dropTime, Aircraft aircraft, List<Unit> targetList)
        {
            //Plugin.I.Log(BepInEx.Logging.LogLevel.Message, "Fired Through Bomb HUD");
            Plugin.I.ResolveLockAudio(Mathf.Abs(___dropTime) < 2f, 0, 0, 0, targetList.Count > 0, ___weaponStation.Ammo > 0, ___weaponStation, true);
        }
    }
    [HarmonyPatch(typeof(HUDBoresightState), nameof(HUDBoresightState.UpdateWeaponDisplay))]
    internal static class GunHUDPatch
    {

        static void Postfix(WeaponStation ___weaponStation, UnityEngine.UI.Image ___boresight, Aircraft aircraft, List<Unit> targetList)
        {
            //Plugin.I.Log(BepInEx.Logging.LogLevel.Message, "Fired Through Gun HUD");
            Plugin.I.ResolveLockAudio(___boresight.color == UnityEngine.Color.green, 0, 0, 0, targetList.Count > 0, ___weaponStation.Ammo > 0, ___weaponStation, true);
        }
    }

}
