# LayoutCore

Pure C# layout data models and change planner, with no Unity or PlateUp references. This is an **integration foundation**, not an installable mod.

The planner supports Replace, Merge, and Paste operations and detects duplicate IDs and destination collisions. An actual game adapter must supply the floor geometry, game-specific appliance identifiers, serialized state, entity lifecycle, multiplayer replication, and transactional rollback.

Important: operation ordering in the returned plan is descriptive; it is not safe to apply these operations directly to game entities without a staged, validated transaction.

Build prerequisites: .NET SDK with netstandard2.1 reference assemblies. This code has not been compiled against the PlateUp runtime.
