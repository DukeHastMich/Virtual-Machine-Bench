; Virtual Computer clean-room PC/AT firmware
; 64 KiB system ROM mapped at physical F0000h.
bits 16
org 0


; Force genuine 80286 instruction encoding. NASM otherwise permits 386+
; near conditional branches (0F 8x), which an 80286 cannot execute.
cpu 286

; 80286-safe arbitrary-distance conditional branches. Each macro uses a
; short inverse condition over a normal near JMP; no 386 near-Jcc opcodes.
%macro J286_E 1
    jne short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_NE 1
    je short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_AE 1
    jb short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_C 1
    jnc short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_B 1
    jae short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_BE 1
    ja short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_A 1
    jbe short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_NC 1
    jc short %%skip
    jmp %1
%%skip:
%endmacro

%define BDA_SEG 0x0040
%define VIDEO_SEG 0xB800
%define EBDA_SEG 0x9FC0
%define TICKS_PER_DAY_LOW  0x00B0
%define TICKS_PER_DAY_HIGH 0x0018

; Standard BIOS data-area disk fields.
%define BDA_DISKETTE_STATUS 0x0041
%define BDA_HDD_STATUS      0x0074
%define BDA_HDD_COUNT       0x0075

; Private disk workspace near the end of the 1 KiB EBDA.  The first 512
; bytes remain available as a sector/IDENTIFY bounce buffer.
%define HDW_CYLINDER        0x03C0
%define HDW_BUFFER          0x03C2
%define HDW_HEAD            0x03C4
%define HDW_SECTOR          0x03C5
%define HDW_REMAINING       0x03C6
%define HDW_COMPLETED       0x03C7
%define HDW_MODE            0x03C8
%define HDW_ERROR           0x03C9

%define HD_CYLINDERS        0x03E0
%define HD_TOTAL_LOW        0x03E2
%define HD_TOTAL_HIGH       0x03E4
%define HD_HEADS            0x03E6
%define HD_SECTORS_TRACK    0x03E7
%define HD_PRESENT          0x03E8

; POST scratch below the option-ROM/VGA apertures and above the BIOS stack.
; This RAM survives the 80286 reset used to return from protected mode.
%define POST_GDT_PHYS       0x8000
%define POST_GDTR_PHYS      0x8020
%define POST_EXT_KB_RESULT  0x8030
%define POST_MEMORY_FLAGS   0x8032
%define CMOS_SETUP_FLAGS    0x20
%define SETUP_NUMLOCK_ON    0x01
%define SETUP_HDD_FIRST     0x02
%define SETUP_FLAGS_WORK    0x03EA
%define SETUP_RTC_CENTURY   0x03EB
%define SETUP_RTC_YEAR      0x03EC
%define SETUP_RTC_MONTH     0x03ED
%define SETUP_RTC_DAY       0x03EE
%define SETUP_RTC_HOUR      0x03EF
%define SETUP_RTC_MINUTE    0x03F0
%define SETUP_RTC_SECOND    0x03F1
%define SETUP_EDIT_0        0x03F2
%define SETUP_EDIT_1        0x03F3
%define SETUP_EDIT_2        0x03F4
%define SETUP_EDIT_3        0x03F5
%define KBW_POST_RESULT     0x03FE
start:
    cli
    cld
    xor ax, ax
    mov ds, ax
    mov es, ax

    ; RESET after an 80286 protected-mode exit must not borrow an arbitrary
    ; conventional-memory stack. Windows may have a demand-loaded segment
    ; buffer anywhere in that RAM; the old 0000:7000 stack demonstrably wrote
    ; BIOS return addresses into such a buffer before the shutdown dispatcher
    ; restored Windows. Use the BIOS-owned EBDA scratch area while deciding
    ; whether this is a shutdown resume.
    mov ax, EBDA_SEG
    mov ss, ax
    mov sp, HDW_CYLINDER

    ; A real AT re-enters the BIOS after the 80286 is reset to leave protected
    ; mode. Honor the CMOS shutdown byte before touching the preserved IVT/BDA.
    call shutdown_resume_check

    ; A normal POST owns conventional RAM and may use the traditional scratch
    ; stack below POST_GDT_PHYS. Shutdown resume paths do not return here.
    xor ax, ax
    mov ss, ax
    mov sp, 0x7000

    ; Build a real-mode IVT. Unknown services safely IRET.
    xor di, di
    mov ax, default_int
    mov dx, 0xF000
    mov cx, 256
.ivt: stosw
    xchg ax, dx
    stosw
    xchg ax, dx
    loop .ivt

    mov word [0x08*4], int08
    mov word [0x09*4], int09
    mov word [0x10*4], int10
    mov word [0x11*4], int11
    mov word [0x12*4], int12
    mov word [0x13*4], int13
    mov word [0x14*4], int14
    mov word [0x15*4], int15
    mov word [0x16*4], int16
    mov word [0x17*4], int17
    mov word [0x19*4], int19
    mov word [0x1A*4], int1a
    mov word [0x70*4], int70
    mov word [0x76*4], int76

    call cmos_validate_or_default

    ; BIOS data area.
    mov ax, BDA_SEG
    mov es, ax
    xor di, di
    xor ax, ax
    mov cx, 128
    rep stosw
    mov word [es:0x0E], 0x9FC0       ; 1 KiB EBDA segment
    mov al, 0x14
    call cmos_read
    xor ah, ah
    and ax, 0x31FF                  ; clear bits 9-11 (COM) and 14-15 (LPT)
    or ax, 0x8400                   ; two serial and two parallel adapters
    mov [es:0x10], ax                ; equipment word from battery-backed CMOS
    mov word [es:0x00], 0x03F8       ; COM1
    mov word [es:0x02], 0x02F8       ; COM2
    mov word [es:0x08], 0x0378       ; LPT1
    mov word [es:0x0A], 0x0278       ; LPT2
    mov word [es:0x13], 639          ; conventional KiB (top 1 KiB reserved EBDA)
    mov word [es:0x1A], 0x001E       ; keyboard ring head
    mov word [es:0x1C], 0x001E       ; keyboard ring tail
    mov byte [es:0x49], 3            ; 80x25 color
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x1000       ; bytes per text page
    mov word [es:0x4E], 0            ; active-page display offset
    mov word [es:0x60], 0x0607       ; cursor start/end scan lines
    mov byte [es:0x62], 0            ; active display page
    mov byte [es:BDA_DISKETTE_STATUS], 0
    mov byte [es:BDA_HDD_STATUS], 0
    mov byte [es:BDA_HDD_COUNT], 0
    mov word [es:0x78], 0x0101       ; LPT1-2 BIOS timeout seconds
    mov word [es:0x7A], 0x0101       ; LPT3-4 (unpopulated)
    mov word [es:0x7C], 0x0101       ; COM1-2 BIOS timeout seconds
    mov word [es:0x7E], 0x0101       ; COM3-4 (unpopulated)

    ; Master/slave 8259 and 18.2 Hz PIT.
    mov al, 0x11
    out 0x20, al
    out 0xA0, al
    mov al, 0x08
    out 0x21, al
    mov al, 0x70
    out 0xA1, al
    mov al, 0x04
    out 0x21, al
    mov al, 0x02
    out 0xA1, al
    mov al, 0x01
    out 0x21, al
    out 0xA1, al
    mov al, 0xB8                    ; IRQ0 timer, IRQ1 keyboard, IRQ2 cascade, IRQ6 floppy
    out 0x21, al
    mov al, 0xFE                    ; IRQ8 RTC enabled; remaining slave inputs masked
    out 0xA1, al
    mov al, 0x36                    ; channel 0, mode 3, divisor 65536
    out 0x43, al
    xor al, al
    out 0x40, al
    out 0x40, al
    mov al, 0x54                    ; channel 1, mode 2, low byte only
    out 0x43, al
    mov al, 18                     ; approximately 15.1 us DRAM refresh cadence
    out 0x41, al

    ; Size and pattern-test installed extended RAM using genuine 80286
    ; protected mode. Returning to real mode resets the CPU, and the CMOS
    ; shutdown dispatcher above resumes execution at post_memory_resume.
    call post_memory_size_and_test

post_memory_resume:
    call post_memory_commit

    ; Detect the ATA master and cache a CHS geometry that matches the
    ; controller's 16-head/63-sector translation.
    ; Initialize/prove the AT keyboard and 8042 before storage discovery.
    call keyboard_init
    call apply_cmos_keyboard_preferences

    call ata_detect
    call cmos_sync_detected_hardware

    ; Scan and initialize expansion ROMs exactly through their guest-visible
    ; address windows.  The Diamond Stealth Pro VGA BIOS at C000:0000 hooks
    ; INT 10h during this pass; the system BIOS remains the fallback handler.
    call scan_option_roms
    mov ax, 0x0003
    int 0x10
    push cs
    pop ds                         ; embedded POST strings live in the ROM segment

    ; AMI-style quiet entry screen.  Keep the setup invitation visible before
    ; POST fills the display, and identify the exact firmware revision at the
    ; bottom.  The full hardware report is intentionally deferred until the
    ; keyboard-entry interval has expired.
    mov dx, 0x0018                 ; row 0, column 24
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_prompt_top
    call print
    mov dx, 0x0212                 ; row 2, column 18
    mov ah, 0x02
    xor bh, bh
    int 0x10
    call print_keyboard_post_status
    mov dx, 0x1812                 ; row 24, column 18
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, bios_revision
    call print
    sti
    call setup_entry_window
    jc short .continue_post
    call cmos_setup
    call apply_cmos_keyboard_preferences
    int 0x19
.continue_post:
    mov ax, 0x0003                 ; clear the quiet screen for full POST
    int 0x10
    push cs
    pop ds
    mov dx, 0x0018                 ; retain the live Setup invitation at top
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_prompt_top
    call print
    mov si, logo
    call print
    mov si, post_text
    call print
    call print_memory_summary
    mov si, post_text_after_memory
    call print
    ; The prompt remains truthful on the completed POST screen: accept Delete
    ; for one final controller-owned interval before selecting boot media.
    call setup_entry_window
    jc short .boot_after_post
    call cmos_setup
.boot_after_post:
    call apply_cmos_keyboard_preferences
    int 0x19
.halt: hlt
    jmp .halt

; IBM-compatible option-ROM scan.  ROMs are probed on 2 KiB boundaries
; from C0000h through DFFFFh.  Valid 55AAh headers and modulo-256 checksums
; are required before entry point +3 is called.  The size byte is in 512-byte
; blocks and determines the next scan address.
scan_option_roms:
    push ax
    push bx
    push cx
    push dx
    push si
    push ds
    push es
    mov bx, 0xC000
.next_segment:
    cmp bx, 0xE000
    jae .done
    mov es, bx
    cmp word [es:0], 0xAA55
    jne .advance_2k
    mov al, [es:2]
    test al, al
    jz .advance_2k

    xor ah, ah
    mov cx, ax
    shl cx, 1
    shl cx, 1
    shl cx, 1
    shl cx, 1
    shl cx, 1
    shl cx, 1
    shl cx, 1
    shl cx, 1
    shl cx, 1                       ; size blocks * 512 bytes
    xor si, si
    xor dl, dl
.checksum:
    add dl, [es:si]
    inc si
    loop .checksum
    test dl, dl
    jnz .advance_2k

    ; Far-call ES:0003 without depending on a host-side ROM callback.
    push bx                         ; retain candidate segment across ROM init
    push cs                         ; far-return address for option ROM RETF
    push word .returned
    push bx
    push word 0x0003
    retf
.returned:
    pop bx

    mov es, bx
    xor ax, ax
    mov al, [es:2]
    shl ax, 1
    shl ax, 1
    shl ax, 1
    shl ax, 1
    shl ax, 1                       ; 512 bytes = 20h paragraphs
    add bx, ax
    ; Option ROM starts are aligned to 2 KiB boundaries.
    add bx, 0x007F
    and bx, 0xFF80
    jmp .next_segment

.advance_2k:
    add bx, 0x0080
    jmp .next_segment
.done:
    pop es
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

clear_screen:
    push ax
    push cx
    push di
    push es
    mov ax, VIDEO_SEG
    mov es, ax
    xor di, di
    mov ax, 0x0720
    mov cx, 2000
    rep stosw
    mov ax, BDA_SEG
    mov es, ax
    mov word [es:0x50], 0
    pop es
    pop di
    pop cx
    pop ax
    ret

reset_cga_start:
    push ax
    push dx
    mov dx, 0x3D4
    mov al, 0x0C
    out dx, al
    inc dx
    xor al, al
    out dx, al
    dec dx
    mov al, 0x0D
    out dx, al
    inc dx
    xor al, al
    out dx, al
    pop dx
    pop ax
    ret

; DS:SI zero-terminated ROM string.  POST deliberately uses the installed
; video BIOS teletype service, just as an AT system BIOS would after the
; adapter ROM has initialized.  Do not mix direct byte offsets with the BDA's
; row/column cursor words (0040:0050); doing so scatters later numeric output.
print:
    push ax
    push bx
.next:
    lodsb
    test al, al
    J286_E .done
    cmp al, 10
    jne short .emit
    ; Historical ROM strings use LF as a logical newline.  Supply CR first so
    ; they remain left-aligned on a real teletype cursor; an explicit CR/LF
    ; pair merely receives an additional harmless CR.
    push ax
    mov ax, 0x0E0D
    xor bh, bh
    int 0x10
    pop ax
.emit:
    mov ah, 0x0E
    xor bh, bh
    int 0x10
    jmp .next
.done:
    pop bx
    pop ax
    ret

; ---------------------------------------------------------------------------
; 80286 protected-mode return and POST memory sizing
; ---------------------------------------------------------------------------

; CMOS 0Fh is the AT shutdown-status byte.  Codes 05h and 0Ah are the
; standard operating-system return paths through the DWORD at 0040:0067.
; Code 09h is the original PC/AT INT 15h/AH=87h block-move return: the
; DWORD at 0040:0067 is SS:SP, not CS:IP, and names a saved IRET frame.
; Code 02h is used by this BIOS after its protected-mode memory-test pass.
shutdown_resume_check:
    mov al, 0x0F
    call cmos_read
    test al, al
    J286_E .normal_reset
    cmp al, 0x02
    J286_E .memory_test_pass
    cmp al, 0x04
    J286_E .bootstrap_request
    cmp al, 0x09
    J286_E .block_move_return
    cmp al, 0x05
    J286_E .external_with_eoi
    cmp al, 0x0A
    J286_E .external_direct
    cmp al, 0x0B
    J286_E .external_iret
    cmp al, 0x0C
    J286_E .external_retf

    ; 01h, 03h and 06h-08h are IBM POST-internal continuations whose
    ; scratch-frame layouts belong to the exact ROM revision that emitted
    ; them.  This clean-room BIOS never emits those values.  If stale CMOS or
    ; foreign firmware leaves one behind, clear it and perform a complete POST
    ; rather than interpreting alien private RAM as a return frame.  0Dh-FFh
    ; are likewise defined as ordinary startup by later AT-compatible BIOSes.
    jmp short .normal_reset_clear

.normal_reset:
    ret

.normal_reset_clear:
    mov ax, 0x000F
    call cmos_write
    ret

.memory_test_pass:
    mov ax, 0x000F
    call cmos_write
    jmp 0xF000:post_memory_resume

.block_move_return:
    ; IBM AT shutdown 09h deliberately bypasses PIC reinitialization.  Windows
    ; 3.1 uses this older, widely-compatible mode-switch return by default.
    ; The original AT path also forces the keyboard-controller A20 gate low
    ; before resuming and enables maskable interrupts.  Windows relies on that
    ; complete firmware contract even though it immediately manages both states
    ; for its next protected-mode transition.
    ; The saved frame, beginning at SS:SP, is:
    ;   DS ES DI SI BP saved-SP BX DX CX AX IP CS FLAGS
    mov ax, 0x000F
    call cmos_write
    call post_disable_a20
    mov bx, [0x0467]
    mov ax, [0x0469]
    mov ss, ax
    mov sp, bx
    pop ds
    pop es
    pop di
    pop si
    pop bp
    add sp, 2                       ; discard the pre-switch SP image
    pop bx
    pop dx
    pop cx
    pop ax
    sti
    iret

.bootstrap_request:
    mov ax, 0x000F
    call cmos_write
    jmp 0xF000:int19

.external_with_eoi:
    mov ax, 0x000F
    call cmos_write
    mov al, 0x20
    out 0xA0, al
    out 0x20, al
.flush_8042:
    in al, 0x64
    test al, 1
    jz .external_jump
    in al, 0x60
    jmp .flush_8042
.external_jump:
    jmp far [0x0467]

.external_direct:
    mov ax, 0x000F
    call cmos_write
    jmp far [0x0467]

.external_iret:
    mov ax, 0x000F
    call cmos_write
    mov bx, [0x0467]
    mov ax, [0x0469]
    mov ss, ax
    mov sp, bx
    iret

.external_retf:
    mov ax, 0x000F
    call cmos_write
    mov bx, [0x0467]
    mov ax, [0x0469]
    mov ss, ax
    mov sp, bx
    retf

; Wait until the 8042 host input buffer is available.  The emulated 8042
; exposes the same status contract even though its current write latency is
; effectively zero.
kbc_wait_input_empty:
    in al, 0x64
    test al, 2
    jnz kbc_wait_input_empty
    ret

post_enable_a20:
    call kbc_wait_input_empty
    mov al, 0xD1
    out 0x64, al
    call kbc_wait_input_empty
    mov al, 0x03                    ; output-port bit0 high, A20 bit1 high
    out 0x60, al
    ret

post_disable_a20:
    call kbc_wait_input_empty
    mov al, 0xD1
    out 0x64, al
    call kbc_wait_input_empty
    mov al, 0x01                    ; output-port bit0 high, A20 bit1 low
    out 0x60, al
    ret

