# PlateUp Layout Editor

A planned bidirectional layout editor for PlateUp! (Windows/Steam).

## Intended workflow
1. Export an entire restaurant or a selected region from the game.
2. Edit the exported layout in a browser.
3. Preview the changes in-game.
4. Apply them, including optional sandbox appliance spawning.

## Status
**Scaffold only. No functional PlateUp integration yet.** The game ECS APIs, appliance IDs, state serialization, and multiplayer behavior must be verified against the installed game version before enabling changes to a live restaurant.

## Safety principles
- Keep walls, doors, and floors immutable in the first version.
- Apply only during preparation, with a preview and explicit confirmation.
- Snapshot before mutation; reject unsupported or occupied appliances until safe handling is implemented.
- The host controls multiplayer mutations; synchronization requires testing.
- Preserve smart-grabber filters and appliance orientations when supported.

## Project structure
- `schema/`: portable layout document definition.
- `mod/`: C# mod integration (to be implemented and tested).
- `editor/`: browser-based editor (to be implemented).
- `docs/`: integration plan and verification checklist.
