# PlateUp Workshop mod — loader milestone

This is a **real KitchenLib BaseMod entry point** adapted from [Aragami's PlateUp Workshop template](https://github.com/Aragami-delp/PlateUpModTemplate). It logs loader activation and does not change the restaurant.

## Build prerequisites
- Windows and a .NET SDK capable of targeting .NET Framework 4.7.2.
- A PlateUp! installation with official Workshop support.
- KitchenLib installed as a Workshop dependency.
- Game and mod assemblies resolved by the mod build utility. The provided csproj is an initial adaptation and may require references/paths to be adjusted against your local installation.

Run from the repository root:

```powershell
dotnet build mod/PlateUpWorkshop/PlateUpLayoutEditor.csproj -c Release -p:PlateUpGameFolder="C:\Program Files (x86)\Steam\steamapps\common\PlateUp\PlateUp"
```

The DLL should be copied to `mod/PlateUpWorkshop/content/` after a successful build. This has **not** been built or installed yet. The exact loader packaging and dependencies must be checked against the installed game version.

## Next step
Confirm that the mod appears in the game's mod menu and the activation log is written. Then add a read-only ECS appliance query and a controlled spawn command.
