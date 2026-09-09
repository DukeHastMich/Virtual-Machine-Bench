; Diamond Stealth Pro ISA revision C5 clean-room VGA option ROM
; S3 86C928, 2 MiB 60 ns VRAM, Brooktree Bt485, 16-bit ISA profile
;
; 32 KiB option ROM mapped at C0000h.  The system BIOS validates the 55AAh
; header/checksum, calls entry point +3, and the card installs INT 10h.
;
; This ROM deliberately exposes standard VGA services and leaves extended
; SVGA mode selection to S3/Diamond drivers, just as period software could
; program the 86C928 enhanced register interface directly.

bits 16
org 0
cpu 286

%define BDA_SEG   0x0040
%define TEXT_SEG  0xB800
%define VGA_SEG   0xA000

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
%macro J286_B 1
    jae short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_AE 1
    jb short %%skip
    jmp %1
%%skip:
%endmacro
%macro J286_BE 1
    ja short %%skip
    jmp %1
%%skip:
%endmacro

; Standard expansion-ROM header.  64 x 512-byte blocks = 32 KiB.
db 0x55, 0xAA, 64
jmp rom_init

db 'DIAMOND STEALTH PRO ISA REV C5 - S3 86C928 - 2MB VRAM - BT485',0

rom_init:
    push ax
    push bx
    push dx
    push ds
    push es

    ; Power-on defaults leave the ISA video subsystem asleep with normal
    ; address decode disabled.  PD8 is strapped high in this profile, so bit 4
    ; of 46E8h selects setup mode.  In setup, 0102h bit0 wakes the subsystem;
    ; returning bit4 low with bit3 high enters normal decoded operation.
    mov dx, 0x46E8
    mov al, 0x10
    out dx, al
    mov dx, 0x0102
    mov al, 1
    out dx, al
    mov dx, 0x46E8
    mov al, 0x08
    out dx, al

    ; Install our option-ROM video service vector through the guest IVT.
    xor ax, ax
    mov ds, ax
    mov word [0x10*4], int10
    mov word [0x10*4+2], cs

    ; A VGA/EGA-class adapter is encoded as display type 00b in equipment-word
    ; bits 4-5.  The system BIOS initially advertises CGA 80x25 until an option
    ; ROM claims the display.  Clear only those two display bits here, leaving
    ; floppy/FPU/other equipment flags untouched.
    mov ax, BDA_SEG
    mov es, ax
    and word [es:0x10], 0xFFCF

    ; Unlock documented S3 extended VGA/system registers and advertise/use the
    ; 16-bit VGA memory data path.  Enhanced drawing registers remain available
    ; to software, while the display starts in ordinary VGA compatibility mode.
    mov dx, 0x3D4
    mov al, 0x38
    out dx, al
    inc dx
    mov al, 0x48
    out dx, al
    dec dx
    mov al, 0x39
    out dx, al
    inc dx
    mov al, 0xA5
    out dx, al
    dec dx
    mov al, 0x31
    out dx, al
    inc dx
    in al, dx
    or al, 0x04
    out dx, al
    dec dx
    mov al, 0x40
    out dx, al
    inc dx
    mov al, 0x01
    out dx, al

    ; Power-on display personality: VGA 80x25 color text.
    xor ax, ax
    xor bx, bx
    mov al, 3
    call set_mode_internal
    call vga_install_save_pointer

    pop es
    pop ds
    pop dx
    pop bx
    pop ax
    retf

; ---------------------------------------------------------------------------
; INT 10h VGA BIOS dispatcher
; ---------------------------------------------------------------------------
int10:
    cmp ah, 0x00
    J286_E video_set_mode
    cmp ah, 0x01
    J286_E video_set_shape
    cmp ah, 0x02
    J286_E video_set_cursor
    cmp ah, 0x03
    J286_E video_get_cursor
    cmp ah, 0x05
    J286_E video_set_page
    cmp ah, 0x06
    J286_E video_scroll_up
    cmp ah, 0x07
    J286_E video_scroll_down
    cmp ah, 0x08
    J286_E video_read_cell
    cmp ah, 0x09
    J286_E video_write_cell
    cmp ah, 0x0A
    J286_E video_write_char
    cmp ah, 0x0C
    J286_E video_write_pixel
    cmp ah, 0x0D
    J286_E video_read_pixel
    cmp ah, 0x0E
    J286_E video_tty
    cmp ah, 0x0F
    J286_E video_get_mode
    cmp ah, 0x10
    J286_E video_palette
    cmp ah, 0x11
    J286_E pcb1v_video_font
    cmp ah, 0x12
    J286_E pcb1v_video_alt_functions
    cmp ah, 0x1A
    J286_E video_display_query
    cmp ah, 0x1B
    J286_E pcb1v_video_state_info
    cmp ah, 0x1C
    J286_E pcb1v_video_save_restore
    iret

; ---------------------------------------------------------------------------
; Mode programming
; ---------------------------------------------------------------------------
video_set_mode:
    push ax
    push bx
    mov bl, al
    and al, 0x7F
    call set_mode_internal
    pop bx
    pop ax
    iret

; AL = requested mode (bit7 already removed). BL may carry original AL when
; called by INT 10h; ROM init calls with BL unspecified, therefore mode clear is
; selected unless bit7 of BL is explicitly set and BL's low 7 bits equal AL.
set_mode_internal:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds
    push es

    mov dl, al
    push cs
    pop ds
    cmp dl, 0x00
    J286_E .mode01
    cmp dl, 0x01
    J286_E .mode01
    cmp dl, 0x03
    J286_E .mode3
    cmp dl, 0x02
    J286_E .mode3
    cmp dl, 0x04
    J286_E .mode4
    cmp dl, 0x05
    J286_E .mode5
    cmp dl, 0x06
    J286_E .mode6
    cmp dl, 0x07
    J286_E .mode7
    cmp dl, 0x0D
    J286_E .mode0d
    cmp dl, 0x0E
    J286_E .mode0e
    cmp dl, 0x0F
    J286_E .mode0f
    cmp dl, 0x10
    J286_E .mode10
    cmp dl, 0x11
    J286_E .mode11
    cmp dl, 0x12
    J286_E .mode12
    cmp dl, 0x13
    J286_E .mode13
    ; Modes 08h-0Ch are reserved by the IBM VGA BIOS interface.  A real VGA
    ; BIOS leaves the active mode alone when one of those values is requested.
    jmp .done