; Build a small RAM GDT, enter 80286 protected mode, and probe every 64 KiB
; bank from 1 MiB through the 16 MiB architectural ceiling.  Each bank must
; retain complementary patterns at both ends before it is counted as DRAM.
; Unpopulated host RAM reads as open bus and therefore terminates sizing.
post_memory_size_and_test:
    cli
    push ds
    push es
    push si
    push di
    push cx
    push ax

    push cs
    pop ds
    xor ax, ax
    mov es, ax
    mov si, post_gdt_template
    mov di, POST_GDT_PHYS
    mov cx, post_gdt_template_end - post_gdt_template
    rep movsb

    xor ax, ax
    mov ds, ax
    mov word [POST_GDTR_PHYS], post_gdt_template_end - post_gdt_template - 1
    mov word [POST_GDTR_PHYS + 2], POST_GDT_PHYS
    mov byte [POST_GDTR_PHYS + 4], 0
    mov byte [POST_GDTR_PHYS + 5], 0
    mov word [POST_EXT_KB_RESULT], 0
    mov byte [POST_MEMORY_FLAGS], 0

    call post_enable_a20
    lgdt [POST_GDTR_PHYS]
    mov ax, 1
    lmsw ax
    jmp 0x0008:post_memory_pm_entry

post_memory_pm_entry:
    mov ax, 0x0018                  ; low-memory data/stack descriptor
    mov ds, ax
    mov ss, ax
    mov sp, 0x7000
    mov bx, 0x0010                  ; 10h * 64 KiB = physical 1 MiB

.bank_loop:
    cmp bx, 0x0100                  ; 100h * 64 KiB = 16 MiB
    J286_AE .banks_done

    ; The probe descriptor has a zero low base word.  Its high base byte is
    ; therefore exactly the 64 KiB bank number.
    mov byte [POST_GDT_PHYS + 16 + 4], bl
    mov ax, 0x0010
    mov es, ax                      ; reload descriptor cache with new base

    mov word [es:0x0000], 0x55AA
    mov word [es:0x0002], 0xAA55
    mov word [es:0xFFFC], 0xA55A
    mov word [es:0xFFFE], 0x5AA5
    cmp word [es:0x0000], 0x55AA
    J286_NE .banks_done
    cmp word [es:0x0002], 0xAA55
    J286_NE .banks_done
    cmp word [es:0xFFFC], 0xA55A
    J286_NE .banks_done
    cmp word [es:0xFFFE], 0x5AA5
    J286_NE .banks_done

    mov word [es:0x0000], 0xAA55
    mov word [es:0x0002], 0x55AA
    mov word [es:0xFFFC], 0x5AA5
    mov word [es:0xFFFE], 0xA55A
    cmp word [es:0x0000], 0xAA55
    J286_NE .banks_done
    cmp word [es:0x0002], 0x55AA
    J286_NE .banks_done
    cmp word [es:0xFFFC], 0x5AA5
    J286_NE .banks_done
    cmp word [es:0xFFFE], 0xA55A
    J286_NE .banks_done

    inc bx
    jmp .bank_loop

.banks_done:
    mov ax, bx
    sub ax, 0x0010                  ; number of populated 64 KiB banks
    shl ax, 1
    shl ax, 1
    shl ax, 1
    shl ax, 1
    shl ax, 1
    shl ax, 1                       ; KiB = banks * 64
    mov [POST_EXT_KB_RESULT], ax
    mov byte [POST_MEMORY_FLAGS], 1

    ; 02h is the conventional AT 'memory test passed' shutdown code.  The
    ; 80286 cannot clear PE with LMSW, so pulse RESET through the 8042.
    mov al, 0x0F
    out 0x70, al
    mov al, 0x02
    out 0x71, al
    mov al, 0xFE
    out 0x64, al
.pm_wait_reset:
    hlt
    jmp .pm_wait_reset

; We arrive here through the reset/shutdown dispatcher in real mode.
; Publish what POST actually found into CMOS and leave A20 in the normal
; disabled-at-boot state for DOS/HIMEM to manage later.
post_memory_commit:
    call post_disable_a20
    xor ax, ax
    mov ds, ax
    mov bx, [POST_EXT_KB_RESULT]

    mov ax, 0x8015                  ; 640 KiB base memory = 0280h
    call cmos_write
    mov ax, 0x0216
    call cmos_write

    mov al, 0x17
    mov ah, bl
    call cmos_write
    mov al, 0x18
    mov ah, bh
    call cmos_write
    mov al, 0x30
    mov ah, bl
    call cmos_write
    mov al, 0x31
    mov ah, bh
    call cmos_write

    mov al, 0x0E
    call cmos_read
    and al, 0xEF                    ; this POST has resolved memory-size status
    mov ah, al
    mov al, 0x0E
    call cmos_write
    call cmos_write_checksum
    ret

; GDT copied to RAM because the bank-data descriptor's base changes while
; sizing memory.  80286 descriptors are limit, base-low, base-high, access,
; followed by two reserved bytes.
post_gdt_template:
    dw 0x0000, 0x0000
    db 0x00, 0x00, 0x00, 0x00
    dw 0xFFFF, 0x0000               ; selector 08h: F000:0000 BIOS code
    db 0x0F, 0x9A, 0x00, 0x00
    dw 0xFFFF, 0x0000               ; selector 10h: variable 64 KiB RAM bank
    db 0x00, 0x92, 0x00, 0x00
    dw 0xFFFF, 0x0000               ; selector 18h: low RAM data/stack
    db 0x00, 0x92, 0x00, 0x00
post_gdt_template_end:

; Print AX as unsigned decimal using the initialized VGA BIOS teletype path.
print_u16_decimal:
    push ax
    push bx
    push cx
    push dx
    xor cx, cx
    mov bx, 10
.dec_divide:
    xor dx, dx
    div bx
    push dx
    inc cx
    test ax, ax
    jnz .dec_divide
.dec_emit:
    pop dx
    mov al, dl
    add al, '0'
    mov ah, 0x0E
    xor bh, bh
    int 0x10
    loop .dec_emit
    pop dx
    pop cx
    pop bx
    pop ax
    ret

print_memory_summary:
    push ax
    push bx
    push ds
    push si
    push cs
    pop ds

    mov al, 0x30
    call cmos_read
    mov bl, al
    mov al, 0x31
    call cmos_read
    mov bh, al

    mov si, memory_line_prefix
    call print
    mov ax, bx
    add ax, 1024
    call print_u16_decimal
    mov si, memory_line_mid
    call print
    mov ax, bx
    call print_u16_decimal
    mov si, memory_line_suffix
    call print

    pop si
    pop ds
    pop bx
    pop ax
    ret; ---------------------------------------------------------------------------

; Show the result of the complete first-peripheral POST sequence: 8042
; controller self-test, keyboard-interface test, keyboard BAT/reset, enhanced
; ID, Set-2 selection, typematic/LED programming, scan enable, and translated
; IRQ1 command-byte installation.  This is status reporting only; the tests
; themselves have already traversed the emulated motherboard and keyboard.
print_keyboard_post_status:
    push ax
    push si
    push es
    mov ax, EBDA_SEG
    mov es, ax
    cmp byte [es:KBW_POST_RESULT], 1
    jne short .error
    mov si, keyboard_post_ok
    call print
    jmp short .done
.error:
    mov si, keyboard_post_error
    call print
.done:
    pop es
    pop si
    pop ax
    ret

; ---------------------------------------------------------------------------
; PC/AT CMOS/RTC services (Motorola MC146818A at ports 70h/71h)
; ---------------------------------------------------------------------------

; AL = CMOS index, returns AL = value. NMI is left enabled while selecting.
cmos_read:
    push dx
    mov dx, 0x70
    out dx, al
    inc dx
    in al, dx
    pop dx
    ret

; AL = CMOS index, AH = value.
cmos_write:
    push dx
    mov dx, 0x70
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    pop dx
    ret

cmos_wait_update:
    push ax
.wait:
    mov al, 0x0A
    call cmos_read
    test al, 0x80
    jnz .wait
    pop ax
    ret

; Return the 16-bit unsigned sum of CMOS bytes 10h through 20h in BX.
cmos_compute_checksum:
    push ax
    push cx
    xor bx, bx
    mov cl, 0x10
.next:
    mov al, cl
    call cmos_read
    xor ah, ah
    add bx, ax
    inc cl
    cmp cl, 0x21
    jne .next
    pop cx
    pop ax
    ret

cmos_write_checksum:
    push ax
    push bx
    call cmos_compute_checksum
    mov al, 0x2E
    mov ah, bh
    call cmos_write
    mov al, 0x2F
    mov ah, bl
    call cmos_write
    pop bx
    pop ax
    ret

; Establish the late-AT/AMI-style defaults used by this motherboard profile.
; Disk presence is synchronized after ATA IDENTIFY completes.
cmos_load_defaults:
    push ax
    mov ax, 0x260A                 ; 32.768 kHz divider, 1024 Hz periodic rate
    call cmos_write
    mov ax, 0x020B                 ; BCD, 24-hour, RTC interrupts disabled
    call cmos_write
    mov ax, 0x000E                 ; diagnostics good
    call cmos_write
    mov ax, 0x000F                 ; normal power-on shutdown state
    call cmos_write
    mov ax, 0x4410                 ; A: and B: are 1.44 MB drives (AMI extension)
    call cmos_write
    mov ax, 0x0012                 ; no fixed disk until ATA detection
    call cmos_write
    mov ax, 0x6314                 ; 2 floppies, 80-column color, 80287 present
    call cmos_write
    mov ax, 0x8015                 ; 640 KiB base memory (0280h)
    call cmos_write
    mov ax, 0x0216
    call cmos_write
    mov ax, 0x0017                 ; POST fills discovered extended memory
    call cmos_write
    mov ax, 0x0018
    call cmos_write
    mov ax, 0x0019                 ; no extended fixed-disk type yet
    call cmos_write
    mov ax, 0x0120                 ; OEM setup flags: boot Num Lock on, floppy first
    call cmos_write
    mov ax, 0x0030                 ; POST fills discovered extended memory
    call cmos_write
    mov ax, 0x0031
    call cmos_write
    call cmos_write_checksum
    pop ax
    ret

; Validate battery-backed configuration RAM and initialize RTC operating mode.
cmos_validate_or_default:
    push ax
    push bx
    push dx
    mov al, 0x0D
    call cmos_read
    test al, 0x80                  ; register D valid-RAM bit
    jz .defaults

    call cmos_compute_checksum
    mov al, 0x2E
    call cmos_read
    mov dh, al
    mov al, 0x2F
    call cmos_read
    mov dl, al
    cmp bx, dx
    jne .defaults

    mov al, 0x0E
    call cmos_read
    and al, 0x3F                  ; clear battery/checksum diagnostic flags
    mov ah, al
    mov al, 0x0E
    call cmos_write
    jmp .mode
.defaults:
    call cmos_load_defaults
.mode:
    mov ax, 0x260A
    call cmos_write
    mov ax, 0x020B
    call cmos_write
    mov al, 0x0C                  ; reading C clears any stale IRQ8 source
    call cmos_read
    pop dx
    pop bx
    pop ax
    ret

; Reflect ATA master presence in the AMI extended drive-type convention:
; type F in byte 12h and user type 47 in byte 19h.
cmos_sync_detected_hardware:
    push ax
    push ds
    mov ax, EBDA_SEG
    mov ds, ax
    cmp byte [HD_PRESENT], 1
    pop ds
    jne .absent
    mov ax, 0xF012
    call cmos_write
    mov ax, 0x2F19                 ; decimal 47
    call cmos_write
    jmp .checksum
.absent:
    mov ax, 0x0012
    call cmos_write
    mov ax, 0x0019
    call cmos_write
.checksum:
    call cmos_write_checksum
    pop ax
    ret

; IRQ8 handler. Reading register C clears PF/AF/UF and releases the RTC IRQ.
int70:
    push ax
    mov al, 0x0C
    call cmos_read
    mov al, 0x20
    out 0xA0, al
    out 0x20, al
    pop ax
    iret

; IRQ14 / INT 76h fixed-disk completion handler.  The IBM AT BIOS contract
; records completion in BDA byte 40:8Eh bit 7.  DOS IDE/ATAPI drivers such as
; VIDE-CDD use that flag while waiting for the primary-channel interrupt.
; A slave-PIC interrupt must be ended at both controllers, slave first.
int76:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    or byte [0x8E], 0x80
    mov al, 0x20
    out 0xA0, al
    out 0x20, al
    pop ds
    pop ax
    iret

; CROMWELL KEYBOARD REALITY BRICK 2 BEGIN
; ---------------------------------------------------------------------------
; Cromwell Technologies AT enhanced-keyboard BIOS
; Real 8042/keyboard transport.  IRQ1 consumes translated Set-1 bytes only.
; ---------------------------------------------------------------------------
%define BDA_KBD_FLAGS1       0x0017
%define BDA_KBD_FLAGS2       0x0018
%define BDA_ALT_NUMPAD       0x0019
%define BDA_KBD_HEAD         0x001A
%define BDA_KBD_TAIL         0x001C
%define BDA_BREAK_FLAG       0x0071
%define BDA_WARM_BOOT        0x0072
%define BDA_KBD_START        0x0080
%define BDA_KBD_END          0x0082
%define BDA_KBD_STATUS3      0x0096
%define BDA_KBD_STATUS4      0x0097

; Private keyboard workspace at the very end of the 1 KiB EBDA.  Current disk
; workspace ends at 03E8h.  Keep these bytes private to firmware; applications
; observe only standard BDA/INT 16h state.
; CMOS Setup owns 03EAh-03F5h.  Keep the runtime keyboard state above it;
; overlapping these areas corrupts typematic/E1/Alt-numpad state on Setup use.
%define KBW_TYPEMATIC        0x03F6
%define KBW_POWER_BAT_SEEN   0x03F7
%define KBW_E1_INDEX         0x03F8
%define KBW_ALT_NUM_ACTIVE   0x03F9
%define KBW_COMMAND_BUSY     0x03FA
%define KBW_LED_PENDING      0x03FB
%define KBW_COMMAND_BYTE     0x03FC
%define KBW_COMMAND_DATA     0x03FD

; Controller polling helpers -------------------------------------------------
kbc_wait_ibf_clear:
    push cx
    mov cx, 0xFFFF
.wait:
    in al, 0x64
    test al, 2
    jz short .ready
    loop .wait
    stc
    pop cx
    ret
.ready:
    clc
    pop cx
    ret

kbc_wait_obf_short:
    push cx
    push dx
    mov dx, 8
.outer:
    mov cx, 0xFFFF
.inner:
    in al, 0x64
    test al, 1
    jnz short .ready
    loop .inner
    dec dx
    jnz short .outer
    stc
    pop dx
    pop cx
    ret
.ready:
    clc
    pop dx
    pop cx
    ret

kbc_wait_obf_long:
    push cx
    push dx
    mov dx, 80
.outer:
    mov cx, 0xFFFF
.inner:
    in al, 0x64
    test al, 1
    jnz short .ready
    loop .inner
    dec dx
    jnz short .outer
    stc
    pop dx
    pop cx
    ret
.ready:
    clc
    pop dx
    pop cx
    ret

; AL = controller command.
kbc_write_command:
    push ax
    call kbc_wait_ibf_clear
    jc short .fail
    pop ax
    out 0x64, al
    clc
    ret
.fail:
    pop ax
    stc
    ret

; AL = byte for port 60h (controller parameter or keyboard byte).
kbc_write_data:
    push ax
    call kbc_wait_ibf_clear
    jc short .fail
    pop ax
    out 0x60, al
    clc
    ret
.fail:
    pop ax
    stc
    ret

; AL expected controller/keyboard response, ES = EBDA.  Keyboard power-on BAT
; may arrive while a controller test response is pending; remember it and keep
; waiting for the requested byte.
kbc_expect_short:
    push bx
    mov bl, al
.next:
    call kbc_wait_obf_short
    jc short .fail
    in al, 0x60
    cmp al, bl
    je short .ok
    cmp al, 0xAA
    jne short .next
    mov byte [es:KBW_POWER_BAT_SEEN], 1
    jmp short .next
.ok:
    clc
    pop bx
    ret
.fail:
    stc
    pop bx
    ret

kbc_expect_long:
    push bx
    mov bl, al
.next:
    call kbc_wait_obf_long
    jc short .fail
    in al, 0x60
    cmp al, bl
    je short .ok
    cmp al, 0xAA
    jne short .next
    mov byte [es:KBW_POWER_BAT_SEEN], 1
    jmp short .next
.ok:
    clc
    pop bx
    ret
.fail:
    stc
    pop bx
    ret

; Send one keyboard byte during POST.  Scanning is disabled before this helper
; is used for configuration, so only ACK/RESEND/BAT traffic can legitimately
; appear. AL = keyboard byte.
kbd_init_send:
    push bx
    push dx
    mov bl, al
    mov dl, 3
.retry:
    mov al, bl
    call kbc_write_data
    jc short .fail
.wait_response:
    call kbc_wait_obf_short
    jc short .fail
    in al, 0x60
    cmp al, 0xFA
    je short .ok
    cmp al, 0xFE
    je short .resend
    cmp al, 0xAA
    jne short .wait_response
    mov byte [es:KBW_POWER_BAT_SEEN], 1
    jmp short .wait_response
.resend:
    dec dl
    jnz short .retry
.fail:
    stc
    pop dx
    pop bx
    ret
.ok:
    clc
    pop dx
    pop bx
    ret

; POST keyboard/controller initialization -----------------------------------
keyboard_init:
    push ax
    push bx
    push cx
    push dx
    push ds
    push es

    mov ax, BDA_SEG
    mov ds, ax
    mov byte [BDA_KBD_FLAGS1], 0
    mov byte [BDA_KBD_FLAGS2], 0
    mov byte [BDA_ALT_NUMPAD], 0
    mov word [BDA_KBD_HEAD], 0x001E
    mov word [BDA_KBD_TAIL], 0x001E
    mov byte [BDA_BREAK_FLAG], 0
    mov word [BDA_KBD_START], 0x001E
    mov word [BDA_KBD_END], 0x003E
    mov byte [BDA_KBD_STATUS3], 0
    mov byte [BDA_KBD_STATUS4], 0

    mov ax, EBDA_SEG
    mov es, ax
    mov byte [es:KBW_TYPEMATIC], 0x2B
    mov byte [es:KBW_POWER_BAT_SEEN], 0
    mov byte [es:KBW_E1_INDEX], 0
    mov byte [es:KBW_ALT_NUM_ACTIVE], 0
    mov byte [es:KBW_COMMAND_BUSY], 0
    mov byte [es:KBW_LED_PENDING], 0
    mov byte [es:KBW_POST_RESULT], 0x80 ; controller/self-test stage

    mov al, 0x21
    out 0x80, al

    ; Capture a power-on BAT byte if the keyboard finished before POST arrived.
    in al, 0x64
    test al, 1
    jz short .disable
    in al, 0x60
    cmp al, 0xAA
    jne short .disable
    mov byte [es:KBW_POWER_BAT_SEEN], 1

