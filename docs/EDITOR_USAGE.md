# Browser editor usage

Open `editor/index.html` in a modern desktop browser. No build step is required.

1. Choose an appliance in the Tool menu and click a free tile.
2. Switch to Select / move to select an appliance. Shift-click adds/removes selection.
3. Drag a selected appliance to move the selection as a group.
4. Use Rotate, Copy, Delete, or Undo. After Copy, Alt-click a destination to paste the selected group.
5. Select one appliance to edit its state JSON, then press Save state.
6. Export JSON to save a layout. Import JSON to load a saved layout.

## Limitations
- This is a standalone browser prototype. It cannot export from or apply to PlateUp! yet.
- Appliance names are illustrative. Game-specific IDs, spawn operations and state fields are not yet validated.
- The grid is fixed at 12x9 for the prototype.
- Existing appliances are replaced on JSON import, so export your current work before importing.
- JSON layout schema v1 supports relative coordinates; future integration must map them to the actual restaurant floor.
- Do not treat a browser-edited layout as a safe game mutation until the mod has a preview, validation, and rollback.