.mode01:
    mov si, mode01_table
    call program_vga_table
    call vga_load_default_font
    call vga_maybe_load_default_palette
    call set_bda_text
    test bl, 0x80
    jnz .text_cursor
    mov ax, TEXT_SEG
    mov es, ax
    xor di, di
    mov ax, 0x0720
    mov cx, 16384
    cld
    rep stosw
    jmp .text_cursor

.mode3:
    mov si, mode03_table
    call program_vga_table
    call vga_load_default_font
    call vga_maybe_load_default_palette
    call set_bda_text
    test bl, 0x80
    jnz .text_cursor
    mov ax, TEXT_SEG
    mov es, ax
    xor di, di
    mov ax, 0x0720
    mov cx, 16384
    cld
    rep stosw
.text_cursor:
    mov ch, 0x0D
    mov cl, 0x0E
    call set_cursor_shape_hw
    xor dx, dx
    xor bh, bh
    call set_cursor_bda_and_hw
    call vga_enable_display
    jmp .done

.mode7:
    mov si, mode07_table
    call program_vga_table
    call vga_load_default_font
    call vga_maybe_load_default_palette
    call set_bda_text
    test bl, 0x80
    jnz .text_cursor
    mov ax, 0xB000
    mov es, ax
    xor di, di
    mov ax, 0x0720
    mov cx, 16384
    cld
    rep stosw
    jmp .text_cursor

.mode4:
    mov si, mode04_table
    jmp .cga_graphics
.mode5:
    mov si, mode05_table
    jmp .cga_graphics
.mode6:
    mov si, mode06_table
.cga_graphics:
    call program_vga_table
    call vga_maybe_load_default_palette
    call set_bda_graphics
    test bl, 0x80
    jnz .graphics_visible
    mov ax, TEXT_SEG
    mov es, ax
    xor di, di
    xor ax, ax
    mov cx, 16384
    cld
    rep stosw
    jmp .graphics_visible

.mode0d:
    mov si, mode0d_table
    jmp .planar_graphics
.mode0e:
    mov si, mode0e_table
    jmp .planar_graphics
.mode0f:
    mov si, mode0f_table
    jmp .planar_graphics
.mode10:
    mov si, mode10_table
    jmp .planar_graphics
.mode11:
    mov si, mode11_table
    jmp .planar_graphics

.mode12:
    mov si, mode12_table
.planar_graphics:
    call program_vga_table
    call vga_maybe_load_default_palette
    call set_bda_graphics
    test bl, 0x80
    jnz .graphics_visible
    mov ax, VGA_SEG
    mov es, ax
    xor di, di
    xor ax, ax
    mov cx, 32768
    cld
    rep stosw
.graphics_visible:
    call vga_enable_display
    jmp .done

.mode13:
    mov si, mode13_table
    call program_vga_table
    call vga_maybe_load_default_palette256
    mov dl, 0x13
    call set_bda_graphics
    test bl, 0x80
    jnz .mode13_visible
    mov ax, VGA_SEG
    mov es, ax
    xor di, di
    xor ax, ax
    mov cx, 32768
    cld
    rep stosw
.mode13_visible:
    call vga_enable_display
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

set_bda_text:
    push ax
    push cx
    push di
    push es
    mov ax, BDA_SEG
    mov es, ax
    mov [es:0x49], dl
    cmp dl, 1
    ja .text80
    mov word [es:0x4A], 40
    mov word [es:0x4C], 0x0800
    jmp .text_geometry_done
.text80:
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x1000
.text_geometry_done:
    mov word [es:0x4E], 0
    xor ax, ax
    mov di, 0x50
    mov cx, 8
.zero_cursor_pages:
    stosw
    loop .zero_cursor_pages
    mov word [es:0x60], 0x0D0E
    mov byte [es:0x62], 0
    mov ax, 0x03D4
    cmp dl, 7
    jne .store_text_crtc
    mov ax, 0x03B4
.store_text_crtc:
    mov [es:0x63], ax
    call vga_update_extended_bda
    pop es
    pop di
    pop cx
    pop ax
    ret

set_bda_graphics:
    push ax
    push cx
    push di
    push es
    mov ax, BDA_SEG
    mov es, ax
    mov [es:0x49], dl
    cmp dl, 0x04
    jb .mode13_values
    cmp dl, 0x06
    ja .not_cga_values
    cmp dl, 0x06
    je .cga640_values
    mov word [es:0x4A], 40
    mov word [es:0x4C], 0x4000
    jmp .have_geometry
.cga640_values:
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x4000
    jmp .have_geometry
.not_cga_values:
    cmp dl, 0x0D
    jne .not_mode0d
    mov word [es:0x4A], 40
    mov word [es:0x4C], 0x2000
    jmp .have_geometry
.not_mode0d:
    cmp dl, 0x0E
    jne .not_mode0e
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x4000
    jmp .have_geometry
.not_mode0e:
    cmp dl, 0x10
    jbe .ega350_values
    cmp dl, 0x12
    jbe .vga480_values
    jmp .mode13_values
.ega350_values:
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x8000
    jmp .have_geometry
.vga480_values:
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x9600
    jmp .have_geometry
.mode13_values:
    mov word [es:0x4A], 40
    mov word [es:0x4C], 0xFA00
.have_geometry:
    mov word [es:0x4E], 0
    xor ax, ax
    mov di, 0x50
    mov cx, 8