.disable:
    mov al, 0xAD
    call kbc_write_command
    J286_C .failed

    ; 8042 controller self-test and keyboard-interface test.
    mov al, 0xAA
    call kbc_write_command
    J286_C .failed
    mov al, 0x55
    call kbc_expect_short
    J286_C .failed
    mov byte [es:KBW_POST_RESULT], 0x81 ; keyboard-interface stage

    mov al, 0xAB
    call kbc_write_command
    J286_C .failed
    xor al, al
    call kbc_expect_short
    J286_C .failed
    mov byte [es:KBW_POST_RESULT], 0x82 ; keyboard BAT/ID/config stage

    ; Command byte: system flag set, IRQ1 off, Set-2 translation off while the
    ; physical keyboard is identified/configured, keyboard interface enabled.
    mov al, 0x60
    call kbc_write_command
    J286_C .failed
    mov al, 0x04
    call kbc_write_data
    J286_C .failed
    mov al, 0xAE
    call kbc_write_command
    J286_C .failed

    ; The keyboard performs its own power-on BAT.  It ignores host commands
    ; during BAT, so do not issue FFh until AAh has actually crossed the wire.
    cmp byte [es:KBW_POWER_BAT_SEEN], 0
    jne short .power_bat_done
    mov al, 0xAA
    call kbc_expect_long
    J286_C .failed
.power_bat_done:

    ; Explicit reset/BAT, then default-disable while scan-set/typematic/LED
    ; configuration is performed.
    mov al, 0xFF
    call kbd_init_send
    J286_C .failed
    mov al, 0xAA
    call kbc_expect_long
    J286_C .failed

    mov al, 0xF5
    call kbd_init_send
    J286_C .failed

    ; Read enhanced keyboard ID with controller translation still disabled.
    mov al, 0xF2
    call kbd_init_send
    J286_C .failed
    mov al, 0xAB
    call kbc_expect_short
    J286_C .failed
    mov al, 0x83
    call kbc_expect_short
    J286_C .failed
    or byte [BDA_KBD_STATUS3], 0x10

    ; Select keyboard scan set 2.  The 8042 will translate it to PC Set 1 for
    ; BIOS/legacy software once translation is enabled below.
    mov al, 0xF0
    call kbd_init_send
    J286_C .failed
    mov al, 0x02
    call kbd_init_send
    J286_C .failed

    mov al, 0xF3
    call kbd_init_send
    J286_C .failed
    mov al, 0x2B
    call kbd_init_send
    J286_C .failed

    mov al, 0xED
    call kbd_init_send
    J286_C .failed
    xor al, al
    call kbd_init_send
    J286_C .failed

    mov al, 0xF4
    call kbd_init_send
    J286_C .failed

    ; Runtime command byte: IRQ1 + system flag + translation, interface enabled.
    mov al, 0x60
    call kbc_write_command
    J286_C .failed
    mov al, 0x45
    call kbc_write_data
    J286_C .failed
    mov al, 0xAE
    call kbc_write_command
    J286_C .failed

    mov al, 0x2F
    out 0x80, al
    mov byte [es:KBW_POST_RESULT], 1
    jmp .done

.failed:
    ; A keyboard failure must not silently fabricate a working device.  Leave a
    ; POST checkpoint, but restore a sane AT controller command byte so firmware
    ; can continue far enough for diagnostics/software to inspect the failure.
    mov al, 0x2E
    out 0x80, al
    mov al, 0x60
    call kbc_write_command
    jc short .enable_only
    mov al, 0x45
    call kbc_write_data
.enable_only:
    mov al, 0xAE
    call kbc_write_command
.done:
    pop es
    pop ds
    pop dx
    pop cx
    pop bx
    pop ax
    ret


; Runtime keyboard helpers ---------------------------------------------------
; These helpers operate with DS = BDA_SEG.  They use the same physical
; controller/keyboard command path POST used; no host-side state is consulted.

; AL = byte to send to the keyboard.  Wait for ACK, retry on RESEND.
; Returns CF set on timeout/error.  Keyboard command responses bypass the
; 8042 scan-code translator in Keyboard Reality Brick 2.
kbd_runtime_send:
    push bx
    push cx
    push dx
    mov bl, al
    mov dl, 3
.retry:
    and byte [BDA_KBD_STATUS4], 0xCF     ; clear ACK/RESEND observations
    mov al, bl
    call kbc_write_data
    jc short .failed
.wait:
    ; With IRQ1 enabled the interrupt handler may consume FAh/FEh before this
    ; foreground command path observes port 64h.  INT 09h records that physical
    ; response in BDA_KBD_STATUS4.  Honor the ISR-owned observation both before
    ; and after the bounded OBF wait; otherwise a successful EDh can time out
    ; after its ACK and leave the real keyboard waiting for its parameter with
    ; scanning suspended.  Do not CLI around the transaction: an edge-triggered
    ; IRQ1 would remain pending after a polled read and deliver a false empty-OBF
    ; interrupt when FLAGS were restored.
    test byte [BDA_KBD_STATUS4], 0x10
    jnz short .acked
    test byte [BDA_KBD_STATUS4], 0x20
    jnz short .resend
    call kbc_wait_obf_short
    jnc short .read_response
    test byte [BDA_KBD_STATUS4], 0x10
    jnz short .acked
    test byte [BDA_KBD_STATUS4], 0x20
    jnz short .resend
    jmp short .failed
.read_response:
    in al, 0x60
    cmp al, 0xFA
    je short .acked
    cmp al, 0xFE
    je short .resend
    ; During ED/F3/F0 parameter transactions scanning is suspended by the
    ; keyboard itself.  A stray byte here is stale pre-command input; consume
    ; it rather than mistaking it for protocol acknowledgement.
    jmp short .wait
.resend:
    or byte [BDA_KBD_STATUS4], 0x20
    dec dl
    jnz short .retry
.failed:
    or byte [BDA_KBD_STATUS4], 0x80
    stc
    pop dx
    pop cx
    pop bx
    ret
.acked:
    or byte [BDA_KBD_STATUS4], 0x10
    and byte [BDA_KBD_STATUS4], 0x7F
    clc
    pop dx
    pop cx
    pop bx
    ret

; Synchronize the three physical keyboard indicators with the lock-active
; bits in BDA 40:17.  EDh and its parameter both traverse the real keyboard.
kbd_update_leds:
    push ax
    push dx
    mov al, [BDA_KBD_FLAGS1]
    shr al, 4
    and al, 7
    mov dl, al
    or byte [BDA_KBD_STATUS4], 0x40
    mov al, 0xED
    call kbd_runtime_send
    jc short .fail
    mov al, dl
    call kbd_runtime_send
    jc short .fail
    and byte [BDA_KBD_STATUS4], 0xF8
    or byte [BDA_KBD_STATUS4], dl
    and byte [BDA_KBD_STATUS4], 0xBF
    clc
    pop dx
    pop ax
    ret
.fail:
    and byte [BDA_KBD_STATUS4], 0xBF
    stc
    pop dx
    pop ax
    ret

; Apply the battery-backed boot keyboard policy through the real BIOS data area
; and the keyboard's EDh LED command. No host keyboard state is consulted.
apply_cmos_keyboard_preferences:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov al, CMOS_SETUP_FLAGS
    call cmos_read
    test al, SETUP_NUMLOCK_ON
    jz short .numlock_off
    or byte [BDA_KBD_FLAGS1], 0x20
    jmp short .sync
.numlock_off:
    and byte [BDA_KBD_FLAGS1], 0xDF
.sync:
    call kbd_update_leds
    pop ds
    pop ax
    ret

; AL = encoded IBM typematic parameter.
kbd_set_typematic:
    push ax
    push dx
    push es
    mov dl, al
    mov ax, EBDA_SEG
    mov es, ax
    mov [es:KBW_TYPEMATIC], dl
    mov al, 0xF3
    call kbd_runtime_send
    jc short .fail
    mov al, [es:KBW_TYPEMATIC]
    call kbd_runtime_send
.fail:
    pop es
    pop dx
    pop ax
    ret

; AX = BIOS keyboard word (AH scan code, AL ASCII/extended marker).
; DS = BDA_SEG.  Returns CF set if ring buffer is full.
kbd_enqueue_ax:
    push bx
    push dx
    mov bx, [BDA_KBD_TAIL]
    mov dx, bx
    add dx, 2
    cmp dx, [BDA_KBD_END]
    jb short .check_full
    mov dx, [BDA_KBD_START]
.check_full:
    cmp dx, [BDA_KBD_HEAD]
    je short .full
    mov [bx], ax
    mov [BDA_KBD_TAIL], dx
    clc
    pop dx
    pop bx
    ret
.full:
    stc
    pop dx
    pop bx
    ret

kbd_clear_buffer:
    mov ax, [BDA_KBD_TAIL]
    mov [BDA_KBD_HEAD], ax
    ret

; AX <- head word, CF set if empty.  Does not consume.
kbd_peek_ax:
    push bx
    mov bx, [BDA_KBD_HEAD]
    cmp bx, [BDA_KBD_TAIL]
    je short .empty
    mov ax, [bx]
    clc
    pop bx
    ret
.empty:
    stc
    pop bx
    ret

; AX <- head word, CF set if empty.  Consumes.
kbd_dequeue_ax:
    push bx
    push dx
    mov bx, [BDA_KBD_HEAD]
    cmp bx, [BDA_KBD_TAIL]
    je short .empty
    mov ax, [bx]
    mov dx, bx
    add dx, 2
    cmp dx, [BDA_KBD_END]
    jb short .save
    mov dx, [BDA_KBD_START]
.save:
    mov [BDA_KBD_HEAD], dx
    clc
    pop dx
    pop bx
    ret
.empty:
    stc
    pop dx
    pop bx
    ret

kbd_drop_head:
    push ax
    call kbd_dequeue_ax
    pop ax
    ret

; Compatibility filtering used only by INT 16h AH=00/01.  Extended services
; see the unfiltered queue word.
; AX in/out, CF set = enhanced-only key should be discarded.
kbd_classic_filter:
    ; IBM enhanced marker E0h becomes ASCII 00h for the 84-key interface.
    cmp al, 0xE0
    jne short .not_e0_marker
    xor al, al
.not_e0_marker:
    ; Keypad Enter is represented E0h/0Dh (or E0h/0Ah under Ctrl).
    cmp ah, 0xE0
    jne short .not_keypad_enter
    cmp al, 0x0D
    je short .map_enter
    cmp al, 0x0A
    je short .map_enter
    cmp al, '/'
    jne short .not_keypad_enter
    mov ah, 0x35
    clc
    ret
.map_enter:
    mov ah, 0x1C
    clc
    ret
.not_keypad_enter:
    ; F11/F12 combinations do not exist on the 84-key compatibility surface.
    cmp al, 0
    jne short .keep
    cmp ah, 0x85
    jb short .keep
    cmp ah, 0x8C
    jbe short .discard
.keep:
    clc
    ret
.discard:
    stc
    ret

; Extended read/status converts the historical F0h low-byte marker to zero.
kbd_extended_filter:
    cmp al, 0xF0
    jne short .done
    xor al, al
.done:
    ret

kbd_pause_tail:
    db 0x1D,0x45,0xE1,0x9D,0xC5

; CROMWELL KEYBOARD REALITY BRICK 2 END

default_int: iret

int08:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    inc word [0x6C]
    jne .check_midnight
    inc word [0x6E]
.check_midnight:
    cmp word [0x6E], TICKS_PER_DAY_HIGH
    jb .eoi
    ja .midnight
    cmp word [0x6C], TICKS_PER_DAY_LOW
    jb .eoi
.midnight:
    sub word [0x6C], TICKS_PER_DAY_LOW
    sbb word [0x6E], TICKS_PER_DAY_HIGH
    inc byte [0x70]                ; midnight rollover flag/counter
.eoi:
    mov al, 0x20
    out 0x20, al
    pop ds
    pop ax
    iret

; CROMWELL KEYBOARD REALITY BRICK 2 IRQ1 BEGIN
int09:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds
    push es

    in al, 0x60
    mov dl, al

    mov ax, BDA_SEG
    mov ds, ax
    mov ax, EBDA_SEG
    mov es, ax

    ; Keyboard command responses are protocol traffic, not scan codes.  When
    ; IRQ1 wins the race with a foreground BIOS ED/F3/F0 transaction, record
    ; the response for kbd_runtime_send and consume it here.  This is the IBM
    ; BIOS BDA command-completion handshake; passing FAh/FEh into the ordinary
    ; Set-1 decoder both loses the acknowledgement and corrupts key state.
    cmp dl, 0xFA
    jne short .not_command_ack
    or byte [BDA_KBD_STATUS4], 0x10
    jmp .complete
.not_command_ack:
    cmp dl, 0xFE
    jne short .scan_byte
    or byte [BDA_KBD_STATUS4], 0x20
    jmp .complete
.scan_byte:

    ; Give ROM extensions/keyboard drivers the documented scan-code intercept.
    ; Default INT 15/AH=4F returns CF set, meaning BIOS continues processing.
    mov al, dl
    mov ah, 0x4F
    stc
    int 0x15
    J286_NC .complete
    mov dl, al

    mov al, dl
    call kbd_process_scan
    J286_C .pause_wait

.complete:
    ; Device-interrupt-complete notification for keyboard device class 02h.
    mov ax, 0x9102
    int 0x15
    mov al, 0x20
    out 0x20, al
    pop es
    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    iret

.pause_wait:
    ; Pause is implemented as BIOS suspension, not as a synthetic key.  EOI this
    ; IRQ, restore caller-visible registers, then allow a nested IRQ1 to clear
    ; BDA pause-active bit 3 on the next make code.
    mov al, 0x20
    out 0x20, al
    pop es
    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax

    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    sti
.pause_loop:
    test byte [BDA_KBD_FLAGS2], 0x08
    jz short .pause_done
    hlt
    jmp short .pause_loop
.pause_done:
    cli
    pop ds
    pop ax
    iret

; Process one translated Set-1 byte from the 8042.
; DS=BDA, ES=EBDA.  CF set requests the outer Pause wait.
kbd_process_scan:
    clc

    ; Continue the exact E1 Pause sequence if one is active.
    cmp byte [es:KBW_E1_INDEX], 0
    je short .prefix_check
    xor bx, bx
    mov bl, [es:KBW_E1_INDEX]
    dec bl
    cmp bl, 5
    jae short .bad_e1
    cmp dl, [cs:kbd_pause_tail+bx]
    jne short .bad_e1
    inc byte [es:KBW_E1_INDEX]
    cmp byte [es:KBW_E1_INDEX], 6
    jb short .ignore
    mov byte [es:KBW_E1_INDEX], 0
    and byte [BDA_KBD_STATUS3], 0xFE
    or byte [BDA_KBD_FLAGS2], 0x08
    stc
    ret
.bad_e1:
    mov byte [es:KBW_E1_INDEX], 0
    and byte [BDA_KBD_STATUS3], 0xFE
    clc
    ret

.prefix_check:
    cmp dl, 0xE1
    jne short .not_e1
    or byte [BDA_KBD_STATUS3], 1
    mov byte [es:KBW_E1_INDEX], 1
    ret
.not_e1:
    cmp dl, 0xE0
    jne short .not_e0
    or byte [BDA_KBD_STATUS3], 2
    ret
.not_e0:

    ; A nested IRQ1 while Pause is active resumes execution and consumes the
    ; resume key.  Break codes do not resume it.
    test byte [BDA_KBD_FLAGS2], 0x08
    jz short .capture_e0
    test dl, 0x80
    jnz short .ignore
    and byte [BDA_KBD_FLAGS2], 0xF7
    ret

.capture_e0:
    mov dh, [BDA_KBD_STATUS3]
    and dh, 2
    and byte [BDA_KBD_STATUS3], 0xFD
    test dh, 2
    J286_E .ordinary
    jmp .extended

.ignore:
    clc
    ret

; ---------------------------------------------------------------------------
; E0-prefixed translated Set-1
; ---------------------------------------------------------------------------
.extended:
    mov bl, dl
    and bl, 0x7F

    ; Artificial shift bytes generated around PrintScreen/navigation are not
    ; physical Shift changes.
    cmp bl, 0x2A
    J286_E .ignore
    cmp bl, 0x36
    J286_E .ignore

    cmp bl, 0x1D
    jne short .ext_not_ctrl
    test dl, 0x80
    jnz short .rctrl_up
    or byte [BDA_KBD_STATUS3], 0x04
    or byte [BDA_KBD_FLAGS1], 0x04
    ret
.rctrl_up:
    and byte [BDA_KBD_STATUS3], 0xFB
    test byte [BDA_KBD_FLAGS2], 0x01
    J286_NE .ignore
    and byte [BDA_KBD_FLAGS1], 0xFB
    ret

.ext_not_ctrl:
    cmp bl, 0x38
    jne short .ext_not_alt
    test dl, 0x80
    jnz short .ralt_up
    or byte [BDA_KBD_STATUS3], 0x08
    or byte [BDA_KBD_FLAGS1], 0x08
    ret
.ralt_up:
    and byte [BDA_KBD_STATUS3], 0xF7
    test byte [BDA_KBD_FLAGS2], 0x02
    J286_NE .ignore
    and byte [BDA_KBD_FLAGS1], 0xF7
    call kbd_finish_alt_numpad
    ret

.ext_not_alt:
    cmp bl, 0x1C
    jne short .ext_not_enter
    test dl, 0x80
    J286_NE .ignore
    test byte [BDA_KBD_FLAGS1], 0x08
    jnz short .ext_enter_alt
    mov ax, 0xE00D
    test byte [BDA_KBD_FLAGS1], 0x04
    jz short .ext_enter_queue
    mov al, 0x0A
.ext_enter_queue:
    call kbd_enqueue_ax
    ret
.ext_enter_alt:
    mov ax, 0xA600
    call kbd_enqueue_ax
    ret

