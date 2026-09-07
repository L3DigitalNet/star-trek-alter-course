# Architecture

## Component map

- The [design wiki](../wiki/architecture.md) is the single source of truth for design; this map is an operational summary of the component graph and must match it.
- `AlterCourse.Core` owns pure simulation and domain behavior. It targets ordinary .NET, has no Godot reference, and remains testable without engine startup.
- `AlterCourse.Godot` owns nodes, scenes, resources, input, UI, and engine adapters. It may reference `AlterCourse.Core`; the reverse dependency is prohibited.
- `AlterCourse.Core.Tests` exercises the pure assembly with xUnit and verifies that its compiled assembly references no `Godot*` assembly.
- GdUnit4 tests under `src/AlterCourse.Godot/tests/` exercise the managed node and scene boundary through the actual Godot runtime.
- `GameSimulation` owns immutable definitions and active Core state. It advances explicit simulation time in deterministic 100 ms tactical quanta.
- World state holds ships in `ShipInstanceId` order, an explicit `PlayerShipId`, and per-ship strategic, tactical, sensor, and repair state.
- Current scheduled work, travel, and repairs target ships explicitly. Public player commands resolve `PlayerShipId`; arbitrary-ship control is not exposed.
- Ship iteration is stable. Each advancement is capped at 1,000,000 moving-ship steps and 10,000 scheduled consequences.
- Finite-long numeric exhaustion fails atomically; it is an explicit limitation rather than an indefinite-successor promise.
- V6 persists bounded world state, definition references, Engineering, orders, sensor knowledge, scheduler work, and allocators; contact reports are derived.
- Loading resolves references through the supplied immutable catalog. The adjacent chain migrates V1 through V6 before candidate validation.
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

## Approved next-slice boundary, not implemented

- [Faction assignment](../wiki/faction-intent-and-autonomous-assignment.md) owns the next approved design and its acceptance contract.
- Asset-side optional `FactionId` is the single control authority; rosters are derived. Only idle controlled NPCs may be assigned, without preempting orders or travel.
- Faction input grants own objective/topology and controlled-asset administrative facts only; sensor knowledge and external reports are not shared.
- Extend the existing scheduler to closed Ship/Faction targets; use typed bootstrap, bounded wakes, stable ordering, and ordinary order commands.
- Planned V7 migration invents no factions or control and preserves ship work. Zero-faction worlds stay valid; current runtime remains V6/content V4.
- No RNG, organization/controller framework, hierarchy runtime, political UI, or intelligence network is included in the first slice.

## Standing backlog

- Implement the approved bounded faction slice through a subsequent governed feature; do not treat design approval as runtime completion.
- Add simulation behavior to `AlterCourse.Core` and its test project as gameplay systems are introduced; preserve the boundary defined by ADR 0001.