.zero_graphics_cursors:
    stosw
    loop .zero_graphics_cursors
    mov byte [es:0x62], 0
    mov word [es:0x63], 0x03D4
    call vga_update_extended_bda
    pop es
    pop di
    pop cx
    pop ax
    ret

; DS:SI -> [misc][5 sequencer][25 CRTC][9 GC][21 attribute]
program_vga_table:
    push ax
    push bx
    push cx
    push dx
    push di
    mov di, dx
    and di, 0x00FF

    ; Blank scan-out before changing any clock, serializer, CRTC, or memory
    ; interpretation state.  Attribute Controller PAS=0 disconnects display
    ; memory from the DAC while retaining sync; otherwise the old visible page
    ; is briefly interpreted through each partially programmed new mode.
    ; IBM VGA/XGA Technical Reference, Attribute Controller Address Register,
    ; Palette Address Source bit (3C0h bit 5).
    mov dx, 0x3CC
    in al, dx
    test al, 1
    mov dx, 0x3DA
    jnz .blank_have_status
    mov dx, 0x3BA
.blank_have_status:
    in al, dx
    mov dx, 0x3C0
    xor al, al
    out dx, al

    mov dx, 0x3C2
    lodsb
    out dx, al

    ; Sequencer reset/program/run.
    mov dx, 0x3C4
    xor bx, bx
    mov cx, 5
.seq_loop:
    mov al, bl
    out dx, al
    inc dx
    lodsb
    out dx, al
    dec dx
    inc bl
    loop .seq_loop

    ; Misc Output bit 0 selects the live CRTC decoder.  Mode 07h is the VGA
    ; monochrome-compatibility personality at 03B4h/03B5h; writing 03D4h after
    ; selecting that personality is electrically invisible on real hardware.
    cmp di, 7
    mov dx, 0x3D4
    jne .have_crtc_port
    mov dx, 0x3B4
.have_crtc_port:
    ; Unlock standard VGA CRTC timing registers before loading the table.
    mov al, 0x11
    out dx, al
    inc dx
    in al, dx
    and al, 0x7F
    out dx, al
    dec dx

    xor bx, bx
    mov cx, 25
.crtc_loop:
    mov al, bl
    out dx, al
    inc dx
    lodsb
    out dx, al
    dec dx
    inc bl
    loop .crtc_loop

    mov dx, 0x3CE
    xor bx, bx
    mov cx, 9
.gc_loop:
    mov al, bl
    out dx, al
    inc dx
    lodsb
    out dx, al
    dec dx
    inc bl
    loop .gc_loop

    ; Reading Input Status 1 resets the attribute-controller flip-flop.
    cmp di, 7
    mov dx, 0x3DA
    jne .have_status_port
    mov dx, 0x3BA
.have_status_port:
    in al, dx
    mov dx, 0x3C0
    xor bx, bx
    mov cx, 21
.ac_loop:
    mov al, bl
    out dx, al
    lodsb
    out dx, al
    inc bl
    loop .ac_loop

    ; Leave PAS cleared.  The caller must finish the font/palette, BDA, VRAM,
    ; and cursor portions of the mode set before making scan-out visible.

    pop di
    pop dx
    pop cx
    pop bx
    pop ax
    ret

vga_enable_display:
    push ax
    push dx
    cmp dl, 7
    mov dx, 0x3DA                   ; force Attribute Controller index phase
    jne .enable_have_status
    mov dx, 0x3BA
.enable_have_status:
    in al, dx
    mov dx, 0x3C0
    mov al, 0x20                    ; PAS=1, palette-address source = video RAM
    out dx, al
    pop dx
    pop ax
    ret

; ---------------------------------------------------------------------------
; Program the canonical 64-entry EGA-compatible VGA DAC palette.
; Standard 16-color Attribute Controller mappings select DAC entries including
; 14h (brown) and 38h-3Fh (bright colors).
load_ega64_dac:
    push ax
    push cx
    push dx
    push si
    push ds
    push cs
    pop ds

    mov dx, 0x3C8
    xor al, al
    out dx, al
    inc dx                          ; 3C9h DAC data

    mov si, ega64_dac_table
    mov cx, 64*3
.load:
    lodsb
    out dx, al
    loop .load

    pop ds
    pop si
    pop dx
    pop cx
    pop ax
    ret

; Mode 13h's defined BIOS state includes the complete 256-entry VGA palette,
; not merely the first 16/64 EGA-compatible entries.  Programs are free to
; replace it afterward through 3C8h/3C9h.
load_vga256_dac:
    push ax
    push cx
    push dx
    push si
    push ds
    push cs
    pop ds

    mov dx, 0x3C8
    xor al, al
    out dx, al
    inc dx
    mov si, default_vga_256_dac
    mov cx, 256*3
.load:
    lodsb
    out dx, al
    loop .load

    pop ds
    pop si
    pop dx
    pop cx
    pop ax
    ret

; Honor INT 10h/AH=12h BL=31h default-palette loading policy.
vga_maybe_load_default_palette:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    test byte [0x89], 0x08
    pop ds
    pop ax
    jnz .skip
    call load_ega64_dac
.skip:
    ret

vga_maybe_load_default_palette256:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    test byte [0x89], 0x08
    pop ds
    pop ax
    jnz .skip
    call load_vga256_dac
.skip:
    ret

; Cursor and page services
; ---------------------------------------------------------------------------
video_set_shape:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov [0x60], cx
    pop ds
    pop ax
    call set_cursor_shape_hw
    iret

set_cursor_shape_hw:
    push ax
    push dx
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov dx, [0x63]
    mov al, 0x0A
    out dx, al
    inc dx
    mov al, ch
    out dx, al
    dec dx
    mov al, 0x0B
    out dx, al
    inc dx
    mov al, cl
    out dx, al
    pop ds
    pop dx
    pop ax
    ret

