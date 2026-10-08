# FPGA component-model checkpoint

An FPGA is not one behavioral part. VMBS must keep three facts separate:

1. **Silicon identity (Component Store):** exact device/package/pins, I/O banks, legal voltages and standards, clock/config/JTAG resources, resource limits, and evidence.
2. **Board occurrence (Board Script):** which package pins are assigned to board signals, bank supply choices, board clocks, configuration topology, and which payload is installed.
3. **Personality/payload:** content-addressed bitstream or an evidence-backed behavioral/HDL representation. A bitstream's presence must never cause VMBS to invent semantics it cannot decode or otherwise substantiate.

This preserves the no-invention rule. A board can be structurally/electrically/physically valid while remaining execution TITS UP when the programmed personality is unknown.

`library_templates/programmable_logic_fpga.template.json` is a schema/template only, deliberately not a fabricated real-world FPGA record. Exact devices enter the Component Store only from evidence-backed data.

The Board Script Prototype-02 specimen demonstrates the intended markup concepts `program`, `pinmap`, `io_bank`, `power`, and `clock`. The syntax remains backend-neutral: no Verilog, VHDL, VB, Python, or C semantics belong in BS.
