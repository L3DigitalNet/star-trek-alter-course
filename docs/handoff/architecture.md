# Architecture

## Component map

- `AlterCourse.Core` owns pure simulation and domain behavior. It targets ordinary .NET, has no Godot reference, and remains testable without engine startup.
- `AlterCourse.Godot` owns nodes, scenes, resources, input, UI, and engine adapters. It may reference `AlterCourse.Core`; the reverse dependency is prohibited.
- `AlterCourse.Core.Tests` exercises the pure assembly with xUnit and verifies that its compiled assembly references no `Godot*` assembly.
- GdUnit4 tests under `src/AlterCourse.Godot/tests/` exercise the managed node and scene boundary through the actual Godot runtime.
- `GameSimulation` is the sole owner of active Core state. It advances explicit simulation time in deterministic 100 ms tactical quanta.
- Core owns scheduler order, runtime IDs, open strategic and tactical state, travel, sensor repair, content, and V1 save semantics.
- Godot projects read-only Core views, submits typed player intents, and converts Core Y-up tactical coordinates to presentation coordinates.

## Standing backlog

- Add simulation behavior to `AlterCourse.Core` and its test project as gameplay systems are introduced; preserve the boundary defined by ADR 0001.