video_set_cursor:
    call set_cursor_bda_and_hw
    iret

; BH page, DH row, DL column.
set_cursor_bda_and_hw:
    push ax
    push bx
    push cx
    push dx
    push si
    push ds
    mov si, dx                      ; MUL below uses DX:AX; preserve requested row/column
    mov cl, bh                      ; preserve requested page independently of BX
    xor ch, ch
    mov al, cl
    xor ah, ah
    shl ax, 1
    mov bx, ax
    mov ax, BDA_SEG
    mov ds, ax
    mov [bx+0x50], dx
    cmp cl, [0x62]
    jne .done
    mov ax, [0x4C]                 ; page size bytes
    shr ax, 1                       ; CRTC character-word address
    mul cx                          ; page number retained in CX
    mov bx, ax
    ; SI retains the requested row/column.  MUL uses DX:AX, so never read
    ; DH/DL after it: doing so pins the hardware cursor to column zero.
    mov ax, si
    mov al, ah
    xor ah, ah
    mul word [0x4A]
    add bx, ax
    mov ax, si
    and ax, 0x00FF
    add bx, ax
    mov dx, [0x63]
    mov al, 0x0E
    out dx, al
    inc dx
    mov al, bh
    out dx, al
    dec dx
    mov al, 0x0F
    out dx, al
    inc dx
    mov al, bl
    out dx, al
.done:
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

video_get_cursor:
    push ax
    push bx
    push ds
    mov al, bh
    and al, 7
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
    push cx
    push dx
    push ds
    mov bl, al
    and bl, 7
    mov ax, BDA_SEG
    mov ds, ax
    mov [0x62], bl
    mov ax, [0x4C]
    xor cx, cx
    mov cl, bl
    mul cx
    mov [0x4E], ax
    shr ax, 1
    mov bx, ax
    mov dx, [0x63]
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
    ; Restore cursor for newly visible page.
    xor bx, bx
    mov bl, [0x62]
    shl bx, 1
    mov dx, [bx+0x50]
    mov bh, [0x62]
    pop ds
    call set_cursor_bda_and_hw
    pop dx
    pop cx
    pop bx
    pop ax
    iret

; Convert page BH + its BDA cursor to ES:DI in the active VGA text aperture.
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
    mov ax, BDA_SEG
    mov ds, ax
    mov cx, [0x4C]
    shr cx, 1                       ; work in CRTC/text-cell words
    mov ax, di
    mul cx
    mov di, ax
    mov al, bh
    and al, 7
    xor ah, ah
    shl ax, 1
    mov bx, ax
    mov dx, [bx+0x50]
    xor ax, ax
    mov al, dh
    mul word [0x4A]
    add di, ax
    xor ax, ax
    mov al, dl
    add di, ax
    shl di, 1
    mov ax, TEXT_SEG
    cmp byte [0x49], 7
    jne .have_text_segment
    mov ax, 0xB000
.have_text_segment:
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
.char_loop:
    mov [es:di], al
    add di, 2
    loop .char_loop
    pop es
    pop di
    pop cx
    pop ax
    iret

; ---------------------------------------------------------------------------
; Teletype and text-window scroll
; ---------------------------------------------------------------------------
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

    ; Character output in every standard VGA alphanumeric mode.  Graphics-mode
    ; teletype requires glyph plotting and is handled separately from text RAM.
    mov si, dx
    mov al, [0x49]
    cmp al, 3
    jbe .write_text
    cmp al, 7
    jne .advance_only
.write_text:
    mov ax, TEXT_SEG
    cmp byte [0x49], 7
    jne .tty_have_text_segment
    mov ax, 0xB000
.tty_have_text_segment:
    mov es, ax
    xor di, di
    mov al, [0x62]
    xor ah, ah
    mov bx, [0x4C]
    mul bx
    mov di, ax
    mov ax, si
    mov al, ah
    xor ah, ah
    mov bx, [0x4A]
    shl bx, 1
    mul bx
    add di, ax
    mov ax, si
    and ax, 0x00FF
    shl ax, 1
    add di, ax
    mov al, cl
    mov ah, 7
    stosw
.advance_only:
    mov dx, si
    inc dl
    mov ax, [0x4A]
    cmp dl, al
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
    push dx                         ; preserve the logical cursor column
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
    mov bh, [0x62]
    call set_cursor_bda_and_hw
    pop ds
    pop es
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    iret

; AH=06/07 rectangular text window.  Coordinates are zero-based BIOS values.
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
    sub sp, 8

    ; AH=06h/07h have no page parameter.  IBM-compatible BIOS semantics apply
    ; the operation to the currently active display page.  The page base is the
    ; byte offset cached by the BIOS Data Area at 0040:004Eh.
    mov ax, BDA_SEG
    mov ds, ax
    mov ax, [0x4E]
    mov [ss:bp-22], ax              ; active page base, bytes
    mov ax, [0x4A]
    shl ax, 1
    mov [ss:bp-24], ax              ; physical bytes per text row

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

    mov ax, TEXT_SEG
    cmp byte [0x49], 7
    jne .scroll_up_segment_ready
    mov ax, 0xB000
.scroll_up_segment_ready:
    mov ds, ax
    mov es, ax
    cld

.copy_row:
    xor ax, ax
    mov al, [ss:bp-18]
    mul word [ss:bp-24]
    mov di, ax
    add di, [ss:bp-22]
    xor ax, ax
    mov al, [ss:bp-6]
    shl ax, 1
    add di, ax

    xor ax, ax
    mov al, [ss:bp-19]
    mul word [ss:bp-24]
    mov si, ax
    add si, [ss:bp-22]
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
    mov ax, TEXT_SEG
    cmp byte [0x49], 7
    jne .scroll_up_clear_segment_ready
    mov ax, 0xB000
.scroll_up_clear_segment_ready:
    mov es, ax

.fill:
    cld

