# Lock Shoot Tone Ping

This is like the original Lock Tone mod. I never got the original one to work so I made it myself.

It has support for all weapons with exception of turrets. 

Please note that guns do not have a pitch scale with distance option because I could not find a decent way to implement it. 

Bombs dont have pitch scaling with distance because the time window to fire the bombs is only a few seconds and so i do not think it would be very helpful.

## INSTALLATION GUIDE

Copy Dll into Bepinex plugins folder.

Run the game, this will generate the required file structure for the mod. The file should also be automatically moved into its own folder under the same name:
```
  Plugins
    ¬LockShootTonePing
      ¬LockShootTonePing.Dll
      ¬Audio
```
You then want to transfer all of your audio files into the audio folder.

Please do note that audio files do not need to adhere to specific names to function

After this, open up the game and open the bepinex Config menu (F1 by default)
you will see a list of sets (up to 10) and a list of weapon types.

Sets are a group of sound that can be assigned to weapon types.

In a set, select a Locking, Shoot, and NEZ sound and then assign that set to your weapon type of choice. 

Please note that leaving a field in an audio set unassigned will result in no audio being played for that field.
(e.g. if i assign Set 0, Locking, null, and then assign IR missiles Set 0, no audio will be played for the missile trying to lock onto an enemy)


I think this mod is fairly resilient and bug proof. If you run into any issues, please dont hesitate to contact me although please do provide as much detail as possible as to how I can recreate the bug in question.


Example of a setup for a weapon.
```
  "Set 0"
    ¬NEZ: Aim9x shoot High Pitched Sound File Name
    ¬SHOOT: Aim9x shoot normal Sound File Name
    ¬LOCKING: Aim9x Growl Sound File Name
    ¬Scale Pitch With Distance = true
  
  "IR Missile"
    ¬Set 0
    ¬Enabled = true
```
Please note that the above is just an example, please feel free to mess around with the settings as you wish.

- The Weapon Classes covered specifically are:
- IR Missiles.
- ARHs/SARHs/ARADs
- Optical Missiles/Glide Bombs
- Laser Guided
- Gravity Bombs
- Guns
- Jamming Pods
- The Medusa Deadly Laser


IMPORTANT: Mod Requires Bepinex Configuration Manager or equivalent to assign audio files to weapons.
I recomend that you install that otherwise changing the configs manually might be difficult