.ext_not_enter:
    cmp bl, 0x35
    jne short .ext_not_divide
    test dl, 0x80
    J286_NE .ignore
    test byte [BDA_KBD_FLAGS1], 0x08
    jnz short .ext_div_alt
    test byte [BDA_KBD_FLAGS1], 0x04
    jnz short .ext_div_ctrl
    mov ax, 0xE02F
    call kbd_enqueue_ax
    ret
.ext_div_ctrl:
    mov ax, 0x9500
    call kbd_enqueue_ax
    ret
.ext_div_alt:
    mov ax, 0xA400
    call kbd_enqueue_ax
    ret

.ext_not_divide:
    cmp bl, 0x37
    jne short .ext_not_prtsc
    test dl, 0x80
    J286_NE .ignore
    int 0x05
    ret

.ext_not_prtsc:
    cmp bl, 0x46
    jne short .ext_not_break
    test dl, 0x80
    J286_NE .ignore
    test byte [BDA_KBD_FLAGS1], 0x04
    jz short .ext_pause
    call kbd_ctrl_break
    ret
.ext_pause:
    or byte [BDA_KBD_FLAGS2], 0x08
    stc
    ret

.ext_not_break:
    cmp bl, 0x47
    J286_B .ignore
    cmp bl, 0x53
    J286_A .ignore
    cmp bl, 0x4A
    J286_E .ignore
    cmp bl, 0x4C
    J286_E .ignore
    cmp bl, 0x4E
    J286_E .ignore

    test dl, 0x80
    jnz short .ext_nav_break

    ; Ctrl+Alt+Delete requests a real 8042 RESET# pulse.
    cmp bl, 0x53
    jne short .ext_no_reset
    mov al, [BDA_KBD_FLAGS1]
    and al, 0x0C
    cmp al, 0x0C
    jne short .ext_no_reset
    call kbd_warm_reset
    ret
.ext_no_reset:

    cmp bl, 0x52
    jne short .ext_nav_translate
    test byte [BDA_KBD_FLAGS2], 0x80
    jnz short .ext_nav_translate
    or byte [BDA_KBD_FLAGS2], 0x80
    xor byte [BDA_KBD_FLAGS1], 0x80

.ext_nav_translate:
    test byte [BDA_KBD_FLAGS1], 0x08
    jnz short .ext_nav_alt
    test byte [BDA_KBD_FLAGS1], 0x04
    jnz short .ext_nav_ctrl
    mov ah, bl
    mov al, 0xE0
    call kbd_enqueue_ax
    ret

.ext_nav_ctrl:
    xor bx, bx
    mov bl, dl
    and bl, 0x7F
    sub bl, 0x47
    mov ah, [cs:kbd_nav_ctrl+bx]
    cmp ah, 0
    J286_E .ignore
    mov al, 0xE0
    call kbd_enqueue_ax
    ret

.ext_nav_alt:
    xor bx, bx
    mov bl, dl
    and bl, 0x7F
    sub bl, 0x47
    mov ah, [cs:kbd_nav_alt+bx]
    cmp ah, 0
    J286_E .ignore
    xor al, al
    call kbd_enqueue_ax
    ret

.ext_nav_break:
    cmp bl, 0x52
    J286_NE .ignore
    and byte [BDA_KBD_FLAGS2], 0x7F
    ret

; ---------------------------------------------------------------------------
; Non-E0 translated Set-1
; ---------------------------------------------------------------------------
.ordinary:
    mov bl, dl
    and bl, 0x7F

    test dl, 0x80
    J286_NE .ordinary_break

    ; Shift keys
    cmp bl, 0x2A
    jne short .not_lshift
    or byte [BDA_KBD_FLAGS1], 0x02
    ret
.not_lshift:
    cmp bl, 0x36
    jne short .not_rshift
    or byte [BDA_KBD_FLAGS1], 0x01
    ret
.not_rshift:

    ; Left Ctrl / Alt.  Overall Ctrl/Alt bits remain set while either side is held.
    cmp bl, 0x1D
    jne short .not_lctrl
    or byte [BDA_KBD_FLAGS2], 0x01
    or byte [BDA_KBD_FLAGS1], 0x04
    ret
.not_lctrl:
    cmp bl, 0x38
    jne short .not_lalt
    or byte [BDA_KBD_FLAGS2], 0x02
    or byte [BDA_KBD_FLAGS1], 0x08
    ret
.not_lalt:

    ; Alt+PrintScreen is SysReq (translated make 54h).
    cmp bl, 0x54
    jne short .not_sysreq
    or byte [BDA_KBD_FLAGS2], 0x04
    xor al, al
    mov ah, 0x85
    int 0x15
    ret
.not_sysreq:

    ; Ctrl+Alt+Delete from the numeric pad as well as dedicated Delete.
    cmp bl, 0x53
    jne short .not_reset
    mov al, [BDA_KBD_FLAGS1]
    and al, 0x0C
    cmp al, 0x0C
    jne short .not_reset
    call kbd_warm_reset
    ret
.not_reset:

    ; Ctrl+ScrollLock = Break; Ctrl+NumLock = Pause.
    cmp bl, 0x46
    jne short .not_ctrl_scroll
    test byte [BDA_KBD_FLAGS1], 0x04
    jz short .scroll_lock
    call kbd_ctrl_break
    ret
.not_ctrl_scroll:
    cmp bl, 0x45
    jne short .not_ctrl_num
    test byte [BDA_KBD_FLAGS1], 0x04
    jz short .num_lock
    or byte [BDA_KBD_FLAGS2], 0x08
    stc
    ret
.not_ctrl_num:

    cmp bl, 0x3A
    jne short .not_caps
    test byte [BDA_KBD_FLAGS2], 0x40
    J286_NE .ignore
    or byte [BDA_KBD_FLAGS2], 0x40
    xor byte [BDA_KBD_FLAGS1], 0x40
    call kbd_update_leds
    ret
.not_caps:

.scroll_lock:
    cmp bl, 0x46
    jne short .num_lock
    test byte [BDA_KBD_FLAGS2], 0x10
    J286_NE .ignore
    or byte [BDA_KBD_FLAGS2], 0x10
    xor byte [BDA_KBD_FLAGS1], 0x10
    call kbd_update_leds
    ret

.num_lock:
    cmp bl, 0x45
    jne short .not_num_lock
    test byte [BDA_KBD_FLAGS2], 0x20
    J286_NE .ignore
    or byte [BDA_KBD_FLAGS2], 0x20
    xor byte [BDA_KBD_FLAGS1], 0x20
    call kbd_update_leds
    ret
.not_num_lock:

    ; Function keys
    cmp bl, 0x3B
    jb short .not_function
    cmp bl, 0x44
    J286_BE .function_f1_f10
    cmp bl, 0x57
    J286_E .function_f11
    cmp bl, 0x58
    J286_E .function_f12
.not_function:

    ; Numeric keypad and navigation cluster (unprefixed).
    cmp bl, 0x47
    J286_B .regular_ascii
    cmp bl, 0x53
    J286_BE .keypad
    jmp .ignore

.function_f1_f10:
    mov ah, bl
    xor al, al
    test byte [BDA_KBD_FLAGS1], 0x08
    jnz short .f_alt
    test byte [BDA_KBD_FLAGS1], 0x04
    jnz short .f_ctrl
    test byte [BDA_KBD_FLAGS1], 0x03
    jnz short .f_shift
    call kbd_enqueue_ax
    ret
.f_shift:
    add ah, 0x19                     ; 3B->54
    call kbd_enqueue_ax
    ret
.f_ctrl:
    add ah, 0x23                     ; 3B->5E
    call kbd_enqueue_ax
    ret
.f_alt:
    add ah, 0x2D                     ; 3B->68
    call kbd_enqueue_ax
    ret

.function_f11:
    mov ah, 0x85
    jmp short .f11_12_common
.function_f12:
    mov ah, 0x86
.f11_12_common:
    xor al, al
    test byte [BDA_KBD_FLAGS1], 0x08
    jnz short .f11_alt
    test byte [BDA_KBD_FLAGS1], 0x04
    jnz short .f11_ctrl
    test byte [BDA_KBD_FLAGS1], 0x03
    jnz short .f11_shift
    call kbd_enqueue_ax
    ret
.f11_shift:
    add ah, 2
    call kbd_enqueue_ax
    ret
.f11_ctrl:
    add ah, 4
    call kbd_enqueue_ax
    ret
.f11_alt:
    add ah, 6
    call kbd_enqueue_ax
    ret

.keypad:
    ; Keypad math keys are direct characters except while Alt is used.
    cmp bl, 0x4A
    jne short .kp_not_minus
    mov ax, 0x4A2D
    call kbd_enqueue_ax
    ret
.kp_not_minus:
    cmp bl, 0x4E
    jne short .kp_not_plus
    mov ax, 0x4E2B
    call kbd_enqueue_ax
    ret
.kp_not_plus:

    ; Alt+numeric keypad accumulates a decimal byte and emits it on Alt release.
    test byte [BDA_KBD_FLAGS1], 0x08
    jz short .kp_not_alt
    xor bx, bx
    mov bl, dl
    and bl, 0x7F
    sub bl, 0x47
    mov cl, [cs:kbd_keypad_digit+bx]
    cmp cl, 0xFF
    J286_E .ignore
    mov al, [BDA_ALT_NUMPAD]
    mov ah, 0
    mov ch, 10
    mul ch
    add al, cl
    mov [BDA_ALT_NUMPAD], al
    mov byte [es:KBW_ALT_NUM_ACTIVE], 1
    ret
.kp_not_alt:

    ; Ctrl keypad navigation uses the IBM enhanced control scan codes.
    test byte [BDA_KBD_FLAGS1], 0x04
    jz short .kp_num_mode
    xor bx, bx
    mov bl, dl
    and bl, 0x7F
    sub bl, 0x47
    mov ah, [cs:kbd_nav_ctrl+bx]
    cmp ah, 0
    J286_E .ignore
    xor al, al
    call kbd_enqueue_ax
    ret

.kp_num_mode:
    ; Numeric mode is NumLock XOR Shift.
    mov al, [BDA_KBD_FLAGS1]
    mov ah, al
    and al, 0x20
    and ah, 0x03
    cmp al, 0
    je short .kp_num_off
    cmp ah, 0
    je short .kp_numeric
    jmp short .kp_navigation
.kp_num_off:
    cmp ah, 0
    jne short .kp_numeric
.kp_navigation:
    cmp bl, 0x52
    jne short .kp_nav_queue
    test byte [BDA_KBD_FLAGS2], 0x80
    jnz short .kp_nav_queue
    or byte [BDA_KBD_FLAGS2], 0x80
    xor byte [BDA_KBD_FLAGS1], 0x80
.kp_nav_queue:
    mov ah, bl
    xor al, al
    call kbd_enqueue_ax
    ret
.kp_numeric:
    xor bx, bx
    mov bl, dl
    and bl, 0x7F
    sub bl, 0x47
    mov al, [cs:kbd_keypad_ascii+bx]
    cmp al, 0
    J286_E .ignore
    mov ah, dl
    and ah, 0x7F
    call kbd_enqueue_ax
    ret

.regular_ascii:
    cmp bl, 0x39
    J286_A .ignore
    xor bx, bx
    mov bl, dl
    and bl, 0x7F

    ; Shift+Tab is a non-ASCII special key.
    cmp bl, 0x0F
    jne short .reg_choose_table
    test byte [BDA_KBD_FLAGS1], 0x03
    jz short .reg_choose_table
    mov ax, 0x0F00
    call kbd_enqueue_ax
    ret

.reg_choose_table:
    mov al, [cs:kbd_ascii+bx]
    test byte [BDA_KBD_FLAGS1], 0x03
    jz short .reg_caps
    mov al, [cs:kbd_shift_ascii+bx]
.reg_caps:
    test byte [BDA_KBD_FLAGS1], 0x40
    jz short .reg_ctrl
    cmp al, 'A'
    jb short .reg_caps_lower
    cmp al, 'Z'
    ja short .reg_caps_lower
    or al, 0x20
    jmp short .reg_ctrl
.reg_caps_lower:
    cmp al, 'a'
    jb short .reg_ctrl
    cmp al, 'z'
    ja short .reg_ctrl
    and al, 0xDF

.reg_ctrl:
    test byte [BDA_KBD_FLAGS1], 0x04
    jz short .reg_alt
    cmp bl, 0x0E
    jne short .ctrl_not_backspace
    mov al, 0x7F
    jmp short .reg_ready
.ctrl_not_backspace:
    cmp bl, 0x1C
    jne short .ctrl_not_enter
    mov al, 0x0A
    jmp short .reg_ready
.ctrl_not_enter:
    mov ch, al
    and ch, 0xDF
    cmp ch, 'A'
    jb short .ctrl_symbols
    cmp ch, 'Z'
    ja short .ctrl_symbols
    mov al, ch
    sub al, 0x40
    jmp short .reg_ready
.ctrl_symbols:
    cmp bl, 0x1A
    jne short .ctrl_not_lbr
    mov al, 0x1B
    jmp short .reg_ready
.ctrl_not_lbr:
    cmp bl, 0x2B
    jne short .ctrl_not_slash
    mov al, 0x1C
    jmp short .reg_ready
.ctrl_not_slash:
    cmp bl, 0x1B
    jne short .reg_ready
    mov al, 0x1D

.reg_alt:
    test byte [BDA_KBD_FLAGS1], 0x08
    jz short .reg_ready
    xor al, al
    cmp bl, 0x02
    jb short .reg_ready
    cmp bl, 0x0D
    ja short .reg_ready
    mov ah, bl
    add ah, 0x76
    jmp short .reg_queue

.reg_ready:
    mov ah, bl
.reg_queue:
    cmp al, 0
    jne short .reg_enqueue
    ; Zero from the translation table is meaningful only for Alt/special keys.
    test byte [BDA_KBD_FLAGS1], 0x08
    J286_E .ignore
.reg_enqueue:
    call kbd_enqueue_ax
    ret

.ordinary_break:
    cmp bl, 0x2A
    jne short .break_not_lshift
    and byte [BDA_KBD_FLAGS1], 0xFD
    ret
.break_not_lshift:
    cmp bl, 0x36
    jne short .break_not_rshift
    and byte [BDA_KBD_FLAGS1], 0xFE
    ret
.break_not_rshift:
    cmp bl, 0x1D
    jne short .break_not_lctrl
    and byte [BDA_KBD_FLAGS2], 0xFE
    test byte [BDA_KBD_STATUS3], 0x04
    J286_NE .ignore
    and byte [BDA_KBD_FLAGS1], 0xFB
    ret
.break_not_lctrl:
    cmp bl, 0x38
    jne short .break_not_lalt
    and byte [BDA_KBD_FLAGS2], 0xFD
    test byte [BDA_KBD_STATUS3], 0x08
    J286_NE .ignore
    and byte [BDA_KBD_FLAGS1], 0xF7
    call kbd_finish_alt_numpad
    ret
.break_not_lalt:
    cmp bl, 0x3A
    jne short .break_not_caps
    and byte [BDA_KBD_FLAGS2], 0xBF
    ret
.break_not_caps:
    cmp bl, 0x45
    jne short .break_not_num
    and byte [BDA_KBD_FLAGS2], 0xDF
    ret
.break_not_num:
    cmp bl, 0x46
    jne short .break_not_scroll
    and byte [BDA_KBD_FLAGS2], 0xEF
    ret
.break_not_scroll:
    cmp bl, 0x52
    jne short .break_not_insert
    and byte [BDA_KBD_FLAGS2], 0x7F
    ret
.break_not_insert:
    cmp bl, 0x54
    J286_NE .ignore
    and byte [BDA_KBD_FLAGS2], 0xFB
    mov al, 1
    mov ah, 0x85
    int 0x15
    ret

; Ctrl-Break BIOS semantics: flush, set break flag, invoke INT 1Bh, then place
; a zero word into the BIOS queue.
kbd_ctrl_break:
    call kbd_clear_buffer
    or byte [BDA_BREAK_FLAG], 0x80
    int 0x1B
    xor ax, ax
    call kbd_enqueue_ax
    ret

kbd_finish_alt_numpad:
    cmp byte [es:KBW_ALT_NUM_ACTIVE], 0
    je short .done
    mov al, [BDA_ALT_NUMPAD]
    xor ah, ah
    call kbd_enqueue_ax
    mov byte [BDA_ALT_NUMPAD], 0
    mov byte [es:KBW_ALT_NUM_ACTIVE], 0
.done:
    ret

kbd_warm_reset:
    mov word [BDA_WARM_BOOT], 0x1234
    mov al, 0xFE                     ; 8042 pulse RESET# output low
    call kbc_write_command
    ; A correctly wired motherboard resets the CPU before this executes again.
.wait_reset:
    sti
    hlt
    jmp short .wait_reset

; Indexed by Set-1 scan 47h..53h. Zero entries are non-navigation math/center.
kbd_nav_ctrl:
    db 0x77,0x8D,0x84,0,0x73,0,0x74,0,0x75,0x91,0x76,0x92,0x93
kbd_nav_alt:
    db 0x97,0x98,0x99,0,0x9B,0,0x9D,0,0x9F,0xA0,0xA1,0xA2,0xA3
kbd_keypad_digit:
    db 7,8,9,0xFF,4,5,6,0xFF,1,2,3,0,0xFF
kbd_keypad_ascii:
    db '7','8','9',0,'4','5','6',0,'1','2','3','0','.'

; Basic Set-1 character translation.  Enhanced/special keys are handled above.
kbd_ascii:
    db 0,27,'1234567890-=',8,9
    db 'qwertyuiop[]',13,0,'asdfghjkl',59,39,96,0,92
    db 'zxcvbnm,./',0,'*',0,' '
kbd_shift_ascii:
    db 0,27,'!@#$%^&*()_+',8,9
    db 'QWERTYUIOP{}',13,0,'ASDFGHJKL:',34,126,0,124
    db 'ZXCVBNM<>?',0,'*',0,' '
; CROMWELL KEYBOARD REALITY BRICK 2 IRQ1 END

int10:
    cmp ah, 0x00
    J286_E video_set_mode
    cmp ah, 0x01
    J286_NE .not01
    jmp video_set_shape
.not01:
    cmp ah, 0x02
    J286_NE .not02
    jmp video_set_cursor
.not02:
    cmp ah, 0x03
    J286_NE .not03
    jmp video_get_cursor
.not03:
    cmp ah, 0x05
    J286_NE .not05
    jmp video_set_page
