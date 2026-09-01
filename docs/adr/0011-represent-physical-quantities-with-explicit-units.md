---
schema_version: '1.1'
id: 'adr-0011-star-trek-alter-course-represent-physical-quantities-with-explicit-units'
title: 'ADR 0011: Represent Physical Quantities with Explicit Units'
description: 'Defines quantity typing, canonical serialization units, UnitsNet use, fictional quantities, precision, and Godot conversion boundaries.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'simulation'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
supersedes: []
superseded_by: null
source:
  - 'https://github.com/angularsen/UnitsNet'
  - 'https://www.nuget.org/packages/UnitsNet/'
  - 'https://github.com/angularsen/UnitsNet/wiki'
confidence: 'high'
visibility: 'public'
license: 'MIT'
project:
  decision_makers:
    - 'project owner'
  consulted: []
  informed: []
  amends: []
  amended_by: []
---

# Represent physical quantities with explicit units

## Context and Problem Statement

Engineering, navigation, tactical combat, sensors, travel, and logistics in Star Trek: Alter Course will exchange values such as power, energy, mass, distance, speed, duration, temperature, pressure, frequency, and fuel quantity. Representing every value as an unqualified `double` makes code compact but allows dimensionally invalid operations and conversion mistakes such as megawatts versus kilowatts, kilometers versus meters, or seconds versus hours.

The setting also contains quantities that are fictional, dimensionless, deliberately abstracted, or not consistently specified in canon: warp factor, shield integrity, sensor confidence, subsystem condition, phaser charge, structural stress, and various gameplay rates. Applying physical units indiscriminately would create false precision and cumbersome bookkeeping without meaningful decisions.

UnitsNet provides strongly typed standard physical quantities and conversions in .NET. Adopting its runtime types everywhere would reduce some local code but could leak a third-party representation into JSON content, saves, hot tactical loops, and Godot adapters.

This decision governs the representation, conversion, validation, and serialization of dimensioned and dimensionless simulation quantities. It applies whenever a Core value has a physical dimension, a canonical unit, a bounded ratio, or domain semantics that would be unsafe as an anonymous primitive.

It does not establish detailed engineering formulas, canon conversion tables, balance values, tactical integration methods, or a commitment to physically exact simulation. The game remains a decision-focused model rather than an engineering analysis program.

How should the project make units and quantity semantics mechanically visible without introducing false precision or allowing a library's representation to become the persistence contract?

## Decision Drivers

- Cross-system engineering calculations must not mix incompatible dimensions silently.
- Content and save files need stable canonical units independent from display preferences and package internals.
- Godot presentation often uses pixels, engine seconds, and `Vector2`, while Core uses domain units.
- Conversion logic should be centralized and testable.
- Common physical quantities and conversions are generic infrastructure that should not be reimplemented casually.
- Tactical loops may require efficient numeric storage after validation and normalization.
- Fictional and abstract quantities need domain-specific semantics rather than forced SI analogies.
- Percentages and ratios often require bounds and meaning beyond `double`.
- Numeric behavior must remain deterministic enough for Core tests and save continuation.
- The model should communicate uncertainty and abstraction rather than imply unsupported precision.

## Considered Options

- Use explicit quantity types, canonical serialized units, and UnitsNet selectively for standard physical dimensions.
- Use UnitsNet types for every numeric gameplay value.
- Use project-owned quantity structs for all dimensions and conversions.
- Use primitive numeric values with naming conventions.
- Represent every serialized quantity as a value-and-unit pair.

## Decision Outcome

Chosen option: "Use explicit quantity types, canonical serialized units, and UnitsNet selectively for standard physical dimensions", because it prevents dimensional mistakes while keeping persistence stable and allowing fictional abstractions to remain project-owned.

This decision governs Core APIs, content models, persistence models, and Godot conversion boundaries for simulation quantities. It does not require wrapping every count, identifier, enum, or local intermediate calculation.

### Quantity classification

Every simulation numeric value is classified before choosing its representation.

#### Standard physical quantity

A value with an established dimension and meaningful unit conversion, such as:

- power;
- energy;
- mass;
- distance or length;
- velocity;
- acceleration;
- duration;
- temperature;
- pressure;
- frequency;
- angle or angular velocity;
- data size or data rate when gameplay-relevant.

Use a strongly typed quantity at public domain boundaries and across subsystem boundaries. UnitsNet is the preferred implementation for standard physical quantities when its supported type and conversion semantics fit the model.

#### Discrete count

