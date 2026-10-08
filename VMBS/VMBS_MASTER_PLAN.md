# VMBS Master Catch-Up Plan

Status: living architecture/implementation plan begun at CP09.

## 0. Non-negotiable architecture

VMBS is deterministic linear software. Runtime tools do not read datasheets or use AI inference. Human/development research turns authoritative component documentation into structured records; VMBS performs indexed lookup, graph traversal, field/range matching, rules, and constrained search.

Board Script (`.bs`) is the authoritative native board format. KiCad and future CAD formats are import/export targets, not the internal model. A valid novel `.bs` design must not require a CAD source sidecar.

Three stable identities are distinct:
- board-local token: compact reference used inside one `.bs` file;
- PART_ID: global component-record identity;
- MODULE_ID: stable VMB behavioral implementation identity.

## 1. Finish the Board Script format [BLOCKING]

CP07 remains the acceptance gate. Add native BS representation for all meaningful source design information still absent from the compact model: board/header information, layer table/stackup, setup/design rules, net table/classes, footprint metadata/properties, complete zone semantics, drawing/text payload, and meaningful manufacturing/document/plot properties.

Acceptance test: import KiCad -> BS -> prohibit access to original source/lexical stream/full parsed tree -> export fresh KiCad -> independently compare semantic/physical/electrical design -> zero meaningful differences.

The raw lexical source may remain only as provenance/debugging material. Export/reconstruction code must be unable to depend on it.

BS must support incomplete designs explicitly: logical connection known/route unresolved; component required/package or placement unresolved; required support circuit unresolved. Incompleteness is not file corruption.

## 2. Mature the global Component Store [STARTED CP08]

Current seed: 23 records from ISA8 Ethernet specimen.

Extend record schema for:
- canonical identity and aliases;
- manufacturer and manufacturer deltas;
- package/footprint alternatives and dimensions;
- pins, pin names/numbers/types/functions, power groups;
- voltage/current/logic limits;
- timing and clock requirements;
- thermal data where relevant;
- structured interfaces/buses;
- configuration/strap pins;
- validation requirements;
- support-circuit requirements;
- provenance per structured fact;
- optional VMB MODULE_ID binding.

Unknown stays unknown. Exact equivalence may alias; meaningful differences remain deltas or distinct PART_IDs.

## 3. Create VMB Code Store / Module Index [START CP09]

Only parts represented at VMB resolution receive a MODULE_ID. Default is NONE.

Likely bindings: CPUs, chipsets, peripheral/controller ICs, RAM, ROM/EPROM/EEPROM/Flash, programmable logic, and other stateful/complex devices. Ordinary support passives normally have no runtime module.

MODULE_ID is stable and independent of VB.NET filename/class/namespace. The module manifest resolves MODULE_ID to the current VMB implementation and declares its machine-readable interface.

VMB modules must expose declarative configuration points such as I/O base, IRQ, DMA, ROM enable/window, clock option, memory mapping, and device enable. These are usable both by the Board Compiler and physical synthesis.

Programmed contents are occurrence/build data, not component identity: ROM image, EEPROM contents, PAL/GAL equations, firmware, etc.

## 4. Circuit Knowledge Store / Circuit Chess

Circuit Chess performs deterministic circuit completion, not free-form invention.

Inputs are largely nailed down: selected VMB-resolution devices, known component pins/requirements, fixed external boundaries/card edges/connectors, and existing solved circuitry from known BS boards. Its job is primarily the interstitial graph.

It must:
- connect compatible endpoints directly when legal;
- insert known support/adaptation circuits when required;
- realize VMB configuration points physically (jumpers, DIP switches, straps, etc.);
- reuse existing board resources where legal;
- recursively satisfy requirements introduced by newly added parts;
- preserve existing valid circuitry unless a change requires modification.

Circuit recipes/patterns are reusable records: decoupling, pull-up/down, oscillator, reset, address decode, buffering, level translation, termination, EEPROM/ROM interface, ISA glue, etc.

Analyzer direction: physical graph -> recognized abstractions.
Synthesis direction: abstractions/requirements -> physical graph.

## 5. Deterministic Analyzer (the ANAL-IZER)

Walk `.bs`, resolve local tokens -> PART_ID, traverse pins/nets, apply component/circuit rules, recognize topology, and reduce the physical board to VMB resolution.