.fill_row:
    xor ax, ax
    mov al, [ss:bp-20]
    mul word [ss:bp-24]
    mov di, ax
    add di, [ss:bp-22]
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

    add sp, 8
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
    sub sp, 8

    ; Same active-page rule as AH=06h above.
    mov ax, BDA_SEG
    mov ds, ax
    mov ax, [0x4E]
    mov [ss:bp-22], ax              ; active page base, bytes
    mov ax, [0x4A]
    shl ax, 1
    mov [ss:bp-24], ax              ; physical bytes per text row

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

    mov ax, TEXT_SEG
    cmp byte [0x49], 7
    jne .scroll_down_segment_ready
    mov ax, 0xB000
.scroll_down_segment_ready:
    mov ds, ax
    mov es, ax
    std

.copy_row:
    xor ax, ax
    mov al, [ss:bp-18]
    mul word [ss:bp-24]
    mov di, ax
    add di, [ss:bp-22]
    xor ax, ax
    mov al, [ss:bp-8]               ; right edge, copy backwards
    shl ax, 1
    add di, ax

    xor ax, ax
    mov al, [ss:bp-19]
    mul word [ss:bp-24]
    mov si, ax
    add si, [ss:bp-22]
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
    mov ax, TEXT_SEG
    cmp byte [0x49], 7
    jne .scroll_down_clear_segment_ready
    mov ax, 0xB000
.scroll_down_clear_segment_ready:
    mov es, ax

.fill:
    cld

.fill_row:
    xor ax, ax
    mov al, [ss:bp-20]
    mul word [ss:bp-24]
    mov di, ax
    add di, [ss:bp-22]
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

    add sp, 8
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
; ---------------------------------------------------------------------------
; Pixel and palette services
; ---------------------------------------------------------------------------
video_write_pixel:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push es
    mov bl, al
    mov ax, BDA_SEG
    mov es, ax
    mov al, [es:0x49]
    cmp al, 0x13
    je .packed_256
    cmp al, 4
    jb .done
    cmp al, 6
    jbe .cga
    cmp al, 0x0D
    jb .done
    cmp al, 0x12
    jbe .planar
    jmp .done

.packed_256:
    mov ax, dx
    mov di, 320
    mul di
    add ax, cx
    mov di, ax
    mov ax, VGA_SEG
    mov es, ax
    mov al, bl
    test al, 0x80
    jz .packed_store
    and al, 0x7F
    xor al, [es:di]
.packed_store:
    mov [es:di], al
    jmp .done

.cga:
    ; IBM modes 04h-06h retain the CGA 8-KiB odd/even scan-line banks in
    ; the B8000h aperture.  This is the CPU-visible packed representation;
    ; the VGA odd/even memory path distributes the bytes to planes 0 and 1.
    mov ax, dx
    and ax, 1
    mov di, ax
    shl di, 13                       ; odd scan lines start at +2000h
    mov ax, dx
    shr ax, 1
    mov si, 80
    mul si
    add di, ax
    mov dl, [es:0x49]
    mov ax, 0xB800
    mov es, ax
    cmp dl, 6
    je .cga_mono

    mov ax, cx
    and ax, 3
    shl ax, 1
    mov si, 6
    sub si, ax                       ; packed two-bit pixel shift
    mov ax, cx
    shr ax, 1
    shr ax, 1
    add di, ax
    mov al, 3
    mov cx, si
    shl al, cl                       ; AL = two-bit field mask
    mov ah, [es:di]
    test bl, 0x80
    jnz .cga_xor
    not al
    and ah, al
    mov al, bl
    and al, 3
    shl al, cl
    or ah, al
    mov [es:di], ah
    jmp .done
.cga_xor:
    mov dl, bl
    and dl, 3
    shl dl, cl
    xor ah, dl
    mov [es:di], ah
    jmp .done

.cga_mono:
    mov ax, cx
    and ax, 7
    mov si, 7
    sub si, ax
    mov ax, cx
    shr ax, 1
    shr ax, 1
    shr ax, 1
    add di, ax
    mov al, 1
    mov cx, si
    shl al, cl
    test bl, 0x80
    jnz .cga_mono_xor
    test bl, 1
    jnz .cga_mono_set
    not al
    and [es:di], al
    jmp .done
.cga_mono_set:
    or [es:di], al
    jmp .done
.cga_mono_xor:
    test bl, 1
    jz .done
    xor [es:di], al
    jmp .done

.planar:
    ; Modes 0Dh-12h use one bit from each VGA plane.  Write mode 2 expands
    ; AL bits 0-3 to the four planes, while the bit mask selects the requested
    ; pixel.  Data-rotate XOR supplies the documented AL bit-7 XOR operation.
    mov si, 80
    cmp byte [es:0x49], 0x0D
    jne .planar_stride_ready
    mov si, 40
.planar_stride_ready:
    mov ax, dx
    mul si
    mov di, ax
    mov ax, cx
    shr ax, 1
    shr ax, 1
    shr ax, 1
    add di, ax
    mov ax, [es:0x4C]
    xor dx, dx
    mov dl, bh
    mul dx
    add di, ax
    mov ax, cx
    and ax, 7
    mov cl, al
    mov al, 0x80
    shr al, cl
    mov ch, al                       ; CH = VGA bit mask

    mov dx, 0x3CE
    mov al, 3
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax                          ; saved Data Rotate
    dec dx
    mov al, 5
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax                          ; saved Graphics Mode
    dec dx
    mov al, 8
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax                          ; saved Bit Mask

    dec dx
    mov ax, 0x0003
    test bl, 0x80
    jz .planar_rotate_ready
    mov ah, 0x18                     ; logical XOR, rotate count zero
