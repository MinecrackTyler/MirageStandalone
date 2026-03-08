# Mirage Standalone Weaver

Mirage Standalone package is a .NET Core version of [Mirage](https://github.com/MirageNet/Mirage). Mirage Standalone allows the core parts of Mirage to be used outside of unity.

Mirage is a rolling-release high-level API for the Unity Game Engine that provides a powerful, yet easy to use networking API. It is designed to work with Unity 3D and is available on GitHub.

This is just the Weaver part of mirage standalone, useful for creating BepInEx mods containing things like custom components that may require RPC's or SyncVar's
## Usage
Download/compile executable for your platform of choice, then run:
`./Mirage.CodeGen(.exe) <path-to-dll> <path-to-game-managed-directory>`

### Example
`./Mirage.CodeGen mod.dll "path/to/SteamLibrary/steamapps/common/Nuclear Option/NuclearOption_Data/Managed/"`