.not05:
    cmp ah, 0x06
    J286_NE .not06
    jmp video_scroll_up
.not06:
    cmp ah, 0x07
    J286_NE .not07
    jmp video_scroll_down
.not07:
    cmp ah, 0x08
    J286_NE .not08
    jmp video_read_cell
.not08:
    cmp ah, 0x09
    J286_NE .not09
    jmp video_write_cell
.not09:
    cmp ah, 0x0A
    J286_NE .not0a
    jmp video_write_char
.not0a:
    cmp ah, 0x0E
    J286_NE .not0e
    jmp video_tty
.not0e:
    cmp ah, 0x0F
    J286_NE .not0f
    jmp video_get_mode
.not0f:
    cmp ah, 0x12
    J286_NE .not12
    jmp video_ega_query
.not12:
    cmp ah, 0x1A
    J286_NE .unknown
    jmp video_display_query
.unknown:
    iret

video_set_mode:
    push ax
    push dx
    push es
    and al, 0x7F
    mov dl, al
    mov ax, BDA_SEG
    mov es, ax
    mov [es:0x49], dl
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x1000
    mov word [es:0x4E], 0
    mov word [es:0x50], 0
    mov byte [es:0x62], 0
    mov dx, 0x3D4
    mov al, 0x0C
    out dx, al
    inc dx
    xor al, al
    out dx, al
    dec dx
    mov al, 0x0D
    out dx, al
    inc dx
    xor al, al
    out dx, al
    pop es
    pop dx
    pop ax
    test al, 0x80
    J286_NE .done
    call clear_screen
.done:
    iret

video_set_shape:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov [0x60], cx
    pop ds
    pop ax
    iret

video_set_cursor:
    push ax
    push bx
    push ds
    mov al, bh
    xor ah, ah
    shl ax, 1
    mov bx, ax
    mov ax, BDA_SEG
    mov ds, ax
    mov [bx+0x50], dx
    pop ds
    pop bx
    pop ax
    iret

video_get_cursor:
    push ax
    push bx
    push ds
    mov al, bh
    xor ah, ah
    shl ax, 1
    mov bx, ax
    mov ax, BDA_SEG
    mov ds, ax
    mov dx, [bx+0x50]
    mov cx, [0x60]
    pop ds
    pop bx
    pop ax
    iret

video_set_page:
    push ax
    push bx
    push dx
    push ds
    mov dl, al
    and dl, 7
    mov ax, BDA_SEG
    mov ds, ax
    mov [0x62], dl
    xor ax, ax
    mov al, dl
    mov bx, 0x1000
    mul bx
    mov [0x4E], ax
    shr ax, 1                       ; 6845 start address is in character words
    mov bx, ax
    mov dx, 0x3D4
    mov al, 0x0C
    out dx, al
    inc dx
    mov al, bh
    out dx, al
    dec dx
    mov al, 0x0D
    out dx, al
    inc dx
    mov al, bl
    out dx, al
    pop ds
    pop dx
    pop bx
    pop ax
    iret

; Convert page BH and cursor from the BDA to ES:DI in color text RAM.
video_cursor_address:
    push ax
    push bx
    push cx
    push dx
    push ds
    mov al, bh
    and al, 7
    xor ah, ah
    mov di, ax
    shl di, 12
    shl ax, 1
    mov bx, ax
    mov ax, BDA_SEG
    mov ds, ax
    mov dx, [bx+0x50]
    mov cx, dx
    xor ax, ax
    mov al, ch
    mov bx, 160
    mul bx
    add di, ax
    xor ax, ax
    mov al, cl
    shl ax, 1
    add di, ax
    mov ax, VIDEO_SEG
    mov es, ax
    pop ds
    pop dx
    pop cx
    pop bx
    pop ax
    ret

video_read_cell:
    push di
    push es
    call video_cursor_address
    mov ax, [es:di]
    pop es
    pop di
    iret

video_write_cell:
    push ax
    push cx
    push di
    push es
    call video_cursor_address
    cld
    mov ah, bl
    rep stosw
    pop es
    pop di
    pop cx
    pop ax
    iret

video_write_char:
    push ax
    push cx
    push di
    push es
    call video_cursor_address
    cld
.loop:
    mov [es:di], al
    add di, 2
    loop .loop
    pop es
    pop di
    pop cx
    pop ax
    iret

video_tty:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push es
    push ds
    mov cl, al
    mov ax, BDA_SEG
    mov ds, ax
    xor bx, bx
    mov bl, [0x62]
    shl bx, 1
    mov dx, [bx+0x50]
    cmp cl, 7
    J286_E .save
    cmp cl, 8
    J286_E .backspace
    cmp cl, 13
    J286_E .carriage
    cmp cl, 10
    J286_E .linefeed
    mov ax, VIDEO_SEG
    mov es, ax
    mov si, dx
    xor di, di
    mov al, [0x62]
    xor ah, ah
    mov di, ax
    shl di, 12
    mov ax, si
    mov al, ah
    xor ah, ah
    mov bx, 160
    mul bx
    add di, ax
    mov ax, si
    and ax, 0x00FF
    shl ax, 1
    add di, ax
    mov al, cl
    ; In text mode AH=0Eh advances the cursor without replacing the cell's
    ; existing attribute.  This lets firmware prepaint blue/gray Setup panes
    ; while retaining the same architecturally correct teletype path.
    mov ah, [es:di+1]
    stosw
    mov dx, si
    inc dl
    cmp dl, 80
    J286_B .save
.carriage:
    xor dl, dl
    cmp cl, 13
    J286_E .save
.linefeed:
    inc dh
    cmp dh, 25
    J286_B .save
    mov ax, 0x0601
    mov bh, 7
    xor cx, cx
    push dx                         ; scrolling window uses DX; preserve cursor column
    mov dx, 0x184F
    int 0x10
    pop dx
    mov dh, 24
    jmp .save
.backspace:
    test dl, dl
    J286_E .save
    dec dl
.save:
    mov ax, BDA_SEG
    mov ds, ax
    xor bx, bx
    mov bl, [0x62]
    shl bx, 1
    mov [bx+0x50], dx
    pop ds
    pop es
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    iret

; AH=06/07, rectangular text-window scroll. AL=0 clears the window.
video_scroll_up:
    push bp
    mov bp, sp
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds
    push es
    sub sp, 4
    mov al, [ss:bp-7]               ; window height = bottom-top+1
    sub al, [ss:bp-5]
    inc al
    mov bl, [ss:bp-2]               ; requested line count
    test bl, bl
    J286_E .clear_all
    cmp bl, al
    J286_AE .clear_all
    mov al, [ss:bp-5]
    mov [ss:bp-18], al              ; destination row
    add al, bl
    mov [ss:bp-19], al              ; source row
    mov ax, VIDEO_SEG
    mov ds, ax
    mov es, ax
    cld
.copy_row:
    xor ax, ax
    mov al, [ss:bp-18]
    mov bx, 160
    mul bx
    mov di, ax
    xor ax, ax
    mov al, [ss:bp-6]
    shl ax, 1
    add di, ax
    xor ax, ax
    mov al, [ss:bp-19]
    mov bx, 160
    mul bx
    mov si, ax
    xor ax, ax
    mov al, [ss:bp-6]
    shl ax, 1
    add si, ax
    xor cx, cx
    mov cl, [ss:bp-8]
    sub cl, [ss:bp-6]
    inc cx
    rep movsw
    inc byte [ss:bp-18]
    inc byte [ss:bp-19]
    mov al, [ss:bp-19]
    cmp al, [ss:bp-7]
    J286_BE .copy_row
    mov al, [ss:bp-7]
    sub al, [ss:bp-2]
    inc al
    mov [ss:bp-20], al              ; first vacated row
    jmp .fill
.clear_all:
    mov al, [ss:bp-5]
    mov [ss:bp-20], al
    mov ax, VIDEO_SEG
    mov es, ax
.fill:
    cld
.fill_row:
    xor ax, ax
    mov al, [ss:bp-20]
    mov bx, 160
    mul bx
    mov di, ax
    xor ax, ax
    mov al, [ss:bp-6]
    shl ax, 1
    add di, ax
    xor cx, cx
    mov cl, [ss:bp-8]
    sub cl, [ss:bp-6]
    inc cx
    mov al, 0x20
    mov ah, [ss:bp-3]
    rep stosw
    inc byte [ss:bp-20]
    mov al, [ss:bp-20]
    cmp al, [ss:bp-7]
    J286_BE .fill_row
    add sp, 4
    pop es
    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    pop bp
    iret
video_scroll_down:
    push bp
    mov bp, sp
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds
    push es
    sub sp, 4
    mov al, [ss:bp-7]
    sub al, [ss:bp-5]
    inc al
    mov bl, [ss:bp-2]
    test bl, bl
    J286_E .clear_all
    cmp bl, al
    J286_AE .clear_all
    mov al, [ss:bp-7]
    mov [ss:bp-18], al              ; destination row
    sub al, bl
    mov [ss:bp-19], al              ; source row
    mov ax, VIDEO_SEG
    mov ds, ax
    mov es, ax
    std
.copy_row:
    xor ax, ax
    mov al, [ss:bp-18]
    mov bx, 160
    mul bx
    mov di, ax
    xor ax, ax
    mov al, [ss:bp-8]               ; right edge, copy backwards
    shl ax, 1
    add di, ax
    xor ax, ax
    mov al, [ss:bp-19]
    mov bx, 160
    mul bx
    mov si, ax
    xor ax, ax
    mov al, [ss:bp-8]
    shl ax, 1
    add si, ax
    xor cx, cx
    mov cl, [ss:bp-8]
    sub cl, [ss:bp-6]
    inc cx
    rep movsw
    mov al, [ss:bp-19]
    cmp al, [ss:bp-5]
    J286_E .copy_done
    dec byte [ss:bp-18]
    dec byte [ss:bp-19]
    jmp .copy_row
.copy_done:
    mov al, [ss:bp-5]
    mov [ss:bp-20], al
    mov bl, [ss:bp-2]
    dec bl
    add bl, al
    mov [ss:bp-19], bl              ; last vacated row
    jmp .fill
.clear_all:
    mov al, [ss:bp-5]
    mov [ss:bp-20], al
    mov al, [ss:bp-7]
    mov [ss:bp-19], al
    mov ax, VIDEO_SEG
    mov es, ax
.fill:
    cld
.fill_row:
    xor ax, ax
    mov al, [ss:bp-20]
    mov bx, 160
    mul bx
    mov di, ax
    xor ax, ax
    mov al, [ss:bp-6]
    shl ax, 1
    add di, ax
    xor cx, cx
    mov cl, [ss:bp-8]
    sub cl, [ss:bp-6]
    inc cx
    mov al, 0x20
    mov ah, [ss:bp-3]
    rep stosw
    inc byte [ss:bp-20]
    mov al, [ss:bp-20]
    cmp al, [ss:bp-19]
    J286_BE .fill_row
    add sp, 4
    pop es
    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    pop bp
    iret

video_get_mode:
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov al, [0x49]
    mov ah, [0x4A]
    mov bh, [0x62]
    pop ds
    iret
video_ega_query:
    cmp bl, 0x10
    J286_NE .done
    mov bh, 0xFF                    ; no EGA feature connector
.done:
    iret
video_display_query:
    xor al, al                      ; INT 10h/1Ah unsupported on CGA
    iret

int11:
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov ax, [0x10]
    pop ds
    iret

int12: mov ax, 639
    iret

; Core disk BIOS for DOS-era CHS callers.
; AH=00 reset, 01 status, 02 read, 03 write, 04 verify, 08 parameters,
;    0C seek, 10 ready, 15 drive type/capacity.
int13:

    push bp
    mov bp, sp
    sub sp, 2
    mov [ss:bp-2], dx               ; preserve the original drive number

    cmp ah, 0x00
    J286_E .reset
    cmp ah, 0x01
    J286_E .status
    cmp ah, 0x02
    J286_E .read
    cmp ah, 0x03
    J286_E .write
    cmp ah, 0x04
    J286_E .verify
    cmp ah, 0x08
    J286_E .params
    cmp ah, 0x09
    J286_E .ready
    cmp ah, 0x0C
    J286_E .seek
    cmp ah, 0x0D
    J286_E .reset
    cmp ah, 0x10
    J286_E .ready
    cmp ah, 0x11
    J286_E .seek
    cmp ah, 0x14
    J286_E .ready
    cmp ah, 0x15
    J286_E .drive_type
    mov ah, 0x01                    ; invalid function
    jmp .failure

.reset:
    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .reset_hard_disk
    cmp dl, 2
    J286_AE .drive_not_ready
    mov dx, 0x3F2
    xor al, al
    out dx, al
    mov al, 0x0C
    out dx, al
    xor ah, ah
    jmp .success
.reset_hard_disk:
    cmp dl, 0x80
    J286_NE .drive_not_ready
    call ata_drive_present
    J286_C .drive_not_ready
    mov dx, 0x3F6
    mov al, 0x06                    ; nIEN + software reset
    out dx, al
    mov al, 0x02                    ; release reset, keep interrupts disabled
    out dx, al
    xor ah, ah
    jmp .success

.status:
    mov dx, [ss:bp-2]
    cmp dl, 2
    J286_B .get_status
    cmp dl, 0x80
    J286_NE .drive_not_ready
.get_status:
    push bx
    mov bl, dl
    call bios_get_disk_status
    pop bx
    test ah, ah
    J286_NE .failure
    jmp .success

.read:

    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .read_hard_disk
    cmp dl, 2
    J286_AE .drive_not_ready

    call floppy_read
    J286_C .controller_failure
    xor ah, ah
    jmp .success
.read_hard_disk:
    cmp dl, 0x80
    J286_NE .drive_not_ready
    call ata_transfer_chs
    J286_C .ata_failure
    xor ah, ah
    jmp .success

.write:
    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .write_hard_disk
    cmp dl, 2
    J286_AE .drive_not_ready
    call floppy_write
    J286_C .controller_failure
    xor ah, ah
    jmp .success
.write_hard_disk:
    cmp dl, 0x80
    J286_NE .drive_not_ready
    call ata_transfer_chs
    J286_C .ata_failure
    xor ah, ah
    jmp .success

.verify:
    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .verify_hard_disk
    cmp dl, 2
    J286_AE .drive_not_ready
    ; The emulated floppy controller validates sectors during normal reads.
    ; DOS installation does not require a destructive or buffer-altering
    ; floppy verify, so report ready here.
    xor ah, ah
    jmp .success
.verify_hard_disk:
    cmp dl, 0x80
    J286_NE .drive_not_ready
    call ata_transfer_chs
    J286_C .ata_failure
    xor ah, ah
    jmp .success

.params:
    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .hard_disk_params
    cmp dl, 2
    J286_AE .drive_not_ready
    mov ch, 79                     ; 80 cylinders, maximum index 79
    mov cl, 18                     ; 18 sectors per track
    mov dh, 1                      ; two heads, maximum index 1
    mov dl, 2                      ; two BIOS diskette drive numbers
    mov bl, 4                      ; 1.44 MB drive type
    push cs
    pop es
    mov di, floppy_dpt
    xor ah, ah
    jmp .success
.hard_disk_params:
    cmp dl, 0x80
    J286_NE .drive_not_ready
    call ata_drive_present
    J286_C .drive_not_ready
    push ds
    mov ax, EBDA_SEG
    mov ds, ax
    mov ax, [HD_CYLINDERS]
    dec ax                         ; INT 13h returns the maximum cylinder
    mov ch, al
    mov cl, [HD_SECTORS_TRACK]
    mov bl, ah
    and bl, 3
    shl bl, 6
    or cl, bl
    mov dh, [HD_HEADS]
    dec dh
    mov dl, 1
    pop ds
    xor ah, ah
    jmp .success

.seek:
.ready:
    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .ready_hard_disk
    cmp dl, 2
    J286_AE .drive_not_ready
    xor ah, ah
    jmp .success
.ready_hard_disk:
    cmp dl, 0x80
    J286_NE .drive_not_ready
    call ata_drive_present
    J286_C .drive_not_ready
    xor ah, ah
    jmp .success

.drive_type:
    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .hard_disk_type
    cmp dl, 2
    J286_AE .drive_not_ready
    mov ah, 1                      ; diskette, no change-line support promised
    jmp .success
.hard_disk_type:
    cmp dl, 0x80
    J286_NE .drive_not_ready
    call ata_drive_present
    J286_C .drive_not_ready
    push ds
    mov ax, EBDA_SEG
    mov ds, ax
    mov dx, [HD_TOTAL_LOW]
    mov cx, [HD_TOTAL_HIGH]
    pop ds
    mov ah, 3                      ; fixed disk; CX:DX contains 512-byte sectors
    jmp .success

.ata_failure:
    call ata_get_last_error
    test ah, ah
    J286_NE .failure
.controller_failure:
    mov ah, 0x20
    jmp .failure
.drive_not_ready:
    mov ah, 0xAA

.failure:
    push ax
    push bx
    mov al, ah
    mov bl, [ss:bp-2]
    call bios_set_disk_status
    pop bx
    pop ax
    xor al, al
    or word [ss:bp+6], 1
    mov sp, bp
    pop bp
    iret

.success:
    push ax
    push bx
    xor al, al
    mov bl, [ss:bp-2]
    call bios_set_disk_status
    pop bx
    pop ax
    and word [ss:bp+6], 0xFFFE
    mov sp, bp
    pop bp
    iret

; AL=status, BL=original BIOS drive.  All registers are preserved.
bios_set_disk_status:
    push ax
    push bx
    push ds
    mov bh, al
    mov ax, BDA_SEG
    mov ds, ax
    test bl, 0x80
    J286_NE .hard_disk
    mov [BDA_DISKETTE_STATUS], bh
    jmp .done
.hard_disk:
    mov [BDA_HDD_STATUS], bh
.done:
    pop ds
    pop bx
    pop ax
    ret

; BL=original BIOS drive; returns AH=last status.  AL is scratch.
bios_get_disk_status:
    push bx
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    test bl, 0x80
    J286_NE .hard_disk
    mov ah, [BDA_DISKETTE_STATUS]
    jmp .done
.hard_disk:
    mov ah, [BDA_HDD_STATUS]
.done:
    pop ds
    pop bx
    ret