.planar_rotate_ready:
    out dx, ax
    mov al, 5
    out dx, al
    inc dx
    in al, dx
    and al, 0xFC
    or al, 2                         ; write mode 2
    mov ah, al
    dec dx
    mov al, 5
    out dx, ax
    mov al, 8
    mov ah, ch
    out dx, ax

    mov ax, VGA_SEG
    mov es, ax
    mov al, [es:di]                  ; load all four VGA latches
    mov al, bl
    and al, 0x0F
    mov [es:di], al

    mov dx, 0x3CE
    pop ax
    mov ah, al
    mov al, 8
    out dx, ax
    pop ax
    mov ah, al
    mov al, 5
    out dx, ax
    pop ax
    mov ah, al
    mov al, 3
    out dx, ax
.done:
    pop es
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    iret

video_read_pixel:
    push bx
    push cx
    push dx
    push bp
    push si
    push di
    push es
    mov ax, BDA_SEG
    mov es, ax
    mov al, [es:0x49]
    cmp al, 0x13
    je .packed_256
    cmp al, 4
    jb .zero
    cmp al, 6
    jbe .cga
    cmp al, 0x0D
    jb .zero
    cmp al, 0x12
    jbe .planar
    jmp .zero

.packed_256:
    mov ax, dx
    mov di, 320
    mul di
    add ax, cx
    mov di, ax
    mov ax, VGA_SEG
    mov es, ax
    mov al, [es:di]
    jmp .done

.cga:
    mov ax, dx
    and ax, 1
    mov di, ax
    shl di, 13
    mov ax, dx
    shr ax, 1
    mov si, 80
    mul si
    add di, ax
    mov dl, [es:0x49]
    mov ax, 0xB800
    mov es, ax
    cmp dl, 6
    je .cga_mono
    mov ax, cx
    and ax, 3
    shl ax, 1
    mov si, 6
    sub si, ax
    mov ax, cx
    shr ax, 1
    shr ax, 1
    add di, ax
    mov al, [es:di]
    mov cx, si
    shr al, cl
    and al, 3
    jmp .done
.cga_mono:
    mov ax, cx
    and ax, 7
    mov si, 7
    sub si, ax
    mov ax, cx
    shr ax, 1
    shr ax, 1
    shr ax, 1
    add di, ax
    mov al, [es:di]
    mov cx, si
    shr al, cl
    and al, 1
    jmp .done

.planar:
    mov si, 80
    cmp byte [es:0x49], 0x0D
    jne .planar_stride_ready
    mov si, 40
.planar_stride_ready:
    mov ax, dx
    mul si
    mov di, ax
    mov ax, cx
    shr ax, 1
    shr ax, 1
    shr ax, 1
    add di, ax
    mov ax, [es:0x4C]
    xor dx, dx
    mov dl, bh
    mul dx
    add di, ax
    mov ax, cx
    and ax, 7
    mov cl, al
    mov ax, 0x0080
    shr al, cl
    mov bp, ax                       ; keep mask independent of plane selector CX

    mov dx, 0x3CE
    mov al, 4
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax                          ; saved Read Map Select
    xor bx, bx                       ; BL accumulates the four plane bits
    xor si, si
    mov ax, VGA_SEG
    mov es, ax
.read_plane:
    mov dx, 0x3CE
    mov ax, si
    mov ah, al
    mov al, 4
    out dx, ax
    mov al, [es:di]
    xor ah, ah
    test ax, bp
    jz .next_plane
    mov ax, 1
    mov cx, si
    shl al, cl
    or bl, al
.next_plane:
    inc si
    cmp si, 4
    jb .read_plane
    pop ax
    mov ah, al
    mov al, 4
    mov dx, 0x3CE
    out dx, ax
    mov al, bl
    jmp .done
.zero:
    xor al, al
.done:
    pop es
    pop di
    pop si
    pop bp
    pop dx
    pop cx
    pop bx
    iret

video_palette:
    cmp al, 0x02
    J286_NE .not_all_attribute_palette
    ; INT 10h AX=1002h: ES:DX points to 16 Attribute Controller palette
    ; values followed by the overscan value.  VGALOGO.LGO relies on this to
    ; replace the mode-set EGA 38h-3Fh mappings with its identity 00h-0Fh
    ; table before programming DAC entries 00h-0Fh.
    push ax
    push bx
    push cx
    push dx
    push si
    push ds
    mov si, dx
    mov ax, es
    mov ds, ax
    mov dx, 0x3DA
    in al, dx                         ; force Attribute Controller index phase
    mov dx, 0x3C0
    xor bx, bx
    mov cx, 16
.all_attribute_palette_loop:
    mov al, bl                        ; PAS=0, palette register BL
    out dx, al
    lodsb
    and al, 0x3F
    out dx, al
    inc bl
    loop .all_attribute_palette_loop
    mov al, 0x11                      ; overscan/border register
    out dx, al
    lodsb
    and al, 0x3F
    out dx, al
    mov dx, 0x3DA
    in al, dx                         ; index phase before restoring PAS
    mov dx, 0x3C0
    mov al, 0x20                      ; video palette address source enabled
    out dx, al
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    iret
.not_all_attribute_palette:
    cmp al, 0x03
    J286_NE .not_blink_intensity
    ; INT 10h AX=1003h: BL=0 selects background intensity; BL=1 selects
    ; attribute-bit-7 blinking.  This is the IBM VGA BIOS interface used by
    ; full-screen DOS applications such as Microsoft/Symantec DEFRAG.
    push ax
    push dx
    mov dx, 0x3DA
    in al, dx                         ; reset Attribute Controller flip-flop
    mov dx, 0x3C0
    mov al, 0x30                      ; index 10h, keep palette source enabled
    out dx, al
    mov dx, 0x3C1
    in al, dx
    test bl, 1
    J286_E .intensity_backgrounds
    or al, 0x08
    jmp .write_mode_control
.intensity_backgrounds:
    and al, 0xF7
.write_mode_control:
    mov ah, al
    mov dx, 0x3DA
    in al, dx                         ; index phase again
    mov dx, 0x3C0
    mov al, 0x30
    out dx, al
    mov al, ah
    out dx, al
    pop dx
    pop ax
    iret
