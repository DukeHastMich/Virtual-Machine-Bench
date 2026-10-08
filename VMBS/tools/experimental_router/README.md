# Experimental routing integration

Start VMBS.bat and choose **11. Route PCB — experimental FreeRouting + ERC/DRC**.

Select a legacy `.kicad_pcb` or semantic `VMBS-BOARD-1` `.bs.json`, a project rules JSON, and optionally a pin-typed KiCad `.net` for electrical checking. `isa8.rules.json` is the tested ISA8 reference profile, not a universal fabrication profile. Copy it for another project and record the source of its constraints in `provenance`. General copper clearance, trace width, minimum width and via diameter come from the board's net class/setup.

By default this routes only boards with no traces, vias or copper zones. Existing copper is preserved by rejecting the job before routing. For a deliberate full reroute, type REROUTE at the menu prompt or supply `--reroute-existing`; only the new draft loses the old routing, never the source. Incremental completion of partially routed boards is not supported yet.

The integration exports Board Script using VMBS's existing serializer, invokes the independent routing backend, runs connectivity and KiCad/SKiDL checks, then imports the candidate using the existing BS codec. It verifies the BS-to-KiCad semantic round trip before making that draft available. It never modifies the input, publishes a VCB or overwrites a design automatically.

Output is in `VMBS/work/experimental-routing/<timestamp-id>/`. `integration-result.json` reports status, source hash, candidate paths and checker counts. Logs stream to the console; Ctrl+C cancels the worker tree. Partial files remain for diagnosis. A failed check, warning, absent ERC, cancellation or backend error cannot be accepted through the menu. Only an all-passed result offers saving a reviewed BS copy, with overwrite protection. Passing these configured checks is still not a guarantee of full electrical operation or fabrication readiness.

Supported scope is deliberately restricted: two-layer legacy KiCad module format, one active net class (unused definitions are allowed), supported circular/rectangular/rounded pads and line/legacy-arc outline. Custom rules, footprint/pad clearance overrides, keepouts, offset/slotted NPTHs and unsupported geometry are rejected. The current importer requires 0.635 mm via drills. In explicit full-reroute mode, existing copper/zones are stripped from the draft before routing; planes/current sizing, differential pairs and timing constraints are not reconstructed. Modern KiCad footprints and arbitrary behavior-to-PCB synthesis are not implemented.

Runtime paths are in ignored `runtime.local.json`; missing paths cause a clear preflight failure. This computer reuses the verified Python 3.12, Java, FreeRouting 2.5.0, KiCad 10.0.6, Shapely and SKiDL installations from the Codex router workspace. Do not delete that workspace without updating the runtime config. Dependencies are not vendored into this repository. FreeRouting and KiCad have their upstream GPL licensing; SKiDL is MIT. See https://github.com/freerouting/freerouting, https://github.com/KiCad/kicad-source-mirror and https://github.com/devbisme/skidl.

The ISA8 reference still has 16 inherited drill errors and electrical warnings, so it will remain a draft despite complete routing and clean copper clearances.