; CROMWELL KEYBOARD REALITY BRICK 2 INT15 BEGIN
; AT system services used during DOS/keyboard initialization.
int15:
    push bp
    mov bp, sp

    cmp ah, 0x4F                   ; keyboard scan-code intercept
    J286_E .keyboard_intercept
    cmp ah, 0x85                   ; SysReq notification
    J286_E .keyboard_success
    cmp ah, 0x90                   ; device busy
    J286_E .keyboard_success
    cmp ah, 0x91                   ; interrupt complete
    J286_E .keyboard_success
    cmp ah, 0x88                   ; extended memory above 1 MiB, in KiB
    J286_E .extended_memory

    mov ah, 0x86                   ; unsupported function
    or word [ss:bp+6], 1
    pop bp
    iret

.keyboard_intercept:
    ; Default BIOS owns the scan.  TSRs/ROM extensions may hook INT 15h and
    ; clear CF to consume it before INT 09h translation.
    or word [ss:bp+6], 1
    pop bp
    iret

.keyboard_success:
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.extended_memory:
    mov al, 0x30
    call cmos_read
    mov dl, al
    mov al, 0x31
    call cmos_read
    mov ah, al
    mov al, dl
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret
; CROMWELL KEYBOARD REALITY BRICK 2 INT15 END

; Read AL sectors from floppy CHS CH/DH/CL into ES:BX via DMA channel 2.
floppy_read:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds

    ; The 8237 cannot cross a 64 KiB page. Read one sector at a time into
    ; a fixed EBDA bounce buffer, then copy with the CPU to caller ES:BX.
    mov si, ax
    mov di, bx
    mov ax, 0x9FC0
    mov ds, ax
    mov [0x200], ch                 ; current cylinder
    mov [0x201], dh                 ; current head
    mov al, cl
    and al, 0x3F
    mov [0x202], al                 ; current sector
    mov [0x203], dl                 ; drive
    mov ax, si
    mov [0x204], al                 ; remaining sectors

.next_sector:
    cmp byte [0x204], 0
    J286_NE .do_sector
    jmp .success

.do_sector:
    ; DMA channel 2: device -> memory, 512 bytes at physical 9FC0:0000.
    xor al, al
    out 0x0C, al
    mov al, 6                      ; mask DMA channel 2 while programming
    out 0x0A, al
    mov al, 0x46                   ; single, device-to-memory, channel 2
    out 0x0B, al
    mov ax, 0xFC00
    out 0x04, al
    mov al, ah
    out 0x04, al
    mov al, 9
    out 0x81, al
    mov ax, 511
    out 0x05, al
    mov al, ah
    out 0x05, al
    mov al, 2
    out 0x0A, al
    mov al, [0x203]
    cmp al, 1
    J286_NE .motor_a
    mov al, 0x2D                    ; motor B, DMA/IRQ enabled, drive B selected
    jmp .motor_ready
.motor_a:
    mov al, 0x1C                    ; motor A, DMA/IRQ enabled, drive A selected
.motor_ready:
    mov dx, 0x3F2
    out dx, al
    mov dx, 0x3F5
    mov al, 0xE6
    out dx, al
    mov al, [0x201]
    shl al, 2
    or al, [0x203]
    out dx, al
    mov al, [0x200]
    out dx, al
    mov al, [0x201]
    out dx, al
    mov al, [0x202]
    out dx, al
    mov al, 2
    out dx, al
    mov al, [0x202]                 ; EOT = this sector only
    out dx, al
    mov al, 0x1B
    out dx, al
    mov al, 0xFF
    out dx, al
    ; Controller completes synchronously in this substrate.
    in al, dx
    test al, 0xC0
    J286_NE .discard_bad
    mov cx, 6
.result: in al, dx
    loop .result

    ; Copy the bounce sector to the caller's buffer.
    push ds
    mov ax, 0x9FC0
    mov ds, ax
    xor si, si
    mov cx, 256
    cld
    rep movsw
    pop ds

    inc byte [0x202]
    cmp byte [0x202], 19
    J286_B .advance_done
    mov byte [0x202], 1
    xor byte [0x201], 1
    cmp byte [0x201], 0
    J286_NE .advance_done
    inc byte [0x200]
.advance_done:
    dec byte [0x204]
    jmp .next_sector

.discard_bad:
    mov cx, 6
.discard: in al, dx
    loop .discard
    stc
    jmp .done
.success:
    clc
.done:
    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; Write AL sectors from ES:BX to floppy CHS CH/DH/CL through DMA channel 2.
floppy_write:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds
    push es

    mov si, ax
    mov di, bx
    mov ax, EBDA_SEG
    mov ds, ax
    mov [0x200], ch                 ; current cylinder
    mov [0x201], dh                 ; current head
    mov al, cl
    and al, 0x3F
    mov [0x202], al                 ; current sector
    mov [0x203], dl                 ; drive
    mov ax, si
    mov [0x204], al                 ; remaining sectors

.next_sector:
    cmp byte [0x204], 0
    J286_E .success

    ; Copy one caller sector into the EBDA bounce buffer.
    push es
    push ds
    push es
    pop ds                          ; DS = caller ES
    mov si, di
    mov ax, EBDA_SEG
    mov es, ax
    xor di, di
    mov cx, 256
    cld
    rep movsw
    mov bx, si                      ; next caller offset
    pop ds                          ; DS = EBDA
    pop es                          ; ES = caller segment
    mov di, bx

    xor al, al
    out 0x0C, al
    mov al, 6                      ; mask DMA channel 2
    out 0x0A, al
    mov al, 0x4A                   ; single, memory-to-device, channel 2
    out 0x0B, al
    mov ax, 0xFC00
    out 0x04, al
    mov al, ah
    out 0x04, al
    mov al, 9
    out 0x81, al
    mov ax, 511
    out 0x05, al
    mov al, ah
    out 0x05, al
    mov al, 2                      ; unmask DMA channel 2
    out 0x0A, al

    mov al, [0x203]
    cmp al, 1
    J286_NE .motor_a
    mov al, 0x2D                    ; motor B, DMA/IRQ enabled, drive B selected
    jmp .motor_ready
.motor_a:
    mov al, 0x1C                    ; motor A, DMA/IRQ enabled, drive A selected
.motor_ready:
    mov dx, 0x3F2
    out dx, al
    mov dx, 0x3F5
    mov al, 0xC5                   ; MFM WRITE DATA
    out dx, al
    mov al, [0x201]
    shl al, 2
    or al, [0x203]
    out dx, al
    mov al, [0x200]
    out dx, al
    mov al, [0x201]
    out dx, al
    mov al, [0x202]
    out dx, al
    mov al, 2
    out dx, al
    mov al, [0x202]
    out dx, al
    mov al, 0x1B
    out dx, al
    mov al, 0xFF
    out dx, al

    in al, dx                       ; ST0
    test al, 0xC0
    J286_NE .discard_bad
    mov cx, 6
.result:
    in al, dx
    loop .result

    inc byte [0x202]
    cmp byte [0x202], 19
    J286_B .advance_done
    mov byte [0x202], 1
    xor byte [0x201], 1
    cmp byte [0x201], 0
    J286_NE .advance_done
    inc byte [0x200]
.advance_done:
    dec byte [0x204]
    jmp .next_sector

.discard_bad:
    mov cx, 6
.discard:
    in al, dx
    loop .discard
    stc
    jmp .done
.success:
    clc
.done:
    pop es
    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; Detect the ATA master and cache geometry/capacity in the EBDA.
ata_detect:
    push ax
    push bx
    push cx
    push dx
    push di
    push ds
    push es

    mov ax, EBDA_SEG
    mov ds, ax
    mov byte [HD_PRESENT], 0
    mov word [HD_CYLINDERS], 0
    mov byte [HD_HEADS], 0
    mov byte [HD_SECTORS_TRACK], 0
    mov word [HD_TOTAL_LOW], 0
    mov word [HD_TOTAL_HIGH], 0

    mov ax, BDA_SEG
    mov es, ax
    mov byte [es:BDA_HDD_COUNT], 0
    mov byte [es:BDA_HDD_STATUS], 0

    mov dx, 0x3F6
    mov al, 0x02                    ; disable ATA interrupts; BIOS uses polling
    out dx, al
    mov dx, 0x1F6
    mov al, 0xA0                    ; master, CHS mode
    out dx, al
    mov dx, 0x1F2
    xor al, al
    out dx, al
    inc dx
    out dx, al
    inc dx
    out dx, al
    inc dx
    out dx, al
    mov dx, 0x1F7
    mov al, 0xEC                    ; IDENTIFY DEVICE
    out dx, al
    call ata_wait_drq
    J286_C .done

    mov ax, EBDA_SEG
    mov es, ax
    xor di, di
    mov cx, 256
    mov dx, 0x1F0
    cld
    rep insw
    call ata_wait_complete
    J286_C .done

    ; IDENTIFY words 1, 3, and 6 contain the translated CHS geometry.
    mov ax, [2]
    test ax, ax
    J286_E .done
    cmp ax, 1024
    J286_BE .cylinders_ok
    mov ax, 1024
.cylinders_ok:
    mov [HD_CYLINDERS], ax

    mov ax, [6]
    test ax, ax
    J286_E .done
    cmp ax, 16
    J286_BE .heads_ok
    mov ax, 16
.heads_ok:
    mov [HD_HEADS], al

    mov ax, [12]
    test ax, ax
    J286_E .done
    cmp ax, 63
    J286_BE .sectors_ok
    mov ax, 63
.sectors_ok:
    mov [HD_SECTORS_TRACK], al

    ; IDENTIFY words 57-58 are the current CHS-addressable capacity.
    mov ax, [114]
    mov [HD_TOTAL_LOW], ax
    mov ax, [116]
    mov [HD_TOTAL_HIGH], ax
    or ax, [HD_TOTAL_LOW]
    J286_NE .capacity_ok
    ; Fallback to words 60-61 if a controller omits current capacity.
    mov ax, [120]
    mov [HD_TOTAL_LOW], ax
    mov ax, [122]
    mov [HD_TOTAL_HIGH], ax
    or ax, [HD_TOTAL_LOW]
    J286_E .done
.capacity_ok:
    mov byte [HD_PRESENT], 1
    mov ax, BDA_SEG
    mov es, ax
    mov byte [es:BDA_HDD_COUNT], 1
.done:
    pop es
    pop ds
    pop di
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; Return CF clear when the cached ATA master is present.
ata_drive_present:
    push ax
    push ds
    mov ax, EBDA_SEG
    mov ds, ax
    cmp byte [HD_PRESENT], 1
    J286_NE .missing
    clc
    jmp .done
.missing:
    stc
.done:
    pop ds
    pop ax
    ret

; Return AH with the most recent ATA BIOS error code.
ata_get_last_error:
    push ds
    mov ax, EBDA_SEG
    mov ds, ax
    mov ah, [HDW_ERROR]
    pop ds
    ret

; Common CHS transfer for INT 13h AH=02 read, AH=03 write, AH=04 verify.
; The controller is deliberately programmed for one sector per ATA command;
; this matches its synchronous PIO data phase and avoids ambiguous multi-sector
; DRQ handshakes.  All caller registers are preserved; CF reports success.
ata_transfer_chs:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds

    mov si, ax                       ; original function/count
    mov di, bx                       ; current caller buffer offset
    mov ax, EBDA_SEG
    mov ds, ax
    mov byte [HDW_ERROR], 0

    cmp byte [HD_PRESENT], 1
    J286_E .present
    mov byte [HDW_ERROR], 0xAA
    jmp .bad
.present:
    mov ax, si
    test al, al
    J286_NE .count_nonzero
    mov byte [HDW_ERROR], 0x01
    jmp .bad
.count_nonzero:
    cmp al, 127
    J286_BE .count_ok
    mov byte [HDW_ERROR], 0x01
    jmp .bad
.count_ok:
    mov [HDW_REMAINING], al
    mov byte [HDW_COMPLETED], 0
    mov [HDW_MODE], ah
    mov [HDW_BUFFER], di

    ; Decode the 10-bit BIOS cylinder from CH plus CL bits 6-7.
    xor ax, ax
    mov al, ch
    xor bx, bx
    mov bl, cl
    and bl, 0xC0
    shl bx, 2
    or ax, bx
    mov [HDW_CYLINDER], ax
    mov al, dh
    mov [HDW_HEAD], al
    mov al, cl
    and al, 0x3F
    mov [HDW_SECTOR], al

    ; ES:BX must not wrap during a read or write request.
    cmp byte [HDW_MODE], 0x04
    J286_E .validate
    mov ax, si
    and ax, 0x00FF
    shl ax, 9
    add ax, di
    J286_NC .validate
    mov byte [HDW_ERROR], 0x09       ; data boundary error
    jmp .bad

.validate:
    call ata_validate_current
    J286_NC .next_sector
    mov byte [HDW_ERROR], 0x04       ; requested sector not found
    jmp .bad

.next_sector:
    call ata_program_current
    mov dx, 0x1F7
    mov al, 0x20                     ; READ SECTORS
    cmp byte [HDW_MODE], 0x03
    J286_NE .issue
    mov al, 0x30                     ; WRITE SECTORS
.issue:
    out dx, al

    call ata_wait_drq
    J286_NC .data_phase
    mov [HDW_ERROR], al
    jmp .bad

.data_phase:

    cmp byte [HDW_MODE], 0x03
    J286_E .write_data
    cmp byte [HDW_MODE], 0x04
    J286_E .verify_data

    mov di, [HDW_BUFFER]
    mov cx, 256
    mov dx, 0x1F0
    cld
    rep insw
    mov [HDW_BUFFER], di
    jmp .data_done

.verify_data:
    push es
    mov ax, EBDA_SEG
    mov es, ax
    xor di, di
    mov cx, 256
    mov dx, 0x1F0
    cld
    rep insw
    pop es
    jmp .data_done

.write_data:
    mov si, [HDW_BUFFER]
    push ds
    push es
    pop ds                          ; OUTSW reads caller ES:SI through DS
    mov cx, 256
    mov dx, 0x1F0
    cld
    rep outsw
    pop ds                          ; restore EBDA workspace segment
    mov [HDW_BUFFER], si

.data_done:

    call ata_wait_complete
    J286_NC .sector_done
    mov [HDW_ERROR], al
    jmp .bad

.sector_done:

    inc byte [HDW_COMPLETED]
    dec byte [HDW_REMAINING]
    J286_E .good
    call ata_advance_current
    J286_NC .next_sector
    mov byte [HDW_ERROR], 0x04
    jmp .bad

.good:
    clc
    jmp .restore
.bad:
    stc
.restore:
    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; Validate the current translated CHS tuple.  DS must equal EBDA_SEG.
ata_validate_current:
    push ax
    mov ax, [HDW_CYLINDER]
    cmp ax, [HD_CYLINDERS]
    J286_AE .bad
    mov al, [HDW_HEAD]
    cmp al, [HD_HEADS]
    J286_AE .bad
    mov al, [HDW_SECTOR]
    test al, al
    J286_E .bad
    cmp al, [HD_SECTORS_TRACK]
    J286_A .bad
    clc
    jmp .done
.bad:
    stc
.done:
    pop ax
    ret

; Advance one sector using the cached 16-head/63-sector geometry.
ata_advance_current:
    push ax
    inc byte [HDW_SECTOR]
    mov al, [HDW_SECTOR]
    cmp al, [HD_SECTORS_TRACK]
    J286_BE .good
    mov byte [HDW_SECTOR], 1
    inc byte [HDW_HEAD]
    mov al, [HDW_HEAD]
    cmp al, [HD_HEADS]
    J286_B .good
    mov byte [HDW_HEAD], 0
    inc word [HDW_CYLINDER]
    mov ax, [HDW_CYLINDER]
    cmp ax, [HD_CYLINDERS]
    J286_AE .bad
.good:
    clc
    jmp .done
.bad:
    stc
.done:
    pop ax
    ret

; Program one CHS sector into the primary ATA task file.  DS=EBDA_SEG.
ata_program_current:
    push ax
    push dx
    mov dx, 0x1F6
    mov al, [HDW_HEAD]
    and al, 0x0F
    or al, 0xA0                     ; master, CHS mode
    out dx, al
    mov dx, 0x1F2
    mov al, 1
    out dx, al
    inc dx
    mov al, [HDW_SECTOR]
    out dx, al
    inc dx
    mov ax, [HDW_CYLINDER]
    out dx, al                      ; cylinder low
    inc dx
    mov al, ah
    out dx, al                      ; cylinder high
    pop dx
    pop ax
    ret

; Wait for BSY=0, ERR=0, DRQ=1.  AL returns a BIOS status on failure.
ata_wait_drq:
    push cx
    push dx
    mov cx, 0xFFFF
    mov dx, 0x1F7
.poll:
    in al, dx
    test al, 0x80
    J286_NE .again
    test al, 0x01
    J286_NE .controller_error
    test al, 0x08
    J286_NE .ready
.again:
    loop .poll
    mov al, 0x80                    ; timeout
    stc
    jmp .done
.controller_error:
    mov al, 0x20                    ; controller failure
    stc
    jmp .done
.ready:
    xor al, al
    clc
.done:
    pop dx
    pop cx
    ret

; Wait for the PIO data phase to finish.  AL returns BIOS status on failure.
ata_wait_complete:
    push cx
    push dx
    mov cx, 0xFFFF
    mov dx, 0x1F7
.poll:
    in al, dx
    test al, 0x80
    J286_NE .again
    test al, 0x01
    J286_NE .controller_error
    xor al, al
    clc
    jmp .done
.again:
    loop .poll
    mov al, 0x80
    stc
    jmp .done
.controller_error:
    mov al, 0x20
    stc
.done:
    pop dx
    pop cx
    ret

; ---------------------------------------------------------------------------
; INT 14h - PC/AT asynchronous communications services
; ---------------------------------------------------------------------------
; These services drive the guest-visible 16550A registers.  They do not call a
; host serial API.  DX selects the BDA-published COM adapter (0..3).
; Service contract and timeout structure follow the IBM Personal Computer XT
; Technical Reference BIOS listing (March 1986), pp. 5-132 through 5-137:
; https://www.minuszerodegrees.net/manuals/IBM/IBM_5155_5160_Technical_Reference_6280089_MAR86.pdf
int14:
    sti                             ; IBM BIOS services permit timer/device IRQs
    push bx
    push cx
    push dx
    push si
    push di
    push bp
    push ds
    mov si, ax                      ; preserve function/parameter or data byte
    mov bp, dx                      ; BDA COM timeout-table index
    cmp dx, 3
    J286_A .not_installed
    mov ax, BDA_SEG
    mov ds, ax
    mov bx, dx
    shl bx, 1
    mov di, [bx+0x00]
    test di, di
    J286_E .not_installed

    mov ax, si
    cmp ah, 0
    J286_E .initialize
    cmp ah, 1
    J286_E .transmit
    cmp ah, 2
    J286_E .receive
    cmp ah, 3
    J286_E .status
    jmp .not_installed

