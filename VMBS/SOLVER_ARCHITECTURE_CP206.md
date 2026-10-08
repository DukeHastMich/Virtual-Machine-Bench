# VMBS Physical Realization Solver — Architecture CP206

## Mission
The solver does not design the machine. Board Script (BS) defines the machine.
The solver searches for a physically realizable implementation that preserves BS intent,
Component Store truth, imported physical evidence, and fabrication constraints.

## Non-negotiable rules
1. Never trade correctness for a lower optimization score.
2. Hard constraints are gates, not penalties.
3. Unknown evidence is never guessed. If required truth is absent, return TITS_UP with a machine-readable reason.
4. Imported authoritative geometry may be LOCKED and bypass solving.
5. Partial historical geometry is preserved; only UNKNOWN/FREE regions may be synthesized.
6. Behavioral deduplication must not erase package, pin, electrical, programmed-content, or reconstruction identity.
7. Solver output is provisional until verification passes.

## Pipeline
BS + Component Store + source evidence
 -> Constraint Compiler
 -> Feasibility Gate
 -> Placement Solver
 -> Topology Optimizer
 -> Tracer/Router
 -> Verification Loop
 -> CAD/Fabrication candidate

The first implementation target is the Constraint Compiler and Feasibility Gate.
Placement and routing algorithms are deliberately deferred until the vocabulary is stable.

## Constraint classes
- HARD_CORRECTNESS: identity, connectivity, package/pin identity, programmed payloads.
- HARD_ELECTRICAL: rails, drive/load, pull requirements, contention, clocks, support networks.
- HARD_PHYSICAL: outline, keepouts, connectors, holes, orientation, courtyards, locked evidence.
- HARD_TIMING: evidenced access/setup/hold/clock/propagation/skew/from-to limits.
- SOFT_HISTORICAL: observed but non-authoritative placement/topology preferences.
- SOFT_OPTIMIZATION: SI margin, manufacturability, area, vias, route complexity, aesthetics.

## Objective ordering
Lexicographic:
1. HARD_CORRECTNESS
2. HARD_ELECTRICAL
3. HARD_PHYSICAL
4. HARD_TIMING
5. historical fidelity
6. timing/SI margin
7. manufacturability
8. board area
9. routing complexity
10. aesthetics

A lower-priority objective may never worsen a higher-priority result.

## Evidence states
Every compiled constraint carries source, confidence, authority, mutability, and failure policy.
Authority: authoritative | verified_reconstruction | supporting | inferred | unknown.
Mutability: locked | bounded | free.
Unknown cannot silently become a numeric default.

## Solver result states
- SOLVED_VERIFIED
- SOLVED_UNVERIFIED
- UNSAT
- TITS_UP_MISSING_EVIDENCE
- TITS_UP_UNSUPPORTED_CONSTRAINT
- TITS_UP_AMBIGUOUS_IDENTITY

UNSAT means known constraints conflict.
TITS_UP means available knowledge is insufficient to decide safely.

## Historical-board modes
AUTHORITATIVE — preserve and validate existing source-truth geometry.
PATCH — lock known geometry and solve only missing/unknown regions.
REFERENCE — use geometry as preferences and record divergence.
NOVEL — full legal search space.

## Interface boundary
Component Store owns reusable component truth.
BS owns board intent and board-specific selections.
Constraint Compiler converts those facts into normalized solver constraints.
The solver must not scrape prose fields during optimization.

## CP206 scope
Constraint vocabulary + schema + conservative Store audit only.
No placement. No routing. No floor planning.
