# Microsoft serial mouse implementation notes

## Hardware contract

The attached COM1 device models the original two-button Microsoft serial mouse:

- 1200 bit/s
- 7 data bits
- no parity
- one stop bit
- three-byte Microsoft movement/button packets
- `M` identification after the mouse is powered/reset through DTR and RTS

The mouse receives operating power from TXD, DTR, and RTS and returns data on RXD. CTS, DSR, RI, and DCD are not driven by the mouse. In particular, DTR/DSR and RTS/CTS must not be modeled as looped pairs.

The identification response is delayed by 14 ms after the DTR/RTS power transition to represent hardware startup. Detection software commonly waits up to 200 ms and expects the response after roughly 10-20 ms.

## Emulator path

Host pointer motion is accumulated without blocking the WinForms UI and drained by the sole machine owner at a slice boundary. After that boundary it follows the guest-visible hardware path:

`host counts -> Microsoft serial mouse -> 1200-baud frames -> COM1 UART -> IRQ4 -> guest driver`

The accumulator only decouples host event frequency from the machine thread. It does not write guest coordinates or bypass UART timing and interrupts.

While captured, the host cursor is repeatedly centered so movement can continue
past the form edges. Movement is measured from the current physical
`Cursor.Position`, not from queued `MouseEventArgs` coordinates: a queued
`WM_MOUSEMOVE` may predate the most recent center warp and must not be converted
into a second, artificial movement. Host positive X/right and positive Y/down
counts are passed unchanged into the Microsoft packet encoder.

The frontend converts four modern host-pointer pixels into one period mouse
count. Signed remainders are retained independently on both axes, so this lower
effective resolution does not discard slow travel or prevent shallow diagonal
movement. This models the large resolution difference between a contemporary
host pointing device and an early Microsoft ball mouse; it does not alter or
bypass the guest-visible serial device.

The host pointer remains visible and produces no guest motion until the user
left-clicks the displayed CRT area (or uses the Machine menu / Ctrl+Alt+M) to
capture it. The click which enters capture is consumed by the host and is not
reported as a guest button press. Releasing capture always restores the host
pointer immediately.

## Diagnostic interpretation

The complete diagnostic dump contains two complementary lines:

- `Serial mouse host frontend` counts raw WinForms movement messages and coalesced boundary transfers.
- `Microsoft serial mouse` reports power, pending counts, identifications, packets, and UART admission deferrals.

The COM1 UART report then shows the guest line configuration, receive FIFO, wire queue, errors, interrupt enable state, and retained register history. A correctly attached idle mouse should show modem status inputs low unless guest loopback mode is selected.

## References

- *Microsoft Mouse Programmer's Reference*, Microsoft Press, second edition, 1991.
- Microsoft Knowledge Base Q29204, “Serial Mouse Pin-Outs.”
