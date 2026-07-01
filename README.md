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
      ¬Packs
        ¬[External Pack Folders Here]
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

## EXTERNAL PACK INSTALLATION (AND EDITING) GUIDE

All packs must contain:
- Audio Files
- A config file (named Configs.txt)

In order to install external packs, simply extract said pack into LockShootTonePing/Packs.

An example of the intended file structure once and external pack is installed is as follows:

```
  Plugins
    ¬LockShootTonePing
      ¬LockShootTonePing.Dll
      ¬Audio
      ¬Packs
        ¬ExamplePack
          ¬ExampleAudio.wav
          ¬Configs.txt
```

After this, you then want to open the game and change the 'Selected Pack' value from :DEFAULT: to the name of your installed pack. 

Please note that doing so will will then replace all of the settings that you have loaded with that of the installed external pack. That said, your settings at the time that you loaded the external pack will be saved in Audio/Configs.txt
These settings can be loaded by selecting the :DEFAULT: pack in the dropdown menu.

Please note, that once you have installed an external pack, you are free to edit its configs as if it were your default pack in exactly the same way that you would change the settings of the mod normally. Any changes to the configs are saved either when you switch packs (to a different pack) or when you close the game.

Please note that if you intend on sharing an external pack you have edited, if you have changed the audio, please ensure that the name of the in brackets next to the audio in your settings are the name of the external pack. 

Example (GOOD):
```
[ExamplePack] ExampleAudio
```
If you decide to use audio from a different pack or from your default audio folder, although it may work on your client, it may fail to work on other peoples as the audio itself in will not be saved in the external pack's folder

Example (BAD):
```
If I were editing "ExamplePack":
[AeriiCats Audio] Locking

In this case, if I were to export ExamplePack, another user would not be able to load it correctly as the audio would be saved in "AeriiCats Audio" folder instead of in the "ExamplePack" folder
```

## EXTERNAL PACK CREATION GUIDE

### CONFIG FORMATS:

There are 3 accepted Confg formats: Raw, Streamlined, Simplfied.

Here are an example of all of the config formats.

RAW:
```
[ConfigCategory].ConfigField=VALUE
```
Raw is lossless and all data can be retrieved from saving a config as Raw

Streamlined:
```
[ConfigCategory]
ConfigField=VALUE
```
Streamlined is lossless and all data can be retrived from saving a config as Streamlined

Simplified:
```
[WEAPON]
-SubCategory
ConfigField=VALUE
```
Simplified is lossy. When a config is saved as simplified what set corresponds to what weapon is lost. Instead sounds are tied directly to weapons.
When you open a Simplified config file, Each weapon is tied to a specific set which is tied to a group of sounds where one weapon has one set.
As a result you will lose any organising you had between sets and weapons that you had before (i.e. set 0 will always be Radar Missiles, set 1 will always be IR and so on and so on)

This change cannot be reversed once done. 

The advantage of Simplified config format comes if you decide to do pack creation as it is a lot more human readable and easier to understand. As a result, creating a pack config in a Simplfiied format will generally be easier.



### PACK CREATION:

There are two methods of external pack creation:


1) You export an existing pack (your default settings works too)

Using this method, you can simply copy, paste and rename your default audio folder to the name of your pack and then compress it and share it. All configs that you had when your exported the audio folder will be carried over and re-created for anyone else installing that pack.

Alternately, you can also edit existing packs and then compress and share those. Please do however take note of the naming convention of audio files in the configuration manager. These are stated in the section above.
Another thing to note is that audio in the audio folder will not have a pack associated directly with it and so will not have the name of its pack of origin next to it in the bepinex configuration manager.

Example:
```
Any External Pack:
[ExamplePack] ExampleAudio

Any Audio in the Audio Folder:
ExampleAudio
```
Please note that if your pack uses audio from other packs, unless you also export those and force the end user to also install those packs as well, the sets which use said audio will be unable to be recreated and in its absence the sound that will be played will either be the sound which occupied that slot previously or :NONE: depending on the user's external pack settings.

Another thing to note:
The mod is unable to tell this difference between two files which have the same name but different file extensions. As the mod will consider both files to have the same name and will always play the sound which has the leading alphabetical letter in its file extension.

I.e. mp3 > ogg > wav

so an mp3 will always be played if you give the program an audio file with the name but on is an mp3 and the other is an ogg. and so on and so fourth for other audio files with the same name but different file extensions.


2) Do it manually

If you have a lot of external audio packs installed, you might find it easier to create the audio pack and its configs manually.

I have left templates for each of the acceptable config formats detailed above. Alternately, you can edit existing configs or if youre a masochist, you can write it all by hand.
When defining audio files in configs, refer to the file by name and do not write its file extension. Reason for this is stated above. Please note that the mod is case sensitive. Do not leave spaces unless there are spaces in the file name. 

If you are writing in for Simplified or Streamline, please do note that the order the lines are in matter too:

i.e.: The category a field is in will extend to the next time a category is defined (using []).
the same is true with subcategories although they are defined with a - at the start of the line.

Please note that the mod does not support comments in the config file. At best, the mod will ignore them. At worst, the mod will crash.

Again, I have example configs for you to download so you can edit those if you plan on making your external pack manually.


## Final Notes

Please note that this README is up to date with version 1.2.0
If I have forgotten to update the README for the current version, tell me. 

Update 1.2.0 has added the ability to install external audio packs. As a result the program is a lot more volatile. I never designed the mod for this so I definitely feel the sins of techical debt crawling on my back.

1.2.0 is over due some refactoring. Which I hope I will get onto at a later point in time.

As of 1.2.0, updating the mod is still manual (you still need to replace the old DLL with the new one)

I highly recomend you backup your bepinex config file (com.Aeriicatmeow.LockToneShootPing.cfg) as, although the update didnt wipe it for me, it might for you and so it is worthwhile. After this update, your bepinex config file will be written to Configs.txt in the audio folder.

Lastly, If you run into any problems, Please do not hesistate to contact me on discord. I am both on the official discord server as well as Primerva 2082. 

If you have a problem feel free to either raise it there or DM me. 

If you would like to share your external packs or would like help on any of it, also feel free to comment on the relevant form for the mod (or to contact me).

I might take a while to get back to you but ill try and get back to you are some point.

Thats All,

AeriiCat 1/7/26.

