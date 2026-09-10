# BASIC ROM compatibility findings

The observed failure is a video-BIOS address calculation bug, not evidence that BASIC requires a different processor or a ROM overlay.

The supplied raw basic110.rom is 32 KiB, SHA256 3033d1a54c99d7e2aa1fc7c8c2e51a56ae1b61bf4e70e8aa580f43c43e37a63e. BASIC110.json5 contains the same bytes. Its entry jumps from F600:0000 to F600:7E92. The system BIOS image maps at physical F0000h; BASIC occupies image offsets 6000h–DFFFh, physical F6000h–FDFFFh.

The 64 KiB IBM Basic for Clones.BIN is a different packaging format: 55 AA 80 option-ROM header with a valid modulo-256 checksum. Its initialization entry is offset 3. The loader installs INT 18h with offset zero and segment CS+13h. The identical 32 KiB BASIC payload begins at file offset 0130h. HI.BIN and LO.BIN are the odd/even byte halves, verified by interleaving. Neither half is an independently executable BASIC ROM, and the full wrapper is not a drop-in replacement for raw basic110.rom or the complete system BIOS.

No overlay was removed during compilation. Comparing the supplied ZIP with the repaired system BIOS shows only the three documented branch changes. The refit handoff explicitly describes direct embedding at F6000h; its INT 18h trampoline has no overlay activation. If an overlay was intended, it was not implemented in that supplied path.

## Reproduced failure and isolated repair

Firmware/stealthpro_vgabios.asm, video_cursor_address: DX contains DH=row and DL=column. MUL word [0x4A] calculates row*columns into DX:AX, destroying DL before it is added to the text-cell offset. Ordinary text rows produce DX=0, so character writes collapse to column zero. BASIC uses INT 10h text-cell services, exposing this bug.

The attached video-ROM source preserves DX with PUSH DX / POP DX around that multiplication. The system BIOS, BASIC bytes and processor source are unchanged for the experiment.

Using the actual CPU, motherboard, keyboard and Diamond video devices in a console harness, the patched video ROM displays the BASIC C1.10 banner, 62940 Bytes free, Ok, and function-key labels. Sending physical key events through AtKeyboard101 for PRINT 2+2 returns 4 and a new Ok prompt. This passed both with and without the 80287. Logs are included. This is a targeted emulator test, not a full BASIC compatibility suite or GUI test.

## Using the isolated fix

Back up Firmware/stealthpro.rom in the extracted compiled application's directory, then replace it with the attached stealthpro.rom and restart the emulator. This is the video ROM, not Firmware/atbios.rom. For source builds, the attached stealthpro_vgabios.asm replaces the corresponding video-BIOS source and uses the existing include file/build-video-bios.ps1. No files in your GitHub checkout or original ZIP were changed.

Historical mapping reference: https://www.pcjs.org/machines/pcx86/ibm/5150/rom/basic/
