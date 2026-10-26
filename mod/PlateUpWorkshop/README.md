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

## Experimental ECS spawn probe (source added)

The `BlueprintSpawnProbe` uses the game's `CCreateAppliance`, `CPosition`, `CLetter`, `CLetterBlueprint`, and `CShopEntity` components. The pattern was cross-checked against [PlateUpEnhancementMod's SpawnItemsHandler](https://github.com/WillWilli/PlateUpEnhancementMod/blob/master/Willi.PlateUpEnhancementMod/EventHandlers/SpawnItemsHandler.cs).

On first initialization, it creates a `spawn-probe.txt` beside the compiled DLL. The default `applianceId=0` disables the probe. To opt in, set a **verified** numeric appliance ID, restart the game, and press **F8** in a disposable **solo** restaurant. `worldX` and `worldZ` control the request location. A successful request should produce a **free blueprint**, not a placed appliance. It is not an Apply implementation.

**Not yet compiled or tested. Do not use in multiplayer.** There is no host-authority check, phase guard, bounds validation, or rollback in the probe. Invalid appliance IDs or positions may produce unexpected results. Never enable it on a valued save.

This is a limited, deliberate first game mutation experiment. After verifying loading and blueprint creation, implement authoritative ECS placement with a verified entity lifecycle rather than guessing at game internals.
