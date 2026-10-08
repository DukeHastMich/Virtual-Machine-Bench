# Sol-Stud → VMBS Solver Handoff Contract (Horizon)

**Sol-Stud** — Solution Studio. Logo concept: a sun with six-pack abs.

Sol-Stud is upstream of Board Script synthesis and the physical solver. It is not part of Circuit Chess.

For a new-design job, Sol-Stud must eventually provide enough explicit, provenance-bearing information to construct a Board Script and a target mechanical profile:

## Functional intent
- requested card/device functions and external interfaces
- target host/bus/platform and compatibility level
- required/optional features
- configuration choices exposed to the finished device

## Selected matter
- exact selected Component Store identities for primary ICs/connectors
- allowed substitutions, if the user explicitly permits them
- programmed-memory / PLD / firmware payload identities
- user-fixed component choices versus solver-selectable support parts

## Resource and interface decisions
- bus widths and interface bindings
- address/decode/resource requirements
- IRQ/DMA/clock/resource choices where applicable
- connector/interface requirements
- voltage/power-source assumptions that are user/platform facts

## Mechanical target
- target device/chassis/card family
- legal board-envelope families or exact envelope
- min/preferred/max dimensions where variable
- mandatory edge connectors, brackets, mounting holes and fixed interfaces
- keepout/height/airflow/insertion constraints
- allowed layer-count/manufacturing classes if user-selected

## Policy
- reconstruction / derivative / novel design mode
- locked, bounded, and free decisions
- optimization priorities selected by the user
- explicit unknowns

## Required behavior
Sol-Stud must not invent electrical or physical facts merely to complete the handoff.
Unknown required evidence remains unknown and may cause the downstream solver to return TITS_UP.
The eventual handoff is declarative data, not AI/ML output and not hidden reasoning.

This document freezes the boundary only. Automatic Sol-Stud implementation and automatic BS synthesis are not CP213 work.
