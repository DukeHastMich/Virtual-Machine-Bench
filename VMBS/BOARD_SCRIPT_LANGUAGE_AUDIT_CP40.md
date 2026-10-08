# Board Script Language Audit — CP40

## Finding 1: there are currently two things called “Board Script”

The preserved Prototype-00 human DSL (`Board Scripts/Samples/*.bs`) expresses component population, placement, straps, and named nets.

The current Python VMBS pipeline uses `.bs` for `VMBS-BOARD-1`: a dictionary-coded, source-independent semantic encoding of the imported KiCad S-expression tree. `board_analyzer.py` consumes this form.

These are not the same language. The collision is survivable internally but must be resolved before BS becomes the public/native multi-destination intermediary format.

## Authority contract

Board Script is authoritative for **board-specific facts**:
- component instances and selected exact part identities;
- connectivity/topology;
- board-specific straps and configuration sources;
- programmed-device assignments;
- placement/orientation/side;
- board mechanics and stackup;
- board-specific electrical/routing intent;
- exposed connectors/interfaces;
- explicit epistemic/completeness state.

Component Store is authoritative for **reusable component facts**:
- manufacturer identity and aliases;
- package/pin map and footprints;
- electrical/timing/thermal limits;
- semantic pin/interface roles;
- reusable support requirements/provisions/patterns;
- executable Code Store binding;
- evidence/provenance for those reusable facts.

Neither authority may silently manufacture a fact missing from the other.

Rule of thumb: if the exact same part moved to another board could legitimately change the fact, it belongs in BS. If the fact remains a property of that part/package, it belongs in Component Store.

## Finding 2: VMBS-BOARD-1 is an excellent lossless CAD semantic capsule, but not yet the full intermediary language

Its current strength is round-trip preservation: KiCad constructs can survive without source bytes or lexical dependence. Its weakness is that the meaning remains KiCad-shaped. A Gerber reconstruction, schematic reconstruction, photo reconstruction, Circuit Chess result, and synthesized board should not have to pretend they originated as a KiCad AST.

Recommendation: retain the lossless CAD capsule as a **source facet**, while defining a normalized Board Script semantic model above it.

Conceptually:

```
Board Script document
  identity
  evidence/completeness
  components
  nets
  configuration
  mechanics
  placement
  electrical intent
  routing intent
  programmed contents
  external interfaces
  source facets
      kicad semantic capsule (optional)
      gerber evidence (optional)
      schematic evidence (optional)
      photo evidence (optional)
```

The source facet preserves exact round-trip information. The normalized semantic domains feed Analyzer, Circuit Chess, Chimera, Tracer, validators, and other exporters.

## Finding 3: “green” must be destination-specific

One boolean cannot honestly represent all consumers.

Recommended readiness matrix:
- `roundtrip_source`: enough information to regenerate the originating CAD semantics.
- `structural`: all asserted component/pin/net identities resolve without invention.
- `vmb_execution`: executable behavior required by VMB resolves.
- `electrical`: required electrical/support constraints are satisfied.
- `physical`: package/footprint/body/placement/mechanical facts resolve.
- `routing`: routing/stackup/net-class constraints are sufficient for Tracer.
- `manufacturing`: fabrication outputs can be generated without unsupported guesses.
- `evidence`: facts meet the requested provenance/confidence threshold.

A board may therefore be executable but not manufacturable, or perfectly round-trippable while containing an unresolved physical part. No consumer gets to reinterpret another consumer's green light.

## Finding 4: Prototype-00’s no-invention rule must survive the normalized model

Unknown identity is not a new component. Unknown pin is not a new pin. Missing electrical constraints are not default constraints.

The current Python Analyzer records a truly unknown component as a warning. That is acceptable only if destination readiness remains UP for every destination that needs to understand the part. It must never become “manufacturing ready” or “structurally complete” merely because the original CAD syntax can be preserved.

Permanent negative conformance remains:
`INVALID-Invented-Part-Must-Fail.bs`

## Language domains still missing from the normalized model

### Board mechanics
- exact outline primitives/arcs/cutouts;
- mounting holes and tooling holes;
- thickness;
- layer stackup/material/copper weight;
- board datum/origin and coordinate system.

### Placement
- exact x/y/rotation/side;
- footprint selection/variant;
- body courtyard/keepout;
- height limits;
- placement groups/relative constraints;
- fitted/DNP state.

### Electrical
- rails and power domains;
- net classes;
- voltage/current expectations;
- differential-pair membership/polarity;
- impedance targets/tolerance;
- termination/bias intent;
- clock class/frequency/jitter where board-specific;
- analog/digital/noisy/sensitive classifications;
- isolation/creepage/clearance constraints.

### Routing / Tracer intent
- width/via rules;
- layer permissions/preferences;
- length/skew constraints;
- topology constraints (point-to-point, multidrop, star, daisy chain);
- guard/return-path requirements;
- copper zones/planes;
- routing keepouts.

### Configuration
- straps/jumpers/DIP switches;
- sampling time: static/reset-latched/live;
- allowed settings and physical realization;
- machine-instance selected state separate from board design truth.

### Programmed devices
- socket/device identity in BS;
- payload slot and size constraints;
- installed payload identity in machine instance, not component identity.

### Evidence
Every asserted or inferred board fact should be able to carry:
- source/provenance;
- observation method;
- confidence;
- observed vs datasheet-constrained vs inferred;
- contradiction/alternative hypotheses.

## Multi-destination invariant

No destination-specific implementation detail belongs in normalized BS.

- VMB behavior stays in Code Store.
- KiCad syntax stays in the KiCad source/export facet.
- Gerber aperture mechanics stay in Gerber import/export.
- Tracer algorithms stay in Tracer.
- Circuit Chess search strategy stays in Circuit Chess.

BS carries the shared board truth those systems exchange.

## Proposed next language version

Do **not** mutate `VMBS-BOARD-1` in place.

Define `VMBS-BOARD-2` as a normalized semantic envelope with optional lossless source facets. Provide a deterministic `VMBS-BOARD-1 -> VMBS-BOARD-2` lift that preserves the entire v1 capsule as its KiCad source facet while extracting only facts the importer can prove.

This lets old KiCad round-trip tests remain exact while the language grows into a true CAD/Gerber/schematic/photo/execution/manufacturing intermediary.

## Constitutional sentence

**Board Script must contain every board-specific fact required by a requested destination; Component Store must contain every reusable component fact required to interpret those facts; if neither knows a required fact, that destination remains TITS UP.**
