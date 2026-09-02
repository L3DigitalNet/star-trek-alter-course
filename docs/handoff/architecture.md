# Architecture

## Component map

- `AlterCourse.Core` owns pure simulation and domain behavior. It targets ordinary .NET, has no Godot reference, and remains testable without engine startup.
- `AlterCourse.Godot` owns nodes, scenes, resources, input, UI, and engine adapters. It may reference `AlterCourse.Core`; the reverse dependency is prohibited.
- `AlterCourse.Core.Tests` exercises the pure assembly with xUnit and verifies that its compiled assembly references no `Godot*` assembly.
- GdUnit4 tests under `src/AlterCourse.Godot/tests/` exercise the managed node and scene boundary through the actual Godot runtime.
- `GameSimulation` owns immutable definitions and active Core state. It advances explicit simulation time in deterministic 100 ms tactical quanta.
- World state holds ships in `ShipInstanceId` order, an explicit `PlayerShipId`, and per-ship strategic, tactical, sensor, and repair state.
- Scheduled work, travel, and repairs target ships explicitly. Public player commands resolve `PlayerShipId`; arbitrary-ship control is not exposed.
- Ship iteration is stable; whole-call work is capped at 1,000,000 ship-steps.
- Finite-long numeric exhaustion fails atomically; it is an explicit limitation rather than an indefinite-successor promise.
- V2 persistence bounds world state, definition references, and scheduler data. Loading resolves references through the supplied immutable catalog.
- Definitions are not serialized. V1 migration creates one ship, targets old work to the player, and uses its design label for the missing vessel name.
- World construction and persistence admit at most 256 ships to bound untrusted input and fixed-step work; this is not a final capacity target.
- Authored strategic-map order remains observable.
- Ship collections and scheduled work are insertion-order independent and canonically ordered.
- Godot projects player-visible state without NPC omniscience.

## Standing backlog

- Add simulation behavior to `AlterCourse.Core` and its test project as gameplay systems are introduced; preserve the boundary defined by ADR 0001.
