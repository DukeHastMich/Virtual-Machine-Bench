# VMB and VMBS changelog

Canonical consolidated checkpoint history. Latest documented VMBS checkpoint: **CP513 (2026-10-04)**.

CP513 records analytic connectivity closure for 87/87 multi-terminal ISA8 Ethernet nets. This is the recorded checkpoint result, not a new build, runtime, electrical, or timing validation.

Checkpoint numbers belong to separate workstreams; motherboard, BIOS, board-script, and 80486 checkpoint numbers are not VMBS versions. Original reports and their historical validation limits are retained below. Undated entries are not assigned invented dates. VMBS status reports are ordered newest checkpoint first; the session journal retains its original chronology.

## Contents

- [VMBS/FORENSIC_SESSION.md](#history-vmbs-forensic-session-md)
- [VMBS/SOLVER_STATUS_CP413.md](#history-vmbs-solver-status-cp413-md)
- [VMBS/CP388_STATUS.md](#history-vmbs-cp388-status-md)
- [VMBS/CP363_STATUS.md](#history-vmbs-cp363-status-md)
- [VMBS/CP338_STATUS.md](#history-vmbs-cp338-status-md)
- [VMBS/CP313_STATUS.md](#history-vmbs-cp313-status-md)
- [VMBS/CP288_STATUS.md](#history-vmbs-cp288-status-md)
- [VMBS/CP263_STATUS.md](#history-vmbs-cp263-status-md)
- [VMBS/CP238_STATUS.md](#history-vmbs-cp238-status-md)
- [VMBS/CP213_STATUS.md](#history-vmbs-cp213-status-md)
- [VMBS/CP211_STATUS.md](#history-vmbs-cp211-status-md)
- [VMBS/CP210_STATUS.md](#history-vmbs-cp210-status-md)
- [VMBS/CP209_STATUS.md](#history-vmbs-cp209-status-md)
- [VMBS/CP208_STATUS.md](#history-vmbs-cp208-status-md)
- [VMBS/CP207_STATUS.md](#history-vmbs-cp207-status-md)
- [VMBS/CP206_STATUS.md](#history-vmbs-cp206-status-md)
- [VMBS/CP205_STATUS.md](#history-vmbs-cp205-status-md)
- [VMBS/CP180_STATUS.md](#history-vmbs-cp180-status-md)
- [VMBS/CP155_STATUS.md](#history-vmbs-cp155-status-md)
- [VMBS/CP154_STATUS.md](#history-vmbs-cp154-status-md)
- [VMBS/CP153_STATUS.md](#history-vmbs-cp153-status-md)
- [VMBS/CP152_STATUS.md](#history-vmbs-cp152-status-md)
- [VMBS/CP151_STATUS.md](#history-vmbs-cp151-status-md)
- [VMBS/CP150_STATUS.md](#history-vmbs-cp150-status-md)
- [VMBS/CP149_STATUS.md](#history-vmbs-cp149-status-md)
- [VMBS/CP148_STATUS.md](#history-vmbs-cp148-status-md)
- [VMBS/CP147_STATUS.md](#history-vmbs-cp147-status-md)
- [VMBS/CP146_STATUS.md](#history-vmbs-cp146-status-md)
- [VMBS/CP145_STATUS.md](#history-vmbs-cp145-status-md)
- [VMBS/CP144_STATUS.md](#history-vmbs-cp144-status-md)
- [VMBS/CP143_STATUS.md](#history-vmbs-cp143-status-md)
- [VMBS/CP142_STATUS.md](#history-vmbs-cp142-status-md)
- [VMBS/CP141_STATUS.md](#history-vmbs-cp141-status-md)
- [VMBS/CP140_STATUS.md](#history-vmbs-cp140-status-md)
- [VMBS/CP139_STATUS.md](#history-vmbs-cp139-status-md)
- [VMBS/CP138_STATUS.md](#history-vmbs-cp138-status-md)
- [VMBS/CP137_STATUS.md](#history-vmbs-cp137-status-md)
- [VMBS/CP136_STATUS.md](#history-vmbs-cp136-status-md)
- [VMBS/CP135_STATUS.md](#history-vmbs-cp135-status-md)
- [VMBS/CP134_STATUS.md](#history-vmbs-cp134-status-md)
- [VMBS/CP133_STATUS.md](#history-vmbs-cp133-status-md)
- [VMBS/CP132_STATUS.md](#history-vmbs-cp132-status-md)
- [VMBS/CP131_STATUS.md](#history-vmbs-cp131-status-md)
- [VMBS/CP130_STATUS.md](#history-vmbs-cp130-status-md)
- [VMBS/CP129_STATUS.md](#history-vmbs-cp129-status-md)
- [VMBS/CP128_STATUS.md](#history-vmbs-cp128-status-md)
- [VMBS/CP127_STATUS.md](#history-vmbs-cp127-status-md)
- [VMBS/CP126_STATUS.md](#history-vmbs-cp126-status-md)
- [VMBS/CP125_STATUS.md](#history-vmbs-cp125-status-md)
- [VMBS/CP110_STATUS.md](#history-vmbs-cp110-status-md)
- [VMBS/CP95_STATUS.md](#history-vmbs-cp95-status-md)
- [VMBS/CP89_STATUS.md](#history-vmbs-cp89-status-md)
- [VMBS/CP86_STATUS.md](#history-vmbs-cp86-status-md)
- [VMBS/CP85_STATUS.md](#history-vmbs-cp85-status-md)
- [VMBS/CP84_STATUS.md](#history-vmbs-cp84-status-md)
- [VMBS/CP83_STATUS.md](#history-vmbs-cp83-status-md)
- [VMBS/CP82_STATUS.md](#history-vmbs-cp82-status-md)
- [VMBS/CP81_STATUS.md](#history-vmbs-cp81-status-md)
- [VMBS/CP80_STATUS.md](#history-vmbs-cp80-status-md)
- [VMBS/CP79_STATUS.md](#history-vmbs-cp79-status-md)
- [VMBS/CP76_STATUS.md](#history-vmbs-cp76-status-md)
- [VMBS/CP74_STATUS.md](#history-vmbs-cp74-status-md)
- [VMBS/CP72_STATUS.md](#history-vmbs-cp72-status-md)
- [VMBS/CP68_STATUS.md](#history-vmbs-cp68-status-md)
- [VMBS/CP65_STATUS.md](#history-vmbs-cp65-status-md)
- [VMBS/CP62_STATUS.md](#history-vmbs-cp62-status-md)
- [VMBS/CP59_STATUS.md](#history-vmbs-cp59-status-md)
- [VMBS/CP58_STATUS.md](#history-vmbs-cp58-status-md)
- [VMBS/CP57_STATUS.md](#history-vmbs-cp57-status-md)
- [VMBS/CP56_STATUS.md](#history-vmbs-cp56-status-md)
- [VMBS/CP55_STATUS.md](#history-vmbs-cp55-status-md)
- [VMBS/CP54_STATUS.md](#history-vmbs-cp54-status-md)
- [VMBS/CP53_STATUS.md](#history-vmbs-cp53-status-md)
- [VMBS/CP51_STATUS.md](#history-vmbs-cp51-status-md)
- [VMBS/CP50_STATUS.md](#history-vmbs-cp50-status-md)
- [VMBS/CP48_STATUS.md](#history-vmbs-cp48-status-md)
- [VMBS/CP46_STATUS.md](#history-vmbs-cp46-status-md)
- [VMBS/CP45_STATUS.md](#history-vmbs-cp45-status-md)
- [VMBS/CP43_STATUS.md](#history-vmbs-cp43-status-md)
- [VMBS/CP42_STATUS.md](#history-vmbs-cp42-status-md)
- [VMBS/CP40_STATUS.md](#history-vmbs-cp40-status-md)
- [VMBS/CP39_STATUS.md](#history-vmbs-cp39-status-md)
- [VMBS/CP38_STATUS.md](#history-vmbs-cp38-status-md)
- [VMBS/CP37_STATUS.md](#history-vmbs-cp37-status-md)
- [VMBS/CP36_STATUS.md](#history-vmbs-cp36-status-md)
- [VMBS/CP35_STATUS.md](#history-vmbs-cp35-status-md)
- [VMBS/CP34_STATUS.md](#history-vmbs-cp34-status-md)
- [VMBS/CP33_STATUS.md](#history-vmbs-cp33-status-md)
- [VMBS/CP32_STATUS.md](#history-vmbs-cp32-status-md)
- [VMBS/CP31_STATUS.md](#history-vmbs-cp31-status-md)
- [VMBS/CP30_STATUS.md](#history-vmbs-cp30-status-md)
- [VMBS/CP29_STATUS.md](#history-vmbs-cp29-status-md)
- [VMBS/CP28_STATUS.md](#history-vmbs-cp28-status-md)
- [VMBS/CP27_STATUS.md](#history-vmbs-cp27-status-md)
- [VMBS-VMB-COMPILED-CARD-STITCH-CP26.md](#history-vmbs-vmb-compiled-card-stitch-cp26-md)
- [VMBS/CP26_STATUS.md](#history-vmbs-cp26-status-md)
- [VMBS/CP25_STATUS.md](#history-vmbs-cp25-status-md)
- [VMBS/CP24_STATUS.md](#history-vmbs-cp24-status-md)
- [VMBS/CP23_STATUS.md](#history-vmbs-cp23-status-md)
- [VMBS/CP22_STATUS.md](#history-vmbs-cp22-status-md)
- [VMBS/CP21_STATUS.md](#history-vmbs-cp21-status-md)
- [VMBS/CP20_STATUS.md](#history-vmbs-cp20-status-md)
- [VMBS/CP19_STATUS.md](#history-vmbs-cp19-status-md)
- [VMBS/CP18_STATUS.md](#history-vmbs-cp18-status-md)
- [VMBS/CP17_STATUS.md](#history-vmbs-cp17-status-md)
- [VMBS/CP16_STATUS.md](#history-vmbs-cp16-status-md)
- [VMBS/CP15_STATUS.md](#history-vmbs-cp15-status-md)
- [VMBS/CP14_STATUS.md](#history-vmbs-cp14-status-md)
- [VMBS/CP13_STATUS.md](#history-vmbs-cp13-status-md)
- [VMBS/CP12_STATUS.md](#history-vmbs-cp12-status-md)
- [VMBS/CP11_STATUS.md](#history-vmbs-cp11-status-md)
- [VMBS/CP10_STATUS.md](#history-vmbs-cp10-status-md)
- [VMBS/CP09_STATUS.md](#history-vmbs-cp09-status-md)
- [VMBS/CP08_STATUS.md](#history-vmbs-cp08-status-md)
- [VMBS/CP07_STATUS.md](#history-vmbs-cp07-status-md)
- [VMBS/analysis_cp06/CP06_STATUS.md](#history-vmbs-analysis-cp06-cp06-status-md)
- [VMBS/analysis_cp05/CP05_STATUS.md](#history-vmbs-analysis-cp05-cp05-status-md)
- [VMBS/analysis_cp04/CP04_STATUS.md](#history-vmbs-analysis-cp04-cp04-status-md)
- [VMBS/analysis_cp03/CP03_STATUS.md](#history-vmbs-analysis-cp03-cp03-status-md)
- [VMBS/analysis_cp02/CP02_STATUS.md](#history-vmbs-analysis-cp02-cp02-status-md)
- [VMBS/analysis/CP01_STATUS.md](#history-vmbs-analysis-cp01-status-md)
- [BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-03.md](#history-bios-cmos-backplane-refit-checkpoint-03-md)
- [BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-02.md](#history-bios-cmos-backplane-refit-checkpoint-02-md)
- [BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT.md](#history-bios-cmos-backplane-refit-checkpoint-md)
- [Motherboard Backplane Refit/CHECKPOINT-06.md](#history-motherboard-backplane-refit-checkpoint-06-md)
- [Motherboard Backplane Refit/CHECKPOINT-05.md](#history-motherboard-backplane-refit-checkpoint-05-md)
- [Motherboard Backplane Refit/CHECKPOINT-04.md](#history-motherboard-backplane-refit-checkpoint-04-md)
- [Motherboard Backplane Refit/CHECKPOINT-03.md](#history-motherboard-backplane-refit-checkpoint-03-md)
- [Motherboard Backplane Refit/CHECKPOINT-02.md](#history-motherboard-backplane-refit-checkpoint-02-md)
- [Motherboard Backplane Refit/CHECKPOINT-01.md](#history-motherboard-backplane-refit-checkpoint-01-md)
- [80486 Construction Checkpoints/CHECKPOINT-05B-SEMANTIC-CORRECTIONS.md](#history-80486-construction-checkpoints-checkpoint-05b-semantic-corrections-md)
- [80486 Construction Checkpoints/CHECKPOINT-04.md](#history-80486-construction-checkpoints-checkpoint-04-md)
- [80486 Construction Checkpoints/CHECKPOINT-03.md](#history-80486-construction-checkpoints-checkpoint-03-md)
- [80486 Construction Checkpoints/CHECKPOINT-02.md](#history-80486-construction-checkpoints-checkpoint-02-md)
- [Board Scripts/BOARD-SCRIPT-PROTOTYPE-CHANGESET.txt](#history-board-scripts-board-script-prototype-changeset-txt)
- [Board Scripts/CHECKPOINT-00.md](#history-board-scripts-checkpoint-00-md)
- [PATCH-NOTES.txt](#history-patch-notes-txt)
- [Original dated VMB changelog](#original-vmb-changelog)

<a id="history-vmbs-forensic-session-md"></a>

## VMBS/FORENSIC_SESSION.md

Original location: `VMBS/FORENSIC_SESSION.md`. Relative paths in this historical report refer to that original directory.


## CP213 final successful repair — 2026-10-03
- Replaced fragile sequential contour assumptions with endpoint-graph traversal.
- Corrected legacy KiCad arc endpoint orientation against the actual connected Edge.Cuts evidence.
- Successful result: 11 Edge.Cuts primitives -> closed 96-vertex contour; 22 courtyard collision envelopes; 12 explicit missing-courtyard gaps.


## CP214–CP238 sprint — 2026-10-03
# CP214–CP238 — 25-Part Solver Sprint

CP214-CP238 integrated regressions: 21/21 PASS

- CP214: polygon geometry predicates
- CP215: finite pose-domain generator
- CP216: courtyard collision engine
- CP217: mechanical edge policy
- CP218: routing clearance grid
- CP219: deterministic net ordering
- CP220: negotiated congestion cost
- CP221: rip-up/reroute state
- CP222: HPWL objective
- CP223: move generator
- CP224: tabu memory
- CP225: improvement selector
- CP226: timing slack equations
- CP227: RC estimate
- CP228: logic-level margins
- CP229: open-collector pull rule
- CP230: power-domain intersection
- CP231: fanout budget
- CP232: feasibility precedence
- CP233: provenance records
- CP234: violation reports
- CP235: determinism hash
- CP236: TargetMechanicalProfile schema
- CP237: solver feedback state machine
- CP238: integrated regression suite

These are coded/tested primitives, not a claim that ISA8 has been automatically placed/routed/verified.


# CP239–CP263 — 25-Part Real-Specimen Solver Integration Sprint

Real ISA8 specimen results:
- 34 BS components; 135 nets
- 22 components with courtyard collision geometry; 11 explicit geometry gaps
- 32 source-CAD courtyard overlap pairs reported for inspection, not silently 'fixed'
- hard identity/anchor conflicts: 0
- forced-overlap mutation detected: PASS
- deterministic byte-for-byte repeat: PASS
- result: SOLVED_UNVERIFIED

- CP239: reconcile BS component inventory with geometry identities
- CP240: derive real outline metrics
- CP241: audit source courtyard collisions
- CP242: audit board-boundary crossing without rejecting edge hardware
- CP243: hash locked source placements deterministically
- CP244: normalize real net endpoint counts
- CP245: build real fanout histogram
- CP246: classify singleton/empty nets
- CP247: measure component geometry coverage
- CP248: classify missing-geometry gaps
- CP249: enforce reconstruction locked-placement policy
- CP250: cross-check CP212 physical anchors against CP213 poses
- CP251: hash net-name set
- CP252: hash topology summary
- CP253: hash real board geometry
- CP254: mutation regression: forced overlap must be detected
- CP255: retain closed-contour evidence invariant
- CP256: synthesize hard-conflict versus evidence-gap result
- CP257: gate SOLVED_VERIFIED versus SOLVED_UNVERIFIED
- CP258: emit explainable hard/gap records
- CP259: generate stable integrated-run signature
- CP260: compute routing readiness
- CP261: compute Circuit Chess readiness
- CP262: refuse Physics-ready claim without complete evidence
- CP263: integrated real ISA8 specimen regression + deterministic repeat

Boundary: this sprint integrates and validates the real specimen. It does not claim free-placement, completed routing, or Physics Referee verification. Existing source-CAD courtyard overlaps are evidence to inspect, not proof that the historical board is invalid.

## CP264–CP288 solver continuation — 2026-10-04
- FOUND canonical Library source: `/Projects/VMBS/Canonical current source/VMBS_VMB_MASTER_CP263_real_specimen_25part_sprint.zip`.
- ACQUIRED/materialized that exact Library checkpoint; older `/Projects/VMB` checkpoint was rejected as continuation authority.
- INSPECTED CP263 and CP214–CP238 solver primitives before modification.
- IMPLEMENTED `solver/real_board_router.py`: deterministic KiCad pad/net extraction, footprint transforms, finite grid, courtyard occupancy, A*, congestion penalty, multi-terminal tree routing, stable provenance digests, explicit TITS_UP no-path failures.
- TESTED new primitive suite: 13/13 PASS.
- VERIFIED real ISA8 integration gates: 12/12 PASS.
- VERIFIED 364 pads; 87 routable multi-terminal nets; 87 coarse deterministic routes at 2.54 mm grid; deterministic repeat byte-identical; max occupancy 13.
- VERIFIED controlled no-path mutation produces failure rather than fabricated route.
- VERIFIED Python syntax with `py_compile`; prior CP214–CP238 regression remains 21/21 PASS; search-kernel regression run.
- DECISION: do not claim routing completion. Congestion, layer/via/width/netclass projection and electrical/timing closure remain outstanding, so solver remains SOLVED_UNVERIFIED.
- Sol-Stud remains an interface horizon only; no Solution Studio implementation was started.

## CP289–CP313 solver continuation — 2026-10-04
- Re-listed canonical Library source before touching code; authoritative head was CP288 `VMBS_VMB_MASTER_CP288_real_router_integration.zip`, not stale CP263.
- Materialized and extracted exact CP288 canonical package.
- Inspected CP288 status, router, regression suite, Sol-Stud horizon note, and recent forensic log.
- IMPLEMENTED `solver/physical_router.py`: real pad copper-layer extraction including legacy KiCad Front/Back aliases, pad sizes, netclass width/clearance/via evidence, deterministic two-layer routing states, explicit via transitions, finite board bounds, congestion cost, multi-terminal trees, path compression, physical length/via metrics, stable route digest, and explicit missing-evidence semantics.
- ADDED `solver/test_sprint_289_313.py` covering CP289–CP312 primitives; 24/24 PASS.
- GENERATED `solver/ISA8_Ethernet.physical_router.cp313.json` from the real ISA8 specimen.
- REAL RESULT: 364 pads, 87 multi-terminal nets, 87 deterministic candidate route trees, 120 candidate vias, 8267.7 mm coarse candidate path length, 984 shared routing states, max usage 9.
- VERIFIED byte-for-byte deterministic repeat and route digest `75fa7deff53d89cb97281d43c6576c600f5c0f9a6731aa3ee4fe3ca50e430855`.
- VERIFIED prior CP264–288 13/13, CP214–238 21/21, CP211 5/5; Python py_compile PASS.
- DECISION: do not claim routing closure. Shared-state congestion and incomplete physical/electrical rules keep state SOLVED_UNVERIFIED.
- CURRENT ARC: finish deterministic solver closure before UI foundation work; UI consumers should read solver/BS truth rather than duplicate solver logic.

## CP314–CP338 solver continuation — 2026-10-04
- Re-listed canonical Library source before code changes; authoritative head was CP313 `VMBS_VMB_MASTER_CP313_two_layer_physical_router.zip`.
- Materialized and extracted exact CP313 canonical package.
- Inspected CP313 status, physical router, real-board adapter, tests, and forensic continuation state.
- IMPLEMENTED `solver/negotiated_router.py`: trace width/clearance envelopes, via envelopes on both copper layers, other-net pad keepouts, physical occupancy accounting, conflict scoring, history costs, and deterministic PathFinder-style rip-up/reroute.
- ADDED `solver/test_sprint_314_338.py`: 24/24 primitive assertions PASS.
- GENERATED `solver/ISA8_Ethernet.negotiated_router.cp338.json` from the real ISA8 specimen.
- REAL RESULT: 364 pads; 87/87 candidate nets routed. Physical-envelope congestion improved from 7,226 shared states / 16,979 excess occupancy to 5,833 / 13,162; max usage 11; 391 candidate vias; 8,760.46 mm centerline length.
- VERIFIED deterministic repeat byte-for-byte; digest `fa883904efeeba83e30330df701393bfabfe891cd724f2dc2b8d2ef359123fc6`.
- VERIFIED prior CP289–313 24/24, CP264–288 13/13, CP214–238 21/21, CP211 5/5; Python py_compile PASS.
- DECISION: physical-envelope metrics supersede centerline-sharing metrics for closure. CP313 984 centerline overlaps and CP338 5,833 envelope conflicts are not directly comparable.
- DECISION: do not claim routing closure. Existing copper/zones/keepouts, board-edge clearance, differential-pair constraints, stronger global negotiation, and Physics Referee remain outstanding. State stays SOLVED_UNVERIFIED.
- CURRENT ARC remains solver completion before UI foundations; SolStud/NeoStud consume shared deterministic solver/BS truth rather than duplicate it.

## CP339–CP363 continuation — existing copper evidence

Canonical continuation source was Library `VMBS_VMB_MASTER_CP338_negotiated_physical_router.zip` (not an older local checkpoint). Added deterministic `solver/existing_copper.py`, `solver/integrate_existing_copper.py`, CP339–363 regression gates, and real ISA8 CP363 artifact. Real specimen extraction: 1219 segments, 95 vias, 5 zone polygons, 87 copper-bearing nets, zero extraction geometry gaps. Existing-copper projection preserves copper layer, same-net exemption, finite board bounds, trace/via physical envelope behavior, and explicit evidence gaps. No AI/ML added. Zone polygon projection is intentionally not represented as proof of exact poured-fill/thermal topology. Full solver regression suite and py_compile passed; deterministic CP363 artifact repeat byte-identical. Next logical sprint: wire this evidence layer into controlled route/collision mutations and Physics Referee provenance, then add explicit keepout/edge-clearance extraction only when supported by board evidence.

## CP364–CP388 continuation — existing-copper referee and hard-obstacle integration

- FOUND canonical Library head CP363 and ACQUIRED/materialized that exact package; no older local checkpoint used as authority.
- INSPECTED CP363 existing-copper extractor, negotiated router, regression suite, status and forensic tail.
- IMPLEMENTED `solver/copper_referee.py` with deterministic route-vs-explicit-copper collision records and provenance.
- REMOVED a circular dependency by localizing the finite disk raster primitive in `existing_copper.py`; behavior remains explicitly tested.
- WIRED CP363 existing-copper evidence into `negotiated_router.py` as route-specific hard obstacles using the netclass clearance already evidenced by the board.
- ADDED `solver/test_sprint_364_388.py`; 25/25 PASS.
- VERIFIED all prior solver regression families and Python syntax.
- REAL ISA8 RESULT: with explicit existing copper hard-blocking at 1.27 mm grid, 0/87 candidate nets find a fresh route; all 87 return `TITS_UP_ROUTE_NO_PATH`; evidence extraction gaps remain 0.
- VERIFIED real result byte-for-byte deterministic on repeat.
- DECISION: this negative result is not evidence the historical PCB is unroutable. The current router exempts same-net copper from obstacles but does not yet import it as reusable connected route-tree topology. That missing representation is the next integration target.
- DECISION: preserve reconstruction-audit versus forward-synthesis distinction; do not manufacture zone fill, electrical, timing, mechanical, or historical facts.
- Sol-Stud remains a future deterministic handoff horizon only; no Sol-Stud implementation started.

## CP389–CP413 continuation — historical topology reconstruction and reuse

- FOUND canonical Library head CP388 at `/Projects/VMBS/Canonical current source/VMBS_VMB_MASTER_CP388_copper_referee_integration.zip` and ACQUIRED/materialized that exact package; no stale local checkpoint used as authority.
- INSPECTED CP388 existing-copper extractor/referee, negotiated router, physical router primitives, regression families, Sol-Stud horizon note, and forensic continuation state.
- IMPLEMENTED `solver/historical_topology.py`: deterministic same-net segment/via/zone raster, pad copper geometry, finite board clipping, connected-component/island reconstruction, pad-to-explicit-copper contact audit, explicit unresolved/TITS_UP records, and stable provenance digest.
- IMPLEMENTED `solver/historical_reuse_router.py`: historical copper/pads become reusable same-net topology islands; disconnected islands are never assumed connected. Deterministic A* may add only explicit joining branches while other-net historical copper remains a hard obstacle.
- ADDED `solver/test_sprint_389_413.py`; 25/25 PASS.
- REAL ISA8 topology audit at 1.27 mm: 134 nonzero named nets inspected; 7 have all represented pads joined to one explicit raster component; 127 remain unresolved; 212 explicit pad/copper evidence gaps. State remains TITS_UP rather than inventing missing historical connectivity.
- REAL ISA8 multi-terminal reuse run: 87 candidate nets; 13 are already unified under the coarse explicit historical topology and require zero new branch length; 74 still return explicit no-path under current hard-obstacle/grid model. This improves the CP388 abstraction (same-net copper is now topology, not merely exempt obstacle) without claiming the historical PCB was complete or electrically verified.
- VERIFIED CP389–413 25/25, CP364–388 25/25, CP339–363 24/24, CP314–338 24/24, CP289–313 24/24, CP264–288 13/13, CP214–238 21/21, CP211 5/5, full solver `py_compile`, and byte-identical repeat of the CP413 real reuse artifact.
- DECISION: keep HISTORICAL_AUDIT_REPAIR distinct from FORWARD_SYNTHESIS. Existing zones remain explicit polygon-interior evidence only; no unsupported poured-fill/thermal/electrical/timing facts are promoted.
- NEXT: improve historical connectivity fidelity using exact segment/pad/via contact geometry before lowering grid resolution or relaxing obstacles; then feed proven topology plus added branches through Physics Referee electrical/RC/timing gates. Sol-Stud remains interface horizon only.


## 2026-10-04 — CP414–CP438 exact copper-contact sprint
- FOUND/IDENTIFIED: Library canonical head CP413 (`VMBS_VMB_MASTER_CP413_historical_topology_reuse.zip`).
- ACQUIRED: exact CP413 Library artifact materialized and extracted as sprint source.
- INSPECTED: CP413 historical topology/reuse code, physical pad extraction, existing KiCad copper extraction, and CP389–413 gates.
- CP414–438: added deterministic analytic contact primitives for point/segment, segment/segment, polygon, pad/segment, pad/via, pad/zone, segment/via, segment/zone, via/via, via/zone, and zone/zone contact; preserved KiCad pad shape and roundrect ratio evidence; implemented circle/oval/rect/roundrect contact handling; built same-net exact copper connectivity graph and provenance digest; added real ISA8 exact-topology audit.
- FAILURE FOUND DURING SPRINT: symmetric contact dispatch initially recursed for unsupported pair ordering. Fixed by explicit reversible-pair dispatch; reran tests.
- REAL ISA8 INSPECTION: 134 named nonzero nets, 364 pads, 124 pads analytically contacting explicit extracted copper, 240 pad-to-explicit-copper gaps, zero copper extraction gaps. No net is declared historically connected by the exact explicit-contact graph at this stage. This is evidence, not a claim that the physical board was disconnected: zone fill/thermal semantics and additional exact pad/copper topology still require representation.
- Evidence policy preserved: missing proof remains TITS_UP; no grid coincidence promoted to historical fact.


## 2026-10-04 — CP439–CP463 stored zone-fill evidence sprint
- FOUND/IDENTIFIED: canonical Library head CP438 exact copper contact.
- ACQUIRED/INSPECTED: CP438 archive and real ISA8 KiCad source.
- IDENTIFIED in real specimen: 5 KiCad zones containing 8 stored `filled_polygon` islands; 1,219 explicit segments; 95 vias. Zone fill rules explicitly store thermal gap 0.508 mm, thermal bridge width 0.508 mm, and connect-pad clearance 0.3556 mm.
- CP439–463: existing-copper extraction now preserves stored filled polygons and explicit zone thermal/connect-pad parameters. Exact topology uses actual filled copper rather than permissive zone outlines. Disjoint filled polygons are separate topology graph nodes so one zone object cannot falsely bridge isolated copper islands. Missing fill remains `TITS_UP_ZONE_FILL_NOT_EXPLICIT`.
- REAL ISA8 RESULT: 134 nets / 364 pads; 121 pads proven touching represented explicit copper; 243 unresolved pad-contact evidence gaps; 0 extraction gaps. No complete net is yet promoted to VERIFIED connected. Stored fill made the evidence model stricter: three prior outline-based contacts correctly disappeared.
- A transient interactive Python import used an older cached module and raised KeyError while inspecting new fill fields; rerun in a clean subprocess verified 1,219 segments, 95 vias, 5 zones, 8 fill islands, 0 extraction gaps. No checkpoint claim was based on the failed inspection.


## 2026-10-04 — CP464–CP488 coordinate truth / connectivity classification sprint
- FOUND/IDENTIFIED/ACQUIRED: canonical Library CP463 and checksum; CP463 used as sole continuation source.
- Root cause identified from real ISA8 evidence: legacy KiCad module-local pad coordinates require inverse footprint rotation in board coordinates. Example RN4 pad 7 source `(at 0.9 -0.4 90)` under module `(at 157.48 74.93 90)` lands at `(157.08,74.03)`, exactly matching its explicit trace endpoint; the previous transform placed it at `(157.88,75.83)`.
- Corrected deterministic footprint transform and pad orientation convention; updated obsolete synthetic transform regression to the specimen-verified convention.
- Added oriented rect/circle/oval/roundrect pad contact geometry. Contact tests now operate in pad-local coordinates.
- Distinguished single-terminal isolated nets from multi-terminal required connectivity. A one-pad net with no copper is preserved as explicit isolated evidence, not counted as a board-connectivity failure.
- REAL ISA8 after correction: 134 named nets, 364 pads, 311 pads touching represented explicit copper. 47 isolated single-terminal nets are classified separately. Of 87 multi-terminal nets, 85 are analytically CONNECTED; only GND and VCC remain UNRESOLVED, with six U1 power/ground pad contacts still TITS_UP. Zero extraction gaps.
- Controlled CP464–488 gates added; prior regressions retained. Full syntax and deterministic repeat checks pass.


## 2026-10-04 — CP489–CP513 ISA8 historical connectivity closure
- FOUND/IDENTIFIED/ACQUIRED: canonical Library CP488 coordinate-truth checkpoint used as sole continuation source.
- INSPECTED: the six remaining U1 GND/VCC pads (14,17,28,47,57,70), their same-net traces, vias and stored zone fills.
- IDENTIFIED: every remaining pad has an explicit F.Cu trace terminating 0.875 mm from pad center. With 2.5 x 0.4 mm roundrect pads this is a real long-axis contact. Raw KiCad evidence shows pad rotations 90/180 degrees. In this legacy specimen, pad position is footprint-local/inverse-rotated, while the pad's stored rotation value is already board orientation; subtracting footprint rotation was incorrect.
- FIXED: physical extraction now preserves pad orientation as the stored pad rotation `pr`, independent of the footprint coordinate transform used for pad center.
- VERIFIED REAL ISA8: 134 named nonzero nets / 364 pads; 317 pads touch represented explicit copper; 47 single-terminal isolated nets; all 87 multi-terminal nets analytically VERIFIED_CONNECTED; zero required connectivity evidence gaps; zero extraction gaps. GND and VCC close without inference.
- Added CP489–513 controlled gates for legacy pad orientation, long-axis trace landing, layer enforcement, roundrect geometry, graph closure, no-copper TITS_UP behavior and deterministic provenance.
- Added `ISA8_Ethernet.connectivity_proof.cp513.json` as deterministic Tracer/Physics Referee handoff: 87/87 multi-terminal nets VERIFIED_CONNECTED, single-terminal nets explicitly classified separately.
- Full sprint regression stack PASS; full solver py_compile PASS; repeated real-board audit byte-identical.

---

<a id="history-vmbs-solver-status-cp413-md"></a>

## VMBS/SOLVER_STATUS_CP413.md

Original location: `VMBS/SOLVER_STATUS_CP413.md`. Relative paths in this historical report refer to that original directory.

# VMBS Solver Status — CP413

Canonical trajectory: deterministic Circuit Chess / Tracer / Physics Referee integration. No AI/ML.

CP413 adds historical same-net copper topology reconstruction and reuse. Existing copper is no longer treated only as an obstacle exemption: explicit segments, vias, zones, and pad copper are grouped into deterministic topology islands. Islands are not assumed connected unless the explicit geometry joins them. Tracer may attempt deterministic joining branches while other-net historical copper remains a hard obstacle.

Real ISA8 at 1.27 mm remains evidence-limited: 134 named nonzero nets inspected, 7 fully joined at this coarse topology representation, 127 unresolved, 212 explicit pad/copper contact gaps. For the 87 multi-terminal routing candidates, 13 require no new joining branch under the current representation and 74 remain TITS_UP_ROUTE_NO_PATH. This is not a claim that the historical PCB is unroutable or incomplete; exact contact geometry and historical fill fidelity are the next audit targets.

Forward synthesis remains separate. Sol-Stud is a future deterministic handoff source for functional intent, exact Component Store identities, resources/interfaces, programmed payloads, mechanical target/profile, fixed/bounded/free choices, and explicit unknowns. It is not implemented here.

---

<a id="history-vmbs-cp388-status-md"></a>

## VMBS/CP388_STATUS.md

Original location: `VMBS/CP388_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP364–CP388 — Existing-Copper Referee / Router Integration Sprint

Continuation source: canonical Library CP363. This sprint wires explicit KiCad copper evidence into deterministic route legality and provenance. No AI/ML and no invented electrical/timing facts.

- CP364–370: explicit other-net collision, same-net exemption, layer separation, provenance, clearance behavior
- CP371–377: route audit PASS/VIOLATION/TITS_UP states, missing-clearance evidence gap, stable counts/digest
- CP378–386: order invariance, bounds, via/zone semantics, multi-net deterministic provenance, empty audit
- CP387: referee emits only evidence-backed copper collision semantics
- CP388: real ISA8 hard-obstacle integration and deterministic repeat

## Real ISA8 result

When CP363 explicit existing copper is promoted from an inspection layer to hard routing obstacles at the current 1.27 mm coarse grid, all 87 candidate multi-terminal nets deterministically return `TITS_UP_ROUTE_NO_PATH`. This is an important negative result, not routing closure. The solver does not fabricate a path.

Interpretation is deliberately narrow: the current coarse-grid fresh-route model cannot legally route the already-routed historical specimen while treating all explicit other-net copper envelopes as hard obstacles. Existing same-net copper is exempt, but it is not yet imported as reusable connected topology. The next increment must model existing same-net copper as route-tree connectivity / reusable trunks (and distinguish reconstruction audit from fresh synthesis) before this hard-obstacle mode can be judged as a design router.

Evidence extraction itself still reports zero geometry gaps. No claim is made that the historical board is unroutable, electrically invalid, or that exact zone-fill/thermal topology is known.

## Verification

- CP364–388 primitive suite: 25/25 PASS
- CP339–363: 24/24 PASS
- CP314–338: 24/24 PASS
- CP289–313: 24/24 PASS
- CP264–288: 13/13 PASS
- CP214–238: 21/21 PASS
- CP211: 5/5 PASS
- `py_compile solver/*.py`: PASS
- Real CP388 artifact deterministic repeat: byte-for-byte PASS

State: `SOLVED_UNVERIFIED` / explicit `TITS_UP_ROUTE_NO_PATH` at the current copper-aware coarse routing gate.

---

<a id="history-vmbs-cp363-status-md"></a>

## VMBS/CP363_STATUS.md

Original location: `VMBS/CP363_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP339–CP363 — Existing Copper Evidence Integration Sprint

25 deterministic solver increments continue from CP338. This sprint adds explicit extraction and finite-grid projection of copper already present in a KiCad specimen. It does not infer copper, electrical rules, zone fill behavior, or historical intent.

- CP339–340 deterministic segment raster primitives
- CP341–342 polygon inclusion/exclusion primitives
- CP343–345 layer-preserving segment obstacles, same-net exemption, clearance expansion
- CP346–348 two-layer via obstacles, same-net exemption, clearance expansion
- CP349–351 zone polygon layer projection, same-net exemption, finite board clipping
- CP352–360 deterministic/bounded geometry and identity invariants
- CP361 finite zone projection
- CP362 explicit evidence-gap contract
- CP363 integrated real ISA8 existing-copper artifact

## Real ISA8 specimen

FOUND/INSPECTED from canonical CP338 lineage: 1,219 explicit segments, 95 vias, 5 zone polygons, 87 copper-bearing nets. Extraction reported 0 unsupported/missing-geometry gaps. The CP363 artifact projects explicit specimen copper at 1.27 mm grid and records per-net other-net obstacle counts. Same-net copper is exempt by identity. Zone polygon interiors are projected as explicit occupied evidence; the solver does **not** pretend polygon outline alone proves exact historical poured-fill/thermal topology.

Artifact: `solver/ISA8_Ethernet.existing_copper.cp363.json`
Deterministic repeat: byte-for-byte PASS.

This does not claim routing closure. It supplies a new evidence layer for Tracer/Circuit Chess and controlled mutation/referee work. Exact edge-clearance, keepout semantics, filled-zone topology, differential-pair rules and electrical/timing closure remain explicit future gates/TITS_UP where required.

## Regression state

- CP339–CP363: 24/24 primitive PASS + CP363 real integration PASS
- CP314–CP338: 24/24 PASS
- CP289–CP313: 24/24 PASS
- CP264–CP288: 13/13 PASS
- CP214–CP238: 21/21 PASS
- CP211: 5/5 PASS
- Python `py_compile`: PASS

---

<a id="history-vmbs-cp338-status-md"></a>

## VMBS/CP338_STATUS.md

Original location: `VMBS/CP338_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP314–CP338 — Negotiated Physical-Clearance Router Sprint

25 deterministic solver increments were added on top of CP313. This sprint changes congestion accounting from centerline sharing to physical copper envelopes and adds deterministic negotiated rip-up/reroute.

- CP314: deterministic disk-envelope primitive
- CP315: trace width + clearance corridor radius
- CP316: via diameter + clearance envelope radius
- CP317: layer-specific planar copper occupancy
- CP318: via occupancy projected onto both copper layers
- CP319: other-net pad copper keepouts
- CP320: own-net pad exemption
- CP321: SMD keepouts restricted to actual copper layer
- CP322: through-hole pad keepouts projected to both layers
- CP323: physical occupancy usage accounting
- CP324: physical conflict-state extraction
- CP325: per-route conflict scoring
- CP326: complete netclass routing-rule gate
- CP327: missing via evidence remains TITS UP
- CP328: deterministic history-cost state
- CP329: finite obstacle no-path behavior
- CP330: legal alternate-layer escape
- CP331: via metrics preserved through routed branches
- CP332: centerline expanded to physical copper envelope
- CP333: clearance monotonically expands occupancy
- CP334: via diameter monotonically expands via envelope
- CP335: excess-occupancy metric
- CP336: deterministic conflicted-net victim ordering
- CP337: explicit routing-closure gate
- CP338: integrated real ISA8 negotiated-router artifact

## Real specimen result

At 1.27 mm grid on the real ISA8 Ethernet specimen:

- 364 pads
- 87 candidate multi-terminal nets
- 87/87 routed candidate trees
- physical-envelope initial congestion: 7,226 shared states / 16,979 excess occupancy
- negotiated result: 5,833 shared states / 13,162 excess occupancy
- reduction: 1,393 shared states (19.28%) and 3,817 excess occupancy (22.48%)
- maximum physical-envelope state usage: 11
- 8 requested negotiation passes; stable best reached after pass 3 and stopped after pass 4
- 391 candidate vias
- 8,760.46 mm candidate centerline length
- deterministic byte-for-byte repeat: PASS
- route digest: `fa883904efeeba83e30330df701393bfabfe891cd724f2dc2b8d2ef359123fc6`

CP313's 984 shared *centerline* states are not directly comparable to CP338's 5,833 shared *physical-envelope* states. CP338 intentionally counts trace width, clearance and via envelopes, so its metric is stricter.

This is still **not routing closure**. Existing-copper/zone obstacles, explicit KiCad keepout extraction, edge-clearance rules, differential-pair constraints, more capable negotiated reroute/global ordering, and Physics Referee closure remain outstanding. State remains `SOLVED_UNVERIFIED`.

## Regression state

- CP314–CP338 primitive regressions: 24/24 PASS + real CP338 integration PASS
- CP289–CP313 primitive regressions: 24/24 PASS
- CP264–CP288 primitive regressions: 13/13 PASS
- CP214–CP238 integrated regressions: 21/21 PASS
- CP211 search-kernel regressions: 5/5 PASS
- Python `py_compile`: PASS

---

<a id="history-vmbs-cp313-status-md"></a>

## VMBS/CP313_STATUS.md

Original location: `VMBS/CP313_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP289–CP313 — Two-Layer Physical Routing Kernel Sprint

25 deterministic solver increments were added on top of CP288. This sprint moves the router from coarse single-plane path evidence toward a PCB-aware two-copper-layer routing kernel while preserving explicit evidence gaps.

- CP289: canonical two-copper-layer state (`F.Cu`, `B.Cu`)
- CP290: through-hole `*.Cu` projection
- CP291: SMD single-copper-layer projection
- CP292: physical pad-size extraction
- CP293: KiCad netclass rule extraction
- CP294: deterministic mm/grid conversion
- CP295: clearance-to-grid inflation primitive
- CP296: pad rectangle occupancy primitive
- CP297: strict finite board-grid bounds
- CP298: layer-independent board-bound test
- CP299: planar layer-state neighbors
- CP300: explicit via layer transitions
- CP301: blocked-state avoidance
- CP302: deterministic A* repeatability
- CP303: via-cost preference
- CP304: layer-only destination via requirement
- CP305: lossless route path compression
- CP306: physical route length metrics
- CP307: multi-terminal routed tree
- CP308: endpoint copper-layer legality
- CP309: congestion-aware state cost
- CP310: explicit no-path primitive
- CP311: stable route digest
- CP312: missing netclass evidence remains explicit / not guessed
- CP313: integrated real ISA8 physical-router evidence artifact

## Real specimen result

At a deliberately coarse 1.27 mm grid, the real ISA8 Ethernet KiCad specimen yields:

- 364 pads parsed with copper-layer/package evidence
- 87 multi-terminal candidate nets
- 87/87 candidate nets receive deterministic two-layer candidate trees
- 120 candidate vias
- 8267.7 mm aggregate coarse candidate path length
- 984 shared routing states
- maximum state usage 9
- deterministic repeat: byte-for-byte PASS
- route digest: `75fa7deff53d89cb97281d43c6576c600f5c0f9a6731aa3ee4fe3ca50e430855`

This is deliberately **not routing closure**. Shared-state congestion remains substantial. The new kernel parses real KiCad trace width, clearance, via diameter and drill evidence and carries trace width/clearance into each route record, but full width/clearance occupancy, via keepout, existing-copper obstacles, copper zones, differential-pair constraints, layer-specific keepouts, negotiated rip-up/reroute closure, and Physics Referee electrical/timing closure remain to be completed. State therefore remains `SOLVED_UNVERIFIED`.

## Regression state

- CP289–CP313 primitive regressions: 24/24 PASS + real CP313 integration PASS
- CP264–CP288 primitive regressions: 13/13 PASS
- CP214–CP238 integrated regressions: 21/21 PASS
- CP211 search-kernel regressions: 5/5 PASS
- Python `py_compile`: PASS

---

<a id="history-vmbs-cp288-status-md"></a>

## VMBS/CP288_STATUS.md

Original location: `VMBS/CP288_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP264–CP288 — Real Pad/Net Routing Integration Sprint

25 deterministic solver increments were integrated and tested against the real ISA8 Ethernet KiCad specimen.

- CP264: deterministic KiCad S-expression tokenization
- CP265: balanced S-expression parser
- CP266: footprint/pad coordinate transform
- CP267: real pad endpoint extraction
- CP268: finite routing-grid snap
- CP269: component courtyard occupancy rasterization
- CP270: deterministic Manhattan A* search
- CP271: hard obstacle avoidance
- CP272: explicit `TITS_UP_ROUTE_NO_PATH` failure semantics
- CP273: negotiated congestion penalty
- CP274: deterministic multi-terminal tree growth
- CP275: repeatability check
- CP276: stable endpoint digest
- CP277: real specimen pad identity gate
- CP278: real specimen net fanout gate
- CP279: finite board routing bounds
- CP280: endpoint-owner courtyard exclusion
- CP281: deterministic net ordering via existing route-order policy
- CP282: real courtyard occupancy integration
- CP283: congestion accounting
- CP284: failure provenance records
- CP285: byte-for-byte route repeat regression
- CP286: controlled no-path mutation regression
- CP287: solver feedback remains unverified while congestion/electrical/timing evidence is incomplete
- CP288: integrated real-board routing evidence artifact

Real specimen result at 2.54 mm coarse routing grid: 364 pads extracted, 87 multi-terminal nets considered, 87 received deterministic coarse paths, max cell usage 13. This is **not** a PCB routing-complete claim: shared-cell congestion remains, the grid is intentionally coarse, copper-layer/via/track-width/netclass rules are not yet completely projected, and electrical/timing Physics Referee closure is still pending. Therefore state remains `SOLVED_UNVERIFIED`; unresolved required evidence remains explicit rather than inferred.

Route digest: `716d12a26deadf52d44cc1ea4baf17c6d67f2fca859146260eb13a56c2f8bb81`

---

<a id="history-vmbs-cp263-status-md"></a>

## VMBS/CP263_STATUS.md

Original location: `VMBS/CP263_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP239–CP263 — 25-Part Real-Specimen Solver Integration Sprint

Real ISA8 specimen results:
- 34 BS components; 135 nets
- 22 components with courtyard collision geometry; 11 explicit geometry gaps
- 32 source-CAD courtyard overlap pairs reported for inspection, not silently 'fixed'
- hard identity/anchor conflicts: 0
- forced-overlap mutation detected: PASS
- deterministic byte-for-byte repeat: PASS
- result: SOLVED_UNVERIFIED

- CP239: reconcile BS component inventory with geometry identities
- CP240: derive real outline metrics
- CP241: audit source courtyard collisions
- CP242: audit board-boundary crossing without rejecting edge hardware
- CP243: hash locked source placements deterministically
- CP244: normalize real net endpoint counts
- CP245: build real fanout histogram
- CP246: classify singleton/empty nets
- CP247: measure component geometry coverage
- CP248: classify missing-geometry gaps
- CP249: enforce reconstruction locked-placement policy
- CP250: cross-check CP212 physical anchors against CP213 poses
- CP251: hash net-name set
- CP252: hash topology summary
- CP253: hash real board geometry
- CP254: mutation regression: forced overlap must be detected
- CP255: retain closed-contour evidence invariant
- CP256: synthesize hard-conflict versus evidence-gap result
- CP257: gate SOLVED_VERIFIED versus SOLVED_UNVERIFIED
- CP258: emit explainable hard/gap records
- CP259: generate stable integrated-run signature
- CP260: compute routing readiness
- CP261: compute Circuit Chess readiness
- CP262: refuse Physics-ready claim without complete evidence
- CP263: integrated real ISA8 specimen regression + deterministic repeat

Boundary: this sprint integrates and validates the real specimen. It does not claim free-placement, completed routing, or Physics Referee verification. Existing source-CAD courtyard overlaps are evidence to inspect, not proof that the historical board is invalid.

---

<a id="history-vmbs-cp238-status-md"></a>

## VMBS/CP238_STATUS.md

Original location: `VMBS/CP238_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP214–CP238 — 25-Part Solver Sprint

CP214-CP238 integrated regressions: 21/21 PASS

- CP214: polygon geometry predicates
- CP215: finite pose-domain generator
- CP216: courtyard collision engine
- CP217: mechanical edge policy
- CP218: routing clearance grid
- CP219: deterministic net ordering
- CP220: negotiated congestion cost
- CP221: rip-up/reroute state
- CP222: HPWL objective
- CP223: move generator
- CP224: tabu memory
- CP225: improvement selector
- CP226: timing slack equations
- CP227: RC estimate
- CP228: logic-level margins
- CP229: open-collector pull rule
- CP230: power-domain intersection
- CP231: fanout budget
- CP232: feasibility precedence
- CP233: provenance records
- CP234: violation reports
- CP235: determinism hash
- CP236: TargetMechanicalProfile schema
- CP237: solver feedback state machine
- CP238: integrated regression suite

These are coded/tested primitives, not a claim that ISA8 has been automatically placed/routed/verified.

---

<a id="history-vmbs-cp213-status-md"></a>

## VMBS/CP213_STATUS.md

Original location: `VMBS/CP213_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP213 — Real Contour Geometry + Sol-Stud Boundary

- Real ISA8 Edge.Cuts: 11 primitives compiled into a closed 96-vertex collision contour.
- Legacy KiCad arcs are polygonized deterministically at <=5-degree chord steps.
- 22 components have world-space F.CrtYd collision envelopes.
- 12 components lacking courtyard evidence remain explicit gaps.
- Sol-Stud horizon handoff is documented only; Solution Studio implementation remains deferred.
- Reconstruction placement remains locked; novel pose-domain generation is the next solver step.

Development note: several failed contour implementations were retained in the forensic history. The final implementation uses endpoint-graph traversal and the verified legacy KiCad arc orientation from the source board.

---

<a id="history-vmbs-cp211-status-md"></a>

## VMBS/CP211_STATUS.md

Original location: `VMBS/CP211_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP211 — Deterministic Search Kernel

Actual coded solver search begins here; no AI/ML.

- CSP/backtracking placement with deterministic MRV selection
- hard bounds, keepouts, overlap and locked-placement checks
- adjacency constraints
- lexicographic scoring: adjacency distance, then occupied area
- deterministic A* grid maze router with explicit layer/via costs
- explicit UNSAT and traceable backtracking

CP211 regressions: 5/5 PASS
placement score: (3, 12) route cost: 9

Real ISA8 placement is intentionally not claimed yet: CAD/BS physical dimensions, outline, keepouts and legal domains still need normalization.

---

<a id="history-vmbs-cp210-status-md"></a>

## VMBS/CP210_STATUS.md

Original location: `VMBS/CP210_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP210 — Electrical Feasibility Foundation

Implemented `solver/evaluate_electrical.py`.

This pass adds enforceable electrical-screening structure without pretending the Store is more complete than it is.

## Implemented
- normalizes explicit component supply-voltage evidence into numeric windows
- screens typed paths for multiple known output-driver candidates
- keeps bidirectional enable semantics as an explicit prerequisite
- refuses to call a multiple-output path a proven short until output-stage / enable behavior is known
- returns `TITS_UP_MISSING_EVIDENCE` when active electrical verification is blocked by missing semantics
- mutation regression proves the contention-candidate detector fires when a path is deliberately changed to contain two known outputs

## ISA8 Ethernet result
- 2 component supply windows could be normalized from current Store evidence
- U2: 4.5–5.5 V
- U3: 2.5–5.5 V
- 0 contention candidates on the unmodified specimen
- 43 electrical knowledge gaps remain
- result: `TITS_UP_MISSING_EVIDENCE`

This is intentionally stricter than CP209 `PASS_WITH_GAPS`: CP210 is asking whether active electrical legality can be established, and currently it cannot.

## Next prerequisites
- output-stage typing: push-pull / tri-state / open-collector / open-drain
- enable-condition semantics for shared/bidirectional buses
- board rail declarations and power-pin semantics
- input threshold / output-level compatibility normalization
- pull-up/down requirement verification

---

<a id="history-vmbs-cp209-status-md"></a>

## VMBS/CP209_STATUS.md

Original location: `VMBS/CP209_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP209 — Interface Classification + Feasibility Gate

- CP208 untyped endpoints: 216
- genuinely unresolved after connector/passive classification: 44
- interface groups compiled: 2
- path classes: {"AMBIGUOUS_DIRECTION": 25, "BIDIRECTIONAL": 18, "DRIVERS_TO_EXTERNAL_SINK": 7, "DRIVER_RECEIVER": 2, "EXTERNAL_SOURCE_TO_RECEIVERS": 34, "TYPED_WITH_BOUNDARY_OR_PASSIVE": 1}
- feasibility: PASS_WITH_GAPS
- proven hard conflicts: 0
- retained knowledge gaps: 28

PASS_WITH_GAPS means only that currently enforceable checks found no contradiction.
It is not a manufacturability or full electrical-verification claim.

---

<a id="history-vmbs-cp208-status-md"></a>

## VMBS/CP208_STATUS.md

Original location: `VMBS/CP208_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP208 Status — Endpoint Semantics

Implemented:
- endpoint-to-Component-Store pin semantic join
- pin direction/role/sampling/interface-facet preservation
- conservative path-role classification
- explicit unresolved endpoint reporting
- evidence-backed device timing requirements kept separate from PCB route budgets

ISA8 Ethernet:
- 87 multi-endpoint paths
- 2 driver/receiver resolved
- 17 bidirectional
- 68 ambiguous
- 216 endpoints still untyped
- 23 device timing requirements
- 0 fabricated PCB route-delay budgets

Device access time or maximum clock rate is not automatically a PCB propagation-delay budget.
Source-cycle/interface timing evidence is still required before allocating one.

---

<a id="history-vmbs-cp207-status-md"></a>

## VMBS/CP207_STATUS.md

Original location: `VMBS/CP207_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP207 Status — Board-Specific Constraint Projection

CP207 moves the solver from Store-wide candidate facts to a concrete board instance.

Implemented:
- `solver/project_board_constraints.py`
- component identity projection
- selected footprint projection
- observed-placement historical constraints
- exact BS net-topology constraints
- named power-net classification without inferred voltages
- external-interface preservation
- programmed-content preservation/gap reporting
- topology path objects for later timing/electrical normalization

ISA8 Ethernet first pass:
- 34 components
- 135 nets
- 245 board-specific constraints
- 87 multi-endpoint topology paths
- 2 explicit verification gaps: unresolved programmed payload IDs
- 0 timed paths

The zero timed paths is intentional and important: CP207 refuses to convert raw component timing evidence into board path budgets until driver/receiver roles and timing semantics are normalized. No guessed nanoseconds.

Next pass: endpoint semantic typing and driver/receiver/path-role resolution, then derive only timing constraints that are actually supported by Component Store evidence.

---

<a id="history-vmbs-cp206-status-md"></a>

## VMBS/CP206_STATUS.md

Original location: `VMBS/CP206_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP206 Status

Head: CP206 — Solver architecture bootstrap

- Architecture/spec frozen for first solver boundary.
- Constraint IR schema added.
- Conservative Component Store audit compiler added.
- No placement/routing/floor planning implemented.
- First audit: 145 components -> 523 constraints, 75 explicit gaps.
- Next: BS/board-specific projection + typed electrical/timing normalization.

---

<a id="history-vmbs-cp205-status-md"></a>

## VMBS/CP205_STATUS.md

Original location: `VMBS/CP205_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP205 Status

Head: CP205

CP181–188 add 8 primary-TI HC/CD74HC identities with exact module identities and verified shared digital parents.
CP189–205 audit 17 existing HC package pin counts against TI product pages while deliberately leaving untranscribed exact pin maps TITS UP.

Validation:
- Component Store: 145 canonical / 2581 family / 10867 recognized variants / 0 errors
- Code Store: 196 module entries
- 37/37 currently present focused CP77+ test scripts PASS
- No .NET SDK build claim

---

<a id="history-vmbs-cp180-status-md"></a>

## VMBS/CP180_STATUS.md

Original location: `VMBS/CP180_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP180 status

CP156-180 add 25 additional Texas Instruments SN74HC identities. Each retains independent Component Store and Code Store identity while delegating only established digital behavior to the matching generic 74xx worker. Exact package pin maps remain TITS UP until transcribed from primary TI package diagrams.

Validation: 39 available CP77-CP180 focused regressions PASS; Component Store v3 validator PASS (137 canonical, 2581 family, 10867 variants); Code Store 188 modules. No .NET SDK build claim.

---

<a id="history-vmbs-cp155-status-md"></a>

## VMBS/CP155_STATUS.md

Original location: `VMBS/CP155_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP155 — Private G: 25-chip TI SN74HC authenticity set

CP131–155 add 25 exact Texas Instruments SN74HC identities with distinct physical/electrical Component Store truth and distinct Code Store module IDs while safely sharing already-regressed computer-visible 74xx behavior.

- Canonical researched Component Store records: 112
- Code Store module entries: 163
- Family records: 2,581
- Recognized variants: 10,867
- CP77+ focused regressions: 35 PASS / 0 FAIL
- Component Store validator: PASS / zero errors
- Exact untranscribed package pin maps remain explicitly TITS UP (`physical_pin_map_complete=false`).

---

<a id="history-vmbs-cp154-status-md"></a>

## VMBS/CP154_STATUS.md

Original location: `VMBS/CP154_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP154 — SN74HC244
Added TI SN74HC244 (octal buffer/line driver with 3-state outputs) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC244, delegating only computer-visible logic behavior to VMB.LOGIC.74XX244. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp153-status-md"></a>

## VMBS/CP153_STATUS.md

Original location: `VMBS/CP153_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP153 — SN74HC157
Added TI SN74HC157 (quad 2-to-1 data selector/multiplexer) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC157, delegating only computer-visible logic behavior to VMB.LOGIC.74XX157. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp152-status-md"></a>

## VMBS/CP152_STATUS.md

Original location: `VMBS/CP152_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP152 — SN74HC153
Added TI SN74HC153 (dual 4-to-1 data selector/multiplexer) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC153, delegating only computer-visible logic behavior to VMB.LOGIC.74XX153. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp151-status-md"></a>

## VMBS/CP151_STATUS.md

Original location: `VMBS/CP151_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP151 — SN74HC151
Added TI SN74HC151 (8-to-1 data selector/multiplexer) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC151, delegating only computer-visible logic behavior to VMB.LOGIC.74XX151. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp150-status-md"></a>

## VMBS/CP150_STATUS.md

Original location: `VMBS/CP150_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP150 — SN74HC148
Added TI SN74HC148 (8-to-3 priority encoder) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC148, delegating only computer-visible logic behavior to VMB.LOGIC.74XX148. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp149-status-md"></a>

## VMBS/CP149_STATUS.md

Original location: `VMBS/CP149_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP149 — SN74HC139
Added TI SN74HC139 (dual 2-to-4 decoder/demultiplexer) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC139, delegating only computer-visible logic behavior to VMB.LOGIC.74XX139. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp148-status-md"></a>

## VMBS/CP148_STATUS.md

Original location: `VMBS/CP148_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP148 — SN74HC138
Added TI SN74HC138 (3-to-8 decoder/demultiplexer) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC138, delegating only computer-visible logic behavior to VMB.LOGIC.74XX138. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp147-status-md"></a>

## VMBS/CP147_STATUS.md

Original location: `VMBS/CP147_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP147 — SN74HC126
Added TI SN74HC126 (quad buffer, active-high 3-state enables) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC126, delegating only computer-visible logic behavior to VMB.LOGIC.74XX126. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp146-status-md"></a>

## VMBS/CP146_STATUS.md

Original location: `VMBS/CP146_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP146 — SN74HC125
Added TI SN74HC125 (quad buffer, active-low 3-state enables) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC125, delegating only computer-visible logic behavior to VMB.LOGIC.74XX125. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp145-status-md"></a>

## VMBS/CP145_STATUS.md

Original location: `VMBS/CP145_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP145 — SN74HC86
Added TI SN74HC86 (quad 2-input XOR) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC86, delegating only computer-visible logic behavior to VMB.LOGIC.74XX86. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp144-status-md"></a>

## VMBS/CP144_STATUS.md

Original location: `VMBS/CP144_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP144 — SN74HC85
Added TI SN74HC85 (4-bit magnitude comparator) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC85, delegating only computer-visible logic behavior to VMB.LOGIC.74XX85. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp143-status-md"></a>

## VMBS/CP143_STATUS.md

Original location: `VMBS/CP143_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP143 — SN74HC42
Added TI SN74HC42 (BCD-to-decimal decoder) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC42, delegating only computer-visible logic behavior to VMB.LOGIC.74XX42. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp142-status-md"></a>

## VMBS/CP142_STATUS.md

Original location: `VMBS/CP142_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP142 — SN74HC32
Added TI SN74HC32 (quad 2-input OR) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC32, delegating only computer-visible logic behavior to VMB.LOGIC.74XX32. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp141-status-md"></a>

## VMBS/CP141_STATUS.md

Original location: `VMBS/CP141_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP141 — SN74HC30
Added TI SN74HC30 (8-input NAND) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC30, delegating only computer-visible logic behavior to VMB.LOGIC.74XX30. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp140-status-md"></a>

## VMBS/CP140_STATUS.md

Original location: `VMBS/CP140_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP140 — SN74HC27
Added TI SN74HC27 (triple 3-input NOR) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC27, delegating only computer-visible logic behavior to VMB.LOGIC.74XX27. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp139-status-md"></a>

## VMBS/CP139_STATUS.md

Original location: `VMBS/CP139_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP139 — SN74HC21
Added TI SN74HC21 (dual 4-input AND) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC21, delegating only computer-visible logic behavior to VMB.LOGIC.74XX21. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp138-status-md"></a>

## VMBS/CP138_STATUS.md

Original location: `VMBS/CP138_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP138 — SN74HC20
Added TI SN74HC20 (dual 4-input NAND) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC20, delegating only computer-visible logic behavior to VMB.LOGIC.74XX20. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp137-status-md"></a>

## VMBS/CP137_STATUS.md

Original location: `VMBS/CP137_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP137 — SN74HC14
Added TI SN74HC14 (hex Schmitt-trigger inverter) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC14, delegating only computer-visible logic behavior to VMB.LOGIC.74XX14. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp136-status-md"></a>

## VMBS/CP136_STATUS.md

Original location: `VMBS/CP136_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP136 — SN74HC11
Added TI SN74HC11 (triple 3-input AND) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC11, delegating only computer-visible logic behavior to VMB.LOGIC.74XX11. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp135-status-md"></a>

## VMBS/CP135_STATUS.md

Original location: `VMBS/CP135_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP135 — SN74HC10
Added TI SN74HC10 (triple 3-input NAND) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC10, delegating only computer-visible logic behavior to VMB.LOGIC.74XX10. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp134-status-md"></a>

## VMBS/CP134_STATUS.md

Original location: `VMBS/CP134_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP134 — SN74HC08
Added TI SN74HC08 (quad 2-input AND) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC08, delegating only computer-visible logic behavior to VMB.LOGIC.74XX08. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp133-status-md"></a>

## VMBS/CP133_STATUS.md

Original location: `VMBS/CP133_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP133 — SN74HC04
Added TI SN74HC04 (hex inverter) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC04, delegating only computer-visible logic behavior to VMB.LOGIC.74XX04. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp132-status-md"></a>

## VMBS/CP132_STATUS.md

Original location: `VMBS/CP132_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP132 — SN74HC02
Added TI SN74HC02 (quad 2-input NOR) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC02, delegating only computer-visible logic behavior to VMB.LOGIC.74XX02. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp131-status-md"></a>

## VMBS/CP131_STATUS.md

Original location: `VMBS/CP131_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP131 — SN74HC00
Added TI SN74HC00 (quad 2-input NAND) as a distinct HC physical/electrical identity with Code Store module VMB.LOGIC.TI_SN74HC00, delegating only computer-visible logic behavior to VMB.LOGIC.74XX00. Exact pin map remains TITS UP until transcribed from the package diagram. Primary evidence: TI product page / linked datasheet.

---

<a id="history-vmbs-cp130-status-md"></a>

## VMBS/CP130_STATUS.md

Original location: `VMBS/CP130_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP130 — twenty-checkpoint private-G long run
CP111–124 added fourteen documented low-voltage/battery-voltage AT27 identities with executable read seams; CP125–129 repaired x16 contracts, physical honesty, provenance and programmed-state consistency and added family regression. CP130 is validation/package checkpoint.

---

<a id="history-vmbs-cp129-status-md"></a>

## VMBS/CP129_STATUS.md

Original location: `VMBS/CP129_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP129 — AT27 family contract regression
Added family-wide payload/binding/read-only contract regression.

---

<a id="history-vmbs-cp128-status-md"></a>

## VMBS/CP128_STATUS.md

Original location: `VMBS/CP128_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP128 — programmed-state reconciliation
Reconciled programmed-state capacity with Code Store payload contracts.

---

<a id="history-vmbs-cp127-status-md"></a>

## VMBS/CP127_STATUS.md

Original location: `VMBS/CP127_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP127 — AT27 provenance hygiene
Removed copied AT27C512R source labels from later AT27 records; canonical source now follows each record evidence URL.

---

<a id="history-vmbs-cp126-status-md"></a>

## VMBS/CP126_STATUS.md

Original location: `VMBS/CP126_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP126 — x16 physical honesty repair
Removed inherited x8 PDIP-28/PLCC-32 maps from x16 AT27 devices. Exact maps remain TITS UP pending per-device table transcription.

---

<a id="history-vmbs-cp125-status-md"></a>

## VMBS/CP125_STATUS.md

Original location: `VMBS/CP125_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP125 — x16 AT27 contract correction
Corrected x16 interfaces and payload capacities for AT27C1024/2048/4096.

---

<a id="history-vmbs-cp110-status-md"></a>

## VMBS/CP110_STATUS.md

Original location: `VMBS/CP110_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP110 — parallel EEPROM + OTP EPROM long run

- Lineage: CP95 -> CP110
- Component Store canonical records: 73
- Recognition families: 2,581
- Recognized variants: 10,867
- Code Store module entries: 124
- CP96–104: AT28 parallel EEPROM family expansion
- CP105–110: AT27 OTP EPROM family expansion
- Exact undocumented/untranscribed package pin maps remain explicitly incomplete; no pin inheritance guesses.
- Focused CP77–110 regressions available in tree: 33 PASS
- Component Store validator: PASS / zero errors

---

<a id="history-vmbs-cp95-status-md"></a>

## VMBS/CP95_STATUS.md

Original location: `VMBS/CP95_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP95 Status — Parallel EEPROM long run

- Canonical Component Store records: 58
- Recognition families: 2,581
- Recognized variants: 10,867
- Code Store module entries: 109
- CP77–CP95 focused regressions present: 18/18 PASS
- New CP91–95 identities: AT28C010, AT28C16-T, AT28BV64B, AT28HC64B, AT28HC64BF
- Primary-manufacturer documentation used; physical/electrical distinctions retained even where digital behavior is shared.
- No .NET build claim was made in this environment.

---

<a id="history-vmbs-cp89-status-md"></a>

## VMBS/CP89_STATUS.md

Original location: `VMBS/CP89_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP89 STATUS — AT28C64B parallel EEPROM

- Added manual-backed Microchip AT28C64B 8K x 8 parallel EEPROM.
- Added authentic board-visible read/write, 64-byte page, busy polling/toggle semantics at digital-functional resolution.
- Added distinct mutable in-system programmed-state contract.
- Added VB.NET worker + Python reference + Code Store binding + regression.
- Component Store: 52 canonical / 2,581 family / 10,867 recognized variants.
- CP77-CP89 focused regressions PASS.
- No .NET compile claim; no SDK compile performed in this checkpoint.

---

<a id="history-vmbs-cp86-status-md"></a>

## VMBS/CP86_STATUS.md

Original location: `VMBS/CP86_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP86 Status — CT2960 evidence Library + first specimen-backed component

## Project-root Library
- Stored the six user-supplied full-card/source photographs once under `VMB/VMBS/Library/CT2960/Images/`.
- Added immutable-source SHA-256 manifest.
- Short intuitive branch policy retained: `Library/<board>/Images`.

## Component Store image policy
- Added only component crops for CT2505-TDQ2 and TEA2025B.
- Each crop carries source image hash + crop coordinates back to the CT2960 Library original.
- No redundant full-card photographs copied into component records.

## TEA2025B
- Added STMicroelectronics TEA2025B canonical Component Store record from primary ST datasheet Rev 4.
- Exact PowerDIP16 pin identity, 3–12V operating supply, 15V absolute max, dual/bridge topology, gain/electrical/thermal facts and application-network requirement recorded.
- No fake digital/VMB behavior binding: this is analog power silicon and its analog simulation model remains unimplemented.

## CT2505-TDQ2
- Direct specimen marking is archived as a crop and provenance packet.
- No primary Creative pin-level datasheet has been established, so exact silicon implementation remains TITS UP rather than promoting secondary claims.

---

<a id="history-vmbs-cp85-status-md"></a>

## VMBS/CP85_STATUS.md

Original location: `VMBS/CP85_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP85 Status — Intel 2147H
- Added Intel 2147H 4096 x 1 high-speed static RAM from Intel manufacturer documentation.
- Preserved DIP-18 pinout, separate DIN/DOUT, active-low CS/WE, tri-state output, automatic CS-controlled power-down and documented speed bins.
- Added source-backed Python/VB behavior and regression.

---

<a id="history-vmbs-cp84-status-md"></a>

## VMBS/CP84_STATUS.md

Original location: `VMBS/CP84_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP84 Status — Intel 2142
- Added Intel 2142 1024 x 4 completely static RAM from Intel documentation.
- Exact DIP-20 pinout, common I/O, CS1 active-low / CS2 active-high selection, WE active-low and OD output-disable behavior represented.
- Added source-backed Python/VB behavior and regression.

---

<a id="history-vmbs-cp83-status-md"></a>

## VMBS/CP83_STATUS.md

Original location: `VMBS/CP83_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP83 Status — Intel 2101A / 5101 physical identity
- Added Intel 2101A 256 x 4 N-channel static RAM using Intel documentation.
- Intel documents 5101 as pin-compatible with 2101; board-facing pin/control behavior is therefore shared where documented.
- Kept 2101A and 5101 as distinct Component Store identities and MODULE_IDs because NMOS vs CMOS electrical/power/retention truth matters for reverse construction.

---

<a id="history-vmbs-cp82-status-md"></a>

## VMBS/CP82_STATUS.md

Original location: `VMBS/CP82_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP82 Status — Intel 5101
- Added Intel 5101 256 x 4 CMOS static RAM from Intel manufacturer documentation.
- Preserved separate DI/DO, CE1 active-low + CE2 active-high selection, OD high-impedance control, R/W semantics, ultra-low standby and 5101L 2V retention distinction.
- Added Python reference and VB worker; regression covers both chip enables, output disable, write/read.

---

<a id="history-vmbs-cp81-status-md"></a>

## VMBS/CP81_STATUS.md

Original location: `VMBS/CP81_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP81 Status — Intel 2102A
- Added Intel 2102A/2102AL 1024 x 1 static RAM from Intel manufacturer documentation.
- Preserved exact DIP-16 pinout, separate DIN/DOUT, active-low CE, R/W semantics, tri-state deselect, +5V TTL interface and documented speed variants.
- Added Python reference and VB worker; regression covers deselect/write/read.

---

<a id="history-vmbs-cp80-status-md"></a>

## VMBS/CP80_STATUS.md

Original location: `VMBS/CP80_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP80 Status — CT2960 project-root Library specimen

- Added the six user-supplied Creative CT2960 photographs under the short project-root path `VMB/VMBS/Library/CT2960/Images/`.
- Full-card/source photographs are stored once; Component Store records should retain only useful component crops plus provenance back to these originals.
- Added SHA-256/size manifest for every source image.
- No interpretation is encoded into the immutable source images.

---

<a id="history-vmbs-cp79-status-md"></a>

## VMBS/CP79_STATUS.md

Original location: `VMBS/CP79_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP79 Status — EPROM interface identity + classic x4 SRAM authenticity

## Guardrails retained
- VMBS remains a bidirectional matter <-> simulation conversion pipeline.
- Component Store owns board-facing component truth; VMB code owns authentic guest-visible behavior; Board Script is the canonical human-readable board IR linking the two.
- No floor planning or Tracer routing work was started.
- Behavioral similarity never erases information needed for reverse physical realization.

## CP77 — EPROM interface identity
- Added Intel 27128A 128-Kbit / 16K x 8 UV EPROM canonical record.
- Added Microchip AT27C512R 512-Kbit / 64K x 8 OTP EPROM canonical record.
- Added distinct source-backed VB and Python installed-machine read workers for both.
- Preserved exact payload-size and reverse-delivery requirements.
- Regression explicitly proves that 27128A, AT27C256R and AT27C512R remain distinct where pins/programming interfaces differ despite common x8 read semantics.
- Fixed programmed-state validator so UV EPROMs are audited rather than accidentally skipped.

## CP78 — Intel 2114
- Added Intel 2114 1024 x 4 completely static RAM.
- Exact DIP-18 board-facing pin map, +5V/TTL interface, common tri-state I/O, active-low CS/WE, no refresh/clock requirement, and documented speed bins recorded.
- Added VB worker + Python reference model + read/write/deselect regression.

## CP79 — Intel 2148H
- Added Intel 2148H 1024 x 4 high-speed static RAM.
- Preserved industry-standard 2114 pinout while recording faster speed bins, automatic CS power-down, and active/standby current distinctions.
- Kept Store identity and VMB MODULE_ID distinct from 2114 even though storage behavior and board wiring are compatible; implementation remains tiny and equivalent rather than losing reverse-build identity.
- Added VB worker + Python reference model + regression.

## Evidence
- Intel 27128A manufacturer datasheet scan.
- Microchip AT27C512R DS20006752A.
- Intel 2114 manufacturer datasheet / Intel memory documentation.
- Intel January 1982 Component Data Catalog, 2148H section.

## Validation
- Component Store now has 45 canonical records.
- CP37-66 tests passed in sequence until command timeout at CP67.
- CP67-79 available tests then passed separately.
- CP70/71/72/73 source audits passed explicitly.
- No .NET SDK compile claim.

---

<a id="history-vmbs-cp76-status-md"></a>

## VMBS/CP76_STATUS.md

Original location: `VMBS/CP76_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP76 Status — Programmed-state authenticity and reverse delivery

## Guardrails
- VMBS remains matter <-> simulation. No floor planning or Tracer work was started.
- Component Store records board-facing silicon/interface/electrical/programming truth.
- VMB behavior must authentically reproduce guest-visible behavior; payload-driven devices should consume the actual payload whenever technically possible.
- Board Script remains the canonical human-readable board IR.
- Behavioral equivalence does not justify collapse when reverse physical realization would lose required interface/programming information.

## CP75
- Added a first-class `programmed_state` Component Store domain.
- Added explicit independent fields for simulation consumption and reverse physical delivery.
- Firmware Store now states that a simulation bypass never removes the payload from the physical manufacturing deliverable.
- Audited SST39SF040 and 93LC46C as mutable nonvolatile payload devices.
- Audited 8042AH as a factory mask-ROM personality device: exact firmware is required for authentic simulation/personality; physical reverse realization requires the exact factory-programmed identity or an explicit compatible programmable substitution.
- Added canonical Microchip/Atmel AT27C256R 32K x 8 OTP EPROM record with PDIP-28 pins, normal-read electrical facts, programming/support requirements, VMB binding, and mandatory 32768-byte payload contract.
- Marked the existing `VMB.AT.KBC8042` shortcut explicitly transitional: exact payload unknown, simulation bypass active, reverse delivery unresolved.

## CP76
- Added canonical Intel 8742AH OTP-EPROM UPI-42 identity from the Intel UPI-41AH/42AH family documentation.
- 8042AH and 8742AH retain the same installed DIP interface map in the Store, but remain distinct physical identities because mask-ROM factory realization and OTP-EPROM programming/delivery are different reverse-build requirements.
- No generic UPI-42 behavioral implementation was invented; both remain TITS UP for exact firmware-executing VMB behavior.

## Validation
- Component Store: 41 canonical documented records, 2,581 family records, 10,867 recognized variants.
- CP37 through CP75 available regression scripts passed in sequence before the aggregate command hit the execution timeout; CP76 then passed separately.
- No .NET SDK compile claim.

---

<a id="history-vmbs-cp74-status-md"></a>

## VMBS/CP74_STATUS.md

Original location: `VMBS/CP74_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP74 Status — Component Store domain-boundary authenticity run

## Architecture guardrails re-checked
- VMBS remains a bidirectional matter <-> simulation conversion pipeline.
- Component Store owns component-boundary truth: identity, package/pins, electrical/timing/support requirements, externally relevant physics, and specified behavior.
- VMB code owns authentic guest-visible behavior; it need not simulate irrelevant internal semiconductor physics.
- Board Script remains the canonical human-readable board IR linking board/input evidence to Store identities and behavioral construction.
- Collapse is allowed only when board-facing interface/support requirements and guest-visible behavior are interchangeable. Behavioral implementation reuse alone must not erase physical/reverse-build identity.
- No Tracer/floor-planning work was started in this run.

## CP73 — Motorola MC146818A RTC + RAM
- Added a canonical manual-backed MC146818A Component Store record with the full 24-pin external interface, 3–6 V supply range, three supported time bases, 64-byte software-visible space (14 clock/control + 50 general RAM), interrupt/square-wave capabilities, bus-mode strap, and timing evidence.
- Corrected the existing VMB RTC's 32.768-kHz update-cycle constant from an approximate 2.000 ms to the Motorola-specified 1.984 ms. The existing 244 us UIP pre-update warning was already correct.
- Explicitly separated MC146818A silicon truth from AT-board integration: ports 70h/71h, NMI masking, century conventions and chipset upper CMOS are not properties of the Motorola RTC itself.
- Existing CmosRtc remains the AT board wrapper hosting authentic MC146818A-visible behavior; the Code Store description now says so.

## CP74 — Intel 8042AH identity / firmware-personality boundary
- Added a canonical manual-backed Intel 8042AH Component Store record with its 40-pin DIP interface, 2K x 8 mask ROM, 256 x 8 RAM, 8-bit timer/counter, 18 programmable I/O pins, UPI host interface, DMA/flag capability, 5 V electrical interface and timing facts.
- Important authenticity correction: Intel 8042AH is a general-purpose mask-ROM slave microcontroller, not intrinsically an IBM AT keyboard controller.
- The existing `VMB.AT.KBC8042` implementation is now explicitly classified as an AT-compatible keyboard-controller firmware personality hosted by an 8042-class board wrapper, not as the generic 8042AH physical identity.
- Generic 8042AH therefore has no automatic VMB behavior binding. Exact mask-ROM/order-code identity is required before reverse construction can claim an exact physical programmed part.
- This matches Intel's own distinction: the 8242 family was sold preprogrammed with keyboard-controller firmware from vendors including Phoenix, IBM and Award.

## Validation
- Component Store validates with 39 canonical documented records, 2,581 family records and 10,867 recognized variants.
- Every available CP37 through CP74 regression/audit script passes.
- No .NET SDK is available in this environment; no VB compile claim is made.

---

<a id="history-vmbs-cp72-status-md"></a>

## VMBS/CP72_STATUS.md

Original location: `VMBS/CP72_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP72 Status — buttered support-silicon run

## CP69 — Intel 8255A
- Added primary-manual-backed Component Store and Code Store records.
- Added Python reference model and VB.NET worker.
- Implements CPU-visible register programming, reset-to-input state, Mode 0, BSR, and Mode 1/2 handshake state.
- Fixed Group A INTE handling during the run: PC4/PC6 BSR control applies to the appropriate Mode 1 direction as well as Mode 2.
- Electrical thresholds and AC timing remain outside the Boolean/handshake worker.

## CP70 — Intel 8254
- Added canonical manual-backed Component Store record for the already substantial `Pit8253` runtime worker.
- Audited six modes, 6/7 aliases, binary/BCD count handling, count latch, read-back status/count, NULL count and gate handling.
- Kept the historical VB class name; Code Store identifies it as the 8254-capable runtime substrate.

## CP71 — Intel 8259A
- Added canonical manual-backed Component Store record and source audit for existing `Pic8259`.
- Explicitly classified the worker as a PC/AT-visible transaction projection.
- Exact buffered-mode/uPM electrical-bus behavior is not claimed.

## CP72 — Intel 8237A
- Added canonical manual-backed Component Store record and source audit for existing `Dma8237`.
- Existing worker covers the PC/AT transaction subset including dual-controller 8/16-bit DMA topology, request/mask/mode/address/count state, demand/single/block handling, cascade exclusion, autoinitialize, decrement, terminal count and rotating priority.
- Standalone 8237A memory-to-memory and exact pin-cycle generation remain outside the projection and are stated as such.

## Store / regression
- Component Store: 37 canonical documented records; 2,581 family records; 10,867 recognized variants.
- CP37 through CP72 available regression scripts pass after the run.
- No .NET SDK is available in this environment; no VB compile claim is made.

---

<a id="history-vmbs-cp68-status-md"></a>

## VMBS/CP68_STATUS.md

Original location: `VMBS/CP68_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP68 Status — support-silicon authenticity run

## CP66 — Intel 8284A completed at digital timing resolution
- Finished the previously partial 8284A digital model instead of compile-enabling the CP65 stub.
- Added F/C source selection, independent OSC observation, CSYNC forcing, RESET falling-edge synchronization, AEN-qualified RDY inputs, and ASYNC-selected one/two-stage READY synchronization.
- Promoted Code Store binding to implemented/compile-eligible only after those documented digital behaviors were represented.
- Analog crystal startup, threshold behavior, propagation tolerances and metastability remain electrical-domain facts, not invented digital behavior.

## CP67 — Intel 8282/8283 and 8286/8287
- Added source-backed Intel 8282 non-inverting and 8283 inverting octal transparent latches with active-high STB and active-low three-state OE.
- Added Intel 8286 non-inverting and 8287 inverting octal bidirectional transceivers with active-low OE and T direction control.
- High impedance is explicit rather than represented as a driven logic level.

## CP68 — Intel 8288 started, deliberately partial
- Added the documented S2/S1/S0 status-to-command decoder.
- Did not fake the 8288's clocked command/control timing or advanced command generation.
- Code Store remains partial and compile_allowed=false until timing, AEN/CEN/IOB, ALE/DEN/DT-R/MCE-PDEN and advanced write-command behavior are implemented from documentation.

## Store / regression
- Component Store: 33 canonical documented records; existing 2,581 family records / 10,867 recognition variants preserved.
- CP37 through CP68 available regression scripts pass.

---

<a id="history-vmbs-cp65-status-md"></a>

## VMBS/CP65_STATUS.md

Original location: `VMBS/CP65_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP65 Status — authenticity-first Component Store walk

## CP63 — EIA-232 support silicon
- Added source-backed MC1488 quad line-driver and MC1489 quad line-receiver digital projections.
- Preserved EIA-232 polarity inversion explicitly instead of flattening line voltage into ordinary TTL Boolean semantics.
- Analog thresholds, voltage magnitude, slew rate, loading and dual-rail behavior remain electrical-domain facts.
- Added primary Motorola/onsemi evidence and regression.

## CP64 — first dynamic RAM
- Added MCM6665A 64Kx1 page-mode DRAM model: 8+8 multiplexed address, x1 storage, 128-cycle/2ms refresh accounting.
- Added TMS4256 256Kx1 page-mode DRAM model: 9+9 multiplexed address, x1 storage, 256-cycle/4ms refresh accounting.
- Refresh expiry is observable but does not fabricate a deterministic decay pattern; real charge loss depends on analog/process/temperature conditions.
- Added Motorola/TI primary-manufacturer databook/application evidence and regression.

## CP65 — Intel 8284A started, deliberately partial
- Added clock/reset core for Intel 8284A from Intel documentation.
- Implements source/3 CPU clock abstraction, PCLK relationship, and RESET synchronization on CLK falling edge.
- READY synchronization and detailed F/C#/EFI/CSYNC source behavior remain unresolved.
- Code Store entry is `partial` and `compile_allowed=false`; no fake green.

## Store integrity
- Component Store now has 28 canonical records plus the existing 2,581 family records / 10,867 recognized variants.
- Updated CP37/CP38 historical tests so they prove preservation of the original 23-record migration baseline while allowing later documented records to be appended.
- CP37 through CP65 regression scripts all pass.

## Next
- Finish 8284A READY/source-selection behavior from the Intel manual before compile-enabling it.
- Continue support/interface silicon, then DRAM variants/organization and increasingly complex controller silicon.

---

<a id="history-vmbs-cp62-status-md"></a>

## VMBS/CP62_STATUS.md

Original location: `VMBS/CP62_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP62 Status — Component Store authenticity power run

## Added since CP59
- CP60: timing-aware digital contract; source-backed SN74LS123/SN74LS221 behavioral models.
- CP61: generic asynchronous x8 SRAM worker; 6116, 6264, 62256 Code Store bindings and primary-source evidence.
- CP62: promoted 27256 EPROM from pseudocode contract to source-backed 32Kx8 normal-read implementation.

## Authenticity boundaries
- LS123/LS221 digital timing consumes a board-resolved pulse width; analog Rext/Cext conversion remains unresolved electrical/timing work.
- SRAM access-time, voltage, retention and current remain exact-variant evidence, not generic behavior.
- 27256 programming VPP/pulse algorithms are not confused with normal installed-machine reads.
- No undocumented behavior was invented to make a test green.

## Regression
CP37 through CP62 available regression scripts pass.

## Next simplest-first candidates
Continue memory/support silicon with primary manuals: additional ROM/EPROM/SRAM exact variants, then DRAM once a primary historical datasheet establishes row/column/refresh semantics; simple line receivers/drivers and oscillator/timer support before complex controllers.

---

<a id="history-vmbs-cp59-status-md"></a>

## VMBS/CP59_STATUS.md

Original location: `VMBS/CP59_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP59 Status — remaining simple gate / tri-state mux tier

Added manual-backed SN74LS01, 132, 133, 136 and 251 functional modules. Open-collector and three-state release is modeled explicitly as high impedance. Schmitt-trigger analog hysteresis remains electrical-domain evidence rather than invented binary state.

Evidence: `compiler_db/simple_logic_manuals_cp59.json`. Regression: `tools/cp59_gate_mux_manual_test.py`.

---

<a id="history-vmbs-cp58-status-md"></a>

## VMBS/CP58_STATUS.md

Original location: `VMBS/CP58_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP58 Status — primitive / SSI gate tier

Manual-backed Store walk added SN74LS03, 05, 06, 10, 11, 14, 20, 21, 27 and 30 behavioral modules. Open-collector parts explicitly release the output (high impedance) rather than fabricating an active HIGH. LS14 is modeled only at binary functional resolution; its analog Schmitt hysteresis remains an evidence-bound electrical property.

Evidence: `compiler_db/simple_logic_manuals_cp58.json`. Regression: `tools/cp58_primitive_gate_manual_test.py`.

---

<a id="history-vmbs-cp57-status-md"></a>

## VMBS/CP57_STATUS.md

Original location: `VMBS/CP57_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP57 Status — adder sibling / parallel-access shift tier

Added `VMB.LOGIC.74XX83A` and `VMB.LOGIC.74XX95B` from manufacturer documentation.

TI explicitly documents LS83A and LS283 as electrically/functionally identical with different terminal arrangement, so 83A deliberately reuses the 283 behavioral truth rather than duplicating invented semantics. LS95B implements the documented parallel-load and shift-right modes on the falling edges of its separate clocks.

Evidence: `compiler_db/simple_logic_manuals_cp57.json`. Regression: `tools/cp57_adder_shift_manual_test.py`, including exhaustive LS83A-vs-LS283 equivalence over all 512 input combinations.

---

<a id="history-vmbs-cp56-status-md"></a>

## VMBS/CP56_STATUS.md

Original location: `VMBS/CP56_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP56 Status — latch / J-K / ripple-counter tier

Primary TI documentation was consulted before promotion. Added source-backed behavioral modules for 74XX73, 74XX75, 74XX76, 74XX90, 74XX92, and 74XX93. Evidence is recorded in `compiler_db/simple_logic_manuals_cp56.json`.

The 73/76 models preserve negative-edge J-K behavior and their different asynchronous controls; 76 represents the documented forbidden PRE#=CLR#=LOW steady-state output condition as Q=Qbar=HIGH. The 75 is a transparent quad latch. The 90/92/93 preserve the separate divide-by-two stage plus divide-by-five/six/eight section and gated reset; 90 also implements gated set-to-nine.

No .NET SDK compile is claimed; Python behavioral/static regression is the available proof in this environment.

---

<a id="history-vmbs-cp55-status-md"></a>

## VMBS/CP55_STATUS.md

Original location: `VMBS/CP55_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP55 Status — Component Store arithmetic / shift / counter tier

CP55 continues the simplest-first Component Store walk with primary TI documentation before behavioral promotion.

## Added
- 74XX192 — BCD synchronous up/down counter, dual clocks, asynchronous load/clear.
- 74XX193 — binary synchronous up/down counter, dual clocks, asynchronous load/clear.
- 74XX194 — 4-bit bidirectional universal shift register.
- 74XX280 — 9-bit odd/even parity generator/checker.
- 74XX283 — 4-bit binary full adder with carry.

## Evidence
`compiler_db/simple_logic_manuals_cp55.json` records manufacturer documentation and the facts used. Family-specific electrical/timing values are not generalized to the behavioral 74XX aliases.

## Implementation
- Python reference behavior in `tools/ic_logic_models.py`.
- VB.NET behavior in `Hardware/IC Modules/DigitalLogicIc.vb` through the generic digital-IC registry.
- Code Store bindings added for all five modules.
- CP55 regression includes exhaustive 512-vector parity verification and exhaustive 16x16x2 full-adder verification plus counter and shift-register state tests.

## Honesty
No .NET SDK is available in this environment, so CP55 does not claim a VB compile. Python/static behavioral regressions are the available proof.

---

<a id="history-vmbs-cp54-status-md"></a>

## VMBS/CP54_STATUS.md

Original location: `VMBS/CP54_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP54 — Component Store simplest-first walk continues

Manual-backed behavioral modules added:
- VMB.LOGIC.74XX42 — BCD-to-decimal decoder
- VMB.LOGIC.74XX85 — 4-bit magnitude comparator with cascade inputs
- VMB.LOGIC.74XX175 — quad D flip-flop, complementary outputs, direct clear
- VMB.LOGIC.74XX190 — synchronous BCD up/down counter
- VMB.LOGIC.74XX191 — synchronous binary up/down counter

Primary TI evidence recorded in `compiler_db/simple_logic_manuals_cp54.json`.

No family-specific electrical/timing values are generalized onto behavioral aliases. CP54 adds computer-visible digital behavior only.

---

<a id="history-vmbs-cp53-status-md"></a>

## VMBS/CP53_STATUS.md

Original location: `VMBS/CP53_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP53 — simplest-first Component Store walk continues

## Added/manual-backed behavioral modules
- VMB.LOGIC.74XX139 — dual 2-to-4 decoder/demultiplexer
- VMB.LOGIC.74XX148 — 8-to-3 active-low priority encoder
- VMB.LOGIC.74XX151 — 8-to-1 mux with complementary outputs
- VMB.LOGIC.74XX153 — dual 4-to-1 mux
- VMB.LOGIC.74XX166 — parallel-load 8-bit serial shift register
- VMB.LOGIC.74XX174 — hex positive-edge D flip-flop with clear
- VMB.LOGIC.74XX125 — quad tri-state buffer, active-low enables
- VMB.LOGIC.74XX126 — quad tri-state buffer, active-high enables
- VMB.LOGIC.74XX51 — LS-family AND-OR-INVERT gate

Manual evidence is recorded in compiler_db/simple_logic_manuals_cp52.json and cp53.json. Store variant stubs were promoted to implemented behavioral bindings without claiming LS electrical data for non-LS aliases.

Important manual correction during CP53: SN74LS51 is asymmetric: channel 1 is two 3-input AND terms into NOR; channel 2 is two 2-input AND terms into NOR. The first draft had those channel shapes reversed; primary TI documentation caught it before checkpoint.

---

<a id="history-vmbs-cp51-status-md"></a>

## VMBS/CP51_STATUS.md

Original location: `VMBS/CP51_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP51 Status — Component Store simplest-first walk, bus/register/counter tier

Date: 2026-10-03

## Completed
- Continued primary-document walk after CP50 with TI SN74LS688, SN74LS244, SN74LS245, SN74LS373, SN74LS374, SN74LS161A, SN74LS163A, and SN74LS393.
- Added `compiler_db/simple_logic_manuals_cp51.json` with exact documented variants and scoped facts.
- Added `tools/cp51_simple_bus_counter_manual_test.py` covering comparator enable/equality, tri-state buffers/transceiver direction, transparent latch behavior, edge register behavior, asynchronous-vs-synchronous counter clear, synchronous load/count, and dual ripple-counter direct clear.
- Existing runtime/reference behavior agrees with the audited digital semantics for this tier; no CP51 runtime correction was required.
- Re-ran CP49/50/51 plus available CP37-48 compiler/store regressions; PASS.

## Evidence discipline
- Electrical/timing facts are attached to exact LS variants and are not silently promoted to every 74xx family alias.
- Runtime models remain computer-visible digital abstractions; analog threshold/delay behavior is Store evidence/constraints, not fabricated gate simulation.

## Next simplest tier
- Walk remaining passive/simple board components first (resistors, capacitor values, resistor arrays, crystals/oscillator support) where manufacturer identity is known or generic constraints can be represented honestly.
- Then proceed to small memories/programmed logic and simple interface/support ICs before complex controllers/chipsets.

---

<a id="history-vmbs-cp50-status-md"></a>

## VMBS/CP50_STATUS.md

Original location: `VMBS/CP50_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP50 Status — Component Store manual/code walk

CP49 and CP50 begin the requested simplest-first Store walk.

## Completed
- Primary TI evidence indexed for 74xx00/02/04/08/32/86 and 74xx138/157/74/273.
- Existing VB.NET implementations audited against documented logic behavior.
- Python reference models provide executable regression coverage in this environment.
- Corrected 74LS74A forbidden asynchronous-input state: PRE#=CLR#=LOW now exposes Q=HIGH and Qbar=HIGH.
- Kept LS electrical/timing facts variant-scoped; generic 74xx functional aliases do not falsely inherit LS-only electrical characteristics.

## Next simplest tier
74xx688 comparator, 74xx244/245 buffers/transceiver, 74xx373/374 latch/register, then 161/163/393 counters. Continue primary-manual-first and correct code when evidence disagrees.

---

<a id="history-vmbs-cp48-status-md"></a>

## VMBS/CP48_STATUS.md

Original location: `VMBS/CP48_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP48 — Manual-grounded RTL8019 Microwire seam + first executable generic adapter

## CP47 manual evidence
- Inspected RTL8019AS, Microchip 93LC46C and SST39SF040 documentation plus AMD/Xilinx FPGA documentation.
- Added `MANUAL_EVIDENCE_CP47.md` and `compiler_db/manual_sources_cp47.json`.
- Build container has no external DNS, so exact PDF bytes could not honestly be cached locally; URLs and extracted facts are indexed for later archival ingestion.
- RTL8019AS worker now exposes its documented page-3 9346/Microwire controller seam: controller-driven CS/CLK/DI event/properties and externally sampled DO.
- Code Store binds these source members; source API validation is green.

## CP48 executable adapter
- Added `tools/chimera_emit_vb_adapters.py`.
- It finds compatible protocol peers from interface signals + reusable composition source contracts only; no board name, part ID, or adapter-name branch exists.
- Real ISA8 Ethernet specimen emits one executable Microwire protocol link U1<->U3 covering CS/CLK/DI/DO (4 nets).
- 52 semantic connections remain pending; boot-ROM/ISA/power topology is not falsely declared executable.

## Honesty boundary
No .NET SDK is available here, so generated VB has static/source-contract regression proof only, not a claimed VB build. RTL8019 boot-ROM decode still requires documented configuration semantics before executable lowering.

---

<a id="history-vmbs-cp46-status-md"></a>

## VMBS/CP46_STATUS.md

Original location: `VMBS/CP46_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP46 — Semantic adapter lowering + live Microwire seam

CP46 continues the board-neutral compiler path after CP45.

## Added
- `tools/chimera_adapter_plan.py`: lowers Glue IR into semantic adapter groups using only instance/interface/composition metadata. No board names or part IDs are special-cased.
- Real ISA8 Ethernet specimen resolves to four adapter groups and 15 cross-adapter nets. Multiplexed `/BD5`, `/BD6`, `/BD7` remain shared topology rather than guessed point-to-point wiring.
- `Microwire93Lc46c.vb` now exposes a live x16 Microwire CS/CLK/DI/DO pin seam (`SetSerialPins`, `SerialDataOut`) with READ/WRITE/ERASE and EWEN/EWDS handling.
- Code Store composition metadata names those real source members.
- Source API validator now checks `drive_method` and `read_property` declarations as well as existing direct members.
- `tools/cp46_adapter_protocol_test.py` freezes these invariants.

## Honesty boundary
This is **not yet executable whole-board wiring**. The RTL8019 controller worker still needs board-neutral seams for its external boot-ROM and Microwire interactions, and the generated board needs transaction/event adapters that consume Adapter IR. CP46 deliberately does not infer missing address-decode/configuration behavior from copper alone.

## Architecture
Board Script remains the canonical human-readable board IR. Glue IR and Adapter IR are derived compiler state and may be serialized as JSON for tests/debugging; they are not replacement authoring languages.

---

<a id="history-vmbs-cp45-status-md"></a>

## VMBS/CP45_STATUS.md

Original location: `VMBS/CP45_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP45 Status — glue IR, FPGA language model, VB topology backend

## CP44
- Added `chimera_glue_plan.py`, lowering Construction IR semantic nets into backend-neutral glue classifications without board/part special cases.
- Real ISA8 Ethernet specimen: 56 operations; 41 platform nets, 15 internal nets; zero unbound module endpoints.
- Preserved multiplexed topology (e.g. `/BD7` simultaneously participates in boot-ROM data and Microwire semantics) instead of guessing point-to-point wiring.
- Added FPGA Component Store template and Board Script Prototype-02 programmable-logic specimen.
- FPGA model separates reusable silicon facts, board occurrence/pin-bank configuration, and programmed personality. Unknown personality remains execution TITS UP.

## CP45
- Added `chimera_emit_vb_topology.py`, first ordinary-language backend consumer of Glue IR.
- Emits all 56 solved nets/endpoints as VB.NET topology metadata with no `runtime.Link` or `AllocateModule` calls.
- This is topology emission, not yet live transaction/event adapter wiring. No false executable-complete claim.

## Architecture guardrails
- Board Script remains canonical human-readable/editable board IR and markup language.
- Construction IR and Glue IR are derived compiler state, not replacement authoring languages.
- JSON is currently a convenient diagnostic/serialization representation of derived state; it is not required as a conceptual language stage.
- Backends must remain board-neutral; no Ethernet/RTL8019/motherboard special cases in generic lowering/emission.

## Verification
CP45, CP44, CP43, CP42, CP41, CP40, CP39, CP38, CP37, CP36, CP35, CP34, CP33, CP32, CP31, CP30, CP19, CP18, CP17, CP16 regression scripts PASS in this environment.
No .NET SDK is available here; no VB compile claim is made.

## Next
Lower compatible binding combinations into executable adapter operations (platform bus transactions, external-memory transaction bridge, Microwire bridge, IRQ/event binding), while preserving multiplexed nets and refusing unsupported combinations. Then emit those operations through VB first and keep the operation vocabulary language-neutral for Python/C/HDL backends.

---

<a id="history-vmbs-cp43-status-md"></a>

## VMBS/CP43_STATUS.md

Original location: `VMBS/CP43_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP43 status — construction IR + Board Script architecture freeze

## Completed
- Froze terminology: Board Script is the canonical human-readable/editable board IR and markup language.
- Explicitly demoted JSON/serialized compiler forms to derived compiler state/dumps/manifests; JSON is not a required conceptual stage.
- Added `VMB-CONSTRUCTION-IR-1`, a language-neutral derived construction plan from solved Board Code + Code Store composition metadata.
- Real ISA8 Ethernet plan resolves 3 IC instances and 56 semantic connections with zero planning errors.
- Added first static VB backend layer: it directly constructs `Rtl8019As`, `Sst39Sf040`, and `Microwire93Lc46c`; it contains no `runtime.AllocateModule` and no `runtime.Link`.
- Honesty gate: static emitter explicitly reports 56 pending semantic connections and `ExecutableWiringComplete=False`; no fake executable-green claim.
- Added source API composition validation for directly named methods/events.
- Expanded actual `.bs` prototype grammar/model with board-level `clock`, `power`, `program`, and `constraint` declarations. Added Prototype-01 specimen and backward-compatible static parser test.
- Existing CP42..CP30 and CP19..CP16 regression suites remain green; Board Script prototype validator remains green including negative invented-part test.

## Important boundary
CP43 does NOT yet generate typed executable glue for the 56 connections. The next compiler task is adapter synthesis: map semantic connection groups and Code Store composition bindings into backend-neutral adapter operations, then emit those operations in VB first. Static instantiation is green; executable wiring remains TITS UP.

## Next
1. Build generic adapter/glue IR from connection graph + composition bindings.
2. Implement generic adapters for parallel x8 memory, Microwire, ISA controller boundary and boot-ROM bus without board-name or chip-name branches.
3. Emit VB typed glue from adapter IR; retain only legitimate external host ABI services.
4. Expand BS semantics further: net/electrical classes, differential pairs, stackup/mechanics, placement constraints, routing intent, evidence/provenance, programmable logic/payload configuration.
5. Add FPGA/CPLD component and programmable-payload contracts after the language semantics are in place.

---

<a id="history-vmbs-cp42-status-md"></a>

## VMBS/CP42_STATUS.md

Original location: `VMBS/CP42_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP42 — Static Composition Contracts + RTL8019AS IC Boundary

## CP41: composition contract seam
Code Store now carries implementation-facing `composition` metadata for source-backed modules. `chimera_contract_validate.py` checks every semantic module endpoint used by Board IR against the selected implementation contract. This remains module-driven and board-neutral.

The SST39SF040 and 93LC46C contracts cover their board-visible interfaces. Removing a contract in mutation testing correctly makes composition fail.

## CP42: RTL8019AS extraction
Added:
`Expansion Cards/Network Cards/RTL8019AS/Rtl8019As.vb`

This is intentionally an IC/controller boundary, not another NE2000 card:
- owns DP8390/RTL8019-compatible controller register state;
- owns integrated 16 KiB packet RAM and station PROM surface;
- exposes controller register reads/writes;
- exposes remote-DMA byte access;
- emits IRQ-state and transmit-frame events.

It deliberately does NOT own:
- ISA slot installation/backplane;
- base-I/O board decode;
- PIC objects or IRQ routing;
- UDP/host networking;
- boot-ROM chip;
- Microwire EEPROM chip;
- jumper/DIP topology.

Those remain external board composition concerns.

## Real three-IC Ethernet proof
The actual specimen now resolves:
- U1 RTL8019AS -> `Rtl8019As.vb`
- U2 SST39SF040 -> `Sst39Sf040.vb`
- U3 93LC46C -> `Microwire93Lc46c.vb`

Chimera source planner:
- instances: 3
- unique VB sources: 3
- ready: TRUE

Composition endpoint validator:
- instances: 3
- errors: 0
- ready: TRUE

Project staging produces one VCB project containing the generated board wrapper plus exactly those three selected IC sources.

## Important remaining gap
The staged generated board wrapper still emits the old `IVmbBoardRuntime` Build calls. Static source selection and endpoint-contract validation are now green, but the emitter does not yet generate the actual typed glue/instantiation from composition metadata. Therefore **no final static `.vcb.dll` success is claimed**.

There is no `dotnet` SDK in this environment, so no VB compile is claimed either.

## Regression
PASS: CP42, CP41, CP40, CP39, CP38, CP37, CP36, CP35, CP34, CP33, CP32, CP31, CP30, CP16, CP17, CP18, CP19.

## Next
Teach the generic chimera emitter to consume `composition` metadata and emit typed module instances/adapters/net glue instead of delegating silicon allocation/wiring back to `IVmbBoardRuntime`. Preserve the small host ABI only for legitimate board-external services.

---

<a id="history-vmbs-cp40-status-md"></a>

## VMBS/CP40_STATUS.md

Original location: `VMBS/CP40_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP40 — Board Script Multi-Destination Language Audit + BOARD-2 Lift

## Audit finding
The tree currently contains two distinct concepts named Board Script:
1. Prototype-00 human `.bs` DSL: component population, placement, straps, named nets.
2. Current Python `VMBS-BOARD-1`: dictionary-coded lossless KiCad semantic capsule consumed by Analyzer.

They must not remain conceptually conflated.

## Architecture decision
Keep `VMBS-BOARD-1` intact as a lossless KiCad source facet. Introduce `VMBS-BOARD-2` as the normalized board-semantic envelope shared by CAD, Gerber, schematic/photo reconstruction, Analyzer, Circuit Chess, Chimera, Tracer, validation, and manufacturing exporters.

Board Script owns board-specific truth. Component Store owns reusable component truth. Neither may invent missing facts.

## Added
- `BOARD_SCRIPT_LANGUAGE_AUDIT_CP40.md`
- `tools/board_v2_lift.py`
- `tools/cp40_board_v2_test.py`
- generated proof `bs_native/ISA8_Ethernet.bs.v2.json`

The lift embeds the complete BOARD-1 payload unchanged as a KiCad source facet, while promoting only Analyzer-proven normalized facts.

## Destination-specific green
BOARD-2 now demonstrates independent readiness domains:
- roundtrip_source
- structural
- vmb_execution
- electrical
- physical
- routing
- manufacturing
- evidence

For the current ISA8 Ethernet proof:
- roundtrip_source: GREEN
- structural: GREEN
- vmb_execution: GREEN at the analyzed-IR level
- electrical/physical/routing/manufacturing/evidence: intentionally NOT GREEN because those facts are not yet proven in normalized BS.

A synthetic unknown-part mutation remains round-trippable but forces structural and VMB-execution readiness false.

## Regression
PASS: CP40, CP39, CP38, CP37, CP36, CP35, CP34, CP33, CP32, CP31, CP30, CP16, CP17, CP18, CP19.

## Compiler status inherited from CP39
Generic arbitrary-N source collection/staging works. The real Ethernet specimen still correctly refuses static chimera manufacture because RTL8019AS has only a pseudocode Code Store contract. Existing SST39SF040 and 93LC46C primitives are now source-backed.

## Next
Define the Code Store static composition/endpoint contract, then implement the exact RTL8019AS chip worker or adapter at the correct IC boundary. Do not substitute the whole-card `Ne2000Isa` FAT contraction merely to make the test green.

---

<a id="history-vmbs-cp39-status-md"></a>

## VMBS/CP39_STATUS.md

Original location: `VMBS/CP39_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP39 — Generic Chimera Source Planner / Stager

## Purpose
Start replacing runtime `AllocateModule` indirection with deterministic static source composition. The compiler must remain board-neutral and arbitrary-N.

## Added
- `tools/chimera_plan.py`: resolves every `ALLOC_MODULE` through Code Store, verifies source-backed VB implementation, hashes/deduplicates sources, records entry class/factory/dependencies, and refuses partial composition.
- `tools/chimera_emit_project.py`: stages one generated board wrapper plus all unique selected module sources into one VB project and emits a source lock.
- `tools/cp39_chimera_test.py`: proves arbitrary-N selection and source deduplication with synthetic modules; contains no NIC/motherboard identity logic.
- Promoted existing `Sst39Sf040.vb` and `Microwire93Lc46c.vb` into source-backed Code Store bindings.

## Ethernet proof status
The real three-IC ISA Ethernet specimen resolves 3 instances and 2 unique VB sources. It intentionally remains **not ready** because `VMB.NET.RTL8019AS` is still a pseudocode behavioral contract, not a source-backed RTL8019AS chip implementation.

This is the desired no-invention failure:
`CHIMERA_SOURCE_REQUIRED: VMB.NET.RTL8019AS / U1`

No fallback to `Ne2000Isa` was invented because that class models a whole card/FAT contraction rather than the exact RTL8019AS IC.

## Important remaining compiler seam
Source collection is now generic, but static *wiring/instantiation* still needs a Code Store composition contract. The linker must know how module endpoints map onto reusable typed APIs without learning board identities. That is CP40 territory.

## Test
PASS: `cp39_chimera_test.py`.
No .NET build claimed: the real specimen is correctly TITS UP until RTL8019AS has source-backed implementation/composition metadata.

---

<a id="history-vmbs-cp38-status-md"></a>

## VMBS/CP38_STATUS.md

Original location: `VMBS/CP38_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP38 — Grok Alignment Audit + Support Mesh Contract

## Grok audit
Preserved Grok-era artifacts were audited against the clean canonical architecture. The useful ideas are metadata-driven VCB properties, machine-instance persistence, separation of Board Script composition from machine population, and immutable validated materialization. The Grok tree remains non-canonical because it was divergent and mixed unrelated regressions/rewrites into the experiment.

See `GROK_VMBS_ALIGNMENT_AUDIT_CP38.md`.

## Canonical changes
- Extended `compiler_db/component_store_v3.schema.json` with support-mesh contract v1.
- Defined typed semantic endpoints: capability, interface, signal, rail, clock, component, component class, constraint.
- Defined evidence-sensitive strengths: mandatory, recommended, reference, observed, inferred.
- Defined reusable support patterns with semantic participant roles.
- Explicitly forbids reusable patterns from using specimen reference-designator roles such as U17.
- Preserved CP37 legacy string `support_requirements` during transition; newly authored mesh claims use typed objects.
- Added semantic validation to `component_store_v3_validate.py`.
- Added `cp38_support_mesh_test.py`.
- No component facts or support circuits were invented/enriched in this checkpoint.

## Regression
PASS: CP38, CP37, CP36, CP35, CP34, CP33, CP32, CP31, CP30, CP16, CP17, CP18, CP19.

No .NET compile claimed or required for this schema/architecture checkpoint.

## Next
Build the generic arbitrary-N VB chimera source compositor/linker. Use the existing three-IC Ethernet VCB as the first proof: resolved Code Store implementations + solved BS wiring -> one generated `.vcb.vb` -> one `.vcb.dll`, without NIC- or motherboard-specific emitter logic.

---

<a id="history-vmbs-cp37-status-md"></a>

## VMBS/CP37_STATUS.md

Original location: `VMBS/CP37_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP37 — Component Store v3 Foundation

## Purpose
Freeze an extensible Component Store contract before the generic VB chimera linker is expanded. The store must serve identification, reconstruction, Circuit Chess, executable composition, validation, and later PCB synthesis without inventing unknown facts.

## Inventory discovered
The current store contains 23 canonical board-derived records plus 2,581 variant-family records containing 10,867 exact recognized variant names. The large recognition corpus lives in `variant_stub_policy`, not as 10,867 independent canonical records.

## Changes
- Added `compiler_db/component_store_v3.schema.json`.
- Added deterministic additive migrator `tools/component_store_v3_migrate.py`.
- Added structural validator `tools/component_store_v3_validate.py`.
- Migrated `compiler_db/component_store.json` to `VMBS-COMPONENT-STORE-3` / schema 3.
- Preserved the pre-migration store as `compiler_db/component_store_v2.cp36.json` for audit/regression.
- Added normalized domains for physical data, pins, electrical/timing/thermal data, interfaces, support mesh, implementation binding, evidence/provenance, and validation/synthesis rules.
- Support mesh now has explicit containers for requirements, provisions, alternatives, and patterns. Migration does not fabricate support relationships.
- Existing v2 fields remain in place during transition so current analyzers/tests remain compatible.
- Added `tools/cp37_component_store_v3_test.py`.

## Migration rule
Unknown stays unknown. CP37 is structural migration only. It does not infer package dimensions, footprints, voltage, timing, support circuitry, provenance confidence, or other missing facts.

A recursive preservation check confirmed every original v2 fact, family entry, and recognized variant remains unchanged; v3 only adds normalized domain containers/defaults.

## Regression
PASS: CP37, CP36, CP35, CP34, CP33, CP32, CP31, CP30, CP16, CP17, CP18, CP19 tests.

No .NET compile was claimed or required for this schema checkpoint.

## Architecture consequence
The next layer can define the support-configuration mesh vocabulary against a stable Component Store domain instead of baking motherboard knowledge into the future chimera emitter. The generic linker remains downstream: solved BS -> arbitrary N module composition -> generated `.vcb.vb` -> one `.vcb.dll`.

---

<a id="history-vmbs-cp36-status-md"></a>

## VMBS/CP36_STATUS.md

Original location: `VMBS/CP36_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP36 — FOX chipset and RTC store installment

Date: 2026-10-02
Lineage: CP35 FOX 27256 memory installment

## Added/bound
- `VMB.HEADLAND.HT12A` — existing `HeadlandHt12BoardScriptWorker`, worker id `headland.ht12-a`; the existing Headland `HT12` recognition record is now functional.
- `VMB.HUALON.HM6818P` — existing `Hm6818Rtc`, a 64-byte HM6818-family RTC/CMOS implementation used by the FOX II board. Added exact HM6818P board-observation identity to Component Store.

## Deliberate CPU hold
The FOX board positively identifies an AMD N80L286-16/S PLCC68 specimen, but the board runtime explicitly reports processor execution unavailable pending a neutral 80286 binding. CP36 records no fake functional CPU binding merely to make the store green.

## Validation
CP36 through CP30 and CP16-CP19 PASS.
No whole-machine materializer rewrite. .NET compilation remains unclaimed in this sandbox.

## Next
Continue positively identified board silicon/store work. The CPU becomes appropriate when the reusable neutral 80286 execution seam is real; DRAM waits for physical specimen evidence.

---

<a id="history-vmbs-cp35-status-md"></a>

## VMBS/CP35_STATUS.md

Original location: `VMBS/CP35_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP35 — FOX BIOS EPROM store installment

Date: 2026-10-02
Lineage: CP34 state/timing glue installment

## Board-driven memory scope
The existing Octek FOX II 286 Rev. 3.2 runtime explicitly identifies the BIOS population as paired 27256 byte lanes. `Ht12MemoryWorker286` requires two 32768-byte lanes and assembles them into one 65536-byte CPU-linear BIOS image.

## Added
`VMB.MEM.EPROM_27256_X8`
- 32768 x 8 UV EPROM behavioral contract
- A0-A14 byte addressing
- active-low chip/output gating and high-impedance disabled output
- exact 32768-byte programmed payload
- programmed contents remain instance/Firmware Store data, not component identity
- in-system VMB behavior is read-only; EPROM programmer physics are not synthesized

19 explicit 27256/27C256 recognition aliases already present in the store were promoted conservatively.

## Deliberate DRAM hold
The FOX source currently identifies RAM population by HT12 capacity class and strap code, not exact DRAM manufacturer/part numbers. CP35 therefore does not guess 41256/41464/etc. specimens or promote a generic DRAM family merely to make the store look fuller.

## Validation
CP35 through CP30 and CP16-CP19 PASS.
No materializer changes. .NET compilation remains unclaimed in this sandbox.

## Next
Find/establish the physical FOX DRAM population from board evidence or authoritative board documentation before promoting exact DRAM silicon; otherwise continue with another board-demanded, positively identified device.

---

<a id="history-vmbs-cp34-status-md"></a>

## VMBS/CP34_STATUS.md

Original location: `VMBS/CP34_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP34 — motherboard state/timing glue installment

Date: 2026-10-02
Lineage: CP33 glue logic installment

## Added reusable IC modules
- VMB.LOGIC.74XX74 — dual positive-edge D flip-flop, asynchronous preset/clear
- VMB.LOGIC.74XX273 — octal positive-edge register, asynchronous clear
- VMB.LOGIC.74XX161 — 4-bit synchronous counter, asynchronous clear
- VMB.LOGIC.74XX163 — 4-bit synchronous counter, synchronous clear
- VMB.LOGIC.74XX393 — dual 4-bit ripple counter, asynchronous clear

Python executable reference models and matching VB.NET runtime implementations were added.

## Component Store
17 exact aliases promoted. 161 and 163 remain separate because their clear semantics differ.

## Validation
CP34, CP33, CP32, CP31, CP30, and CP16-CP19 PASS after updating the CP30 aggregate functional-module registry expectation.
No whole-machine materializer changes. .NET compilation remains unclaimed in this sandbox.

## Next
Memory-device substrate: BIOS EPROM/ROM and motherboard SRAM/DRAM recognition/behavior, driven by the selected 286 board requirements rather than blanket memory-family promotion.

---

<a id="history-vmbs-cp33-status-md"></a>

## VMBS/CP33_STATUS.md

Original location: `VMBS/CP33_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP33 — motherboard glue logic installment

Date: 2026-10-02
Lineage: CP32 NEAT store installment

## Scope
Continue store population without beginning the deferred whole-machine materializer rewrite.

## Added reusable digital ICs
- `VMB.LOGIC.74XX157` — quad 2-to-1 multiplexer, common select, active-low enable.
- `VMB.LOGIC.74XX688` — 8-bit equality comparator, active-low enable/output.

Both have executable Python reference models and matching VB.NET runtime implementations through `VmbDigitalIcRegistry`.

## Component Store promotions
74x157: 74157, 74LS157, 74F157, 74HCT157, 74HC157.
74x688: 74688, 74LS688, 74ALS688, 74F688, 74HCT688, 74HC688.

The neighboring 74153/74158 families were deliberately left unresolved because they are not behaviorally interchangeable with 74157.

## Validation
CP33, CP32, CP31, CP30, and CP16-CP19 regression tests PASS.
.NET compilation is not claimed in this sandbox.

## Next
Continue motherboard-useful glue/state logic, with 74x74/273 and 74x161/163/393 as likely next candidates, then memory devices demanded by the selected motherboard.

---

<a id="history-vmbs-cp32-status-md"></a>

## VMBS/CP32_STATUS.md

Original location: `VMBS/CP32_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP32 — C&T NEAT chipset store installment

Date: 2026-10-02
Lineage: CP31 motherboard IC run

## Scope
Continue filling the stores for the upcoming whole-machine PCB materialization work. No materializer rewrite in this checkpoint.

## Added functional chipset packages
The existing VMB source already contains separate Board Script workers for four physical Chips & Technologies NEAT packages. CP32 exposes those implementations through stable Code Store MODULE_IDs and promotes only the matching canonical recognition aliases:

- `VMB.CT.82C206` — Integrated Peripherals Controller — `Ct82C206BoardScriptWorker`
- `VMB.CT.82C211` — CPU/Bus Controller — `Ct82C211BoardScriptWorker`
- `VMB.CT.82C212` — Page/Interleave and EMS Memory Controller — `Ct82C212BoardScriptWorker`
- `VMB.CT.82C215` — Data/Address Buffer — `Ct82C215BoardScriptWorker`

Each retains its own physical IC identity. The four-package chipset is not collapsed into one magic module.

## Deliberate exclusions
`82C235` and `82C236` remain blackholed because the current tree does not provide matching audited per-package implementations. Other OPTi/UMC/PicoPower 82C206-family recognition seeds were not conflated with the C&T part.

## Validation
- CP32 NEAT store test: PASS — 4 physical packages.
- CP31 motherboard IC test: PASS.
- CP30 74xx functional suite: PASS.
- CP16/17/18/19 mutation regressions: PASS.
- Windows/.NET compilation remains unclaimed in this sandbox.

## Next store work
Continue motherboard-first store population: likely glue logic and memory parts actually demanded by the selected test motherboard, while preferring already implemented/audited VMB silicon when available.

---

<a id="history-vmbs-cp31-status-md"></a>

## VMBS/CP31_STATUS.md

Original location: `VMBS/CP31_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP31 — Motherboard IC functional binding run

Date: 2026-10-02
Lineage: CP30 functional IC installment 01

## Direction
Motherboard-oriented silicon is now the priority. Adjacent glue remains allowed when it directly supports motherboard manufacture, but broad catalog completion is deferred.

## Promoted motherboard functions
Five major AT motherboard functions are now first-class implemented Code Store MODULE_IDs instead of recognition-only blackhole seeds:

- `VMB.AT.PIC8259A` -> existing `Pic8259` runtime
- `VMB.AT.PIT8254` -> existing `Pit8253` AT 8253/8254-compatible runtime
- `VMB.AT.DMA8237A` -> existing `Dma8237` runtime
- `VMB.AT.KBC8042` -> existing `KeyboardController8042` runtime
- `VMB.AT.RTC146818` -> existing `CmosRtc` runtime

This deliberately reuses the mature motherboard behavior already present in VMB rather than writing duplicate toy state machines in VMBS.

## Recognition promotion
The canonical Intel-family recognition groups for 8237A, 8259A, 8253/8254, 8042/8742, and MC146818 were promoted to functional bindings. Seventeen exact aliases in those canonical groups now resolve to implemented motherboard MODULE_IDs. Other provenance/variant seeds remain untouched until they are audited; no blanket alias inference was made.

## Runtime stitch
`VmbModuleRegistry.Supports()` now recognizes the five motherboard MODULE_IDs. Actual motherboard construction still needs a generic motherboard-module factory/materialization contract; CP31 does not pretend the ISA-card root factory can construct motherboard-local devices.

## Tests
Focused CP31 motherboard binding test: PASS.
CP30 digital IC suite: PASS after scoping its assertion to `VMB.LOGIC.*` functional variants so later functional families are legal.
Existing CP16-CP19 regression chain remained PASS in this run. CP14/15 were also observed PASS before the long aggregate test invocation was interrupted by sandbox execution limits.
Python startup emits an unrelated artifact_tool spreadsheet warmup warning in this environment; tested scripts still returned zero.
Windows/.NET compile remains unclaimed in this sandbox.

## Next motherboard manufacture work
1. Define the generic motherboard-local module construction contract, including dependencies (PIC references, memory callbacks, keyboard endpoint, persistence).
2. Promote/audit the exact 82C206 / NEAT chipset parts selected for the test motherboard.
3. Fill only the glue/clock/buffer/latch/ROM/RAM parts demanded by that board.
4. Print the new BS/VCB test motherboard early and let TITS expose the next missing silicon rather than completing the catalog first.

---

<a id="history-vmbs-cp30-status-md"></a>

## VMBS/CP30_STATUS.md

Original location: `VMBS/CP30_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP30 — Functional IC modules installment 01

Date: 2026-10-02
Lineage: CP29 clean + recognition installment 04

## Implemented

Promoted eleven deterministic 74xx families from recognition-only blackhole stubs to reusable functional MODULE_IDs:

- VMB.LOGIC.74XX00 — quad 2-input NAND
- VMB.LOGIC.74XX02 — quad 2-input NOR
- VMB.LOGIC.74XX04 — hex inverter
- VMB.LOGIC.74XX08 — quad 2-input AND
- VMB.LOGIC.74XX32 — quad 2-input OR
- VMB.LOGIC.74XX86 — quad 2-input XOR
- VMB.LOGIC.74XX138 — 3-to-8 active-low decoder
- VMB.LOGIC.74XX244 — octal tri-state buffer
- VMB.LOGIC.74XX245 — octal bidirectional tri-state transceiver
- VMB.LOGIC.74XX373 — octal transparent latch
- VMB.LOGIC.74XX374 — octal positive-edge register

55 exact aliases across TTL/LS/ALS/F/HC/HCT/ACT variants are covered where those aliases already existed in the recognition catalog. No package alias was promoted unless the family pinout/function represented here is explicitly known.

## Architecture

- Real behavior lives under stable Code Store MODULE_IDs.
- `Hardware/IC Modules/DigitalLogicIc.vb` provides the VMB implementation registry and IC classes.
- `tools/ic_logic_models.py` provides a deterministic Python reference implementation for VMBS-side regression without requiring dotnet.
- Functional variant-family entries now carry explicit pin semantics. Analyzer consumes those semantics without fabricating canonical PART_ID records.
- Functional variants use `VARIANT:<normalized part>` identities; untouched recognition-only families remain `VARIANT_STUB:*` and still detonate through `VMB.STUB.DIPSHIT_BLACKHOLE` during manufacture.
- Digital logic is modeled at VMB computer-visible resolution. Analog TTL thresholds, drive strength, propagation delay, and power behavior are not claimed.

## Validation

- CP30 functional truth/state/tri-state suite: PASS (11 MODULE_IDs, 11 families, 55 aliases).
- Synthetic two-74LS04 board: Analyzer 2 modules / 2 DIGITAL interface links / TITS DOWN; Constructor TITS DOWN; lowering 5 instructions; validator PASS.
- Existing Ethernet end-to-end path unchanged: 34 devices / 135 nets / 3 modules / 4 controls / 56 links / 85 instructions; Board Code validator PASS.
- CP16, CP17, CP18, CP19 mutation regressions: PASS.
- CP29 blackhole regression: PASS for unimplemented ICs.
- Python syntax compile across VMBS tools: PASS.
- Static VMB project inclusion: PASS.
- Windows/.NET compile is not claimed because dotnet/vbc are not present in this sandbox.

## Known pre-existing test issue

`tools/cp20_vb_mutation_test.py` still expects the old `generated_vmb/ISA8_Ethernet.GeneratedBoard.vb` filename. CP29 renamed retained generated VCB source to `*.vcb.vb`; this stale test path predates CP30 and was not treated as an IC regression.

## Next logical IC installment

Keep the same rule: manuals/pinouts first, then implementation. Good next candidates are 74LS139/153/157/158, 74LS74/273, 161/163/393, 688, followed by SRAM/EPROM primitives. Programmable controllers (8259/8254/8237/8255/16550/8042) should follow as separate state-machine installments rather than being approximated as glue logic.

---

<a id="history-vmbs-cp29-status-md"></a>

## VMBS/CP29_STATUS.md

Original location: `VMBS/CP29_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP29 — VCB DLL manufacturing pivot

CP29 changes the VMBS/VMB seam from interpreted `.vmbcard` packages to manufactured Virtual Circuit Board plugins.

Canonical layout:

    VMB/
      VMBS/
        VMBS.bat
        VCB Store/
          PCB.meta
          <board>/<board>.vcb.vb
                  <board>.vcb.dll

Build ownership:
- BS is editable source truth.
- VMBS analyzes BS and emits `*.vcb.vb`.
- VMBS invokes the .NET SDK to compile `*.vcb.dll` against VMB's public VCB ABI.
- VMB does not design or compile boards.
- VMB polls only `PCB.meta`; a count change invalidates the catalog and causes one DLL rescan.
- Configurator discovers finished VCB DLLs; installed-board UI is Properties, not a PCB editor.

Implemented in CP29:
- `VcbRuntimeApi.vb`: IVirtualCircuitBoard/IVmbBoardRuntime ABI, DLL catalog/loader, ISA materialization runtime.
- VMBS moved under VMB.
- `vb_board_emit.py` now emits an IVirtualCircuitBoard plugin with embedded install metadata.
- `vcb_compile.py` manufactures the DLL with `dotnet build`, referencing the already-built VMB assembly.
- launcher manufacture path writes source/DLL directly to VMBS/VCB Store and publishes board count.
- VMB catalog compatibility facade now obtains network choices from VCB DLL metadata.
- generated VCB Build() is executed for installation; CP29 intentionally retains the existing FAT RTL8019 contraction.
- VCB selection prefix changed from `vmbcard:` to `vcb:`.

Validated here:
- fresh KiCad -> BS -> Analyzer -> Construction IR -> 85 Board Code instructions -> generated `ISA8_Ethernet.vcb.vb`.
- 3 modules, 4 controls, 15 config bindings, 56 links, 2 persistent slots.
- IO domain 0x200..0x3E0 step 0x20, default 0x300.
- IRQ domain {3,4,5,9}, default 9.
- Python syntax checks pass.

Not validated here:
- .NET/VB compilation: this sandbox has no `dotnet` executable.
- runtime DLL loading/boot: must be hard-gated on the user's Windows/.NET machine.

Next after compile gate:
- add standardized Ethernet endpoint/RJ45 Connected Boolean + TX/RX boundary without an invented packet FIFO;
- expose VCB controls/device slots through installed-board Properties;
- remove remaining legacy `.vmbcard` parser once the DLL path passes runtime testing.

---

<a id="history-vmbs-cp28-status-md"></a>

## VMBS/CP28_STATUS.md

Original location: `VMBS/CP28_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP28 — AT27BV010

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp27-status-md"></a>

## VMBS/CP27_STATUS.md

Original location: `VMBS/CP27_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP27 — AT27LV4096

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-vmb-compiled-card-stitch-cp26-md"></a>

## VMBS-VMB-COMPILED-CARD-STITCH-CP26.md

Original location: `VMBS-VMB-COMPILED-CARD-STITCH-CP26.md`. Relative paths in this historical report refer to that original directory.

# VMBS ↔ VMB compiled-card stitch — CP26

## Goal
Make the known-powering-on modular VMB load a board artifact emitted by the Python VMBS compiler without generating per-board VB source.

## Implemented
- Added `VMB-CARD-1` package format: inspectable install header + `VMB-BOARD-CODE-1` payload.
- Added Python `tools/vmbcard_pack.py` to VMBS.
- Packaged Sergey RTL8019AS specimen as `Card Store/ISA8_Ethernet_RTL8019.vmbcard`.
- Added VB `VmbCompiledCardStore`, package/header parser, loader and `VmbModuleRegistry`.
- Network-card configurator now enumerates NETWORK cards from Card Store alongside legacy built-in choice.
- Added generic compiled-card configuration dialog driven by install-header properties and physical control declarations.
- Machine materializer can install `vmbcard:<CARD_ID>` through the real ISA backplane.
- Modified real `Ne2000Isa` module so legal base/IRQ domains are instance-specific and can come from compiled physical-board truth.
- Added real VB storage-device modules `Sst39Sf040` and `Microwire93Lc46c` for future skinny topology wiring.
- Machine Build Script accepts installed compiled network-card IDs and validates its base/IRQ against the card header.

## Current specimen truth
- Card ID: `SERGEY.ISA8.RTL8019`
- Bus: ISA8
- Primary module: `VMB.NET.RTL8019AS`
- IO_BASE: 200h..3E0h in 20h increments; default 300h
- IRQ: 3, 4, 5, 9; default 9
- Physical controls: SW1 x2, SW2 x2, SW3 x4, SW4 x5; power-off-only
- Hardware TITS: down
- Default execution TITS: down
- All-selectable-config execution TITS: up (boot ROM payload/path remains unfinished)

## Honest validation boundary
Static stitch audit passes. This sandbox has no dotnet/msbuild/vbc toolchain, so VB compilation and runtime boot are NOT claimed. Next hard gate is compile/run on the user's Windows VMB environment.

---

<a id="history-vmbs-cp26-status-md"></a>

## VMBS/CP26_STATUS.md

Original location: `VMBS/CP26_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP26 — AT27LV1024

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp25-status-md"></a>

## VMBS/CP25_STATUS.md

Original location: `VMBS/CP25_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP25 — AT27LV080

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp24-status-md"></a>

## VMBS/CP24_STATUS.md

Original location: `VMBS/CP24_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP24 — AT27LV040

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp23-status-md"></a>

## VMBS/CP23_STATUS.md

Original location: `VMBS/CP23_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP23 — AT27LV020

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp22-status-md"></a>

## VMBS/CP22_STATUS.md

Original location: `VMBS/CP22_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP22 — AT27LV010

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp21-status-md"></a>

## VMBS/CP21_STATUS.md

Original location: `VMBS/CP21_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP21 — AT27LV512R

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp20-status-md"></a>

## VMBS/CP20_STATUS.md

Original location: `VMBS/CP20_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP20 — AT27LV256R

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp19-status-md"></a>

## VMBS/CP19_STATUS.md

Original location: `VMBS/CP19_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP19 — AT27LV040A

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp18-status-md"></a>

## VMBS/CP18_STATUS.md

Original location: `VMBS/CP18_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP18 — AT27LV020A

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp17-status-md"></a>

## VMBS/CP17_STATUS.md

Original location: `VMBS/CP17_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP17 — AT27LV010A

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp16-status-md"></a>

## VMBS/CP16_STATUS.md

Original location: `VMBS/CP16_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP16 — AT27LV512A

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp15-status-md"></a>

## VMBS/CP15_STATUS.md

Original location: `VMBS/CP15_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP15 — AT27LV256A

Added primary-manufacturer-family-documented identity and executable installed-machine read seam. Unknown exact physical/electrical facts remain TITS UP.

---

<a id="history-vmbs-cp14-status-md"></a>

## VMBS/CP14_STATUS.md

Original location: `VMBS/CP14_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP14 — PCB-to-VMB semantic foundation

CP14 changes the Analyzer from a board inventory pass into a deterministic VMB-facing semantic pass without becoming a circuit simulator.

## Implemented

- Component Store now carries behaviorally relevant pin semantics for the current VMB-capable seed devices where needed.
- RTL8019AS reset-latched configuration pins are represented as component knowledge, including their post-reset multiplexed BROM roles.
- 93LC46C pin semantics include live Microwire pins and static ORG behavior.
- SST39SF040 declares a 524288-byte programmable payload slot without assuming payload identity.
- Generic DIP-switch primitives are derived from component/package semantics and marked power-off configurable.
- Generic resistor-array contraction allows the VMB semantic resolver to cross passive elements without creating runtime resistor objects.
- Added reusable ISA8 platform connector semantics in `compiler_db/platform_store.json`.
- Analyzer resolves component configuration sources as CONSTANT, INTERNAL_DEFAULT, USER_CONTROL, or unresolved CONNECTED.
- Analyzer enumerates physical controls into Board IR instead of compiling them away.
- Code Store implementation placeholders are now documented pseudocode behavioral contracts. They explicitly require complete VMB-visible modes rather than specimen-only behavior.
- Added Firmware Store skeleton and ownership policy. Firmware payload identity remains separate from ROM/EEPROM/Flash component identity.
- Composite-module policy is documented: prefer one reusable module per meaningful IC; contraction is an exceptional optimization and must be invariant for every board matched by that composite pattern.

## Ethernet specimen result

The Analyzer finds four DIP controls and derives their relationships to RTL8019AS configuration pins through the PCB graph:

- SW1 -> JP / PNP
- SW2 -> IRQS0 / IRQS1; IRQS2 uses the RTL8019AS documented internal reset default on this topology
- SW3 -> IOS0..IOS3
- SW4 -> BS0..BS4
- 93LC46C ORG -> constant high -> x16 organization

These are derived from Component Store pin intentions + generic passive/control semantics + BS connectivity. No board hash, board name, reference-specific answer table, or specimen-specific IO/IRQ constant is used.

`ISA8_Ethernet.board_ir.cp14.json` reaches TITS down for the **Board-IR derivation target**. This does NOT claim a runnable VMB machine yet; firmware installation and the later Board Compiler/runtime integration are separate targets.

## Anti-smuggling regression

`tools/cp14_mutation_test.py` modifies the source fixture only to generate a deliberately altered BS: SW3 element 1 is disconnected from RN2 and tied to GND. The same Analyzer then changes U1 IOS0 from `USER_CONTROL(SW3.1)` to the RTL8019AS `INTERNAL_DEFAULT(0)`.

The mutation test passes. This demonstrates that the derived configuration responds to changed topology rather than a memorized specimen answer.

## Next

1. Expand semantic contracts for all VMB-relevant RTL8019AS interfaces required by this card.
2. Derive module-to-module/interface wiring (EEPROM, boot flash, ISA, reset, Ethernet-facing interface) from the graph.
3. Encode configuration truth tables from authoritative component documentation so user-control states can map to IO base, IRQ, and BROM configuration without hard-coded board answers.
4. Add further topology mutations for IRQ and BROM controls.
5. Only after the Board IR is mature, cross-examine these pseudocode contracts against the real VMB architecture and implement/patch VB.NET modules.

---

<a id="history-vmbs-cp13-status-md"></a>

## VMBS/CP13_STATUS.md

Original location: `VMBS/CP13_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP13 — ANAL-IZER bootstrap

CP13 begins the deterministic BS -> VMB Board IR path using the externally validated CP12 Ethernet specimen.

Implemented `tools/board_analyzer.py`. Runtime inputs are only the VMBS board, Component Store, board-local component map, and Code Store. It does not consult KiCad source, source sidecars, web data, or AI inference.

First-pass extraction from `ISA8_Ethernet.bs.json`:
- 34 physical device instances
- 135 nets
- 364 component-pad endpoints attached to nets
- 0 unknown component types against the current Component Store
- ISA8 edge connector recognized as BUS1
- 3 VMB-resolution device bindings resolved through Component Store -> MODULE_ID -> Code Store:
  - U1 RTL8019AS -> VMB.NET.RTL8019AS
  - U2 SST39SF040 -> VMB.MEM.SST39SF040
  - U3 93LC46C -> VMB.MEM.MICROWIRE_93LC46C

Generated `compiler_db/ISA8_Ethernet.board_ir.json` using `VMBS-BOARD-IR-1`.

## Honest completeness gate

The physical graph is extracted, but VMB compilation is NOT complete yet. The analyzer deliberately emits required errors rather than guessing:
- `IO_DECODE_UNRESOLVED`: RTL8019 ISA I/O base still must be derived from board decode/configuration topology.
- `CONFIGURATION_UNRESOLVED`: IRQ / boot-ROM configuration still must be derived from switches, straps, and topology.

Therefore for the **VMB executable substrate**:

- BS physical/KiCad substrate: COMPLETE, TITS DOWN (validated in CP12 with independent Altium load)
- Analyzer -> runnable VMB substrate: INCOMPLETE, TITS UP

Next: deterministic topology recognition for switch/jumper networks, ISA decode, IRQ selection, ROM mapping, clocks, and programmed contents.

---

<a id="history-vmbs-cp12-status-md"></a>

## VMBS/CP12_STATUS.md

Original location: `VMBS/CP12_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP12 Status

## KiCad exporter formatting correction

The CP11 sidecar-blind exporter emitted the entire board as one long line and, more importantly, its formatting routine grouped parent atoms before child nodes. That grouping could alter semantic token order in mixed-content S-expressions.

CP12 replaces that exporter with an order-preserving KiCad-style pretty printer:
- atomic-only lists remain on one line;
- nested lists are indented with tabs and emitted on separate lines;
- mixed atom/child order is preserved exactly;
- no source whitespace or lexical sidecar is consulted.

Blind regeneration from `ISA8_Ethernet.bs.json` produces `ISA8_Ethernet.from_bs.pretty.kicad_pcb`.

Verification against the untouched upstream board after removing whitespace tokens:
- original non-whitespace tokens: 108,763
- generated non-whitespace tokens: 108,763
- sequence identical: TRUE

The generated file is ~539 KB and 27,135 lines. External Altium 2D/3D validation remains pending.

TITS remains UP until the external CAD rendering discrepancy is resolved.

---

<a id="history-vmbs-cp11-status-md"></a>

## VMBS/CP11_STATUS.md

Original location: `VMBS/CP11_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP11 STATUS — BS CAD completeness gate

## Result
The first specimen now passes the CP07 sidecar-independence acceptance test at KiCad semantic-design level.

- Source: `ISA8_Ethernet.kicad_pcb`
- Native board file: `bs_native/ISA8_Ethernet.bs.json`
- Standalone exporter: `tools/bs_export.py`
- Independent semantic comparator result: **PASS** (`semantic_equal: true`)
- Byte equality: intentionally **false**; exporter emits canonical formatting rather than source whitespace.
- Export test was run in `/mnt/data/cp11_blind` containing only the BS file and standalone exporter. No original KiCad file, lexical token stream, reconstructed copy, or parsed source tree was available to the exporter.

## Representation
`VMBS-BOARD-1` stores the design as dictionary-coded semantic records:
- 149 native construct/head types
- 11,624 unique typed scalar atoms
- ordered occurrence tree referencing those dictionaries
- no whitespace, source byte spans, lexical tokens, or raw source bytes

This is deliberately generic enough to retain every meaningful construct encountered in the specimen while later typed/optimized BS families can replace generic records without changing semantics.

## TITS gate
For the KiCad-export substrate on this specimen:

`BS: COMPLETE`  
`TITS: DOWN`  
`EXPORT: PASS`

This does **not** claim the format is complete for every KiCad version or every PCB in existence. New specimens can introduce constructs that require schema growth.

## Next
Begin deterministic Analyzer/Board-IR work against this now-complete specimen while continuing to refine generic semantic records into compact typed BS families.

---

<a id="history-vmbs-cp10-status-md"></a>

## VMBS/CP10_STATUS.md

Original location: `VMBS/CP10_STATUS.md`. Relative paths in this historical report refer to that original directory.

# CP10 STATUS

Catch-up implementation checkpoint.

## Implemented
- Component Store promoted to schema v2; all 23 seed records carried forward.
- RTL8019AS, SST39SF040 and 93LC46C records enriched from authoritative manufacturer material, without inventing unspecified facts.
- Deterministic SQLite indexes rebuilt for PART_ID, aliases/MPN and MODULE_ID.
- Code Store v2 seeded with three VMB-resolution MODULE_ID contracts and declarative configuration points.
- Programmed-device content schema separates physical part identity from ROM/EEPROM/Flash contents.
- Circuit Store v2 seeded with reusable deterministic support/configuration recipes.
- TITS diagnostic contract frozen as a one-bit gate with separate detailed resolution states.
- Native BS core snapshot now captures header/general/title/layers/setup/net table/net classes and 239 board graphics outside the old CP07 compact physical dictionaries.

## Still blocking
CP07 sidecar-independent export is NOT yet a pass. Footprint metadata, full zones, text/graphics normalization and exporter/semantic comparator still need closure before claiming a complete `.bs` format.

---

<a id="history-vmbs-cp09-status-md"></a>

## VMBS/CP09_STATUS.md

Original location: `VMBS/CP09_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP09 — Architecture Catch-Up Plan and Compiler Boundaries

CP09 records the expanded VMBS architecture before implementation outruns the design.

Added:
- `VMBS_MASTER_PLAN.md` — ordered catch-up/implementation plan.
- `compiler_db/code_store.json` — initial stable MODULE_ID/code-store contract.
- `compiler_db/circuit_store.json` — initial deterministic Circuit Chess recipe-store contract.
- `compiler_db/board_ir.schema.json` — initial Analyzer -> Board Compiler IR contract including one-bit TITS gate.

No claim is made that CP07 sidecar independence is fixed. CP07 remains a failing acceptance fixture until compact BS data alone can regenerate a semantically equivalent KiCad board.

The CP08 component store remains the current 23-record seed and is carried forward unchanged pending schema v2 work.

---

<a id="history-vmbs-cp08-status-md"></a>

## VMBS/CP08_STATUS.md

Original location: `VMBS/CP08_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP08 — Global Component Record Store Bootstrap

CP08 moves component specifications out of the board-local token dictionary and into a reusable compiler/analyzer record store.

## Architecture

- **Global component record store:** stable 128-bit `PART_ID` records.
- **Board-local dictionary:** compact local token -> global `PART_ID` only.
- **Occurrence/location directory:** local token + board-instance data.
- A board therefore does not duplicate the canonical part record.
- The compiler/analyzer resolves local token -> `PART_ID` -> global record deterministically.
- Aliases are indexed separately so second-source or alternate markings can converge on one canonical record without duplicating the model.
- Small manufacturer differences can later be stored as deltas; true differences get separate records.

## Bootstrap from ISA8 Ethernet specimen

- 23 canonical dictionary entries were imported into the global store.
- 23 board-local token mappings were emitted.
- The specimen still uses 8-bit local component tokens.
- Exact named devices currently researched into structured records:
  - RTL8019AS
  - SST39SF040
  - 93LC46C
  - Amphenol RJMG1BD3B8K1ANR
- Generic passives, switches, connectors, crystal, mounting holes and board graphics are entered strictly from CAD-provided information; unspecified electrical/manufacturer properties remain unknown rather than invented.

## Files

- `compiler_db/component_store.sqlite3` — authoritative linear lookup DB prototype.
- `compiler_db/component_store.json` — human-readable/export form.
- `compiler_db/ISA8_Ethernet.component_map.json` — board-local token -> PART_ID map.
- `tools/build_component_store.py` — deterministic bootstrap builder.
- `tools/component_lookup.py` — exact PART_ID/alias lookup utility.

## ID choice

The global `PART_ID` is currently a deterministic 128-bit SHA-256 prefix over the canonical CAD-derived intrinsic identity. This is separate from BS's adaptive board-local token width. The global-ID scheme can be revised before format freeze if we want registry-assigned IDs instead.

## Important next work

The global record schema is intentionally richer than the current records. Pin maps, package dimensions, electrical limits, timing, behavioral-model binding, validation rules, support requirements and synthesis rules can be filled incrementally from verified source records. The runtime analyzer itself remains deterministic: lookup, field/range comparison, and rule execution only.

---

<a id="history-vmbs-cp07-status-md"></a>

## VMBS/CP07_STATUS.md

Original location: `VMBS/CP07_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP07 — Sidecar Independence Gate

The first hard semantic-only reconstruction test was run against CP06.

**Result: FAIL (expected and useful).**

The test forbids the generator from consulting the original KiCad file, lexical token stream, byte-exact reconstructed copy, or the full semantic parse tree. Only the compact component/physical dictionaries and occurrence/location directories are visible.

Those structures correctly retain the already-atomized components, footprints, pads, drills, trace segments, vias, zones and drawings, but CP06 still relies on the broader parsed/source representation for several board-level and metadata structures. Therefore CP06 cannot honestly regenerate a complete KiCad board from the compact representation alone.

This checkpoint turns that distinction into an explicit acceptance gate. Future checkpoints must not claim semantic reconstruction until a fresh `.kicad_pcb` is generated with the source sidecar unavailable and an independent comparison reports zero meaningful differences.

Missing compact-authoritative families currently include board header/general data, layer table, setup/design rules, net table, full drawing/text payload semantics, footprint metadata/properties, complete zone semantics, and document/plot metadata.

The original source/token sidecar remains useful provenance but is forbidden input to reconstruction machinery.

---

<a id="history-vmbs-analysis-cp06-cp06-status-md"></a>

## VMBS/analysis_cp06/CP06_STATUS.md

Original location: `VMBS/analysis_cp06/CP06_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP06 — Full KiCad Semantic Accounting Pass

CP06 promotes every S-expression construct present in the RTL8019 ISA Ethernet specimen into either a dedicated semantic atom/family or an explicitly consumed structural field. Numeric child records under `(layers ...)` are recognized as KiCad layer-table rows rather than unknown numeric node heads.

## Preservation oracle
- Source bytes: 494,840
- Source SHA-256: `04b0f21717bf0751f340f6df4d1ba428f7d705669745d59fe7dd4d3b9fa2c8dc`
- Reconstructed SHA-256: identical
- Byte-exact reconstruction: PASS
- Unaccounted bytes: 0

## Semantic accounting
- Parsed S-expression occurrences: 23,949
- Unique node heads: 149
- Semantic occurrences: 7,648
- Structural/consumed occurrences: 16,301
- Preserved-only occurrences: **0**
- Preserved-only heads: **0**

New explicit families include board/setup/design rules, net classes and membership, layer-table rows, footprint metadata, 3-D model transforms, text/font/effect data, zone/fill rules, title/document metadata, plotting/export metadata, and legacy KiCad statistics.

`semantic_promote.py` stores source spans and source values for promoted records. This semantic accounting is additive: the lexical token stream remains the byte-exact reconstruction authority.

## Existing physical dictionaries retained
- Components: 23 types / 34 instances, 8-bit type tokens
- Footprints: 26 / 34
- Pads: 20 / 366
- Drills: 9 / 229
- Trace segments: 9 / 1,219
- Vias: 1 / 95
- Zones: 1 / 5
- Drawings: 23 / 820

## Important scope statement
Zero preserved-only nodes means complete semantic *accounting for this specimen's KiCad grammar*, not that VMBS already understands every electrical consequence of every field. Engineering validation and behavioral synthesis remain later layers.

---

<a id="history-vmbs-analysis-cp05-cp05-status-md"></a>

## VMBS/analysis_cp05/CP05_STATUS.md

Original location: `VMBS/analysis_cp05/CP05_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP05 — Semantic Coverage Audit

CP05 adds a node-by-node KiCad semantic coverage audit on top of the CP04 byte-exact atomization.

## Invariants

- Source bytes: 494,840
- Byte-exact reconstruction: PASS
- Unaccounted bytes: 0
- Source/reconstructed SHA-256: `04b0f21717bf0751f340f6df4d1ba428f7d705669745d59fe7dd4d3b9fa2c8dc`

## Coverage

- S-expression node occurrences: 23,949
- Unique node heads: 149
- Dedicated semantic heads: 15
- Structural/consumed field heads: 19
- Preserved-only heads: 115
- Preserved-only occurrences: 1,029

`PRESERVED_ONLY` does NOT mean lost. Every occurrence remains in the lossless lexical token stream and reconstructs byte-for-byte. It means CP05 has not yet promoted that construct into a dedicated VMBS semantic atom.

## Largest preserved-only families

- `thickness` — 163 occurrences; parents: font=162, general=1
- `font` — 162 occurrences; parents: effects=162
- `add_net` — 134 occurrences; parents: net_class=134
- `xyz` — 87 occurrences; parents: rotate=29, scale=29, at=29
- `justify` — 66 occurrences; parents: effects=66
- `angle` — 35 occurrences; parents: gr_line=22, fp_arc=8, gr_arc=5
- `descr` — 34 occurrences; parents: module=34
- `tags` — 34 occurrences; parents: module=34
- `tedit` — 34 occurrences; parents: module=34
- `path` — 32 occurrences; parents: module=32
- `model` — 29 occurrences; parents: module=29
- `rotate` — 29 occurrences; parents: model=29
- `scale` — 29 occurrences; parents: model=29
- `attr` — 25 occurrences; parents: module=25
- `arc_segments` — 5 occurrences; parents: fill=5
- `connect_pads` — 5 occurrences; parents: zone=5
- `hatch` — 5 occurrences; parents: zone=5
- `min_thickness` — 5 occurrences; parents: zone=5
- `thermal_bridge_width` — 5 occurrences; parents: fill=5
- `user_trace_width` — 4 occurrences; parents: setup=4
- `comment` — 3 occurrences; parents: title_block=3
- `uvia_drill` — 3 occurrences; parents: net_class=2, setup=1
- `via_drill` — 3 occurrences; parents: net_class=2, setup=1
- `diff_pair_gap` — 2 occurrences; parents: net_class=2
- `diff_pair_width` — 2 occurrences; parents: net_class=2
- `net_class` — 2 occurrences; parents: kicad_pcb=2
- `trace_width` — 2 occurrences; parents: net_class=2
- `uvia_dia` — 2 occurrences; parents: net_class=2
- `via_dia` — 2 occurrences; parents: net_class=2
- `0` — 1 occurrences; parents: layers=1

## Next semantic promotion targets

1. Board/setup and design-rule data (`setup`, `net_class`, widths, clearances, via/uvia constraints).
2. Footprint metadata and 3-D model transforms (`descr`, `tags`, `attr`, `model`, `scale`, `rotate`, `xyz`).
3. Text/font/effects details (`font`, `thickness`, `justify`).
4. Zone/fill parameters (`hatch`, `connect_pads`, `min_thickness`, thermal/fill settings).
5. Plot/title/document metadata. These are non-electrical but remain part of lossless CAD round trip.
6. Layer-table rows are syntactically unusual KiCad lists whose first item is the numeric layer ID; promote them explicitly rather than treating numeric heads as unknown constructs.

## Rule

No preserved-only construct may be silently discarded. Promotion to semantic atoms is additive; the byte-exact preservation layer remains the reconstruction oracle.

---

<a id="history-vmbs-analysis-cp04-cp04-status-md"></a>

## VMBS/analysis_cp04/CP04_STATUS.md

Original location: `VMBS/analysis_cp04/CP04_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP04 — Physical Primitive Canonicalization

CP04 extends the byte-exact CP03 representation with reusable physical primitive dictionaries and separate occurrence/location records.

## Verified source round trip
- Source: `ISA8_Ethernet.kicad_pcb`
- Source bytes: 494,840
- Source SHA-256: `04b0f21717bf0751f340f6df4d1ba428f7d705669745d59fe7dd4d3b9fa2c8dc`
- Reconstructed SHA-256: identical
- Unaccounted source bytes: 0

## Canonical dictionaries / occurrences
- Components: 23 types / 34 instances
- Footprint physical definitions: see manifest (canonical relative geometry separated from board placement)
- Pads: 20 types / 366 occurrences
- Drills: 9 types / 229 occurrences
- Copper segments: 9 types / 1,219 occurrences
- Vias: 1 type / 95 occurrences
- Zones: 1 type / 5 occurrences
- Drawings: 23 types / 820 occurrences

Every physical dictionary independently chooses the smallest token width after cardinality is known. On this specimen all fit in 8-bit type tokens.

## Separation rule
Dictionary = reusable physical specification.
Occurrence = owner/reference, position/geometry placement, net/connectivity, or other occurrence-specific facts.
Raw lexical tokens remain the byte-exact authority, so semantic canonicalization cannot silently lose unsupported KiCad data.

## Next
Audit footprint deduplication and expand accounting to explicit coverage reports for every KiCad node head, distinguishing semantically interpreted from losslessly-preserved-only constructs.

---

<a id="history-vmbs-analysis-cp03-cp03-status-md"></a>

## VMBS/analysis_cp03/CP03_STATUS.md

Original location: `VMBS/analysis_cp03/CP03_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP03 Status — corrected component dictionary

Specimen: `ISA8_Ethernet.kicad_pcb`

## Corrections made
- Component dictionary now describes only canonical source-specified component identity/specification.
- Reference, location, rotation/layer, pad/net wiring and other instance data live in the instance list.
- Repeated same-type components collapse to one type token.
- Token width is selected only after dictionary construction.
- Width header is now: `000=8, 001=16, 010=24, 011=32, 100=40, 101=48, 110=56, 111xxxxx=extended`.
- Extended width is `56 + (xxxxx + 1)*8`, giving 64..312 bits.
- Unknown/source-specific properties are preserved literally; VMBS does not invent missing specifications.

## RTL8019 specimen results
- Source bytes: 494,840
- Byte-exact reconstruction: PASS
- Unaccounted bytes: 0
- Source/reconstruction SHA-256: identical
- Raw lossless tokens: 169,891
- Raw token dictionary entries: 11,754
- Component instances: 34
- Canonical component types: 23 (down from erroneous 31 in CP02)
- Selected component token width: 8 bits
- Component header byte: 0x00
- Drilled holes: 229 (95 vias + 134 pad/NPTH holes)

## Important boundary
The component dictionary is intentionally not the footprint/pad geometry store. Physical footprint geometry, pads, holes, traces, vias, zones, drawings and all other KiCad constructs remain represented by the lossless CAD atom stream and semantic inventories. Future passes will give each physical primitive its own compact semantic token/data section without weakening byte-exact source preservation.

---

<a id="history-vmbs-analysis-cp02-cp02-status-md"></a>

## VMBS/analysis_cp02/CP02_STATUS.md

Original location: `VMBS/analysis_cp02/CP02_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP02 — Adaptive Component-Type Tokens

## Frozen token-width header
The component dictionary is written first. VMBS then selects the smallest supported component-token width that can address every canonical type. ID zero is reserved INVALID/NULL.

3-bit header mapping:
- `000` = 16-bit (default)
- `001` = 24-bit
- `010` = 32-bit
- `011` = 64-bit
- `100` = 96-bit
- `101` = 128-bit
- `110` = 144-bit
- `111` = reserved extended/custom format

The flag is a property of the file, so all component-type references in that file use the selected width.

## RTL8019 specimen result
- Component instances: 34
- Canonical component types: 31
- Selected width: 16-bit (`000`)
- Raw CAD lexical dictionary entries: 11,754
- Raw CAD tokens: 169,891
- Holes: 229
- Byte-exact source reconstruction: PASS
- Unaccounted source bytes: 0
- Original/reconstructed SHA-256: `04b0f21717bf0751f340f6df4d1ba428f7d705669745d59fe7dd4d3b9fa2c8dc`

## Component dictionary rule
A component type contains reusable specification facts (library/value, package-local pad geometry, pad type/shape/size/drill/layers, and non-reference properties). An instance contains only placement/reference and wiring facts that vary per placed component. If a supposedly identical component differs in a specification-bearing field, it canonicalizes to a different type token.

The raw token stream remains the lossless authority, so semantic canonicalization cannot discard an obscure KiCad field: every source byte remains reconstructable even before VMBS understands it.

---

<a id="history-vmbs-analysis-cp01-status-md"></a>

## VMBS/analysis/CP01_STATUS.md

Original location: `VMBS/analysis/CP01_STATUS.md`. Relative paths in this historical report refer to that original directory.

# VMBS CP01 — Byte-exact atomization checkpoint

Source: `ISA8_Ethernet.kicad_pcb`

- Source bytes: 494,840
- Token stream entries: 169,891
- Dictionary entries: 11,754
- Byte-exact reconstruction: PASS
- Source SHA-256: `04b0f21717bf0751f340f6df4d1ba428f7d705669745d59fe7dd4d3b9fa2c8dc`
- Reconstructed SHA-256: identical
- Unaccounted source bytes: **0**
- Physical drilled holes represented as `token[data]`: **229**
  - 95 via drills
  - 134 drilled component/NPTH pads

Each hole token records kind, absolute board X/Y in mm, drill shape and dimensions, owner/pad where applicable, plating state, and pad shape. The original token stream remains the lossless authority; semantic tokens are an indexed interpretation of that exact source, not a replacement that can silently discard CAD constructs.

CP01 invariant: a source is not called losslessly atomized unless reconstruction reproduces the original bytes and SHA-256 exactly.

---

<a id="history-bios-cmos-backplane-refit-checkpoint-03-md"></a>

## BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-03.md

Original location: `BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-03.md`. Relative paths in this historical report refer to that original directory.

# BIOS/CMOS Backplane Refit — Checkpoint 03 / Codex handoff

Date: 2026-09-09
Status: source refit complete; external NASM + VB.NET validation required.

Final source additions beyond Checkpoint 02:

- first accepted Delete now sends keyboard Disable Scanning, drains protocol/repeat residue and clears the BIOS ring while IRQ1 is owned by POST;
- CMOS Setup explicitly sends Enable Scanning after taking ownership and clears trigger residue;
- Main / Advanced / Boot / Exit tabs are functional Left/Right pages rather than decorative labels;
- completed POST roll-up reports detected 80287/video-option-ROM population plus all four IDE positions;
- stale active `Firmware/atbios.rom` and `Firmware/atbios.lst` were intentionally removed from the handoff so Codex must assemble the new source before the VB build; exact old versions remain in the pre-refit backup;
- `Firmware/build-bios.ps1` validates BASIC identity, 64 KiB ROM size, FFF0 reset vector and exact F6000h-FDFFFh BASIC bytes before publishing generated ROM/listing.

Static final checks:

- 153 BIOS global labels, zero duplicates;
- no 386-style near conditional Jcc syntax in `cpu 286` source;
- `%if/%endif` balanced;
- BASIC is exactly 32768 bytes, SHA-256 `3033d1a54c99d7e2aa1fc7c8c2e51a56ae1b61bf4e70e8aa580f43c43e37a63e`;
- CPU core, timing, Intel80287, HardwarePopulation and IsaBackplane remain byte-identical to the incoming hardware-population refit;
- VB project has 55 Compile entries / 55 unique; the same two pre-existing generated `My Project` files are absent and were not fabricated.

Use `CODEX-VALIDATE-BIOS-CMOS-BACKPLANE.md` as the validation script/checklist.

---

<a id="history-bios-cmos-backplane-refit-checkpoint-02-md"></a>

## BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-02.md

Original location: `BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-02.md`. Relative paths in this historical report refer to that original directory.

# BIOS/CMOS Backplane Refit — Checkpoint 02

Date: 2026-09-09
Status: source integration milestone; NASM/Visual Studio validation still required.

Completed since Checkpoint 01:

- preserved exact pre-edit active BIOS ASM/LST/ROM under `Firmware/Backup - pre BIOS CMOS backplane refit 2026-09-09/`;
- added real PIT2/port-61h POST beep source;
- added optional 80287 guest-visible probe and CMOS/BDA equipment-bit synchronization;
- added four-position primary/secondary, master/slave IDE ATA/ATAPI/Empty reset-signature inventory;
- guarded primary-master IDENTIFY so Empty/ATAPI sockets are not sent ATA IDENTIFY;
- added Fast/Thorough memory POST CMOS option;
- added keyboard-error halt Yes/No CMOS option;
- made POST/Setup report detected NPX/video/IDE population rather than fixed 80287/Diamond text;
- installed INT 18h ROM BASIC fallback;
- embedded user-supplied IBM BASIC ROM at historical F6000h-FDFFFh; no dynamic overlay required because the Phalanx code/data remains below offset 6000h by design;
- added hardened `Firmware/build-bios.ps1` + `.cmd` wrapper with BASIC hash/window validation;
- added Codex validation/compile instructions and exact source diff.

IBM BASIC identity:
- 32768 bytes
- SHA-256 `3033d1a54c99d7e2aa1fc7c8c2e51a56ae1b61bf4e70e8aa580f43c43e37a63e`

Static checks at this checkpoint:
- 142 BIOS global labels, zero duplicates;
- preprocessor conditional count balanced;
- 55 VB Compile entries / 55 unique;
- two pre-existing missing generated VB files remain: `My Project\\Resources.Designer.vb`, `My Project\\Settings.Designer.vb`;
- ProcessorCore, ProcessorTiming, Intel80287, HardwarePopulation, and IsaBackplane are byte-identical to the incoming hardware-population refit.

Pending validation:
- NASM assembly is unavailable in the assistant container;
- Codex/Windows must run `Firmware/build-bios.ps1` and then build the VB.NET project;
- runtime smoke matrix is in `CODEX-VALIDATE-BIOS-CMOS-BACKPLANE.md`.

---

<a id="history-bios-cmos-backplane-refit-checkpoint-md"></a>

## BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT.md

Original location: `BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT.md`. Relative paths in this historical report refer to that original directory.

# Virtual Machine Bench — BIOS/CMOS Backplane Refit Checkpoint

Date: 2026-09-09
Checkpoint: 01 — recovery and integration baseline

## Authoritative working base
- Hardware-population refit source produced from the user's `9-9-20261AM.zip`.
- CPU, numeric coprocessor, video, audio, and network population are physical/removable and support `None` where applicable.
- CPU/80287 pre-refit green backups remain preserved under the sorted hardware folders.

## BIOS source being refit
- `Firmware/atbios.asm` is the active BIOS source.
- `Firmware/Backup - pre revision/` is historical only and must not replace the active source.
- Active BIOS already contains CMOS validation/checksum, protected-mode extended-memory POST, keyboard/controller POST, setup-entry windows, ATA support, and Cromwell CMOS Setup scaffolding.

## Confirmed missing/incomplete behavior to finish
1. POST beep: active BIOS has no functional PIT2/port-61h POST-tone sequence.
2. Storage discovery: current POST path still calls the older `ata_detect` path and does not yet represent all four IDE positions as first-class detected population.
3. Empty storage is valid: POST must complete cleanly with all IDE positions empty and must not wait on nonexistent devices.
4. BASIC fallback: user supplied `basic110.rom` (32 KiB IBM Cassette BASIC). It is present in `Firmware/basic110.rom` for this refit.
5. BASIC must not permanently overlap the AT BIOS ROM window. Planned implementation is a disabled-by-default firmware ROM overlay/bank device enabled only after the BIOS exhausts boot-device search with no usable storage/boot media, followed by guest-visible transfer into the BASIC ROM. Reset/power-cycle disables the overlay again.
6. CMOS setup/presentation must report current physical CPU/NPX/ISA population instead of hardcoded installed devices. CMOS remains firmware/motherboard policy; removable board population remains the backplane's authority.

## User-requested integration policy
- Do not use fake disable flags for removable hardware.
- Do not host-intercept boot or BASIC execution.
- Do not copy BASIC into RAM as a shortcut.
- No-drive configuration is a legitimate machine configuration.
- Firmware should POST, beep, report empty drives, search boot devices, then enter BASIC as the final fallback.

## Validation handoff target
Codex/Visual Studio should validate:
- NASM assembly and exact 64 KiB AT BIOS ROM fit/checksum.
- VB.NET compile after firmware/backplane ROM-overlay changes.
- POST with ordinary populated machine.
- POST with no IDE devices.
- POST beep audible through the existing PC speaker path.
- No-drive/no-boot path maps BASIC only at fallback time and reaches BASIC.
- Reset/power-cycle removes the BASIC overlay.
- CPU/NPX/ISA `None` states remain electrically absent and BIOS/setup reporting does not fabricate them.

This checkpoint intentionally records the recovered baseline before the remaining BIOS/backplane changes are applied.

---

<a id="history-motherboard-backplane-refit-checkpoint-06-md"></a>

## Motherboard Backplane Refit/CHECKPOINT-06.md

Original location: `Motherboard Backplane Refit/CHECKPOINT-06.md`. Relative paths in this historical report refer to that original directory.

# Motherboard Backplane Refit — Checkpoint 06 / closure

Date: 2026-09-09
Status: configurator/profile refit complete; second-board execution boundary explicitly gated pending a real 80486 machine fabric.

Final cleanup beyond CP05:

- fixed cross-board RAM staging so a QDI RAM choice remains attached to the pending QDI board even when its capacity happens to equal the live NEAT board;
- motherboard activation validation now checks both fixed-fabric availability and the selected processor package;
- live chipset diagnostics are obtained through the motherboard runtime boundary where possible instead of teaching the configurator that `Chipset` always means NEAT;
- chipset help text is board-neutral;
- Firmware page reports the selected board's firmware-image contract and battery-backed state;
- added QDI board implementation notes;
- added `MOTHERBOARD-RUNTIME-BOUNDARY.md` documenting the exact non-fake seam required for a future 80486 core;
- added a Windows/PowerShell validation script that performs required-source checks and invokes `dotnet build` when available.

Definition of done for this refit:

- two physical motherboard profiles exist in the configurator;
- CPU/NPX/RAM/cache/clock/firmware/CMOS/slot context changes with motherboard selection;
- board-specific settings persist independently;
- the QDI OPTi 82C895/82C602 register model exists as real source;
- the proven 286 board remains the only executable board in this branch;
- the QDI board cannot be falsely powered using the NEAT memory bridge or 80286 core.

External validation still required because this assistant container has no .NET/VB build toolchain. Run `Motherboard Backplane Refit\VALIDATE-MOTHERBOARD-BACKPLANE.ps1` on the Windows source tree, then perform the NEAT boot/configurator smoke described by the script.

---

<a id="history-motherboard-backplane-refit-checkpoint-05-md"></a>

## Motherboard Backplane Refit/CHECKPOINT-05.md

Original location: `Motherboard Backplane Refit/CHECKPOINT-05.md`. Relative paths in this historical report refer to that original directory.

# Motherboard Backplane Refit — Checkpoint 05

Date: 2026-09-09
Status: board-owned configuration/persistence boundary complete; 286 runtime remains unchanged.

Completed:

- added per-motherboard persistent context sections (`Motherboard.<profile-id>`) for RAM population, L2-cache population and clock/jumper selection;
- preserved the old `[Machine] MemoryMB` key as a NEAT-only migration/compatibility path, never as a QDI default;
- QDI default context is now 16 MB RAM, 256 KB L2 and the manual's 33 MHz JP38/JP39 default; NEAT retains its own settings;
- motherboard profiles now own firmware image path, CMOS/NVRAM path and CMOS capacity;
- QDI expects its own `Firmware\qdi-v4p895p3-smt-v5.rom` image and its own NVRAM file instead of sharing the 286 BIOS/CMOS;
- extended `CmosRtc` from a fixed 128-byte store to an optional 128/256-byte device with a bank-select callback; the unchanged NEAT constructor still creates exactly 128 bytes;
- the 82C602's F9h upper-CMOS bit can therefore drive a real second 128-byte bank when the QDI fabric is attached later;
- added contextual motherboard clock and cache selectors to System Configuration;
- motherboard changes restore that board's saved/default RAM/cache/clock context rather than inheriting the previous board's physical settings;
- added motherboard-profile ISA connector compatibility counting and an apply-time gate; the existing Diamond/SB16/NE2000 population is treated as three 16-bit ISA cards and fits the QDI board's six 16-bit slots;
- System Configuration now reports per-board firmware path/presence, NVRAM capacity/path and installed-card slot fit.

Activation boundary:

The QDI board remains deliberately power-on gated because no 80486 execution core/common non-NEAT memory bridge exists yet. Its silicon, physical profile and persistent motherboard context are real and separate; no NEAT device is used as a substitute.

---

<a id="history-motherboard-backplane-refit-checkpoint-04-md"></a>

## Motherboard Backplane Refit/CHECKPOINT-04.md

Original location: `Motherboard Backplane Refit/CHECKPOINT-04.md`. Relative paths in this historical report refer to that original directory.

# Motherboard Backplane Refit — Checkpoint 04

Date: 2026-09-09
Status: OPTi register-level silicon milestone; QDI whole-machine activation remains deliberately gated.

Completed:

- added `Opti895Chipset486`, with the documented shared index port 22h, IPC data port 23h, and main data port 24h;
- modeled decoded 82C895 control indexes 20h-2Fh (2Ch explicitly not decoded) and power-management indexes E0h-EFh;
- installed documented reset values, including IPC index 01h = C0h;
- modeled the 82C602 F7h-FAh write-only companion register bank and its upper-CMOS-bank select bit;
- kept write-only 82C602 state from becoming guest-readable host magic;
- normalized documented reserved bits on key 82C895 writes rather than accepting impossible register states;
- added host diagnostics for L2-cache encoding, fast Gate A20, global cache, AT-clock selection and 82C602 bank state;
- added a reset-default validator invoked whenever the QDI runtime object is created;
- replaced the CP03 `Pending` runtime object with a real QDI runtime object carrying the actual OPTi silicon model.

Intentional gate:

The QDI runtime still reports `FixedFabricAvailable = False`. The existing machine's shared AT peripherals and CMOS are currently constructed as globals inside the NEAT-oriented `Declares` graph. Registering the OPTi chipset on top of that would create a mislabeled hybrid motherboard. CP05 separates motherboard-owned persistent context and fixed-fabric composition before any QDI board is allowed to become the live machine.

Processor execution remains unavailable because no 80486 core exists in this branch. No 80286 substitution is permitted.

---

<a id="history-motherboard-backplane-refit-checkpoint-03-md"></a>

## Motherboard Backplane Refit/CHECKPOINT-03.md

Original location: `Motherboard Backplane Refit/CHECKPOINT-03.md`. Relative paths in this historical report refer to that original directory.

# Virtual Machine Bench — Motherboard Backplane Refit Checkpoint 03

Date: 2026-09-09
Status: runtime motherboard factory seam complete; NEAT behavior intentionally delegated unchanged; QDI fixed fabric still gated for CP04.

## Added
- `IMotherboardRuntimeBackplane` runtime contract.
- `MotherboardRuntimeFactory` and `MotherboardRuntimeManager`.
- `NeatAt286RuntimeBackplane`, an adapter over the existing proven `Declares.InitializeHardware()` graph.
- Explicit pending QDI runtime implementation that cannot register substitute hardware.
- Form1 startup now asks the runtime manager to instantiate the persisted motherboard instead of directly assuming one global motherboard implementation.
- Commit gating now queries the runtime factory rather than hard-coding knowledge of QDI readiness into the UI.

## Preserved
- Existing NEAT device construction and registration order.
- Existing global device identities used by the 80286 core/diagnostics.
- Existing ISA removable-card behavior.
- No QDI device is registered in this checkpoint.

## Next
Checkpoint 04 replaces the QDI pending runtime with a real QDI/OPTi fixed-fabric object and implements the documented 82C895/82C602 configuration decode skeleton.

---

<a id="history-motherboard-backplane-refit-checkpoint-02-md"></a>

## Motherboard Backplane Refit/CHECKPOINT-02.md

Original location: `Motherboard Backplane Refit/CHECKPOINT-02.md`. Relative paths in this historical report refer to that original directory.

# Virtual Machine Bench — Motherboard Backplane Refit Checkpoint 02

Date: 2026-09-09
Status: second-board physical/configurator profile complete; QDI runtime intentionally gated until the backplane device factory exists.

## Added
- QDI V4P895P3/SMT V5.x / OPTi 895 Green PC as motherboard profile #2.
- General processor-package metadata distinct from execution-core availability.
- Manual-supported QDI CPU families: Intel 80486SX/DX/DX2/SL, P24C DX4, P24T, P24D, AMD Am486 DX/DX2/DX4, Cyrix Cx486 DX/DX2 (M7).
- QDI Socket 3 identity and no separate NPX socket choice (486DX-family FPU is integral; 487-style upgrades are CPU packages).
- QDI RAM choices: 1, 2, 4, 6, 8, 10, 12, 16, 20, 32, 64 MB.
- QDI L2 choices: 64, 128, 256, 512 KB.
- QDI JP38/JP39 base clocks: 25, 33, 40, 50 MHz.
- QDI expansion topology: 3 VLB, 6 16-bit ISA, 1 8-bit ISA.
- QDI fixed silicon/context: OPTi 82C895 + 82C602, IMI SC468, AMI WinBIOS family.
- Configurator motherboard page now shows socket, clock generator, RAM/cache topology, slots and runtime implementation status.
- Memory selector is motherboard-contextual instead of globally hard-coded to the 286 list.

## Safety boundary
The QDI profile is real and selectable, but Apply/Power Cycle refuses to commit it in this checkpoint because the executable still owns one hard-wired NEAT motherboard fabric. No NEAT silicon is silently presented behind the QDI label and no 286 CPU is substituted for a 486 package.

## Research basis
- QDI VL/ISA-PB486P3 / V4P895P3/SMT V5 manual supplied in `Opti 895.zip`.
- OPTi 82C895 System/Power Management Controller data book, September 1994.
- OPTi 82C601/82C602 Buffer Devices data book, November 1994.

## Next
Checkpoint 03 replaces the single hard-wired motherboard fabric assumption with a runtime motherboard/backplane factory while preserving the existing NEAT object graph byte-for-behavior.

---

<a id="history-motherboard-backplane-refit-checkpoint-01-md"></a>

## Motherboard Backplane Refit/CHECKPOINT-01.md

Original location: `Motherboard Backplane Refit/CHECKPOINT-01.md`. Relative paths in this historical report refer to that original directory.

# Virtual Machine Bench — Motherboard Backplane Refit Checkpoint 01

Date: 2026-09-09
Status: foundation source checkpoint; external VB.NET build/runtime validation required.

## Working base

This checkpoint is built from:

- `VMB-BIOS-CMOS-BACKPLANE-REFIT-CHECKPOINT-03-2026-09-09.zip`
- base SHA-256: `aee63fffc3bfe7d6bc86dd4f44da4c04f4ef65c1b3708fb8068a7d9dbcdd6b73`

The OPTi/QDI research input supplied for the second motherboard is:

- `Opti 895.zip`
- research-pack SHA-256: `875f9ce347d64d4d233c7a5b15d0b60c3ebc5c361d2447620fcea58784a03f2f`

Checkpoint 01 intentionally does **not** activate the QDI board yet.  It installs the motherboard/profile seam first so the proven 286 machine can be tested before a second physical board is admitted to the catalog.

## Completed in Checkpoint 01

1. Added `Motherboards/MotherboardCatalog.vb`.
   - Introduces `MotherboardProfile` as host-side physical-board metadata.
   - Introduces `MotherboardCatalog` as the source of known motherboard models.
   - Registers the existing C&T CS8221 NEAT-class AT board as the sole live profile.
   - Records the board's fixed chipset pieces, configuration interface, BIOS identity, supported CPU packages and supported numeric-coprocessor packages.
   - Separates profile metadata from future runtime/backplane construction through `BackplaneImplementationId`.

2. Added persistent motherboard population identity.
   - `MachineHardwarePopulationConfiguration.MotherboardId` now participates in clone/load/save/summary state.
   - Existing `VirtualComputer.machine.ini` files remain compatible; a missing motherboard key defaults to the current NEAT board and is normalized on save.

3. Made motherboard replacement a staged cold-chassis configuration item.
   - Motherboard identity participates in pending-population equality.
   - Added `StageMotherboardSelectionInBed`.
   - No motherboard is hot-swapped into a running machine.

4. Converted the motherboard selector from a decorative one-item display into a real catalog-backed selector.
   - System Overview and Motherboard pages use `GetMotherboardChoicesInBed()`.
   - The current checkpoint still shows exactly one board, by design.

5. Began context-appropriate socket population.
   - CPU choices now come from the selected motherboard profile.
   - Numeric-coprocessor choices now come from the selected motherboard profile.
   - With only the existing board in the catalog, the visible CPU/80287 choices remain unchanged.

6. Split staged selection from running-machine reporting.
   - `MotherboardHardwareId` follows the staged selector state.
   - Chipset/BIOS descriptive fields continue to report the actually asserted running motherboard until the cold power-cycle commits a future replacement.

7. Added the new source file to `VirtualComputer.Modern.vbproj`.

## Deliberately unchanged

- 80286 CPU core and timing.
- Intel 80287 implementation.
- NEAT chipset runtime objects and decode.
- ISA backplane electrical behavior.
- ISA card `None` semantics.
- Diamond Stealth Pro 928, SB16 and NE2000 hardware.
- IDE/ATAPI, floppy, keyboard, BIOS/CMOS refit source and BASIC fallback.
- Current machine default hardware population.

This is an architecture seam checkpoint, not a motherboard emulation change.

## Static validation performed here

- `VirtualComputer.Modern.vbproj` parses as XML.
- 56 Compile entries / 56 unique after adding the catalog source.
- Only the two pre-existing generated Visual Basic files are absent:
  - `My Project\Resources.Designer.vb`
  - `My Project\Settings.Designer.vb`
- Required motherboard persistence/catalog/UI symbols are present.
- The QDI/OPTi board is not live in the catalog yet.
- No `dotnet`, MSBuild, Mono or VB compiler is installed in this assistant container, so a real compile was not fabricated.

## External validation gate before Checkpoint 02

Build the project in Visual Studio/Codex, then verify:

1. The project compiles with no new errors.
2. System Configuration → Motherboard shows exactly:
   - `C&T CS8221 NEAT-class AT motherboard`
3. System Overview shows the same motherboard.
4. CPU choices remain:
   - None
   - Harris/Intersil CS80C286-25
5. Numeric coprocessor choices remain:
   - None
   - Intel 80287
6. Existing stored `VirtualComputer.machine.ini` without a Motherboard entry opens normally and gains the normalized motherboard identity when saved.
7. Power-on/POST and ordinary 286 operation remain behaviorally unchanged.

## Next checkpoint after green validation

Checkpoint 02 will add the **QDI V4P895P3/SMT V5.x / OPTi 82C895 + 82C602** board as a real catalog definition and add its board-specific CPU/socket, memory, clock and expansion-topology metadata.  Runtime power-on for that board remains gated until the motherboard fabric/backplane implementation is ready; the configurator will not silently instantiate 286 silicon behind a 486 board selection.

Exact source delta: `Motherboard Backplane Refit/CHECKPOINT-01.diff`.

---

<a id="history-80486-construction-checkpoints-checkpoint-05b-semantic-corrections-md"></a>

## 80486 Construction Checkpoints/CHECKPOINT-05B-SEMANTIC-CORRECTIONS.md

Original location: `80486 Construction Checkpoints/CHECKPOINT-05B-SEMANTIC-CORRECTIONS.md`. Relative paths in this historical report refer to that original directory.

# Intel486 DX2/66 Construction — Checkpoint 05B / semantic corrections

Date: 2026-09-10
Status: CP05 work-in-progress subcheckpoint. Salvaged instruction engine retained, first architectural correctness pass applied. Not yet CP05 closure.

## Baseline
- Restored/salvaged from CP04 + CP05A WIP.
- Green 80286 and Intel80287 remain byte-identical to the CP01 baseline.

## Corrections in this subcheckpoint
- Added explicit project Compile entries for Intel486ExecutionTypes.vb and Intel486ExecutionEngine.vb.
- Corrected Intel486 exception-class handling: divide error is contributory; contributory->contributory and page-fault->contributory/page-fault escalate to #DF.
- Exception/interrupt entry page faults now remain architectural rather than escaping as host exceptions.
- Double-fault delivery failure enters processor shutdown; NMI can release ordinary shutdown, while shutdown entered during NMI requires RESET.
- NMI remains internally blocked until IRET.
- V86 INT n / PUSHF privilege behavior and software-INT effective CPL checks corrected.
- Protected-only 0F00 system instructions and LAR/LSL are rejected outside protected mode/V86 as required.
- Intel486 CR0.ET is hardwired high on software CR0 writes; LMSW continues to preserve ET/NE.
- Removed signed 32-bit VB conversion/mask hazards by using modulo-width helpers.
- Word/dword segmented and system-linear operand paths pre-translate the complete operand before any data transfer, preventing a later segment/page fault from leaving an earlier byte write visible.

## Static/semantic validation performed in this container
Compiler toolchain is not installed, so this subcheckpoint is not declared build-green.

Passed:
- method begin/end balance for all changed 486 source;
- explicit project compile inclusion;
- risky CInt(FetchCodeDword)/&HFFFFFFFFL scan;
- CR0.ET hardwired source check;
- exhaustive 256-value parity formula test;
- exhaustive 8-bit ADD/ADC and SUB/SBB result/CF/AF/OF vector sweep;
- signed modulo-wrap edge vectors;
- representative Intel486 double-fault class vectors;
- protected 286/287 SHA-256 verification.

See `CP05B-STATIC-SEMANTIC-VALIDATION.txt` alongside this note.

## Still open before CP05 can close
- finish opcode/LOCK/privilege audit against Intel 240486-002 / 241245-002;
- add focused execution fixture vectors for real-mode and protected-mode instruction retirement;
- audit multi-memory instructions for restart state, especially PUSHA/POPA, ENTER, BOUND, far-pointer loads, task switch and REP restart points;
- audit debug-register side effects and RF/TF behavior;
- retain x87 execution as CP07, but ensure integer-front-end ESC/WAIT fault behavior is architecturally correct;
- external dotnet build still required because no VB compiler exists in this container.

---

<a id="history-80486-construction-checkpoints-checkpoint-04-md"></a>

## 80486 Construction Checkpoints/CHECKPOINT-04.md

Original location: `80486 Construction Checkpoints/CHECKPOINT-04.md`. Relative paths in this historical report refer to that original directory.

# Intel486 DX2/66 Construction — Checkpoint 04

Status: Intel486 segmentation/paging/TLB silicon implemented and owned by the DX2 package. QDI activation remains gated.

## Added `Intel486Mmu`
- six visible segment selectors + hidden base/limit/attribute caches;
- reset CS hidden base FFFF0000h, normal real-mode selector<<4 reload behavior;
- 32-bit protected-segment cache representation including D/B, G, expand-down, executable/read/write and DPL fields;
- CR0 reset 60000010h and 486 control semantics for PE/MP/EM/TS/ET/NE/WP/AM/NW/CD/PG;
- invalid CR0 CD=0,NW=1 rejection;
- paging requires PE;
- CR2 page-fault linear address;
- CR3 page-directory base plus PWT/PCD and mandatory TLB flush on reload;
- 4 KiB, two-level, 1024-entry page directory/page table walk;
- Accessed/Dirty bit updates as locked physical read/modify/write cycles;
- most-restrictive PDE/PTE U/S and R/W protection plus Intel486 CR0.WP supervisor semantics;
- exact 486 page-fault error-code P/W/U bits;
- 32-entry TLB implemented as 8 sets x 4 ways;
- TLB index uses linear-page low three bits, 17-bit tag, matching Intel TR6 documentation;
- Intel three-bit pseudo-LRU replacement tree used per set;
- PWT/PCD retained in each TLB entry;
- INVLPG-style single-page invalidation and full flush;
- A20M# masks physical A20 at the package pin boundary after translation.

## Electrical page walk
Page-directory/table reads and A/D updates go through `IProcessorLocalBusFabric` as real 32-bit processor bus transactions. There is no host RAM shortcut. A small private fixture fabric exists only for the self-test and is not attached to VMB runtime.

## Self-test coverage
- no-paging linear=physical;
- A20M# masking;
- two-level user page walk;
- PDE/PTE Accessed update;
- write Dirty update;
- TLB hit and INVLPG miss;
- user read-only write #PF = P|W|U (7);
- supervisor WP=0 compatibility;
- supervisor WP=1 write #PF = P|W (3).

## Green-core protection
No protected 286/287/shared files changed. Only new 486 source and the project compile list changed.

## Next
CP05: 32-bit instruction front end and integer execution — prefixes, 16/32 ModR/M, SIB, register aliasing, 486 instruction additions, stack/control transfers and restartable exception plumbing.

---

<a id="history-80486-construction-checkpoints-checkpoint-03-md"></a>

## 80486 Construction Checkpoints/CHECKPOINT-03.md

Original location: `80486 Construction Checkpoints/CHECKPOINT-03.md`. Relative paths in this historical report refer to that original directory.

# Intel486 DX2/66 Construction — Checkpoint 03

Status: physical Intel486 DX2/66 package shell, reset identity, integrated FPU state container and deterministic host-worker engine implemented. No instruction execution yet; QDI remains activation-gated.

## Intel486 DX2/66 package
`Processors/80486/Intel486Dx2.vb`

- package id `intel-80486dx2-66`;
- 168-pin PGA, nominal 5 V;
- 33 MHz external board clock / 66 MHz internal core clock;
- integrated FPU is physically owned by the DX2 package;
- stepping-B reset identity EDX=00000433h (component 04h, revision 33h);
- reset CR0=60000010h, EFLAGS=00000002h;
- reset visible CS=F000h with hidden CS base FFFF0000h and EIP=0000FFF0h -> first physical fetch FFFFFFF0h;
- IDTR=0:03FFh, DR6=FFFF0FF0h, DR7=0;
- ordinary RESET invalidates processor state but does not claim to initialize undefined integer/FPU values;
- BIST path initializes x87 architectural state;
- external input state includes INTR, NMI edge, A20M#, HOLD, AHOLD, BOFF#, UP#, IGNNE# and FLUSH# hooks;
- UP# power-down is sampled as a reset-boundary package mode rather than a software flag;
- executable signal-to-PGA-coordinate map added for address/data/control/test pins.

## Integrated FPU shell
`Processors/80486/Intel486IntegratedFpu.vb`

- eight raw 80-bit x87 register images;
- control/status/tag words;
- instruction/data pointers and last opcode;
- explicit “state undefined until BIST/FNINIT” representation;
- FINIT/FNINIT/BIST reset state is 037Fh/0000h/FFFFh.

## Host acceleration
`Processors/Common/DeterministicSiliconWorkerPool.vb`

- defaults to all available host logical processors;
- preserves input/output ordering;
- workers are explicitly pure-compute only;
- 1-worker vs multi-worker deterministic validator included;
- no worker may mutate guest state or motherboard time.

## Board timing authority
`GrantExternalClocks()` accepts only motherboard-granted external clocks and derives exactly two internal clocks per external clock. It refuses a fabric whose external clock is not the selected 33 MHz DX2/66 profile. No host sleep/delay appears in the processor path.

## QDI activation
Still FALSE. This checkpoint deliberately does not claim an instruction engine or 486 fixed fabric.

## Green 286 protection
The 80286 core, Intel80287, HardwareBus, MachineTiming, Declares, MotherboardCatalog and MotherboardRuntime remain byte-identical to the CP01 baseline. The project file changed only to compile the new 486/common source.

## Next
CP04: segmentation, control registers, 4 KiB two-level paging, 32-entry/4-way TLB, page protection/cacheability and A20M# physical-address masking.

---

<a id="history-80486-construction-checkpoints-checkpoint-02-md"></a>

## 80486 Construction Checkpoints/CHECKPOINT-02.md

Original location: `80486 Construction Checkpoints/CHECKPOINT-02.md`. Relative paths in this historical report refer to that original directory.

# Intel486 DX2/66 Construction — Checkpoint 02

Status: processor-family-neutral electrical seam added; existing 286 runtime remains untouched.

## Added
- `Motherboards/Common/MachineFabricContracts.vb`
  - processor bus cycle kind, width, ready/BRDY/backoff result types;
  - board-owned external-clock counter;
  - memory/I/O/INTA/special transaction vocabulary;
  - active byte-lane mask corresponding to active-low BE3#:BE0# pins;
  - explicit cacheability, lock/pseudo-lock and burst intent;
  - `IProcessorLocalBusFabric` and capability contract;
  - compatibility-only adapter over `CpuLocalBus286` proving the neutral vocabulary can represent the green NEAT path without rewiring it.
- `Processors/Common/ProcessorPackageContracts.vb`
  - first-class physical package interface;
  - explicit integrated-FPU identity;
  - RESET/INTR/NMI/A20M#/HOLD input boundary;
  - motherboard-granted external-clock execution contract.

## Timing rule frozen
The motherboard/runtime owns simulated time. Processor packages receive external-clock grants; they never sleep or advance wall-clock time. Future host workers may compute in parallel but may not commit registers, memory, I/O, interrupt state, cache state or pins independently.

## Green-core protection
No changes were made to `Processors/80286/ProcessorCore.vb`, `Math Coprocessors/Intel 80287/Intel80287.vb`, `HardwareBus.vb`, `MachineTiming.vb`, `Declares.vb`, `Motherboards/MotherboardCatalog.vb`, or `Motherboards/MotherboardRuntime.vb`.

Only the project compile list was extended to include the two new source files.

## Next
Checkpoint 03 creates the physical Intel486 DX2/66 package shell, exact reset state, 2:1 clock domain, pin state, integrated-FPU ownership, and deterministic pure-compute worker pool. QDI activation remains gated.

---

<a id="history-board-scripts-board-script-prototype-changeset-txt"></a>

## Board Scripts/BOARD-SCRIPT-PROTOTYPE-CHANGESET.txt

Original location: `Board Scripts/BOARD-SCRIPT-PROTOTYPE-CHANGESET.txt`. Relative paths in this historical report refer to that original directory.

VMB Board Script Prototype 00 - changes from preserved FOX/HT12 checkpoint
Added: 19
  + Board Scripts/BOARD-SCRIPT-PROTOTYPE-00-EVIDENCE.md
  + Board Scripts/BOARD-SCRIPT-PROTOTYPE-STATIC-VALIDATION.txt
  + Board Scripts/CHECKPOINT-00.md
  + Board Scripts/Samples/INVALID-Invented-Part-Must-Fail.bs
  + Board Scripts/Samples/NEAT-CS8221-Reference.bs
  + Board Scripts/Samples/Octek-FOX-II-286-Rev3.2.bs
  + Board Scripts/VALIDATE-BOARD-SCRIPT-PROTOTYPE.py
  + Hardware/Board Script/BoardScriptCatalog.vb
  + Hardware/Board Script/BoardScriptCli.vb
  + Hardware/Board Script/BoardScriptCompiler.vb
  + Hardware/Board Script/BoardScriptLexer.vb
  + Hardware/Board Script/BoardScriptModel.vb
  + Hardware/Board Script/BoardScriptParser.vb
  + Hardware/Board Script/BoardScriptWorkers.vb
  + Hardware/Board Script/README.md
  + Hardware/Chipsets/Chips and Technologies/CS8221 NEAT/CtNeatBoardScriptWorkers.vb
  + Hardware/Chipsets/Chips and Technologies/CS8221 NEAT/PARTS-METADATA.json
  + Hardware/Chipsets/Chips and Technologies/CS8221 NEAT/WORKER-METADATA.json
  + Hardware/Chipsets/Headland/HT12/WORKER-METADATA.json
Modified: 2
  * Program.vb
  * VirtualComputer.Modern.vbproj
Removed: 0

Protected live hardware source modified: NONE
Expected existing modifications: Program.vb, VirtualComputer.Modern.vbproj only

---

<a id="history-board-scripts-checkpoint-00-md"></a>

## Board Scripts/CHECKPOINT-00.md

Original location: `Board Scripts/CHECKPOINT-00.md`. Relative paths in this historical report refer to that original directory.

# VMB Board Script — Checkpoint 00 / language + dual-chipset prototype

Date: 2026-09-10
Status: source prototype complete; static validation passed; external VB.NET build still required.

## Baseline

Built on the preserved FOX/HT12 full-source WIP checkpoint.  That baseline remains separately archived and was not overwritten.

## Added language engine

`Hardware/Board Script/` now contains:

- lexical scanner with `#` and `//` comments;
- parser and AST for boards, components, placement, straps and nets;
- recursive metadata catalog loader;
- worker registry;
- compiler/validator;
- command-line check path;
- language README.

Prototype syntax uses `.bs` files.  A board describes physical population and wiring, not chipset behavior.

## First two chipset forms

1. **Headland HT12/A** — existing real `HeadlandHt12` behavior worker is bound through `WORKER-METADATA.json`; FOX sample applies sourced power-on strap fields through the worker adapter.
2. **C&T CS8221 NEAT** — new parallel Board-Script-first workers represent the physical 82C211, 82C212, 82C215 and 82C206 packages independently. Seven first-pass inter-chip nets in the sample use sourced PLCC84 pin identities.

The existing live `NeatCs8221Chipset` is not replaced or edited.

## Hard compiler rules already present

- unknown physical part ID => error;
- worker metadata cannot create a part absent from parts metadata;
- duplicate physical part IDs / worker bindings => error;
- unknown worker => error;
- straps on a part with no behavioral worker => error;
- duplicate refdes or net => error;
- unknown component or unknown pin in a net => error;
- net requires at least two endpoints;
- placement must be inside the declared board outline;
- prototype rotation must be a multiple of 90 degrees;
- side must be top or bottom;
- incomplete pin/footprint evidence leaves the graph logic-valid but `ManufacturingReady=False`.

`INVALID-Invented-Part-Must-Fail.bs` is retained as a permanent negative conformance specimen.

## Existing source modified

Only two pre-existing files differ from the preserved FOX checkpoint:

- `Program.vb` — early dispatch for `--board-script-check`; ordinary WinForms startup remains the fallback path.
- `VirtualComputer.Modern.vbproj` — explicit compile/content entries for prototype source and metadata.

All other Board Script / NEAT prototype files are additions. No existing CPU, memory bus, live NEAT chipset, ISA, video, audio, storage or timing implementation was edited.

## Validation performed here

`Board Scripts/VALIDATE-BOARD-SCRIPT-PROTOTYPE.py` passed:

- FOX grammar/catalog specimen: 5 components, 0 nets;
- NEAT grammar/catalog specimen: 4 components, 7 nets;
- invented-part negative test;
- sourced NEAT pin subset assertions;
- unique project Compile entries and presence of all prototype sources;
- coarse VB type/module block-balance scan.

The environment has no dotnet, MSBuild or VBC toolchain, so no VB.NET compile or runtime success is claimed.

## External validation

On the Windows/VMB tree:

```text
python "Board Scripts\VALIDATE-BOARD-SCRIPT-PROTOTYPE.py"
dotnet build VirtualComputer.Modern.vbproj
VirtualComputer.Modern.exe --board-script-check "Board Scripts\Samples\Octek-FOX-II-286-Rev3.2.bs" --source-root "."
VirtualComputer.Modern.exe --board-script-check "Board Scripts\Samples\NEAT-CS8221-Reference.bs" --source-root "."
VirtualComputer.Modern.exe --board-script-check "Board Scripts\Samples\INVALID-Invented-Part-Must-Fail.bs" --source-root "."
```

The first two checks should exit 0 and write `.check.txt` reports; the invalid specimen must exit 2.

## Next architectural step

Do not immediately replace the live NEAT implementation.  First give the `.bs` compiler a runtime graph attachment seam, then construct a Board-Script-driven test fabric for HT12 and the split NEAT workers.  Compare the split NEAT register/decode behavior against the green monolith before migration.

---

<a id="history-patch-notes-txt"></a>

## PATCH-NOTES.txt

Original location: `PATCH-NOTES.txt`. Relative paths in this historical report refer to that original directory.

IDE HDD hot-trace disable

Purpose:
Reduce host-side diagnostic overhead while testing the MD/RD slowdown.

Changed:
- IdeController.vb
  * disables READ/WRITE command text tracing on the ATA hard-disk hot path
  * disables per-sector READ/WRITE DRQ text tracing
  * leaves cheap numeric read/write phase counters active
  * leaves ATAPI and low-frequency ATA/channel tracing active
  * diagnostic dump states that HDD hot-path text tracing is disabled
- HardwareDocs/IDE_ATAPI_IMPLEMENTATION_NOTES.md
  * documents the new diagnostic policy

Not changed:
- ATA 512-byte sector phasing
- DRQ behavior
- IRQ behavior
- task-file updates
- media I/O
- ATAPI protocol
- CPU/video/audio/wavetable code

This is intentionally a diagnostic-performance patch only.

---

<a id="original-vmb-changelog"></a>

## Original dated VMB changelog

# Change log

## 2026-09-03
- Reorganized IDE hardware configuration by physical topology: Channel 1/2, each with Drive 0 (master) and Drive 1 (slave), instead of separate master/slave device-class pages.
- Added selectable ATA hard-disk model profiles with capacity, CHS, ATA identity, timing identity, and `.meta` sidecar persistence. Initial profiles include the exact legacy Generic 64 MiB image, Generic 500 MB, Conner CP-2064, and Conner CP30064H.
- Model changes are staged until power cycle; incompatible platter images are disconnected intact instead of being resized, while exact-capacity images can adopt the selected physical-drive sidecar identity. New shelf drives are created at the selected model's exact capacity.
- Routed IDE activity telemetry by electrical socket so HDD0-HDD3 correspond to primary master/slave and secondary master/slave ATA activity instead of one lamp per channel.

### Front panel / media controls

- Rebuilt the left drive rail as a compact physical bay stack: two floppy drives, the fitted ATAPI CD-ROM, two honest blank 5.25-inch reserve plates, and a period MFM/RLL-style facade for the fitted IDE primary-master hard disk.
- Wired the ATAPI facade to current media state, ISO mount/eject actions, and the Sneaker Net Optical/Disc Box workbench.
- Wired the hard-disk facade to the live IDE Drive Shelf, mount/create/boot/disconnect controls, the shelf folder, and the Sneaker Net Hard Drive utilities (inspect, clone, backup, attach/disconnect, and image creation).
- Blank reserve bays remain inert and do not imply guest-visible IDE devices that are not implemented.

## 2026-09-02

### Fixed

- Restored the form's embedded "demon dreaming" chassis artwork while machine power is off. The S3 presentation worker no longer replaces the intended power-off face with a synthetic black CRT frame.
- Began the `working-beta` boundary cleanup by removing the disabled WinForms CGA/text rendering timers and their unreachable `VrMem`/bitmap/font-cache subsystem. The active Diamond Stealth Pro/S3 presentation path is unchanged. Host diagnostic viewers were moved from the main form implementation into `Form1.Diagnostics.vb` as the first behavior-preserving responsibility split.
- Lowered the host-only Diamond Stealth Pro 928 CRT raster worker to below-normal scheduling priority. Under host saturation, cosmetic presentation now yields to the coordinated CPU/bus/device timeline; guest VRAM, CRTC, ISA and interrupt behavior are unchanged.
- Corrected ATAPI media-change handling so `UNIT ATTENTION 06/28/00` is consumed when first reported as CHECK CONDITION while the sense tuple remains available for `REQUEST SENSE`. Legacy DOS drivers that retry `READ TOC` directly no longer receive the media-change failure forever.
- Corrected ATA/ATAPI ready-state status from `40h` to `50h` by including the device-seek-complete bit. VIDE-CDD's `DIR D:` path explicitly waits for this hardware status before issuing its next packet.
- Removed continuous 48 kHz zero-sample synthesis while port `61h` disconnects the PC speaker. The guest-visible PIT channel 2, gate, data-enable, and motherboard timing paths are unchanged; only the final host transducer now takes a silent fast path.

### Diagnostics

- Added PC-speaker port `61h` data/gate/node state, generated and skipped PCM samples, queued and dropped host buffers, and `waveOut` error state to Dump All Diagnostics.
- Removed continuous construction of full architectural forensic strings every 1,024 instructions. Dump All retains the lightweight rolling CS:IP sampler, while detailed register/code/stack captures remain available on actual fault and reset events.
- Bypassed dormant DOS-return and QB-execution forensic method calls unless their bounded recorders are armed.

### Removed

- Removed the obsolete `BiosCalls.vb` high-level BIOS prototype. Normal execution already uses the assembled AT BIOS ROM, guest IVT, processor interrupt mechanism, and bus-connected hardware; dormant CPU fallbacks now require an explicitly supplied host firmware handler instead of silently reaching the prototype.
- Added the missing AT BIOS IRQ14 / INT 76h fixed-disk completion handler.
- The handler now records completion in BDA `40:8E` bit 7 and ends the cascaded interrupt at the slave and master 8259A controllers in hardware order.
- Prevents an early IDE/ATAPI interrupt from leaving IRQ14 permanently in service and forcing DOS CD-ROM drivers into multi-minute timeouts.
- Removed the Disc Box Optical page's silent Mount no-op. With no selected path, Mount now opens an ISO picker rooted in Disc Box; missing callbacks/files and mount failures are reported visibly.

### Diagnostics and documentation

- Identified the bundled OAKCDROM failure as execution of a 386 operand-size-prefix / PCI BIOS probe on the emulated 80286.
- Identified BTCDROM as a BusLogic SCSI driver, not an IDE/ATAPI driver.
- Confirmed VIDE-CDD 2.14 reaches the emulated ATAPI device and isolated its wait to the missing BIOS IRQ14 contract.
- Preserved the evidence and required guest retest in `HardwareDocs/IDE_ATAPI_IMPLEMENTATION_NOTES.md` and `logs/2026-09-02-VIDE-CDD-IRQ14.log`.