.initialize:
    mov cl, al
    and cl, 0x1F                    ; UART LCR layout matches BIOS parameter
    mov bl, al
    shr bl, 5
    xor bh, bh
    shl bx, 1
    mov ax, [cs:serial_divisors+bx]
    mov bx, ax
    mov dx, di
    add dx, 3
    mov al, cl
    or al, 0x80                     ; DLAB
    out dx, al
    mov dx, di
    mov al, bl
    out dx, al                      ; DLL
    inc dx
    mov al, bh
    out dx, al                      ; DLM
    mov dx, di
    add dx, 3
    mov al, cl
    out dx, al
    mov dx, di
    inc dx
    xor al, al                      ; BIOS uses polled I/O (IER = 0)
    out dx, al
    mov dx, di
    add dx, 4
    mov al, 3                       ; DTR and RTS asserted after initialization
    out dx, al
    jmp .status

.transmit:
    mov dx, di
    add dx, 4
    mov al, 3                       ; assert DTR and RTS
    out dx, al
    add dx, 2                       ; modem status register
    mov bh, 0x30                    ; require DSR and CTS
    call .wait_status
    J286_NE .tx_timeout
    mov dx, di
    add dx, 5
    mov bh, 0x20                    ; transmitter holding register empty
    call .wait_status
    J286_E .tx_ready
.tx_timeout:
    or ah, 0x80                     ; BIOS timeout indication
    mov bx, si
    mov al, bl
    jmp short .done
.tx_ready:
    mov dx, di
    mov ax, si
    out dx, al
    mov dx, di
    add dx, 5
    in al, dx
    mov ah, al
    mov bx, si
    mov al, bl
    jmp short .done

.receive:
    mov dx, di
    add dx, 4
    mov al, 1                       ; assert DTR
    out dx, al
    add dx, 2                       ; modem status register
    mov bh, 0x20                    ; require DSR
    call .wait_status
    J286_NE .rx_timeout
    mov dx, di
    add dx, 5
    mov bh, 1                       ; data ready
    call .wait_status
    J286_E .rx_ready
.rx_timeout:
    or ah, 0x80
    xor al, al
    jmp short .done
.rx_ready:
    mov dx, di
    in al, dx
    mov bl, al
    add dx, 5
    in al, dx
    mov ah, al
    mov al, bl
    jmp short .done

; IBM-compatible two-level timeout loop. BL is the BDA outer-loop count;
; CX deliberately wraps to 65536 inner polls, matching the published BIOS.
; Entry: DX=status port, BH=required asserted bits, BP=COM index.
; Exit: AH=last status, ZF set only when every requested bit was observed.
.wait_status:
    mov bl, [bp+0x7C]
.wait_outer:
    xor cx, cx
.wait_inner:
    in al, dx
    mov ah, al
    and al, bh
    cmp al, bh
    je short .wait_done
    loop .wait_inner
    dec bl
    jnz short .wait_outer
    or bh, bh                       ; required masks are nonzero: clear ZF
.wait_done:
    ret

.status:
    mov dx, di
    add dx, 5
    in al, dx
    mov ah, al                      ; line status
    inc dx
    in al, dx                       ; modem status
    jmp short .done

.not_installed:
    mov ax, 0x8000
.done:
    pop ds
    pop bp
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    iret

serial_divisors:
    dw 1047, 768, 384, 192, 96, 48, 24, 12

; ---------------------------------------------------------------------------
; INT 17h - PC/AT parallel printer services
; ---------------------------------------------------------------------------
; Register polarity, timeout loops and strobe ordering follow the IBM BIOS
; printer-service listing in that same Technical Reference, pp. 5-138 onward.
int17:
    sti                             ; published IBM service enables interrupts
    push bx
    push cx
    push dx
    push si
    push di
    push bp
    push ds
    mov si, ax
    mov bp, dx                      ; BDA printer timeout-table index
    cmp dx, 3
    J286_A .not_installed
    mov ax, BDA_SEG
    mov ds, ax
    mov bx, dx
    shl bx, 1
    mov di, [bx+0x08]
    test di, di
    jz short .not_installed
    mov ax, si
    cmp ah, 0
    J286_E .print
    cmp ah, 1
    J286_E .initialize
    cmp ah, 2
    J286_E .status
    jmp short .not_installed

.print:
    mov dx, di
    out dx, al                      ; latch data
    inc dx                          ; status register
    mov bl, [bp+0x78]               ; BDA outer-loop timeout count
.print_outer:
    xor cx, cx                      ; 65536 inner polls
.print_wait:
    in al, dx
    mov ah, al
    test al, 0x80                   ; +BUSY set means ready for a character
    jnz short .print_ready
    loop .print_wait
    dec bl
    jnz short .print_outer
    and ah, 0xF8                    ; remove undefined adapter inputs
    xor ah, 0x48                    ; IBM BIOS logical ACK and error polarity
    or ah, 1                        ; timeout
    mov bx, si
    mov al, bl
    jmp short .done
.print_ready:
    mov dx, di
    add dx, 2                       ; control register
    in al, dx
    or al, 1                        ; inverted /STROBE asserted low
    out dx, al
    and al, 0xFE                    ; release /STROBE
    out dx, al
    mov dx, di
    inc dx
    in al, dx
    and al, 0xF8
    xor al, 0x48
    mov ah, al
    mov bx, si
    mov al, bl
    jmp short .done

.initialize:
    mov dx, di
    add dx, 2
    in al, dx
    and al, 0xFB                    ; assert /INIT
    out dx, al
    mov cx, 64
.init_delay:
    loop .init_delay
    or al, 4                        ; release /INIT
    out dx, al
    jmp short .status

.status:
    mov dx, di
    inc dx
    in al, dx
    and al, 0xF8
    xor al, 0x48
    mov ah, al
    mov bx, si
    mov al, bl
    jmp short .done

.not_installed:
    mov ax, 0x0100                  ; timeout / absent
.done:
    pop ds
    pop bp
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    iret

; CROMWELL KEYBOARD REALITY BRICK 2 INT16 BEGIN
int16:
    push bp
    mov bp, sp

    cmp ah, 0x00
    J286_E .read_classic
    cmp ah, 0x01
    J286_E .status_classic
    cmp ah, 0x02
    J286_E .shift_status
    cmp ah, 0x03
    J286_E .set_typematic
    cmp ah, 0x05
    J286_E .store_key
    cmp ah, 0x09
    J286_E .functionality
    cmp ah, 0x0A
    J286_E .keyboard_id
    cmp ah, 0x10
    J286_E .read_extended
    cmp ah, 0x11
    J286_E .status_extended
    cmp ah, 0x12
    J286_E .extended_shift_status
    pop bp
    iret

.read_classic:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
.read_classic_loop:
    call kbd_dequeue_ax
    jnc short .classic_have
    mov ax, 0x9002                  ; INT 15 device-busy, keyboard
    int 0x15
    sti
    hlt
    cli
    jmp short .read_classic_loop
.classic_have:
    call kbd_classic_filter
    jc short .read_classic_loop
    pop ds
    pop bx
    pop bp
    iret

.status_classic:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
.status_classic_loop:
    call kbd_peek_ax
    jc short .status_classic_none
    call kbd_classic_filter
    jnc short .status_classic_yes
    call kbd_drop_head
    jmp short .status_classic_loop
.status_classic_yes:
    and word [ss:bp+6], 0xFFBF
    pop ds
    pop bx
    pop bp
    iret
.status_classic_none:
    or word [ss:bp+6], 0x0040
    pop ds
    pop bx
    pop bp
    iret

.shift_status:
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov al, [BDA_KBD_FLAGS1]
    xor ah, ah
    pop ds
    pop bp
    iret

.set_typematic:
    ; Enhanced AT interface supports subfunction AL=05h: BH delay 0..3,
    ; BL rate 0..31.  Other subfunctions are deliberately unsupported.
    cmp al, 0x05
    jne short .typematic_done
    push bx
    push ds
    and bh, 3
    and bl, 0x1F
    mov al, bh
    shl al, 5
    or al, bl
    mov bx, BDA_SEG
    mov ds, bx
    call kbd_set_typematic
    pop ds
    pop bx
.typematic_done:
    pop bp
    iret

.store_key:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
    mov ah, ch
    mov al, cl
    call kbd_enqueue_ax
    mov al, 0
    jnc short .store_done
    mov al, 1
.store_done:
    pop ds
    pop bx
    pop bp
    iret

.functionality:
    mov al, 0x30                    ; AH=10-12 and AH=0A supported
    pop bp
    iret

.keyboard_id:
    push ax
    push cx
    push dx
    push ds
    mov dx, BDA_SEG
    mov ds, dx
    mov al, 0xF2
    call kbd_runtime_send
    jc short .id_failed
    call kbc_wait_obf_short
    jc short .id_failed
    in al, 0x60
    mov bl, al                     ; first byte ABh
    call kbc_wait_obf_short
    jc short .id_failed
    in al, 0x60
    mov bh, al                     ; second byte 83h
    jmp short .id_done
.id_failed:
    xor bx, bx
.id_done:
    pop ds
    pop dx
    pop cx
    pop ax
    pop bp
    iret

.read_extended:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
.read_extended_loop:
    call kbd_dequeue_ax
    jnc short .extended_have
    mov ax, 0x9002
    int 0x15
    sti
    hlt
    cli
    jmp short .read_extended_loop
.extended_have:
    call kbd_extended_filter
    pop ds
    pop bx
    pop bp
    iret

.status_extended:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
    call kbd_peek_ax
    jc short .status_extended_none
    call kbd_extended_filter
    and word [ss:bp+6], 0xFFBF
    pop ds
    pop bx
    pop bp
    iret
.status_extended_none:
    or word [ss:bp+6], 0x0040
    pop ds
    pop bx
    pop bp
    iret

.extended_shift_status:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
    mov al, [BDA_KBD_FLAGS1]
    mov ah, [BDA_KBD_FLAGS2]
    ; Shift-flags-2 layout differs from BDA byte 18h only for SysReq:
    ; BDA bit 2 becomes returned AH bit 7.
    mov bl, ah
    and ah, 0x73                   ; caps/num/scroll + left Alt/Ctrl
    test bl, 0x04
    jz short .no_sysreq
    or ah, 0x80
.no_sysreq:
    mov bl, [BDA_KBD_STATUS3]
    and bl, 0x0C                   ; right Alt / right Ctrl
    or ah, bl
    pop ds
    pop bx
    pop bp
    iret
; CROMWELL KEYBOARD REALITY BRICK 2 INT16 END

int1a:
    push bp
    mov bp, sp
    cmp ah, 0x00
    J286_NE .not00
    jmp .get_ticks
.not00:
    cmp ah, 0x01
    J286_NE .not01
    jmp .set_ticks
.not01:
    cmp ah, 0x02
    J286_NE .not02
    jmp .read_time
.not02:
    cmp ah, 0x03
    J286_NE .not03
    jmp .set_time
.not03:
    cmp ah, 0x04
    J286_NE .not04
    jmp .read_date
.not04:
    cmp ah, 0x05
    J286_NE .not05
    jmp .set_date
.not05:
    cmp ah, 0x06
    J286_NE .not06
    jmp .set_alarm
.not06:
    cmp ah, 0x07
    J286_NE .unsupported
    jmp .reset_alarm

.get_ticks:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
    mov dx, [0x6C]
    mov cx, [0x6E]
    mov al, [0x70]
    mov byte [0x70], 0
    pop ds
    pop bx
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.set_ticks:
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
    mov [0x6C], dx
    mov [0x6E], cx
    mov byte [0x70], 0
    pop ds
    pop bx
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.read_time:
    call cmos_wait_update
    mov al, 0x0D
    call cmos_read
    test al, 0x80
    J286_E .rtc_error
    mov al, 0x04
    call cmos_read
    mov ch, al
    mov al, 0x02
    call cmos_read
    mov cl, al
    mov al, 0x00
    call cmos_read
    mov dh, al
    mov al, 0x0B
    call cmos_read
    and al, 1
    mov dl, al
    xor ah, ah
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.set_time:
    push bx
    mov al, 0x0B
    call cmos_read
    mov bl, al
    or al, 0x80
    mov ah, al
    mov al, 0x0B
    call cmos_write
    mov al, 0x00
    mov ah, dh
    call cmos_write
    mov al, 0x02
    mov ah, cl
    call cmos_write
    mov al, 0x04
    mov ah, ch
    call cmos_write
    and bl, 0x7E
    and dl, 1
    or bl, dl
    mov al, 0x0B
    mov ah, bl
    call cmos_write
    pop bx
    xor ah, ah
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.read_date:
    call cmos_wait_update
    mov al, 0x0D
    call cmos_read
    test al, 0x80
    J286_E .rtc_error
    mov al, 0x32
    call cmos_read
    mov ch, al
    mov al, 0x09
    call cmos_read
    mov cl, al
    mov al, 0x08
    call cmos_read
    mov dh, al
    mov al, 0x07
    call cmos_read
    mov dl, al
    xor ah, ah
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.set_date:
    push bx
    mov al, 0x0B
    call cmos_read
    mov bl, al
    or al, 0x80
    mov ah, al
    mov al, 0x0B
    call cmos_write
    mov al, 0x07
    mov ah, dl
    call cmos_write
    mov al, 0x08
    mov ah, dh
    call cmos_write
    mov al, 0x09
    mov ah, cl
    call cmos_write
    mov al, 0x32
    mov ah, ch
    call cmos_write
    and bl, 0x7F
    mov al, 0x0B
    mov ah, bl
    call cmos_write
    pop bx
    xor ah, ah
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.set_alarm:
    push bx
    mov al, 0x01
    mov ah, dh
    call cmos_write
    mov al, 0x03
    mov ah, cl
    call cmos_write
    mov al, 0x05
    mov ah, ch
    call cmos_write
    mov al, 0x0B
    call cmos_read
    or al, 0x20
    mov ah, al
    mov al, 0x0B
    call cmos_write
    pop bx
    xor ah, ah
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.reset_alarm:
    mov al, 0x0B
    call cmos_read
    and al, 0xDF
    mov ah, al
    mov al, 0x0B
    call cmos_write
    xor ah, ah
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

.rtc_error:
    mov ah, 0x80
.unsupported:
    or word [ss:bp+6], 1
    pop bp
    iret

; ---------------------------------------------------------------------------
; Cromwell AT CMOS Setup
; ---------------------------------------------------------------------------
; The setup program lives in the system ROM, as it did on late AT-compatible
; motherboards. CMOS byte 20h is OEM configuration RAM covered by the normal
; 10h-20h checksum: bit 0 is boot Num Lock and bit 1 selects HDD-first boot.

setup_entry_window:
    push ax
    push bx
    push cx
    push dx
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov bx, [0x006C]
    xor dx, dx                      ; DL tracks an E0 prefix for documentation

    ; During early POST, firmware owns the controller output buffer.  Mask IRQ1
    ; so the interrupt handler cannot race this polling loop for port 60h;
    ; leave IRQ0 unmasked so the 18.2 Hz deadline remains genuine machine time.
    in al, 0x21
    mov cl, al
    or al, 0x02
    out 0x21, al
.wait:
    in al, 0x64
    test al, 0x01                   ; 8042 output buffer full?
    jz short .tick
    in al, 0x60                     ; translated Set-1 keyboard byte
    cmp al, 0xE0
    jne short .not_prefix
    mov dl, 1
    jmp short .wait
.not_prefix:
    cmp al, 0x53                    ; dedicated Delete or keypad Delete make
    je short .requested
    xor dl, dl
.tick:
    sti
    hlt
    mov ax, [0x006C]
    sub ax, bx
    cmp ax, 36                      ; about 1.98 seconds at 18.2065 Hz
    jb short .wait
    stc
    jmp short .done
.requested:
    clc
.done:
    pushf
    mov al, cl
    out 0x21, al                    ; restore the runtime PIC mask exactly
    popf
    pop ds
    pop dx
    pop cx
    pop bx
    pop ax
    ret

cmos_setup:
    push ax
    push bx
    push cx
    push dx
    push si
    push ds
    push es
    mov ax, EBDA_SEG
    mov es, ax
    mov al, CMOS_SETUP_FLAGS
    call cmos_read
    and al, SETUP_NUMLOCK_ON | SETUP_HDD_FIRST
    mov [es:SETUP_FLAGS_WORK], al
    call cmos_wait_update
    mov al, 0x32
    call cmos_read
    mov [es:SETUP_RTC_CENTURY], al
    mov al, 0x09
    call cmos_read
    mov [es:SETUP_RTC_YEAR], al
    mov al, 0x08
    call cmos_read
    mov [es:SETUP_RTC_MONTH], al
    mov al, 0x07
    call cmos_read
    mov [es:SETUP_RTC_DAY], al
    mov al, 0x04
    call cmos_read
    mov [es:SETUP_RTC_HOUR], al
    mov al, 0x02
    call cmos_read
    mov [es:SETUP_RTC_MINUTE], al
    mov al, 0x00
    call cmos_read
    mov [es:SETUP_RTC_SECOND], al
.redraw:
    mov ax, 0x0003
    int 0x10
    push cs
    pop ds
    call setup_draw_chrome
    mov dx, 0x0303                 ; main pane, row 3 column 3
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_hardware_heading
    call print
    mov si, setup_cpu_line
    call print
    call print_memory_summary
    mov si, setup_video_line
    call print
    mov si, setup_keyboard_line
    call print
    mov si, setup_serial_line
    call print
    mov si, setup_parallel_line
    call print
    call setup_print_floppies
    call setup_print_hard_disk
    mov si, setup_preferences_heading
    call print
    mov si, setup_numlock_prefix
    call print
    test byte [es:SETUP_FLAGS_WORK], SETUP_NUMLOCK_ON
    jz short .numlock_off_text
    mov si, setup_on
    jmp short .numlock_text_ready
