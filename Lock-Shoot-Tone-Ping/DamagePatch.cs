using HarmonyLib;
using NuclearOption.Networking;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lock_Shoot_Tone_Ping
{
    [HarmonyPatch(typeof(Pilot), nameof(Pilot.ApplyDamage))]
    internal static class DamagePatch
    {
        static void Prefix(Pilot __instance, out bool __state)//code is similar to that seen in death blackout.
        {
            __state = __instance.dead;
        }
        static void Postfix(Pilot __instance, bool __state)
        {
            if(__instance == null
                || __state || !__instance.dead 
                || !GameManager.IsLocalPlayer<Player>(__instance.aircraft.Player)
                )
            {
                return;
            }
            else
            {
                Plugin.I.StopAudioAndRatchetUpJustification();
            }
        }//this hopefully means that when you die...you die...even if the update loop gets interrupted.
    }
}
