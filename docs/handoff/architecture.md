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
- V5 persistence bounds world state, definition references, scheduler data, active orders, Engineering state, repairs, and the order allocator.
- Loading resolves references through the supplied immutable catalog. The adjacent chain migrates V1 through V5 before candidate validation.
- Definitions are not serialized. V1 migration creates one ship, targets old work to the player, and uses its design label for the missing vessel name.
- World construction and persistence admit at most 256 ships to bound untrusted input and fixed-step work; this is not a final capacity target.
- Authored strategic-map order remains observable.
- Ship collections and scheduled work are insertion-order independent and canonically ordered.
- Godot projects player-visible state without NPC omniscience.
- The tactical plot is player-centered and local, so legitimate sustained movement keeps marker and direction visible while numeric Core coordinates remain status truth.
- Ordinary ships may have one optional stable `ShipOrder`: `TravelTo`, `PatrolRoute`, or `HoldUntil`; old travel remains orderless.
- Order execution shares the internal travel application with player travel. Cancellation removes only the identified order and its exact hold wake.
- Strategic-only intervals jump event-to-event. Only at-location ships with nonzero tactical motion take fixed steps; repairs materialize analytically.
- Ship Engineering state owns bounded conditions, available power, allocations, derived sensor and impulse capability, and one exact system repair.
- Sensor contacts, scans, tactical courses, and cautious AI consume actor-owned effective capability; strategic travel remains unchanged.
- Player-relevant advance processes hidden NPC work but reports and stops only on `PlayerShipId`; Godot results stay filtered.
- Public advancement outcomes use player-semantic event names at the Godot boundary; scheduler consequences and proof traces remain internal surfaces.
- The Godot command shell owns stable player controls and projections, while `GameSimulation` remains the command and state authority.
- `GameScreen` owns one session-lifetime `GameSimulation`; Command and Engineering workspaces are persistent views over that same state.
- Command Deck map views reuse the strategic and tactical adapters. Godot owns display transforms, selection, and context presentation.
- Command-interface fixtures are deterministic presentation data only. They cannot submit commands, persist state, or invent Core truth.
- `scripts/launch-game.sh` is the safe direct-launch boundary: it restores and builds the Godot project before starting the editor.

## Standing backlog

- Add simulation behavior to `AlterCourse.Core` and its test project as gameplay systems are introduced; preserve the boundary defined by ADR 0001.