.numlock_off_text:
    mov si, setup_off
.numlock_text_ready:
    call print
    mov si, setup_boot_prefix
    call print
    test byte [es:SETUP_FLAGS_WORK], SETUP_HDD_FIRST
    jz short .floppy_first_text
    mov si, setup_hdd_first_text
    jmp short .boot_text_ready
.floppy_first_text:
    mov si, setup_floppy_first_text
.boot_text_ready:
    call print
    mov si, setup_rtc_prefix
    call print
    call setup_print_rtc
.key:
    xor ah, ah
    int 0x16
    cmp ah, 0x44                    ; F10
    je short .save
    cmp ah, 0x01                    ; Escape
    je short .cancel
    and al, 0xDF
    cmp al, 'N'
    je short .toggle_numlock
    cmp al, 'B'
    je short .toggle_boot
    cmp al, 'D'
    je short .defaults
    cmp al, 'A'
    je short .edit_date
    cmp al, 'T'
    je short .edit_time
    jmp short .key
.toggle_numlock:
    xor byte [es:SETUP_FLAGS_WORK], SETUP_NUMLOCK_ON
    jmp .redraw
.toggle_boot:
    xor byte [es:SETUP_FLAGS_WORK], SETUP_HDD_FIRST
    jmp .redraw
.defaults:
    mov byte [es:SETUP_FLAGS_WORK], SETUP_NUMLOCK_ON
    jmp .redraw
.edit_date:
    call setup_edit_date
    jmp .redraw
.edit_time:
    call setup_edit_time
    jmp .redraw
.save:
    call setup_commit_rtc
    mov ah, [es:SETUP_FLAGS_WORK]
    mov al, CMOS_SETUP_FLAGS
    call cmos_write
    call cmos_write_checksum
.cancel:
    mov ax, 0x0003
    int 0x10
    pop es
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; Paint the Phalanx Setup shell entirely through VGA text memory semantics.
; The screen is still ordinary mode 03h: the blue bars, gray work area and
; help pane are cell attributes, not a host-side graphical overlay.
setup_draw_chrome:
    push ax
    push bx
    push cx
    push dx
    push di
    push es
    push si

    ; Blue outer field and header/footer bars.
    mov ax, 0x0600
    mov bh, 0x1F                    ; bright white on blue
    xor cx, cx
    mov dx, 0x184F
    int 0x10

    ; Gray main work surface, rows 3 through 22.
    mov ax, 0x0600
    mov bh, 0x70                    ; black on light gray
    mov cx, 0x0301
    mov dx, 0x164E
    int 0x10

    ; Blue help pane at the right, leaving a visible divider.
    mov ax, 0x0600
    mov bh, 0x1F
    mov cx, 0x033B                  ; row 3, column 59
    mov dx, 0x164E
    int 0x10

    mov dx, 0x001A
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_title
    call print

    mov dx, 0x0103
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_tabs
    call print

    mov dx, 0x043D
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_help_pane
    call print

    mov dx, 0x1801
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, bios_revision
    call print

    pop si
    pop es
    pop di
    pop dx
    pop cx
    pop bx
    pop ax
    ret

setup_print_floppies:
    push ax
    push bx
    push si
    mov al, 0x10
    call cmos_read
    mov bl, al
    mov si, setup_floppy_a_prefix
    call print
    mov al, bl
    shr al, 4
    call setup_print_floppy_type
    mov si, setup_floppy_b_prefix
    call print
    mov al, bl
    and al, 0x0F
    call setup_print_floppy_type
    pop si
    pop bx
    pop ax
    ret

setup_print_floppy_type:
    push si
    cmp al, 1
    je short .f360
    cmp al, 2
    je short .f12
    cmp al, 3
    je short .f720
    cmp al, 4
    je short .f144
    cmp al, 5
    je short .f288
    mov si, setup_none
    jmp short .emit
.f360: mov si, setup_f360
    jmp short .emit
.f12: mov si, setup_f12
    jmp short .emit
.f720: mov si, setup_f720
    jmp short .emit
.f144: mov si, setup_f144
    jmp short .emit
.f288: mov si, setup_f288
.emit:
    call print
    pop si
    ret

setup_print_hard_disk:
    push ax
    push bx
    push cx
    push dx
    push si
    push ds
    mov ax, EBDA_SEG
    mov ds, ax
    cmp byte [HD_PRESENT], 1
    jne short .missing
    push cs
    pop ds
    mov si, setup_hdd_prefix
    call print
    call setup_print_ata_model
    push cs
    pop ds
    mov si, setup_hdd_geometry_prefix
    call print
    mov ax, EBDA_SEG
    mov ds, ax
    mov ax, [HD_CYLINDERS]
    call print_u16_decimal
    push cs
    pop ds
    mov si, setup_x
    call print
    mov ax, EBDA_SEG
    mov ds, ax
    xor ax, ax
    mov al, [HD_HEADS]
    call print_u16_decimal
    push cs
    pop ds
    mov si, setup_x
    call print
    mov ax, EBDA_SEG
    mov ds, ax
    xor ax, ax
    mov al, [HD_SECTORS_TRACK]
    call print_u16_decimal
    push cs
    pop ds
    mov si, setup_newline
    call print
    jmp short .done
.missing:
    push cs
    pop ds
    mov si, setup_hdd_none
    call print
.done:
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; IDENTIFY strings store the two bytes in every ATA word swapped.
setup_print_ata_model:
    push ax
    push bx
    push cx
    push dx
    push si
    push ds
    mov ax, EBDA_SEG
    mov ds, ax
    mov si, 54                      ; IDENTIFY word 27
    mov cx, 20
.word:
    lodsw
    xchg al, ah
    push ax
    mov ah, 0x0E
    xor bh, bh
    int 0x10
    pop ax
    mov al, ah
    mov ah, 0x0E
    xor bh, bh
    int 0x10
    loop .word
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

setup_print_rtc:
    push ax
    push bx
    mov al, [es:SETUP_RTC_CENTURY]
    call setup_print_bcd
    mov al, [es:SETUP_RTC_YEAR]
    call setup_print_bcd
    mov al, '-'
    call setup_teletype
    mov al, [es:SETUP_RTC_MONTH]
    call setup_print_bcd
    mov al, '-'
    call setup_teletype
    mov al, [es:SETUP_RTC_DAY]
    call setup_print_bcd
    mov al, ' '
    call setup_teletype
    mov al, [es:SETUP_RTC_HOUR]
    call setup_print_bcd
    mov al, ':'
    call setup_teletype
    mov al, [es:SETUP_RTC_MINUTE]
    call setup_print_bcd
    mov al, ':'
    call setup_teletype
    mov al, [es:SETUP_RTC_SECOND]
    call setup_print_bcd
    mov al, 13
    call setup_teletype
    mov al, 10
    call setup_teletype
    pop bx
    pop ax
    ret

setup_edit_date:
    push ax
    push bx
    push si
    mov ax, 0x0003
    int 0x10
    push cs
    pop ds
    call setup_draw_chrome
    mov dx, 0x0503
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_date_prompt
    call print
.century:
    call setup_read_two_digits
    jc short .done
    mov bl, al
    call setup_bcd_to_binary
    cmp al, 1
    jb short .century
    mov [es:SETUP_EDIT_0], bl
    call setup_read_two_digits
    jc short .done
    mov [es:SETUP_EDIT_1], al
    mov al, '-'
    call setup_teletype
.month:
    call setup_read_two_digits
    jc short .done
    mov bl, al
    call setup_bcd_to_binary
    cmp al, 1
    jb short .month
    cmp al, 12
    ja short .month
    mov [es:SETUP_EDIT_2], bl
    mov al, '-'
    call setup_teletype
.day:
    call setup_read_two_digits
    jc short .done
    mov bl, al
    call setup_bcd_to_binary
    cmp al, 1
    jb short .day
    cmp al, 31
    ja short .day
    mov [es:SETUP_EDIT_3], bl
    mov al, [es:SETUP_EDIT_0]
    mov [es:SETUP_RTC_CENTURY], al
    mov al, [es:SETUP_EDIT_1]
    mov [es:SETUP_RTC_YEAR], al
    mov al, [es:SETUP_EDIT_2]
    mov [es:SETUP_RTC_MONTH], al
    mov al, [es:SETUP_EDIT_3]
    mov [es:SETUP_RTC_DAY], al
.done:
    pop si
    pop bx
    pop ax
    ret

setup_edit_time:
    push ax
    push bx
    push si
    mov ax, 0x0003
    int 0x10
    push cs
    pop ds
    call setup_draw_chrome
    mov dx, 0x0503
    mov ah, 0x02
    xor bh, bh
    int 0x10
    mov si, setup_time_prompt
    call print
.hour:
    call setup_read_two_digits
    jc short .done
    mov bl, al
    call setup_bcd_to_binary
    cmp al, 23
    ja short .hour
    mov [es:SETUP_EDIT_0], bl
    mov al, ':'
    call setup_teletype
.minute:
    call setup_read_two_digits
    jc short .done
    mov bl, al
    call setup_bcd_to_binary
    cmp al, 59
    ja short .minute
    mov [es:SETUP_EDIT_1], bl
    mov al, ':'
    call setup_teletype
.second:
    call setup_read_two_digits
    jc short .done
    mov bl, al
    call setup_bcd_to_binary
    cmp al, 59
    ja short .second
    mov [es:SETUP_EDIT_2], bl
    mov al, [es:SETUP_EDIT_0]
    mov [es:SETUP_RTC_HOUR], al
    mov al, [es:SETUP_EDIT_1]
    mov [es:SETUP_RTC_MINUTE], al
    mov al, [es:SETUP_EDIT_2]
    mov [es:SETUP_RTC_SECOND], al
.done:
    pop si
    pop bx
    pop ax
    ret

; Read exactly two decimal digits and return packed BCD in AL. Escape sets CF.
setup_read_two_digits:
    push bx
.first:
    xor ah, ah
    int 0x16
    cmp ah, 0x01
    je short .cancel
    cmp al, '0'
    jb short .first
    cmp al, '9'
    ja short .first
    call setup_teletype
    sub al, '0'
    mov bl, al
.second:
    xor ah, ah
    int 0x16
    cmp ah, 0x01
    je short .cancel
    cmp al, '0'
    jb short .second
    cmp al, '9'
    ja short .second
    call setup_teletype
    sub al, '0'
    mov ah, bl
    shl ah, 4
    or al, ah
    clc
    pop bx
    ret
.cancel:
    stc
    pop bx
    ret

setup_bcd_to_binary:
    push bx
    mov bl, al
    and bl, 0x0F
    shr al, 4
    mov bh, 10
    mul bh
    add al, bl
    pop bx
    ret

setup_commit_rtc:
    push ax
    push bx
    mov al, 0x0B
    call cmos_read
    mov bl, al
    or al, 0x80                    ; SET freezes the update divider while editing
    mov ah, al
    mov al, 0x0B
    call cmos_write
    mov ah, [es:SETUP_RTC_CENTURY]
    mov al, 0x32
    call cmos_write
    mov ah, [es:SETUP_RTC_YEAR]
    mov al, 0x09
    call cmos_write
    mov ah, [es:SETUP_RTC_MONTH]
    mov al, 0x08
    call cmos_write
    mov ah, [es:SETUP_RTC_DAY]
    mov al, 0x07
    call cmos_write
    mov ah, [es:SETUP_RTC_HOUR]
    mov al, 0x04
    call cmos_write
    mov ah, [es:SETUP_RTC_MINUTE]
    mov al, 0x02
    call cmos_write
    mov ah, [es:SETUP_RTC_SECOND]
    mov al, 0x00
    call cmos_write
    mov ah, bl
    and ah, 0x7F
    mov al, 0x0B
    call cmos_write
    pop bx
    pop ax
    ret

setup_print_bcd:
    push ax
    mov bl, al
    shr al, 4
    and al, 0x0F
    add al, '0'
    call setup_teletype
    mov al, bl
    and al, 0x0F
    add al, '0'
    call setup_teletype
    pop ax
    ret

setup_teletype:
    push ax
    push bx
    mov ah, 0x0E
    xor bh, bh
    int 0x10
    pop bx
    pop ax
    ret

int19:
    cli
    mov al, CMOS_SETUP_FLAGS
    call cmos_read
    test al, SETUP_HDD_FIRST
    jnz short .hard_first
    call int19_try_floppy
    jnc short .floppy_go
    call int19_try_harddisk
    jnc short .hard_go
    jmp short .failed
.hard_first:
    call int19_try_harddisk
    jnc short .hard_go
    call int19_try_floppy
    jnc short .floppy_go
    jmp short .failed
.floppy_go:
    xor dl, dl
    jmp short .go
.hard_go:
    mov dl, 0x80
.go:
    call clear_screen
    xor ax, ax
    mov ds, ax
    mov ss, ax
    mov sp, 0x7C00
    sti
    jmp 0x0000:0x7C00
.failed:
    push cs
    pop ds
    mov si, boot_fail
    call print
    sti
.wait: hlt
    jmp .wait

int19_clear_buffer:
    xor ax, ax
    mov es, ax
    mov di, 0x7C00
    mov cx, 256
    cld
    rep stosw
    ret

int19_try_floppy:
    call int19_clear_buffer
    mov bx, 0x7C00
    mov ax, 0x0201
    xor cx, cx
    mov cl, 1
    xor dx, dx
    int 0x13
    jc short .bad
    cmp word [es:0x7DFE], 0xAA55
    jne short .bad
    clc
    ret
.bad:
    stc
    ret

int19_try_harddisk:
    call int19_clear_buffer
    mov bx, 0x7C00
    mov ax, 0x0201
    mov cx, 1
    xor dh, dh
    mov dl, 0x80
    int 0x13
    jc short .bad
    cmp word [es:0x7DFE], 0xAA55
    jne short .bad
    clc
    ret
.bad:
    stc
    ret

floppy_dpt:
    db 0xDF,0x02,0x25,0x02,18,0x1B,0xFF,0x6C,0xF6,15,8

dummy db 0
setup_prompt_top db 'Press DELETE to enter Setup',0
bios_revision db 'Cromwell Technologies - Phalanx v.5.3.0',0
keyboard_post_ok db 'Keyboard: 8042 / enhanced keyboard ........ OK',0
keyboard_post_error db 'Keyboard error or no keyboard present',0
logo db 10,'                 CROMWELL TECHNOLOGIES',13,10
     db '                      PHALANX BIOS',13,10,13,10
     db '  +-----------------------------------------------------------------------+',13,10
     db '  | System:     Virtual Computer Modular AT                               |',13,10
     db '  | Processor:  Harris CS80C286-25 with Intel 80287                       |',13,10
     db '  | Chipset:    Chips & Technologies CS8221 NEAT                          |',13,10
     db '  | Video:      Diamond Stealth Pro ISA / S3 86C928                       |',13,10
     db '  | I/O:        COM1 3F8/4  COM2 2F8/3  LPT1 378/7  LPT2 278/5           |',13,10
     db '  +-----------------------------------------------------------------------+',13,10,13,10,0
post_text db 'Memory test: ',0
memory_line_prefix db '',0
memory_line_mid db ' KB total / ',0
memory_line_suffix db ' KB extended ... OK',13,10,0
post_text_after_memory db 'CMOS/RTC, PIC, PIT, DMA and keyboard controller ... OK',13,10
          db 'Detecting boot devices...',13,10,0
boot_fail db '  No bootable disk was found.',10,10
          db '  Mount a bootable floppy or hard-disk image,',10
          db '  then reset the machine.',10,0

setup_title db 'PHALANX BIOS SETUP UTILITY',0
setup_tabs db 'Main     Advanced     Boot     Exit',0
setup_hardware_heading db 'SYSTEM INFORMATION',10,10,0
setup_cpu_line db '  CPU:       Harris CS80C286-25 with Intel 80287',10,0
setup_video_line db '  Video:     Diamond Stealth Pro ISA / S3 86C928',10,0
setup_keyboard_line db '  Keyboard:  101/102-key enhanced AT keyboard',10,0
setup_serial_line db '  Serial:    COM1 03F8/IRQ4, COM2 02F8/IRQ3 (16550A)',10,0
setup_parallel_line db '  Parallel:  LPT1 0378/IRQ7, LPT2 0278/IRQ5 (SPP)',10,0
setup_floppy_a_prefix db '  Floppy A:  ',0
setup_floppy_b_prefix db '  Floppy B:  ',0
setup_f360 db '360 KB 5.25-inch',10,0
setup_f12 db '1.2 MB 5.25-inch',10,0
setup_f720 db '720 KB 3.5-inch',10,0
setup_f144 db '1.44 MB 3.5-inch',10,0
setup_f288 db '2.88 MB 3.5-inch',10,0
setup_none db 'Not installed',10,0
setup_hdd_prefix db '  Primary master: ',0
setup_hdd_geometry_prefix db 10,'  BIOS geometry:  ',0
setup_hdd_none db '  Primary master: Not installed',10,0
setup_x db ' x ',0
setup_newline db 10,0
setup_preferences_heading db 10,'CMOS preferences',10,'----------------',10,0
setup_numlock_prefix db '  [N] Boot Num Lock: ',0
setup_boot_prefix db '  [B] Boot order:    ',0
setup_on db 'On',10,0
setup_off db 'Off',10,0
setup_hdd_first_text db 'Hard disk, then floppy',10,0
setup_floppy_first_text db 'Floppy, then hard disk',10,0
setup_rtc_prefix db '  RTC: ',0
setup_date_prompt db 'Enter date as YYYY-MM-DD (Esc cancels): ',0
setup_time_prompt db 'Enter time as HH:MM:SS (Esc cancels): ',0
setup_help_pane db 'PHALANX SETUP',10
           db '                                                             System hardware',10
           db '                                                             detected by POST.',10,10
           db '                                                             N  Boot Num Lock',10
           db '                                                             B  Boot order',10
           db '                                                             A  Set date',10
           db '                                                             T  Set time',10
           db '                                                             D  Load defaults',10,10
           db '                                                             F10 Save and exit',10
           db '                                                             ESC Exit',0

times 0xFFF0-($-$$) db 0xFF
reset_vector: jmp 0xF000:start
times 0x10000-($-$$) db 0xFF
