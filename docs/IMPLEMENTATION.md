# Implementation plan

## Phase 1: Game API discovery
- Verify PlateUp! version and supported mod loader.
- Inspect game ECS appliance creation, positioning, rotation, and ownership APIs.
- Determine how appliance-specific state (especially smart grabber filters) is stored.
- Test host-to-client synchronization on a disposable save.

## Phase 2: Read-only export
- Export all appliances and their supported state to schema v1.
- Export a selected rectangular region with relative coordinates.
- Capture fixed floor plan as read-only metadata.

## Phase 3: Browser editor
- Import/export schema v1 and implement grid editing, multi-select, copy/paste, rotate, and appliance palette.
- Build a verified adapter for PlateUp Tool share links; do not assume its URL format.
- Validate collisions, bounds, and unsupported appliance types.

## Phase 4: Preview and apply
- Show a diff and placement preview.
- Support Replace, Paste, and Merge modes.
- Allow optional sandbox spawning without inventory/currency requirements.
- Snapshot before applying; provide undo and error rollback.

## Phase 5: Multiplayer
- Make the host authoritative for changes.
- Verify state replication and late join behavior.
- Test real multiplayer saves before release.

No game mutation code should be enabled until the API and rollback paths are validated.