.not_blink_intensity:
    cmp al, 0x10
    J286_NE .not_single_dac
    ; BX=index, DH=red, CH=green, CL=blue (6-bit VGA DAC values)
    ; Preserve DH before loading DX with the DAC port address.  DX is both an
    ; INT 10h argument register and the x86 OUT port register: loading 03C8h
    ; first would make DH=03h and force every programmed red component to 03h.
    ; IBM VGA/XGA Technical Reference, Video BIOS Interface, INT 10h
    ; AH=10h/AL=10h defines the component registers used here.
    push ax
    push dx
    mov ah, dh
    mov dx, 0x3C8
    mov al, bl
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    mov al, ch
    out dx, al
    mov al, cl
    out dx, al
    pop dx
    pop ax
    iret
.not_single_dac:
    cmp al, 0x12
    J286_NE .not_read_single_dac
    ; BX=start, CX=count, ES:DX -> RGB triplets.
    push ax
    push bx
    push cx
    push dx
    push si
    push ds
    mov si, dx
    mov ax, es
    mov ds, ax
    mov dx, 0x3C8
    mov al, bl
    out dx, al
    inc dx
.block_loop:
    lodsb
    out dx, al
    lodsb
    out dx, al
    lodsb
    out dx, al
    loop .block_loop
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    iret
.not_read_single_dac:
    cmp al, 0x15
    J286_NE .not_read_dac_block
    ; IBM VGA/XGA Technical Reference, Video BIOS Interface, INT 10h
    ; AH=10h/AL=15h: BX selects one DAC register and the BIOS returns its
    ; red/green/blue components in DH/CH/CL.  A program is entitled to save
    ; the hardware palette through this service and later restore it through
    ; AL=10h; returning stale CPU registers here destroys that round trip.
    push ax
    push bx
    push dx
    mov dx, 0x3C7
    mov al, bl
    out dx, al
    add dx, 2                         ; 03C9h, DAC data port
    in al, dx
    mov ah, al                        ; retain red while DX becomes an output
    in al, dx
    mov ch, al                        ; green
    in al, dx
    mov cl, al                        ; blue
    pop dx
    pop bx
    mov dh, ah                        ; red
    pop ax
    iret
.not_read_dac_block:
    cmp al, 0x17
    J286_NE .done
    ; IBM VGA/XGA Technical Reference, INT 10h AH=10h/AL=17h:
    ; BX=start, CX=count, ES:DX -> destination RGB triplets.  This is the
    ; inverse of AL=12h and is used by full-screen DOS software to preserve
    ; the VGA DAC across a session.  The Bt485's VGA-compatible read-address
    ; and data ports auto-advance after every complete RGB triplet.
    push ax
    push bx
    push cx
    push dx
    push di
    mov di, dx
    mov dx, 0x3C7
    mov al, bl
    out dx, al
    add dx, 2                         ; 03C9h, DAC data port
    cld
.read_block_loop:
    jcxz .read_block_complete
    in al, dx
    stosb
    in al, dx
    stosb
    in al, dx
    stosb
    loop .read_block_loop
.read_block_complete:
    pop di
    pop dx
    pop cx
    pop bx
    pop ax
.done:
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
    xor bh, bh                      ; VGA present / color display
    mov bl, 3                       ; 256 KiB-or-more EGA memory class
    mov ch, 0
    mov cl, 3
.done:
    iret

video_display_query:
    cmp al, 0
    J286_NE .unsupported
    mov al, 0x1A
    mov bl, 0x08                    ; VGA color analog display
    xor bh, bh
    iret
.unsupported:
    xor al, al
    iret

; ---------------------------------------------------------------------------
%include "stealthpro_vgabios_state.inc"

; Register tables: standard IBM-compatible VGA programming sequences.
; ---------------------------------------------------------------------------
; Canonical 64-entry EGA-compatible VGA DAC cube.
; DAC component values are 6-bit: 00h, 15h, 2Ah, 3Fh.
ega64_dac_table:
    db 0x00,0x00,0x00,0x00,0x00,0x2A,0x00,0x2A,0x00,0x00,0x2A,0x2A,0x2A,0x00,0x00,0x2A,0x00,0x2A,0x2A,0x2A,0x00,0x2A,0x2A,0x2A
    db 0x00,0x00,0x15,0x00,0x00,0x3F,0x00,0x2A,0x15,0x00,0x2A,0x3F,0x2A,0x00,0x15,0x2A,0x00,0x3F,0x2A,0x2A,0x15,0x2A,0x2A,0x3F
    db 0x00,0x15,0x00,0x00,0x15,0x2A,0x00,0x3F,0x00,0x00,0x3F,0x2A,0x2A,0x15,0x00,0x2A,0x15,0x2A,0x2A,0x3F,0x00,0x2A,0x3F,0x2A
    db 0x00,0x15,0x15,0x00,0x15,0x3F,0x00,0x3F,0x15,0x00,0x3F,0x3F,0x2A,0x15,0x15,0x2A,0x15,0x3F,0x2A,0x3F,0x15,0x2A,0x3F,0x3F
    db 0x15,0x00,0x00,0x15,0x00,0x2A,0x15,0x2A,0x00,0x15,0x2A,0x2A,0x3F,0x00,0x00,0x3F,0x00,0x2A,0x3F,0x2A,0x00,0x3F,0x2A,0x2A
    db 0x15,0x00,0x15,0x15,0x00,0x3F,0x15,0x2A,0x15,0x15,0x2A,0x3F,0x3F,0x00,0x15,0x3F,0x00,0x3F,0x3F,0x2A,0x15,0x3F,0x2A,0x3F
    db 0x15,0x15,0x00,0x15,0x15,0x2A,0x15,0x3F,0x00,0x15,0x3F,0x2A,0x3F,0x15,0x00,0x3F,0x15,0x2A,0x3F,0x3F,0x00,0x3F,0x3F,0x2A
    db 0x15,0x15,0x15,0x15,0x15,0x3F,0x15,0x3F,0x15,0x15,0x3F,0x3F,0x3F,0x15,0x15,0x3F,0x15,0x3F,0x3F,0x3F,0x15,0x3F,0x3F,0x3F