It must distinguish at least:
- known and relevant;
- known but irrelevant to current VMB resolution;
- unknown but non-blocking;
- unresolved and required for current target.

An unknown resistor manufacturer must not halt VMB compilation. Unknown PAL logic controlling ISA address decode must.

## 6. VMB-resolution Board IR

Define a stable intermediate representation between Analyzer and Board Compiler. It expresses what the machine is, not VB.NET implementation details.

Required families include devices/MODULE_IDs, buses, pin/interface relationships, address decoding, memory/I/O ranges, IRQ/DMA, clocks, configuration points, inter-device connections, programmed images, and machine-visible settings.

## 7. Board Compiler

Consume VMB-resolution IR, resolve MODULE_IDs through Code Store, instantiate existing VMB modules, bind buses, address maps, IRQ/DMA, configuration and programmed contents, and materialize a runnable VMB board/machine.

The compiler owns knowledge of how the current VMB implementation is constructed. `.bs` and analyzer IR do not contain VB class names.

## 8. Placement engine

For newly synthesized/modified hardware, place unresolved components while respecting fixed anchors, board outline, connectors/card edge, keepouts, thermal/physical restrictions, component orientation and placement constraints. Existing valid placement should remain locked/preserved by default.

## 9. Tracer / physical routing engine

Circuit Chess decides WHAT connects. Tracer decides HOW copper physically connects.

Tracer consumes nets, placement, board geometry and rule data. It owns trace/via/layer geometry and physical constraints including clearance, width/current, allowed layers, via rules, length/skew, impedance where applicable, interference/noise separation, return paths/planes, thermal/copper requirements, keepouts and connector escape constraints.

Existing traces/vias are preserved by default. New/changed nets can be routed independently.

## 10. TITS bit

TITS = Truncated Information To Substrate.

At each target boundary it is a simple high/low gate:
- 0 / DOWN = sufficient information for this target;
- 1 / UP = required information unresolved; halt that target operation.

Do not turn TITS itself into a complex status enum. Detailed diagnostics live separately and explain which unresolved property raised the bit.

## 11. First end-to-end target

ISA8 Ethernet card remains the primary specimen.

Milestones:
1. complete sidecar-independent BS round trip;
2. deepen RTL8019AS/SST39SF040/93LC46C and relevant connector/support records;
3. bind VMB-resolution parts to MODULE_IDs;
4. analyze physical BS into board IR;
5. compile IR into the existing/new VMB NE2000 implementation;
6. verify guest-visible behavior;
7. remove selected interstitial circuitry and test Circuit Chess reconstruction against the original as an answer sheet;
8. remove selected routes and test Tracer for a valid alternative route rather than exact artistic reproduction.

## 12. Implementation order

A. Freeze schemas/interfaces before algorithms: BS sections, Component Store v2, Code Store manifest, circuit-recipe schema, Board IR, diagnostic/TITS contract.
B. Finish CP07 sidecar-independence gap.
C. Populate first component records from authoritative datasheets.
D. Bind first VMB modules and emit first Board IR.
E. Build Board Compiler for the NIC.
F. Add circuit recognition/completion.
G. Add placement.
H. Add tracer/routing rules.
I. Expand specimens: sound card, VGA, then progressively more complex boards.

The stores are cumulative assets and must be carried forward between checkpoints rather than regenerated as board-local scratch data.

## CP11 completion note
The original CP07 sidecar-independence gate is now **PASS for the ISA8 Ethernet specimen**. `VMBS-BOARD-1` can reconstruct a semantically identical KiCad board using only its dictionary-coded semantic records. Byte identity is not required. Universal format coverage remains incremental across additional specimens.

## CP15 architecture refinement
Board IR now includes a behavioral interface graph derived from BS net topology + Component Store pin intentions + Platform Store pin intentions. This graph is not an electrical simulator. It exists to tell the Board Compiler which reusable VMB modules/interfaces are required and how they are connected/configured. Physical pins may expose multiple behavioral facets when the component itself multiplexes their function by operating phase. Firmware payload selection remains separate and manual at the machine PCB-instance level for now.

## CP16 architecture freeze — configuration decoding
Component-owned decode tables may translate physical/reset-latched selector pins into VMB-facing properties. The Analyzer supplies only values justified by BS topology, fixed rails, documented component defaults, or retained user controls. User-configurable hardware produces a selectable property domain rather than an invented switch state. Decode rules must not contain specimen board/reference identities.