A count of items, people, torpedoes, compartments, events, or work units uses an appropriate integral type or a project-owned value object when bounds and identity matter. A count is not converted into a physical quantity merely because it participates in a rate.

#### Bounded ratio or probability

Integrity, confidence, efficiency, morale fraction, probability, and similar values use a project-owned bounded type when invalid values below zero, above one, or nonfinite would be consequential.

Distinct concepts should not automatically share one generic percentage type. Shield integrity and sensor confidence can have different rules even when both display as percentages.

#### Fictional or setting-specific quantity

Warp factor, stardate, shield capacity, phaser charge, warp-core stress, subspace distortion, and similar concepts use project-owned domain types with explicit rules. A fictional quantity may internally use a standard physical quantity where the design defines a conversion, but the public type preserves its game meaning.

#### Scalar tuning value

A local coefficient or score with no independent domain meaning may remain a primitive inside a bounded implementation. It must acquire an explicit name and validation at configuration or subsystem boundaries.

### UnitsNet adoption

UnitsNet is the selected first candidate for standard physical dimensions in Core.

Before its first production use, a focused proof must verify:

- the required quantities and unit conversions exist or can be extended safely;
- arithmetic and comparison behavior fit the domain;
- JSON content and save adapters can serialize project-defined canonical numeric forms without exposing UnitsNet's internal representation;
- mappings to and from Godot vectors and engine APIs are explicit;
- representative tactical and long-running simulation workloads show acceptable allocation and execution behavior;
- nonfinite values and unsupported units fail predictably;
- package license and current .NET compatibility satisfy ADR 0003.

If the proof succeeds, the package may be admitted through central package management without another ADR. If it fails materially, the implementation uses focused project-owned quantity structs and records the evidence before choosing another library.

UnitsNet types may appear in Core domain APIs and calculations. They do not appear directly as the canonical JSON schema, save wire format, Godot exported-property contract, or public mod contract.

### Canonical units

Every serialized physical field has exactly one canonical unit for its schema version.

The canonical unit is documented by at least one of:

- an unambiguous property suffix such as `_km`, `_s`, `_kg`, or `_mw`;
- a schema annotation and definition whose property name remains sufficiently clear;
- a containing type whose unit contract is singular and explicit.

A bare property such as `range`, `power`, `time`, or `speed` is not acceptable when its unit cannot be inferred from a stable type contract.

Content and persistence models store normalized numeric values in the canonical unit. They do not serialize arbitrary input unit strings or UnitsNet's formatted text as the authoritative form.

Examples of acceptable logical contracts include:

```json
{
	"effective_range_km": 125000,
	"power_draw_mw": 18.5,
	"cycle_time_s": 4
}
```

or an explicitly typed schema definition with equivalent unambiguous meaning.

Human-facing tools may accept alternate units as input, but conversion occurs before canonical validation and output. A generated or reformatted file uses the canonical unit.

### Domain and persistence boundaries

Core domain constructors and commands accept typed quantities or project-owned value objects. Conversion from raw numeric JSON occurs in the loader after schema validation and before the definition enters the runtime catalog.

Persistence mapping normalizes runtime quantities into the save schema's canonical units. Loading reconstructs typed values only after finite, range, and semantic validation.

Changing the canonical unit or numeric interpretation of a persisted field is a schema change requiring migration under ADR 0006, even when the JSON property type remains `number`.

Third-party quantity type names, enum numeric values, and formatting conventions are not serialized as compatibility identifiers.

### Godot boundary

`AlterCourse.Godot` converts between domain quantities and presentation values explicitly.

Examples include:

- domain distance to map-space coordinates;
- domain duration to animation duration;
- domain angle to Godot radians where required;
- domain velocity to an interpolated visual transform;
- domain power to a formatted gauge or localized label.

Pixels are presentation units and do not enter Core as distance. A screen-space `Vector2` is not a tactical position. A Godot frame delta is not the authoritative simulation duration under ADR 0007.

Conversion helpers live at adapter boundaries and are covered by integration tests. They must state scale, origin, orientation, and unit assumptions.

### Arithmetic and precision

Use numeric precision appropriate to the gameplay rule.

- Integral values are preferred for inherently discrete counts and quantized work.
- Floating-point values are acceptable for continuous simulation where their limitations are understood.
- Decimal arithmetic is considered when exact base-10 accounting is a genuine domain requirement, not as a universal determinism solution.
- Comparisons involving floating-point calculations use domain tolerances or quantization where exact equality is not meaningful.
- NaN and positive or negative infinity are rejected at content, save, and command boundaries unless a specific type deliberately defines them, which is not expected for ordinary gameplay.
- Overflow and underflow behavior in consequential calculations is tested.