; IBM VGA/XGA Technical Reference, Figure 2-4 and pp. 2-12..2-23:
; https://bitsavers.org/pdf/ibm/pc/cards/IBM_VGA_XGA_Technical_Reference_Manual_May92.pdf
; Video Seven VGA Technical Reference, Tables 4-8/4-9, pp. 4-37..4-38:
; https://bitsavers.org/components/video7/700-0242_V7_VGA_Technical_Reference_Manual_Jun88.pdf
; Layout: [MISC][SR0..4][CR0..18][GR0..8][AR0..14].

mode01_table:
    db 0x67
    db 0x03,0x08,0x03,0x00,0x02
    db 0x2D,0x27,0x28,0x90,0x2B,0xA0,0xBF,0x1F,0x00,0x4F,0x0D,0x0E,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x14,0x1F,0x96,0xB9,0xA3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x10,0x0E,0x0F,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x0C,0x00,0x0F,0x08,0x00

mode03_table:
    db 0x67
    db 0x03,0x00,0x03,0x00,0x02
    db 0x5F,0x4F,0x50,0x82,0x55,0x81,0xBF,0x1F,0x00,0x4F,0x0D,0x0E,0x00,0x00,0x00,0x50,0x9C,0x8E,0x8F,0x28,0x1F,0x96,0xB9,0xA3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x10,0x0E,0x00,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x0C,0x00,0x0F,0x08,0x00

mode07_table:
    db 0x66
    db 0x03,0x00,0x03,0x00,0x02
    db 0x5F,0x4F,0x50,0x82,0x55,0x81,0xBF,0x1F,0x00,0x4F,0x0D,0x0E,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x0F,0x96,0xB9,0xA3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x10,0x0A,0x0F,0xFF
    db 0x00,0x08,0x08,0x08,0x08,0x08,0x08,0x08,0x10,0x18,0x18,0x18,0x18,0x18,0x18,0x18,0x0E,0x00,0x0F,0x08,0x00

mode04_table:
    db 0x63
    db 0x03,0x09,0x03,0x00,0x02
    db 0x2D,0x27,0x28,0x90,0x2B,0x80,0xBF,0x1F,0x00,0xC1,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x14,0x00,0x96,0xB9,0xA2,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x30,0x0F,0x0F,0xFF
    db 0x00,0x13,0x15,0x17,0x02,0x04,0x06,0x07,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x01,0x00,0x03,0x00,0x00

mode05_table:
    db 0x63
    db 0x03,0x09,0x03,0x00,0x02
    db 0x2D,0x27,0x28,0x90,0x2B,0x80,0xBF,0x1F,0x00,0xC1,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x14,0x00,0x96,0xB9,0xA2,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x30,0x0F,0x0F,0xFF
    db 0x00,0x13,0x15,0x17,0x02,0x04,0x06,0x07,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x01,0x00,0x03,0x00,0x00

mode06_table:
    db 0x63
    db 0x03,0x01,0x01,0x00,0x06
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0xC1,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x00,0x96,0xB9,0xC2,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x0D,0x0F,0xFF
    db 0x00,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x01,0x00,0x01,0x00,0x00

mode0d_table:
    db 0x63
    db 0x03,0x09,0x0F,0x00,0x06
    db 0x2D,0x27,0x28,0x90,0x2B,0x80,0xBF,0x1F,0x00,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x14,0x00,0x96,0xB9,0xE3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x01,0x00,0x0F,0x00,0x00

mode0e_table:
    db 0x63
    db 0x03,0x01,0x0F,0x00,0x06
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x00,0x96,0xB9,0xE3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x01,0x00,0x0F,0x00,0x00

mode0f_table:
    db 0xA3
    db 0x03,0x01,0x0F,0x00,0x06
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0x83,0x85,0x5D,0x28,0x0F,0x63,0xBA,0xE3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF
    db 0x00,0x08,0x00,0x00,0x18,0x18,0x00,0x00,0x00,0x08,0x00,0x00,0x00,0x18,0x00,0x00,0x01,0x00,0x01,0x00,0x00

mode10_table:
    db 0xA3
    db 0x03,0x01,0x0F,0x00,0x06
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0x83,0x85,0x5D,0x28,0x0F,0x63,0xBA,0xE3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x01,0x00,0x0F,0x00,0x00

mode11_table:
    db 0xE3
    db 0x03,0x01,0x0F,0x00,0x06
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0x0B,0x3E,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0xEA,0x8C,0xDF,0x28,0x00,0xE7,0x04,0xE3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF
    db 0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x01,0x00,0x0F,0x00,0x00

mode12_table:
    db 0xE3
    db 0x03,0x01,0x0F,0x00,0x06
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0x0B,0x3E,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0xEA,0x8C,0xDF,0x28,0x00,0xE7,0x04,0xE3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x01,0x00,0x0F,0x00,0x00

mode13_table:
    db 0x63
    db 0x03,0x01,0x0F,0x00,0x0E
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0x41,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x40,0x96,0xB9,0xA3,0xFF
    db 0x00,0x00,0x00,0x00,0x00,0x40,0x05,0x0F,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,0x41,0x00,0x0F,0x00,0x00

%include "stealthpro_vga_256_dac.inc"

; The card's resident 8x16 CP437 font.  Mode 2/3 and the implemented AH=11h
; ROM-font services load or return this actual option-ROM payload.
%include "stealthpro_cp437_8x16.inc"

; Fill to exactly 32 KiB.  Installer patches final byte so the whole option ROM
; checksum is zero modulo 256.
times 32767-($-$$) db 0
db 0
