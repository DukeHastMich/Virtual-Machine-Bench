# Grok VMBS Alignment Audit — CP38

## Scope
The original Grok `VirtualComputer.Modern.zip` is not presently mounted, so this audit uses the preserved Grok-era source artifacts and the forensic record. No Grok tree is merged wholesale.

## Steal outright / preserve concept
- Metadata-driven Board Properties from VCB `InstallHeaderJson`.
- Machine-instance persistence under `[MachinePcb.<card_id>]`; design truth remains in VCB/BS.
- Physical controls and configuration bindings remain generic declarations, not NE2000-specific UI.
- Board Script owns motherboard/PCB composition and wiring; machine population remains a separate concern.
- Immutable materialized machine/chassis as the result of validated configuration is directionally aligned.

## Good idea, wrong/currently incomplete implementation
- `IVmbBoardRuntime` is a useful transitional ABI, but `AllocateModule`/`Link` currently leave the actual silicon in the monolithic VMB assembly.
- FAT `CreatePrimaryIsaModule` contraction is acceptable only as a compatibility bridge for the NIC; it must not become the motherboard architecture.
- Dynamic properties are useful, but configuration/persistence consumption must remain generic and must not encode specimen reference designators.

## Revealed missing abstractions
- Generic arbitrary-N module source composition into one generated `.vcb.vb` chimera.
- Typed support-circuit knowledge for Circuit Chess rather than board recipes.
- Programmed-device slot/payload flow through Firmware Store to installed machine PCB state.
- Generic external endpoints/capabilities beyond the first Ethernet specimen.

## Reject / quarantine
- Any Grok changes from the divergent/older VMB tree that mix unrelated scheduler, BIOS, ATAPI, 80287, or device changes into VMBS architecture work.
- Any board-specific runtime/materializer special case disguised as a generic module API.
- Any promotion of observed topology to mandatory electrical truth without evidence.

## CP38 action
The aligned architectural lesson is applied to the canonical tree by freezing a typed Component Store support-mesh contract. This is additive and does not fabricate facts. It explicitly separates mandatory, recommended, reference, observed, and inferred support relationships and forbids specimen reference designators in reusable support patterns.

Next: generic VB chimera linker/source compositor, proved first against the existing three-IC Ethernet board.
