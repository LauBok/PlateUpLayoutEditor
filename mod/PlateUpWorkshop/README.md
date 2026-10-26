# Workshop mod

Entry point `Main` (`KitchenMods.IModInitializer`, loaded by the game's own mod loader; KitchenLib is
not required). Systems are discovered through `IModSystem`.

| File | Role |
|---|---|
| `LayoutEditorSystem.cs` | Host-side executor: snapshot, export, apply (via `ApplyPlanner`), delete, rotate, move, undo |
| `ApplianceStateCodec.cs` | Saves/restores `CConveyPushRotatable.Target` and `CConveyPushItems` smart filter |
| `LayoutEditorView.cs` | Networked screen view: client requests → host, host results → clients (Harmony prefab patch) |
| `EditorChannel.cs` | Host executes locally, clients go through the view |
| `EditorWindow.cs`, `TileOverlay.cs` | IMGUI window (F7), mouse tools and floor outlines |
| `ApplianceCatalog.cs` | All appliances from `GameData`, name → ID resolution |

How a change is made (host): appliances are created with `CCreateAppliance { ID }` + `CPosition`;
once the game turned the request into an appliance, the tile occupant is registered and saved state
is applied. Moves set `CPosition`, update occupancy and recreate the view (`CRemoveView`), the same
way UrFriendKen's Drag'n'Drop Designer drops appliances. Removal destroys the entity (and anything it
holds) and clears its occupancy.

## Build (Windows)

Requirements: PlateUp! installed through Steam, .NET SDK 6+ (targets .NET Framework 4.7.2 via
reference packages), the Harmony Workshop mod subscribed (it is referenced from the Workshop folder).

```powershell
dotnet build mod/PlateUpWorkshop -c Release
# if the game is not found automatically:
dotnet build mod/PlateUpWorkshop -c Release -p:GamePath="D:\SteamLibrary\steamapps\common\PlateUp\PlateUp"
```

Output: `mod/PlateUpWorkshop/content/PlateUpLayoutEditor-Workshop.dll`. For a local test copy the
`mod/PlateUpWorkshop` folder (with `content/`) to `<PlateUp>\Mods\PlateUpLayoutEditor\`, or upload
that folder with the game's Workshop uploader.

## First in-game test (disposable solo save)

1. Main log (`%USERPROFILE%\AppData\LocalLow\It's Happening\PlateUp\Player.log`) shows
   `[Layout Editor] v0.2.0 loaded`.
2. Start a restaurant, preparation phase, F7. Place a Countertop and a Smart Grabber; rotate; delete.
3. Select a 3x3 area with appliances, Move by dx=1; check items on counters moved along.
4. Files → Export restaurant; Undo; Files → apply the export as whole restaurant.
5. Set a smart grabber filter by letting it grab an item, export, delete it, re-apply: the filter returns.
6. Start the day: appliances must work (customers path, grabbers move items). Save, quit, reload.
7. Multiplayer: second player opens F7 and places something; both screens show it.

Report the `[Layout Editor]` lines and any exceptions from Player.log if something fails.
