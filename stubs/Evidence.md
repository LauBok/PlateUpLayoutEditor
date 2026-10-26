# Where the stub signatures come from

The stubs only let CI compile the mod without the game. These are the open-source mods that compile
against the real PlateUp! assemblies and use each API the same way. "UFK" = github.com/UrFriendKen.

| API used by the mod | Evidence |
|---|---|
| `IModInitializer` (`PostActivate`, `PreInject`, `PostInject`), `IModSystem`, `IModComponent` | UFK/PlateUpIRegretIt `Main.cs`; UFK/PlateUpDecorOnDemand `Main.cs`, `SpawnRequestView.cs` |
| `GameSystemBase`, `GenericSystemBase.Initialise/OnUpdate`, `Require`, `Has`, `Set`, `TryGetSingletonEntity` | UFK/PlateUpAutomationPlus `PreferenceActionController.cs`; UFK/PlateUpMoveLayoutFeatures `MoveFrontDoor.cs`; DecorOnDemand `SpawnRequestView.cs` |
| `GetOccupant`, `SetOccupant(pos, entity, layer)`, `GetPrimaryOccupant`, `GetTile`, `GetRoom`, `CanReach`, `GetFrontDoor(bool)`, `Bounds` | UFK/PlateUpDragNDropDesigner `MousePickUpAndDropAppliance.cs`; AutomationPlus `PreferenceActionController.cs`; UFK/PlateUpGetOverHere `DropMobileAppliance.cs` |
| `QueryHelper().All().None()` | AutomationPlus `PreferenceActionController.cs` |
| `CCreateAppliance { ID }` + `CPosition` creates an appliance | KitchenMods/KitchenLib `Systems/BuildLocalDishOptions.cs`; UFK/PlateUpApplianceChest `RetrieveAppliance.cs` |
| `CPosition.Position/Rotation`, `new CPosition(Vector3)`, `quaternion` rotation | DragNDropDesigner `MousePickUpAndDropAppliance.cs`, `MouseRotateChairs.cs`; GetOverHere `DropMobileAppliance.cs` |
| Moving an appliance: set `CPosition`, `CRemoveView` + `CRequiresView{Type=Appliance}`, update occupancy | DragNDropDesigner `MousePickUpAndDropAppliance.cs` (`PerformDrop`) |
| Removing appliances with `EntityManager.DestroyEntity` | AutomationPlus `PreferenceActionController.cs` |
| `CAppliance { ID, Layer }`, `CImmovable`, `CHeldBy`, `CHeldAppliance`, `CItemHolder.HeldItem`, `CApplianceBlueprint` | DragNDropDesigner, ApplianceChest `StoreAppliance.cs` |
| `CLetter` | WillWilli/PlateUpEnhancementMod `SpawnItemsHandler.cs` |
| `CApplianceGhostChair`, `CApplianceChair` | DragNDropDesigner `MouseRotateChairs.cs`; KitchenLib `KitchenPropertiesUtils.cs` |
| `CDestroyApplianceAtDay` | AutomationPlus `PreferenceActionController.cs` |
| `CConveyPushItems` fields (`GrabSpecificType`, `SpecificType`, `SpecificComponents`, ...) | KitchenLib `Utils/KitchenPropertiesUtils.cs` (`GetCConveyPushItems`); UFK/PlateUpSmartGrabberPullBugFix |
| `CConveyPushRotatable.Target` (`Orientation`) | AutomationPlus `AutoRotatePush.cs`, `Customs/ConveyorFast.cs` |
| `ItemList` (`Count`, indexer, `Add`) | AutomationPlus `ReplaceRefilledBroth.cs`, `InhibitConveySpecificTypeChange.cs` |
| `GameData.Main.Get<Appliance>()`, `Appliance.ID/Name/Layer/Properties/IsPurchasable` | DecorOnDemand `Main.cs`; KitchenLib `CustomAppliance.cs` |
| `GameInfo.CurrentScene`, `GameInfo.IsPreparationTime`, `SceneType.Kitchen` | UFK/PlateUpRestoreFranchise `Patches/MainMenu_Patch.cs` |
| `Session.CurrentGameNetworkMode`, `GameNetworkMode.Client/Host` | MoveLayoutFeatures `Main.cs` |
| `ResponsiveObjectView`, `ResponsiveViewSystemBase`, `SendUpdate`, `ApplyUpdates`, `IViewData.ICheckForChanges`, `IResponseData`, `CLinkedView`, `CRequiresView{ViewMode.Screen}`, `CPersistThroughSceneChanges`, `CDoNotPersist`, `InputSourceIdentifier.Identifier` | DecorOnDemand `SpawnRequestView.cs`; DragNDropDesigner `Views/MouseInputView.cs` |
| Harmony prefix on `LocalViewRouter.GetPrefab(ViewType view_type)` for a custom view type | DecorOnDemand `Patches/LocalViewRouter_Patch.cs` |
| Thumbnail camera: "Snapshot Camera" layer, orthographic camera, `_NightFade` shader global (plain Unity API otherwise) | DecorOnDemand `Utils/SnapshotUtils.cs`, `Utils/PrefabSnapshotUtils.cs` |
| Harmony comes from the "Harmony" Workshop mod | OndrejNepozitek/KitchenDesigner `MissingDependenciesGUIManager.cs` |

Exact member types that are not visible from usage (for example whether `EntityQuery` is a class
or struct, or the numeric values of enums) are guesses that do not affect compiled call sites in a
release build, because the release build references the real game assemblies instead.
