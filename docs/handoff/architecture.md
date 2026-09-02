# Architecture

## Component map

- `AlterCourse.Core` owns pure simulation and domain behavior. It targets ordinary .NET, has no Godot reference, and remains testable without engine startup.
- `AlterCourse.Godot` owns nodes, scenes, resources, input, UI, and engine adapters. It may reference `AlterCourse.Core`; the reverse dependency is prohibited.
- `AlterCourse.Core.Tests` exercises the pure assembly with xUnit and verifies that its compiled assembly references no `Godot*` assembly.
- GdUnit4 tests under `src/AlterCourse.Godot/tests/` exercise the managed node and scene boundary through the actual Godot runtime.
- `GameSimulation` owns immutable definitions and active Core state. It advances explicit simulation time in deterministic 100 ms tactical quanta.
- World state holds ships in `ShipInstanceId` order, an explicit `PlayerShipId`, and per-ship strategic, tactical, sensor, and repair state.
- Scheduled work, travel, and repairs target ships explicitly. Public player commands resolve `PlayerShipId`; arbitrary-ship control is not exposed.
- Ship iteration is stable. Each advancement is capped at 1,000,000 moving-ship steps and 10,000 scheduled consequences.
- Finite-long numeric exhaustion fails atomically; it is an explicit limitation rather than an indefinite-successor promise.
- V3 persistence bounds world state, definition references, scheduler data, active orders, and the order allocator.
- Loading resolves references through the supplied immutable catalog. Adjacent V1-to-V2-to-V3 and V2-to-V3 migrations preserve rule identities.
- Definitions are not serialized. V1 migration creates one ship, targets old work to the player, and uses its design label for the missing vessel name.
- World construction and persistence admit at most 256 ships to bound untrusted input and fixed-step work; this is not a final capacity target.
- Authored strategic-map order remains observable.
- Ship collections and scheduled work are insertion-order independent and canonically ordered.
- Godot projects player-visible state without NPC omniscience.
- Ordinary ships may have one optional stable `ShipOrder`: `TravelTo`, `PatrolRoute`, or `HoldUntil`; old travel remains orderless.
- Order execution shares the internal travel application with player travel. Cancellation removes only the identified order and its exact hold wake.
- Strategic-only intervals jump event-to-event. Only at-location ships with nonzero tactical motion take fixed steps; repairs materialize analytically.
- Player-relevant advance processes hidden NPC work but reports and stops only on `PlayerShipId`; Godot results stay filtered.
- `CancelOrder` and complete consequence traces are internal M2 surfaces until a real command or diagnostic consumer exists.

## Standing backlog

- Add simulation behavior to `AlterCourse.Core` and its test project as gameplay systems are introduced; preserve the boundary defined by ADR 0001.