The model does not display more significant digits than the underlying rule, sensor certainty, or player decision requires. Canonical units do not imply scientific fidelity.

### Rates and derived quantities

A rate must identify both numerator and denominator semantics. Values such as repair rate, fuel consumption, shield regeneration, and sensor sweep progress should use a typed rate or an explicit formula rather than an anonymous scalar multiplied by elapsed time.

Derived quantities are calculated through named domain operations where that improves correctness and explanation. A convenience conversion must not hide a gameplay rule such as efficiency loss, damage, or power allocation.

### Content authoring

Schemas under ADR 0005 define units, bounds, and finite-number requirements.

Content authors should normally write canonical units. Authoring tools may display or accept friendlier units and convert deterministically.

Validation catches:

- incompatible or unsupported units;
- values outside domain bounds;
- nonfinite numbers;
- ambiguous fields without a unit contract;
- impossible relationships such as a negative mass or cycle time;
- unit conversion that would overflow the target representation.

### Consequences

- Good, because incompatible dimensions are harder to combine accidentally.
- Good, because unit conversion becomes centralized and testable.
- Good, because JSON and saves retain a stable project-owned wire contract.
- Good, because standard conversions can reuse UnitsNet rather than local tables.
- Good, because fictional and dimensionless mechanics retain appropriate domain types.
- Good, because Godot pixels and frame time cannot silently become simulation units.
- Bad, because typed quantities add verbosity to domain APIs and tests.
- Bad, because mappings are required at JSON, persistence, and Godot boundaries.
- Bad, because selective use requires judgment about whether a value is physical, fictional, or merely a coefficient.
- Bad, because a UnitsNet upgrade can still affect compile-time APIs and must be reviewed even though wire formats remain stable.

### Confirmation

A change is in scope when it introduces or changes a dimensioned value, unit conversion, bounded ratio, fictional quantity, serialized numeric field, or Godot-to-Core coordinate or time conversion.

Conformance is confirmed by:

- APIs that expose typed or explicitly named quantities at subsystem boundaries;
- schemas that declare canonical units and bounds;
- tests for conversions, extreme values, nonfinite values, and incompatible dimensions;
- persistence migration review when canonical units or meaning change;
- Godot adapter tests for scale, orientation, and conversion;
- property or metamorphic tests that equivalent units produce equivalent domain outcomes;
- representative performance tests before quantity wrappers are used in hot high-volume paths;
- review that displayed precision does not exceed the model's meaning.

## Pros and Cons of the Options

### Use explicit quantity types, canonical serialized units, and UnitsNet selectively for standard physical dimensions

- Good, because it combines mature conversions with project-owned persistence.
- Good, because domain-specific and fictional values remain expressive.
- Good, because performance-sensitive internals can normalize after a typed boundary.
- Bad, because several representation categories must be understood.
- Bad, because adapter and mapping code is unavoidable.

### Use UnitsNet types for every numeric gameplay value

- Good, because one library supplies a consistent quantity vocabulary.
- Good, because common physical conversions are readily available.
- Bad, because many gameplay values are not standard physical quantities.
- Bad, because package types would spread into persistence and presentation unless carefully contained.
- Bad, because wrapping counts, scores, and fictional mechanics would create misleading semantics.

### Use project-owned quantity structs for all dimensions and conversions

- Good, because the project controls every API and representation.
- Good, because types can match gameplay exactly.
- Bad, because standard unit catalogs, parsing, conversion, and edge cases would be reimplemented.
- Bad, because local infrastructure would grow substantially as engineering depth increases.

### Use primitive numeric values with naming conventions

- Good, because implementation is compact and fast.
- Good, because serialization is direct.
- Bad, because the compiler cannot prevent dimensional mistakes.
- Bad, because conversions become scattered.
- Bad, because ambiguous or stale names are easy to introduce during refactoring.

### Represent every serialized quantity as a value-and-unit pair

- Good, because each serialized value is self-describing.
- Good, because authors can choose convenient units.
- Bad, because validation and canonical comparison become more complex.
- Bad, because semantically identical content can have many textual forms.
- Bad, because saves become coupled to unit-name parsing and migration.
- Bad, because the extra flexibility provides little value for machine-produced snapshots.

## More Information

This ADR is about semantic correctness, not maximum physical realism. The engineering model should include only quantities that create meaningful command decisions or clear operational consequences.

UnitsNet is approved through a bounded proof rather than adopted blindly. The proof protects the save and Godot boundaries while avoiding a second ADR for a package that already satisfies the declared criteria.
