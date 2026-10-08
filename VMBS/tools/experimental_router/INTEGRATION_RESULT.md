# Installed and verified — 2026-10-08

Live entry: VMBS.bat → option 11, Route PCB - experimental FreeRouting + ERC/DRC.

Default operation accepts untraced boards only. Existing traces/vias/zones require explicitly choosing full reroute into a new draft. Partial-route completion is not implemented.

Six host integration tests passed, covering missing runtimes, cancellation, real worker timeout/termination, failed-check draft handling, semantic round-trip rejection, and requiring an explicit reroute for existing copper. Installed modules compile successfully.

Two real integration runs completed:

- `work/experimental-routing/20261008-011246-feb430cb`: untraced ISA8 Board Script; all nets routed in 115.37 seconds; zero shorts, copper clearance conflicts or missing links. KiCad has no edge/hole/track-width errors. Remaining 16 drill errors and 71 ERC warnings correctly yield FAILED/unaccepted. The generated candidate.bs.json passes semantic round-trip comparison. Source hash is unchanged.
- `work/experimental-routing/20261008-011345-7babfa65`: small untraced KiCad board; routing and semantic round-trip pass. Missing electrical netlist correctly yields INCOMPLETE/unaccepted.

No candidate was published into the VCB store or substituted for its source. The only existing application file edited is vmbs_launcher.py; new code is under tools/experimental_router, tools/experimental_routing.py and tools/test_experimental_routing.py. A launcher backup is retained at C:/Users/Eric/Documents/Codex/vmbs-router-integration/vmbs_launcher.before.py.

Runtime paths are machine-local and reuse the verified tools under C:/Users/Eric/Documents/Codex/router-review-20261007. Keep that directory or update runtime.local.json before moving the toolchain.
