# PlateUp game adapter — pending game API verification

This folder will contain the actual game-specific mod entry point and ECS query.

The read-only contract is `IReadOnlyRestaurantSource` in `mod/LayoutCore/ExportPipeline.cs`. It requires:
- stable IDs that survive export/import, with explicit mapping to real entity IDs;
- actual appliance identifiers, not the illustrative browser palette names;
- grid coordinates and quarter-turn orientation;
- safe, appliance-specific state serialization (smart grabber filters in particular);
- explicit behavior for multiple entities in a tile, which the v1 schema cannot represent.

## Before building the real mod

1. Obtain the installed PlateUp version and the relevant Workshop modding SDK/reference assemblies.
2. Inspect and confirm actual ECS component names and read-only query patterns.
3. Implement an adapter without mutation APIs.
4. Test with a throwaway restaurant and compare the JSON to the on-screen layout.
5. Test host-only installation in a four-player lobby before adding Apply.

No entry point or speculative Harmony/ECS patches are included here yet.
