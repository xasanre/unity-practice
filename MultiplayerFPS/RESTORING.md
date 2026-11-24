# Restore the Unity FPS project

This public copy contains the game code, authored assets, scenes and settings. It omits the PUN SDK, course models and sounds, font files, private project configuration and Windows build. Obtain the course pack through your own course access. Use **Unity 6000.3.26f1** and keep the original Unity `.meta` files for all imported model and SDK assets: the project references them by GUID.

## 1. Prepare dependencies before opening MultiplayerFPS

The game scripts reference PUN types at compile time. To avoid opening the checkout in Safe Mode with missing scripts, use a fresh **3D (Built-In Render Pipeline)** project made with the same editor version:

1. In that temporary project, select **Window > TextMeshPro > Import TMP Essential Resources**. Import Essentials, not Examples & Extras. Unity 6 supplies the TMP code via the Unity UI package; its font and settings assets are a separate import.
2. Import your own **PUN 2 Free, version 2.52**, from the Unity Asset Store or your authorized course copy. If the Photon setup wizard appears, you can configure it later in the FPS project.
3. Close the temporary editor. Copy its entire `Assets/Photon` and `Assets/TextMesh Pro` directories into this project's `Assets` directory, **including** their contents, all enclosed `.meta` files, and the adjacent folder meta files (`Photon.meta` and `TextMesh Pro.meta`).
4. The public project uses the **LiberationSans SDF** font asset from TMP Essentials. It does not need the original Windows Times New Roman font.

If you already opened the checkout and got compile errors, close it without writing changes to any scene or prefab. Restore these dependency folders while Unity is closed, then reopen it.

## 2. Restore the course models and sounds

In the authorized Lab 6 FPS game pack, find the directory that directly contains `Prefabs_withmesh` and `Additional_Sound(2)`. Copy those two directories into `Assets/FPSGamePack/` with their supplied meta files. Do not add another outer `[assets]FPSGamePack` directory. The layout should be:

```text
Assets/FPSGamePack/
  Prefabs_withmesh/Prefabs_withmesh/
    OriginalModels/
    Prefabs/                 (Launcher, Hesco, Pallette Box, Pallette)
  Additional_Sound(2)/Audio/
    FootStep/Footstep.wav
    GunShots/ak.mp3
    GunShots/gunshot.ogg
    GunShots/long_gun_shoot.wav
    GunShots/machine-gun.ogg
    Reload&Handing/ak47Reload.wav
```

The spelling **Pallette** in the original filenames is intentional. Copy the whole `Prefabs_withmesh` tree so prefab references to FBX models resolve. If you received the Lab 8 sound pack separately, copy its `Audio` folder to the same `Additional_Sound(2)/Audio` path shown above.

The course audio files do not come with the Unity metadata this project used. With Unity closed, run the commands below from the `MultiplayerFPS` directory (Python 3 on Windows):

```text
py -3 restore_audio.py --apply
py -3 restore_audio.py
```

The first command checks the SHA-256 of all six required clips before writing anything. It creates missing AudioImporter metadata or updates just the GUID in existing metadata, preserving other importer settings. Existing metadata that changes is backed up under `Library/CourseAudioMetaBackups/`. The second command is read-only and should report that all six match. Use a fresh checkout: changing a GUID in a project with unrelated existing work can break other references made to the old GUID.

## 3. Configure multiplayer and test

1. Add **MultiplayerFPS**, not the repository root, to Unity Hub. Open it with the editor version above and let Package Manager resolve the checked-in manifest, including AI Navigation 2.0.16.
2. For **online** play, open **Window > Photon Unity Networking > PUN Wizard** and enter your own Photon **PUN/Realtime App ID**, not a Fusion, Voice or Chat ID. Alternatively set **App Id Realtime** in `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`. Use the same App ID and game version for all clients. This game selects the `asia` region. Offline modes need the SDK but do not need a cloud App ID.
3. Open **Assets/Scenes/game.unity**, then run **Assignment3 > Run self-checks**. This checks the existing build scenes, prefabs, course references, weapons and baked navigation; there is no need to regenerate the scenes. Press **Play** and select **Offline practice > Ground Arena** to explore, or **Offline AI match** to play against bots.
4. For a Windows build, install Windows Build Support for this editor version and include the three scenes in the order already stored in Build Profiles: `game`, `SkyArena`, `GroundArena`. Build to a local folder. Both source and built clients use the lobby in `game` as their entry point.

Keep locally restored third-party files, App IDs and builds out of commits.
