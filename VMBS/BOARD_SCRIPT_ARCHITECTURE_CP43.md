# Board Script architecture freeze — CP43

Board Script (`.bs`) is VMBS's canonical, human-readable, editable board intermediate representation and board-description markup language.

It is not a decorative front end for JSON. Parser object graphs, Construction IR, JSON dumps, compiler manifests and backend plans are derived compiler state. They may be serialized for diagnostics, regression, caching or tooling, and may be useful direct machine interfaces, but they do not replace Board Script's role by policy or by artificial restriction. Board Script must earn that role through useful board semantics.

Canonical flow:

    evidence/design/importers -> Board Script -> parse/analyze/solve -> derived construction state -> VB/Python/C/C++/HDL/etc.
                                                        \-> Tracer/KiCad/Gerber/manufacturing

Rules:
1. Do not put VB/Python/C/HDL implementation syntax into Board Script.
2. Do not require JSON as a conceptual compilation stage. In-memory models are sufficient.
3. If a serialized derived form is useful, name it as IR/dump/manifest rather than "Board Script 2".
4. Circuit Chess and Tracer decisions that are durable board facts must be representable back in Board Script.
5. No-invention remains absolute. Unknown required facts keep the relevant destination TITS UP.
6. Backends may consume a normalized in-memory/serialized construction plan, but that plan is derived from the solved board and is not a second canonical board language.
7. Alternative machine-to-machine inputs are not intentionally crippled to protect Board Script. If BS is not the best representation for a workflow, fix BS or use the better interface.

Prototype-01 starts adding explicit board-level intent: clocks, power domains, programmed-device payload declarations and generic named constraints. These are board facts, not backend instructions.
