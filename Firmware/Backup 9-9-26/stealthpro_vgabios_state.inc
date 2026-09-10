; ---------------------------------------------------------------------------
; IBM VGA state / alternate-service completion layer for the clean-room
; Diamond Stealth Pro / S3 86C928 option ROM.
;
; This file deliberately implements STANDARD VGA state only.  It does not
; claim to save undocumented Diamond/S3 extended registers.  Those belong in
; an explicitly documented OEM/VESA extension, not in IBM INT 10h/AH=1Ch.
; ---------------------------------------------------------------------------

%define VGA_HW_STATE_SIZE   0x46
%define VGA_BDA_STATE_SIZE  0x60      ; 0040:0049 through 0040:00A8 inclusive
%define VGA_DAC_STATE_SIZE  0x304
%define VGA_STATE_HEADER    0x20
%define VGA_ROM_SEG         0xC000

; Called after a successful mode set / option-ROM initialization.
vga_install_save_pointer:
    push ax
    push es
    mov ax, BDA_SEG
    mov es, ax
    mov word [es:0xA8], video_save_pointer_table
    mov word [es:0xAA], cs
    pop es
    pop ax
    ret

; Refresh the extended VGA BDA fields which are common to all modes currently
; exposed by this ROM.  DL=current mode, BL=original mode request (bit7=no clear).
vga_update_extended_bda:
    push ax
    push es
    mov ax, BDA_SEG
    mov es, ax

    ; 0040:0087 - EGA/VGA video control.  Bits 6:5=11b denotes the standard
    ; 256-KiB-or-more VGA memory class.  Bit7 records the no-clear mode request.
    mov al, 0x60
    test bl, 0x80
    jz .have_no_clear
    or al, 0x80
.have_no_clear:
    mov [es:0x87], al

    ; 0040:0088 - VGA color switch/configuration setting.
    mov byte [es:0x88], 0x09

    ; 0040:0089 - VGA display data.  Preserve only display-switching (bit6)
    ; and the caller's default-palette policy (bit3).  This BIOS does not
    ; advertise grayscale summing, monochrome emulation, or bit0 (reserved).
    ; Bit 4 is the IBM VGA 400-scan-line text-mode flag.  It is not a generic
    ; 200/350/400/480 enumeration; graphics and 350/480-line modes leave it 0.
    mov al, [es:0x89]
    and al, 0x48                    ; preserve bits 6 and 3 only
    cmp dl, 3
    jbe .mark_400_line_text
    cmp dl, 7
    jne .store_display_data
.mark_400_line_text:
    or al, 0x10
.store_display_data:
    mov [es:0x89], al
    mov byte [es:0x8A], 0          ; first DCC-table entry = VGA color

    cmp dl, 0x04
    jb .text_16
    cmp dl, 0x06
    jbe .graphics_8
    cmp dl, 0x07
    je .text_16
    cmp dl, 0x0D
    je .graphics_8
    cmp dl, 0x0E
    je .graphics_8
    cmp dl, 0x10
    jbe .graphics_14
    cmp dl, 0x12
    jbe .graphics_16_30
    ; Mode 13h.
    jmp .graphics_8

.text_16:
    mov byte [es:0x84], 24
    mov word [es:0x85], 16
    jmp .done
.graphics_8:
    mov byte [es:0x84], 24
    mov word [es:0x85], 8
    jmp .done
.graphics_14:
    mov byte [es:0x84], 24
    mov word [es:0x85], 14
    jmp .done
.graphics_16_30:
    mov byte [es:0x84], 29
    mov word [es:0x85], 16
.done:
    pop es
    pop ax
    ret

; ---------------------------------------------------------------------------
; INT 10h AH=11h - character-generator services.
;
; Supported here:
;   AL=00h  user alpha load (mode reset, regen buffer preserved)
;   AL=03h  character-map block select
;   AL=04h  load built-in 8x16 alpha font
;   AL=10h  user alpha load after mode set + text-metric recalculation
;   AL=14h  load built-in 8x16 alpha font + text-metric recalculation
;   AL=20h  set INT 1Fh user graphics-font pointer
;   AL=21h  set INT 43h graphics-font pointer and row/point metadata
;   AL=24h  select built-in 8x16 graphics font
;   AL=30h  return current/user or built-in 8x16 font information
; ---------------------------------------------------------------------------
pcb1v_video_font:
    cmp al, 0x00
    je .user_alpha_reset
    cmp al, 0x03
    je .set_block
    cmp al, 0x04
    je .rom_alpha_16
    cmp al, 0x10
    je .user_alpha_after_mode
    cmp al, 0x14
    je .rom_alpha_16_metrics
    cmp al, 0x20
    je .set_int1f
    cmp al, 0x21
    je .set_int43
    cmp al, 0x24
    je .rom_graphics_16
    cmp al, 0x30
    je .get_info
    iret

.user_alpha_reset:
    ; The original EGA/VGA 00h service performs a mode set while preserving
    ; the regeneration buffer, then loads the caller's font.
    push ax
    push bx
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov al, [0x49]
    cmp al, 2
    je .reset_supported
    cmp al, 3
    jne .reset_abort
.reset_supported:
    mov bl, al
    or bl, 0x80                    ; BIOS mode-set "do not clear" convention
    call set_mode_internal
    pop ds
    pop bx
    pop ax
    call vga_load_user_font
    iret
.reset_abort:
    pop ds
    pop bx
    pop ax
    iret

.user_alpha_after_mode:
    call vga_load_user_font
    call vga_recalculate_text_metrics
    iret

.rom_alpha_16:
    call vga_load_default_font
    iret

.rom_alpha_16_metrics:
    call vga_load_default_font
    push bx
    mov bh, 16
    call vga_recalculate_text_metrics
    pop bx
    iret

.rom_graphics_16:
    call vga_install_default_font_pointer
    push bx
    mov bh, 16
    call vga_recalculate_text_metrics
    pop bx
    iret

.set_block:
    push ax
    push dx
    mov dx, 0x3C4
    in al, dx
    mov ah, al                     ; preserve Sequencer Address register
    mov al, 3
    out dx, al
    inc dx
    mov al, bl
    and al, 0x3F                   ; six defined Character Map Select bits
    out dx, al
    dec dx
    mov al, ah
    out dx, al
    pop dx
    pop ax
    iret

.set_int1f:
    push ax
    push ds
    xor ax, ax
    mov ds, ax
    mov [0x1F*4], bp
    mov ax, es
    mov [0x1F*4+2], ax
    pop ds
    pop ax
    iret

.set_int43:
    push ax
    push bx
    push ds

    xor ax, ax
    mov ds, ax
    mov [0x43*4], bp
    mov ax, es
    mov [0x43*4+2], ax

    mov ax, BDA_SEG
    mov ds, ax
    mov [0x85], cx                 ; points / bytes per character

    ; BL row specifier: 0=user DL, 1=14, 2=25, 3=43 character rows.
    cmp bl, 0
    jne .rows_not_user
    mov al, dl
    jmp .have_rows
.rows_not_user:
    cmp bl, 1
    jne .rows_not_14
    mov al, 14
    jmp .have_rows
.rows_not_14:
    cmp bl, 2
    jne .rows_not_25
    mov al, 25
    jmp .have_rows
.rows_not_25:
    cmp bl, 3
    jne .set43_done
    mov al, 43
.have_rows:
    test al, al
    jz .set43_done
    dec al
    mov [0x84], al                 ; BDA stores rows minus one
.set43_done:
    pop ds
    pop bx
    pop ax
    iret

.get_info:
    cmp bh, 6
    je .info_rom16
    cmp bh, 1
    ja .font_info_unsupported
    push ax
    push ds
    xor ax, ax
    mov ds, ax
    test bh, bh
    jnz .info_int43
    mov bp, [0x1F*4]
    mov ax, [0x1F*4+2]
    jmp .info_pointer_ready
.info_int43:
    mov bp, [0x43*4]
    mov ax, [0x43*4+2]
.info_pointer_ready:
    mov es, ax
    mov ax, BDA_SEG
    mov ds, ax
    mov cx, [0x85]
    mov dl, [0x84]
    pop ds
    pop ax
    iret
.info_rom16:
    push ax
    push ds
    push cs
    pop es
    mov bp, default_font_8x16
    mov cx, 16
    mov ax, BDA_SEG
    mov ds, ax
    mov dl, [0x84]
    pop ds
    pop ax
.font_info_unsupported:
    iret

; Load ES:BP user glyph data into VGA plane 2.
; Inputs are the documented AH=11h user-alpha parameters:
;   BH=bytes per character (1..32), BL=font block (0..7),
;   CX=count, DX=first character, ES:BP=source.
vga_load_user_font:
    cmp bl, 7
    ja .load_return
    cmp bh, 1
    jb .load_return
    cmp bh, 32
    ja .load_return
    test cx, cx
    jz .load_return

    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push bp
    push ds

    mov si, cx                     ; outer glyph count

    ; Font block N is the Nth 8-KiB region of VGA plane 2.
    xor ax, ax
    mov al, bl
    mov di, ax
    shl di, 13
    mov ax, dx
    and ax, 0x00FF
    shl ax, 5                      ; 32-byte slot per character
    add di, ax

    ; Save Misc Output RAM-enable state.
    mov dx, 0x3CC
    in al, dx
    xor ah, ah
    push ax
    or al, 0x02
    mov dx, 0x3C2
    out dx, al

    ; Save Sequencer index, Map Mask (02h), Memory Mode (04h).
    mov dx, 0x3C4
    in al, dx
    xor ah, ah
    push ax
    mov al, 2
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax
    dec dx
    mov al, 4
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax

    ; Save Graphics index and all registers altered for raw plane-2 writes:
    ; Enable Set/Reset, Data Rotate, Read Map, Mode, Miscellaneous and Bit Mask.
    dec dx                          ; 03C4h, then move to Graphics Controller
    mov dx, 0x3CE
    in al, dx
    xor ah, ah
    push ax
    mov al, 1
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax
    dec dx
    mov al, 3
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax
    dec dx
    mov al, 4
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax
    dec dx
    mov al, 5
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax
    dec dx
    mov al, 6
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax
    dec dx
    mov al, 8
    out dx, al
    inc dx
    in al, dx
    xor ah, ah
    push ax

    ; CPU writes map directly to plane 2 at A0000h.
    mov dx, 0x3C4
    mov al, 2
    out dx, al
    inc dx
    mov al, 0x04
    out dx, al
    dec dx
    mov al, 4
    out dx, al
    inc dx
    mov al, 0x06                   ; sequential addressing, extended memory
    out dx, al

    mov dx, 0x3CE
    mov al, 1
    out dx, al
    inc dx
    xor al, al                     ; disable Set/Reset
    out dx, al
    dec dx
    mov al, 3
    out dx, al
    inc dx
    xor al, al                     ; rotate 0, replace
    out dx, al
    dec dx
    mov al, 4
    out dx, al
    inc dx
    mov al, 2                      ; read map plane 2
    out dx, al
    dec dx
    mov al, 5
    out dx, al
    inc dx
    xor al, al                     ; read mode 0, write mode 0
    out dx, al
    dec dx
    mov al, 6
    out dx, al
    inc dx
    mov al, 0x04                   ; A0000h 64-KiB aperture
    out dx, al
    dec dx
    mov al, 8
    out dx, al
    inc dx
    mov al, 0xFF
    out dx, al

    mov ax, VGA_SEG
    mov ds, ax
.load_glyph:
    test si, si
    jz .load_complete
    xor cx, cx
    mov cl, bh
.load_row:
    mov al, [es:bp]
    inc bp
    mov [di], al
    inc di
    loop .load_row
    xor ax, ax
    mov al, 32
    sub al, bh
    add di, ax                     ; skip unused rows in 32-byte glyph slot
    dec si
    jmp .load_glyph

.load_complete:
    ; Restore Graphics registers in exact reverse order.
    mov dx, 0x3CE
    pop ax                         ; GC08
    mov ah, al
    mov al, 8
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; GC06
    mov ah, al
    mov al, 6
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; GC05
    mov ah, al
    mov al, 5
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; GC04
    mov ah, al
    mov al, 4
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; GC03
    mov ah, al
    mov al, 3
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; GC01
    mov ah, al
    mov al, 1
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; saved GC index
    out dx, al

    ; Restore Sequencer Memory Mode, Map Mask and index.
    mov dx, 0x3C4
    pop ax                         ; SEQ04
    mov ah, al
    mov al, 4
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; SEQ02
    mov ah, al
    mov al, 2
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    dec dx
    pop ax                         ; saved Sequencer index
    out dx, al

    ; Restore Misc Output, including RAM Enable.
    pop ax
    mov dx, 0x3C2
    out dx, al

    pop ds
    pop bp
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
.load_return:
    ret

; Install the option ROM's built-in 8x16 CP437 font through the same VGA
; plane-2 register path used by AH=11h caller-supplied fonts.  This is firmware
; behavior: the 86C928 device does not infer a mode transition or mutate font
; memory behind the guest's back.
vga_load_default_font:
    push ax
    push bx
    push cx
    push dx
    push bp
    push es
    push cs
    pop es
    mov bp, default_font_8x16
    mov bh, 16
    xor bl, bl
    mov cx, 256
    xor dx, dx
    call vga_load_user_font
    call vga_install_default_font_pointer
    pop es
    pop bp
    pop dx
    pop cx
    pop bx
    pop ax
    ret

vga_install_default_font_pointer:
    push ax
    push ds
    xor ax, ax
    mov ds, ax
    mov word [0x43*4], default_font_8x16
    mov ax, cs
    mov [0x43*4+2], ax
    pop ds
    pop ax
    ret

; AL=10h post-mode-set text-font calls must update text metrics and the
; documented subset of CRTC registers to match BH points per character.
vga_recalculate_text_metrics:
    cmp bh, 1
    jb .metrics_return
    cmp bh, 32
    ja .metrics_return
    push ax
    push bx
    push cx
    push dx
    push si
    push ds

    mov si, bx
    shr si, 8                      ; SI = points / bytes per character

    mov ax, BDA_SEG
    mov ds, ax
    mov al, [0x49]
    cmp al, 2
    je .metrics_text
    cmp al, 3
    jne .metrics_done
.metrics_text:
    mov ax, si
    mov [0x85], ax

    ; Select the current programmed scan-line family from VGA BDA 40:89.
    mov ax, 350
    test byte [0x89], 0x80
    jz .metrics_not_200
    mov ax, 200
    jmp .metrics_have_scanlines
.metrics_not_200:
    test byte [0x89], 0x10
    jz .metrics_have_scanlines
    mov ax, 400
.metrics_have_scanlines:
    xor dx, dx
    mov cx, si
    div cx                          ; AX = whole character rows
    test ax, ax
    jz .metrics_done
    mov bx, ax                     ; BX = row count
    dec ax
    mov [0x84], al                 ; rows minus one

    mov ax, bx
    mul word [0x4A]                ; rows * columns
    shl ax, 1
    mov [0x4C], ax                 ; regeneration-buffer page length
    mov word [0x4E], 0
    mov byte [0x62], 0             ; page 0 active

    ; CRTC text geometry.  Preserve unrelated timing/overflow state.
    mov dx, [0x63]
    mov al, 0x09
    out dx, al
    inc dx
    mov ax, si
    dec al
    out dx, al

    dec dx
    mov al, 0x0A
    out dx, al
    inc dx
    mov ax, si
    sub al, 2
    out dx, al

    dec dx
    mov al, 0x0B
    out dx, al
    inc dx
    mov ax, si
    dec al
    out dx, al

    ; Store/get-cursor uses the BIOS CH=start, CL=end representation.
    mov ax, si
    sub al, 2
    mov ch, al
    mov ax, si
    dec al
    mov cl, al
    mov [0x60], cx

    ; Vertical Display End low byte = rows * points - 1.
    mov ax, bx
    mov cx, si
    mul cx
    dec ax
    mov cl, al
    dec dx                          ; CRTC index port
    mov al, 0x12
    out dx, al
    inc dx
    mov al, cl
    out dx, al

    ; Active-page start address is zero after the post-mode-set service.
    dec dx
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
.metrics_done:
    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
.metrics_return:
    ret

; ---------------------------------------------------------------------------
; INT 10h AH=12h - EGA/VGA alternate functions implemented by this adapter.
; Unsupported subfunctions deliberately return without fabricating success.
; BL=30h is withheld until authentic 200/350/400 text timing + ROM-font
; selection exists; recording a BDA preference without honoring it on mode set
; would be a compatibility shortcut, not an implementation.
; ---------------------------------------------------------------------------
pcb1v_video_alt_functions:
    cmp bl, 0x10
    je .configuration
    cmp bl, 0x31
    je .default_palette
    cmp bl, 0x32
    je .video_addressing
    cmp bl, 0x36
    je .screen_control
    iret

.configuration:
    xor bh, bh                      ; color VGA / no feature connector
    mov bl, 3                       ; 256-KiB-or-more standard VGA memory class
    xor ch, ch
    mov cl, 3
    iret

.default_palette:
    cmp al, 1
    ja .unsupported
    push ax
    push ds
    mov ah, al
    mov ax, BDA_SEG
    mov ds, ax
    test ah, ah
    jnz .disable_palette
    and byte [0x89], 0xF7          ; enable default palette loading
    jmp .palette_done
.disable_palette:
    or byte [0x89], 0x08           ; suppress default palette on later mode sets
.palette_done:
    pop ds
    pop ax
    mov al, 0x12
    iret

.video_addressing:
    cmp al, 1
    ja .unsupported
    push ax
    push dx
    mov ah, al
    mov dx, 0x3CC                  ; Miscellaneous Output read port
    in al, dx
    test ah, ah
    jnz .disable_addressing
    or al, 0x02                    ; enable CPU video-memory decode
    jmp .write_misc
.disable_addressing:
    and al, 0xFD
.write_misc:
    mov dx, 0x3C2                  ; Miscellaneous Output write port
    out dx, al
    pop dx
    pop ax
    mov al, 0x12
    iret

.screen_control:
    cmp al, 1
    ja .unsupported
    push ax
    push bx
    push dx
    mov ah, al
    mov dx, 0x3C0
    in al, dx                      ; current AC index + PAS bit on VGA
    mov bl, al
    test ah, ah
    jnz .screen_off
    or bl, 0x20
    jmp .screen_write
.screen_off:
    and bl, 0xDF
.screen_write:
    mov dx, 0x3DA
    in al, dx                      ; reset AC flip-flop to index phase
    mov dx, 0x3C0
    mov al, bl
    out dx, al
    pop dx
    pop bx
    pop ax
    mov al, 0x12
    iret
.unsupported:
    iret

; ---------------------------------------------------------------------------
; INT 10h AH=1Bh - VGA functionality/state information.
; BX must be 0000h.  ES:DI points to the 64-byte state buffer.
; ---------------------------------------------------------------------------
pcb1v_video_state_info:
    cmp bx, 0
    jne .unsupported
    push bx
    push cx
    push dx
    push si
    push di
    push ds

    mov si, di
    xor ax, ax
    mov cx, 32
    cld
    rep stosw                       ; zero all reserved/unsupported fields
    mov di, si

    mov word [es:di+0x00], video_static_functionality
    mov word [es:di+0x02], cs

    mov ax, BDA_SEG
    mov ds, ax
    mov al, [0x49]
    mov [es:di+0x04], al
    mov ax, [0x4A]
    mov [es:di+0x05], ax
    mov ax, [0x4C]
    mov [es:di+0x07], ax
    mov ax, [0x4E]
    mov [es:di+0x09], ax

    push si
    push di
    mov si, 0x50
    add di, 0x0B
    mov cx, 8
.copy_cursors:
    mov ax, [si]
    mov [es:di], ax
    add si, 2
    add di, 2
    loop .copy_cursors
    pop di
    pop si

    mov ax, [0x60]
    mov [es:di+0x1B], ax
    mov al, [0x62]
    mov [es:di+0x1D], al
    mov ax, [0x63]
    mov [es:di+0x1E], ax
    mov al, [0x65]
    mov [es:di+0x20], al
    mov al, [0x66]
    mov [es:di+0x21], al
    mov al, [0x84]
    mov [es:di+0x22], al
    mov ax, [0x85]
    mov [es:di+0x23], ax
    mov byte [es:di+0x25], 0x08     ; active DCC: VGA color analog
    mov byte [es:di+0x26], 0x00

    mov al, [0x49]
    cmp al, 4
    jb .info_text
    cmp al, 5
    jbe .info_cga4
    cmp al, 6
    je .info_cga2
    cmp al, 7
    je .info_text
    cmp al, 0x0D
    je .info_mode0d
    cmp al, 0x0E
    je .info_mode0e
    cmp al, 0x0F
    je .info_mode0f
    cmp al, 0x10
    je .info_mode10
    cmp al, 0x11
    je .info_mode11
    cmp al, 0x12
    je .info_mode12
    cmp al, 0x13
    je .info_mode13
.info_text:
    mov word [es:di+0x27], 16
    mov byte [es:di+0x29], 8
    mov byte [es:di+0x2A], 2        ; 400 scan lines
    jmp .mode_fields_done
.info_cga4:
    mov word [es:di+0x27], 4
    mov byte [es:di+0x29], 1
    mov byte [es:di+0x2A], 0        ; 200 scan lines
    jmp .mode_fields_done
.info_cga2:
    mov word [es:di+0x27], 2
    mov byte [es:di+0x29], 1
    mov byte [es:di+0x2A], 0
    jmp .mode_fields_done
.info_mode0d:
    mov word [es:di+0x27], 16
    mov byte [es:di+0x29], 8
    mov byte [es:di+0x2A], 0
    jmp .mode_fields_done
.info_mode0e:
    mov word [es:di+0x27], 16
    mov byte [es:di+0x29], 4
    mov byte [es:di+0x2A], 0
    jmp .mode_fields_done
.info_mode0f:
    mov word [es:di+0x27], 2
    mov byte [es:di+0x29], 2
    mov byte [es:di+0x2A], 1        ; 350 scan lines
    jmp .mode_fields_done
.info_mode10:
    mov word [es:di+0x27], 16
    mov byte [es:di+0x29], 2
    mov byte [es:di+0x2A], 1
    jmp .mode_fields_done
.info_mode11:
    mov word [es:di+0x27], 2
    mov byte [es:di+0x29], 1
    mov byte [es:di+0x2A], 3
    jmp .mode_fields_done
.info_mode12:
    mov word [es:di+0x27], 16
    mov byte [es:di+0x29], 1
    mov byte [es:di+0x2A], 3        ; 480 scan lines
    jmp .mode_fields_done
.info_mode13:
    mov word [es:di+0x27], 256
    mov byte [es:di+0x29], 1
    mov byte [es:di+0x2A], 0        ; 200 scan lines
.mode_fields_done:

    ; Decode sequencer character-map select into primary/secondary 0..7 blocks.
    ; Preserve the caller-visible Sequencer Address register while probing 03h.
    mov dx, 0x3C4
    in al, dx
    mov ch, al
    mov al, 3
    out dx, al
    inc dx
    in al, dx
    mov ah, al
    ; Primary map number F2:F1:F0 = SR3 bits 1:0:4.
    mov al, ah
    and al, 0x02
    shl al, 1                       ; SR3 bit1 -> result bit2
    mov bl, ah
    and bl, 0x01
    shl bl, 1                       ; SR3 bit0 -> result bit1
    or al, bl
    mov bl, ah
    and bl, 0x10
    shr bl, 1
    shr bl, 1
    shr bl, 1
    shr bl, 1                       ; SR3 bit4 -> result bit0
    or al, bl
    mov [es:di+0x2B], al

    ; Secondary map number F2:F1:F0 = SR3 bits 3:2:5.
    mov al, ah
    and al, 0x08
    shr al, 1                       ; SR3 bit3 -> result bit2
    mov bl, ah
    and bl, 0x04
    shr bl, 1                       ; SR3 bit2 -> result bit1
    or al, bl
    mov bl, ah
    and bl, 0x20
    shr bl, 1
    shr bl, 1
    shr bl, 1
    shr bl, 1
    shr bl, 1                       ; SR3 bit5 -> result bit0
    or al, bl
    mov [es:di+0x2C], al
    dec dx                          ; back to 03C4h
    mov al, ch
    out dx, al                      ; restore original Sequencer index

    ; Dynamic miscellaneous state flags.  Only state we genuinely maintain is
    ; reported; unsupported capability flags remain zero.
    xor bl, bl
    mov al, [0x87]
    test al, 1                      ; bit0=1 inhibits cursor emulation
    jnz .no_cursor_emulation
    or bl, 0x10                    ; report emulation when bit0 is clear
.no_cursor_emulation:
    mov al, [0x89]
    test al, 0x08
    jz .palette_enabled
    or bl, 0x08
.palette_enabled:
    test al, 0x02
    jz .not_gray
    or bl, 0x02
.not_gray:

    ; Attribute Mode Control bit3 selects blink vs background intensity.
    mov dx, [0x63]
    add dx, 6
    in al, dx
    mov dx, 0x3C0
    in al, dx
    mov bh, al                      ; preserve AC index/PAS
    mov dx, [0x63]
    add dx, 6
    in al, dx
    mov dx, 0x3C0
    mov al, 0x30
    out dx, al
    mov dx, 0x3C1
    in al, dx
    test al, 0x08
    jz .no_blink
    or bl, 0x20
.no_blink:
    mov dx, [0x63]
    add dx, 6
    in al, dx
    mov dx, 0x3C0
    mov al, bh
    out dx, al

    mov [es:di+0x2D], bl
    mov byte [es:di+0x31], 3        ; standard VGA 256-KiB-or-more memory class
    mov byte [es:di+0x32], 0        ; no active dynamic override table
    mov byte [es:di+0x33], 0x04     ; color display, no unsupported status bits

    pop ds
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    mov al, 0x1B
    iret
.unsupported:
    xor al, al
    iret

; ---------------------------------------------------------------------------
; INT 10h AH=1Ch - IBM VGA save/restore state.
;
; Header is 20h bytes.  It contains word offsets to the selected sections:
; +00 hardware, +02 BIOS data, +04 DAC/color; remaining header words zero.
; ---------------------------------------------------------------------------
pcb1v_video_save_restore:
    cmp al, 0
    je .size
    cmp al, 1
    je .save
    cmp al, 2
    je .restore
    iret

.size:
    push cx
    push dx
    mov dx, cx
    mov bx, VGA_STATE_HEADER
    test dx, 1
    jz .size_no_hw
    add bx, VGA_HW_STATE_SIZE
.size_no_hw:
    test dx, 2
    jz .size_no_bda
    add bx, VGA_BDA_STATE_SIZE
.size_no_bda:
    test dx, 4
    jz .size_no_dac
    add bx, VGA_DAC_STATE_SIZE
.size_no_dac:
    add bx, 63
    shr bx, 1
    shr bx, 1
    shr bx, 1
    shr bx, 1
    shr bx, 1
    shr bx, 1
    pop dx
    pop cx
    mov al, 0x1C
    iret

.save:
    push bx
    push cx
    push dx
    push si
    push di
    push bp
    push ds
    mov bp, bx                      ; buffer base
    mov si, cx                      ; requested-state mask

    mov di, bx
    xor ax, ax
    mov cx, 16
    cld
    rep stosw                       ; clear 20h-byte header
    mov dx, VGA_STATE_HEADER        ; next section offset

    test si, 1
    jz .save_no_hw
    mov [es:bp+0x00], dx
    mov di, bp
    add di, dx
    call vga_save_hardware_state
    add dx, VGA_HW_STATE_SIZE
.save_no_hw:
    test si, 2
    jz .save_no_bda
    mov [es:bp+0x02], dx
    mov di, bp
    add di, dx
    call vga_save_bda_state
    add dx, VGA_BDA_STATE_SIZE
.save_no_bda:
    test si, 4
    jz .save_done
    mov [es:bp+0x04], dx
    mov di, bp
    add di, dx
    call vga_save_dac_state
.save_done:
    pop ds
    pop bp
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    mov al, 0x1C
    iret

.restore:
    push bx
    push cx
    push dx
    push si
    push di
    push bp
    push ds
    mov bp, bx
    mov si, cx

    ; Restore DAC/color first, BDA second, and hardware selectors last.
    ; This leaves the final AC/CRTC/SEQ/GC index registers exactly as saved.
    test si, 4
    jz .restore_no_dac
    mov di, [es:bp+0x04]
    test di, di
    jz .restore_no_dac
    add di, bp
    call vga_restore_dac_state
.restore_no_dac:
    test si, 2
    jz .restore_no_bda
    mov di, [es:bp+0x02]
    test di, di
    jz .restore_no_bda
    add di, bp
    call vga_restore_bda_state
.restore_no_bda:
    test si, 1
    jz .restore_done
    mov di, [es:bp+0x00]
    test di, di
    jz .restore_done
    add di, bp
    call vga_restore_hardware_state
.restore_done:
    pop ds
    pop bp
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    mov al, 0x1C
    iret

; ES:DI = hardware-state destination (46h bytes).
vga_save_hardware_state:
    push ax
    push bx
    push cx
    push dx
    push si
    push ds

    ; Index/general registers.
    mov dx, 0x3C4
    in al, dx
    mov [es:di+0x00], al

    mov ax, BDA_SEG
    mov ds, ax
    mov dx, [0x63]
    in al, dx
    mov [es:di+0x01], al
    mov ax, dx
    mov [es:di+0x40], ax

    mov dx, 0x3CE
    in al, dx
    mov [es:di+0x02], al
    mov dx, 0x3C0
    in al, dx
    mov [es:di+0x03], al
    mov dx, 0x3CA
    in al, dx
    mov [es:di+0x04], al

    ; Sequencer 1..4.
    mov dx, 0x3C4
    mov si, 1
    mov bx, 0x05
    mov cx, 4
.save_seq:
    mov ax, si
    out dx, al
    inc dx
    in al, dx
    dec dx
    mov [es:di+bx], al
    inc bx
    inc si
    loop .save_seq

    mov dx, 0x3CC
    in al, dx
    mov [es:di+0x09], al

    ; CRTC 00h..18h.
    mov ax, BDA_SEG
    mov ds, ax
    mov dx, [0x63]
    xor si, si
    mov bx, 0x0A
    mov cx, 25
.save_crtc:
    mov ax, si
    out dx, al
    inc dx
    in al, dx
    dec dx
    mov [es:di+bx], al
    inc bx
    inc si
    loop .save_crtc

    ; Attribute Controller 00h..13h.
    xor si, si
    mov bx, 0x23
    mov cx, 20
.save_attr:
    push dx
    mov dx, [0x63]
    add dx, 6
    in al, dx
    mov dx, 0x3C0
    mov ax, si
    or al, 0x20
    out dx, al
    mov dx, 0x3C1
    in al, dx
    pop dx
    mov [es:di+bx], al
    inc bx
    inc si
    loop .save_attr

    ; Graphics Controller 00h..08h.
    mov dx, 0x3CE
    xor si, si
    mov bx, 0x37
    mov cx, 9
.save_gc:
    mov ax, si
    out dx, al
    inc dx
    in al, dx
    dec dx
    mov [es:di+bx], al
    inc bx
    inc si
    loop .save_gc

    ; Capture the four VGA latches using write mode 1.  This intentionally
    ; disturbs A000:FFFF, matching the documented warning that save-state may
    ; alter current video state; applications are expected to restore it.
    mov dx, 0x3CC
    in al, dx
    or al, 0x02
    mov dx, 0x3C2
    out dx, al

    mov dx, 0x3C4
    mov al, 2
    out dx, al
    inc dx
    mov al, 0x0F
    out dx, al
    dec dx
    mov al, 4
    out dx, al
    inc dx
    mov al, 0x06
    out dx, al

    mov dx, 0x3CE
    mov al, 5
    out dx, al
    inc dx
    mov al, 1                       ; write mode 1
    out dx, al
    dec dx
    mov al, 6
    out dx, al
    inc dx
    mov al, 0x05                    ; A0000 64-KiB graphics aperture
    out dx, al

    mov ax, VGA_SEG
    mov ds, ax
    mov byte [0xFFFF], 0            ; CPU data ignored in write mode 1

    mov dx, 0x3CE
    xor si, si
    mov bx, 0x42
    mov cx, 4
.save_latches:
    mov al, 4
    out dx, al
    inc dx
    mov ax, si
    out dx, al
    dec dx
    mov al, [0xFFFF]
    mov [es:di+bx], al
    inc bx
    inc si
    loop .save_latches

    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; ES:DI = hardware-state source.
vga_restore_hardware_state:
    push ax
    push bx
    push cx
    push dx
    push si
    push ds

    ; First reconstruct the four system latches in a scratch byte.  Enable VGA
    ; memory decode temporarily even if the saved state had it disabled.
    mov al, [es:di+0x09]
    or al, 0x02
    mov dx, 0x3C2
    out dx, al

    mov dx, 0x3C4
    mov al, 4
    out dx, al
    inc dx
    mov al, 0x06
    out dx, al

    mov dx, 0x3CE
    mov al, 1
    out dx, al
    inc dx
    xor al, al
    out dx, al
    dec dx
    mov al, 3
    out dx, al
    inc dx
    xor al, al
    out dx, al
    dec dx
    mov al, 5
    out dx, al
    inc dx
    xor al, al                      ; write mode 0
    out dx, al
    dec dx
    mov al, 6
    out dx, al
    inc dx
    mov al, 0x05
    out dx, al
    dec dx
    mov al, 8
    out dx, al
    inc dx
    mov al, 0xFF
    out dx, al

    mov ax, VGA_SEG
    mov ds, ax
    xor bx, bx
    mov cx, 4
.restore_latch_bytes:
    mov dx, 0x3C4
    mov al, 2
    out dx, al
    inc dx
    mov al, 1
    push cx
    mov cl, bl
    shl al, cl
    pop cx
    out dx, al
    mov al, [es:di+bx+0x42]
    mov [0xFFFF], al
    inc bl
    loop .restore_latch_bytes

    ; Reading scratch once loads all four restored plane bytes into the latches.
    mov dx, 0x3CE
    mov al, 4
    out dx, al
    inc dx
    xor al, al
    out dx, al
    mov al, [0xFFFF]

    ; Restore saved Miscellaneous Output, then standard VGA register groups.
    mov al, [es:di+0x09]
    mov dx, 0x3C2
    out dx, al

    ; Sequencer: synchronous reset around registers 1..4.
    mov dx, 0x3C4
    xor al, al
    out dx, al
    inc dx
    mov al, 1
    out dx, al
    dec dx
    mov si, 1
    mov bx, 0x05
    mov cx, 4
.restore_seq:
    mov ax, si
    out dx, al
    inc dx
    mov al, [es:di+bx]
    out dx, al
    dec dx
    inc bx
    inc si
    loop .restore_seq
    xor al, al
    out dx, al
    inc dx
    mov al, 3
    out dx, al

    ; CRTC, using saved base address.  Unlock protected indices 0..7 first.
    mov dx, [es:di+0x40]
    mov al, 0x11
    out dx, al
    inc dx
    in al, dx
    and al, 0x7F
    out dx, al
    dec dx
    xor si, si
    mov bx, 0x0A
    mov cx, 25
.restore_crtc:
    mov ax, si
    out dx, al
    inc dx
    mov al, [es:di+bx]
    out dx, al
    dec dx
    inc bx
    inc si
    loop .restore_crtc

    ; Graphics Controller.
    mov dx, 0x3CE
    xor si, si
    mov bx, 0x37
    mov cx, 9
.restore_gc:
    mov ax, si
    out dx, al
    inc dx
    mov al, [es:di+bx]
    out dx, al
    dec dx
    inc bx
    inc si
    loop .restore_gc

    ; Attribute Controller.
    xor si, si
    mov bx, 0x23
    mov cx, 20
.restore_attr:
    mov dx, [es:di+0x40]
    add dx, 6
    in al, dx
    mov dx, 0x3C0
    mov ax, si
    or al, 0x20
    out dx, al
    mov al, [es:di+bx]
    out dx, al
    inc bx
    inc si
    loop .restore_attr

    ; Restore Feature Control using the write alias paired with the saved CRTC.
    mov dx, [es:di+0x40]
    add dx, 6
    mov al, [es:di+0x04]
    out dx, al

    ; Finally restore the three indexed-register selectors and AC selector/PAS.
    mov dx, 0x3C4
    mov al, [es:di+0x00]
    out dx, al
    mov dx, [es:di+0x40]
    mov al, [es:di+0x01]
    out dx, al
    mov dx, 0x3CE
    mov al, [es:di+0x02]
    out dx, al
    mov dx, [es:di+0x40]
    add dx, 6
    in al, dx
    mov dx, 0x3C0
    mov al, [es:di+0x03]
    out dx, al

    pop ds
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

vga_save_bda_state:
    push ax
    push cx
    push si
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov si, 0x49
    mov cx, VGA_BDA_STATE_SIZE
    cld
    rep movsb
    pop ds
    pop si
    pop cx
    pop ax
    ret

vga_restore_bda_state:
    push ax
    push cx
    push si
    push di
    push ds
    push es
    mov si, di                      ; source offset in caller ES
    mov ax, es
    mov ds, ax
    mov ax, BDA_SEG
    mov es, ax
    mov di, 0x49
    mov cx, VGA_BDA_STATE_SIZE
    cld
    rep movsb
    pop es
    pop ds
    pop di
    pop si
    pop cx
    pop ax
    ret

; ES:DI = 304h-byte DAC/color-state destination.
vga_save_dac_state:
    push ax
    push bx
    push cx
    push dx

    mov dx, 0x3C7
    in al, dx
    mov [es:di+0x00], al            ; DAC state: 00 write / 03 read
    mov dx, 0x3C8
    in al, dx
    mov [es:di+0x01], al            ; current DAC address register
    mov dx, 0x3C6
    in al, dx
    mov [es:di+0x02], al

    ; Save all 256 DAC entries.
    mov dx, 0x3C7
    xor al, al
    out dx, al
    mov dx, 0x3C9
    mov bx, 0x03
    mov cx, 768
.save_dac_loop:
    in al, dx
    mov [es:di+bx], al
    inc bx
    loop .save_dac_loop

    ; Attribute Color Select register 14h.
    mov dx, 0x3DA
    in al, dx
    mov dx, 0x3C0
    mov al, 0x34
    out dx, al
    mov dx, 0x3C1
    in al, dx
    mov [es:di+0x303], al

    pop dx
    pop cx
    pop bx
    pop ax
    ret

; ES:DI = 304h-byte DAC/color-state source.
vga_restore_dac_state:
    push ax
    push bx
    push cx
    push dx

    mov dx, 0x3C6
    mov al, [es:di+0x02]
    out dx, al

    mov dx, 0x3C8
    xor al, al
    out dx, al
    mov dx, 0x3C9
    mov bx, 0x03
    mov cx, 768
.restore_dac_loop:
    mov al, [es:di+bx]
    out dx, al
    inc bx
    loop .restore_dac_loop

    ; Restore Attribute Color Select 14h.
    mov dx, 0x3DA
    in al, dx
    mov dx, 0x3C0
    mov al, 0x34
    out dx, al
    mov al, [es:di+0x303]
    out dx, al

    ; Restore the recorded DAC access mode/address last.
    mov al, [es:di+0x00]
    and al, 3
    cmp al, 3
    jne .dac_write_state
    mov dx, 0x3C7
    mov al, [es:di+0x01]
    out dx, al
    jmp .dac_state_done
.dac_write_state:
    mov dx, 0x3C8
    mov al, [es:di+0x01]
    out dx, al
.dac_state_done:
    pop dx
    pop cx
    pop bx
    pop ax
    ret

; IBM standard-mode capability map.  Bytes 0-2 follow the VGA BIOS
; functionality table described by the Video Seven Technical Reference,
; pp. 4-25..4-27.  Reserved modes 08h-0Ch remain reserved even though their
; bit positions participate in the historical packed capability map.
video_static_functionality:
    db 0xFF,0xE0,0x0F              ; standard modes 00h-07h and 0Dh-13h
    db 0,0,0,0
    db 0x07                         ; 200-, 350-, and 400-line text families
    db 8                            ; eight VGA character-map blocks in hardware
    db 2                            ; two simultaneously selectable maps
    db 0x4C                         ; user font + default/color palette control
    db 0x0E                         ; save/restore, blink/intensity, DCC query
    db 0,0
    db 0                            ; no active override extensions advertised
    db 0

; Video Save Pointer table at 0040:A8.  Required pointers are present; optional
; override pointers remain null until their semantics are actually implemented.
video_save_pointer_table:
    dw video_parameter_table, VGA_ROM_SEG
    dw 0,0                          ; dynamic save area
    dw 0,0                          ; alpha character override
    dw 0,0                          ; graphics character override
    dw video_secondary_save_table, VGA_ROM_SEG
    dw 0,0
    dw 0,0

video_secondary_save_table:
    dw 0x001A
    dw video_dcc_table, VGA_ROM_SEG
    dw 0,0                          ; alpha override 0
    dw 0,0                          ; user palette profile 0
    dw 0,0
    dw 0,0
    dw 0,0

; Minimal, truthful DCC table: one active color-VGA pairing.
video_dcc_table:
    db 1, 1, 8, 0                 ; one entry, VGA display-code maximum
    db 0x00, 0x08                    ; no secondary display / color VGA

; IBM-format 64-byte Video Parameter Table elements, indexed by mode number.
; Format and mode geometry: IBM VGA/XGA Technical Reference, INT 10h video
; parameter/save-pointer descriptions; register ordering: Video Seven VGA
; Technical Reference, Tables 4-8 and 4-9.  Entries intentionally duplicate
; the mode-programming tables in stealthpro_vgabios.asm so BIOS clients see
; exactly the state this ROM installs.  Only IBM-reserved modes 08h-0Ch are
; zero-filled.
video_parameter_table:
%macro EMIT_VPT_MODE01 0
    db 40,24,16
    dw 0x0800
    db 0x08,0x03,0x00,0x02,0x67
    db 0x2D,0x27,0x28,0x90,0x2B,0xA0,0xBF,0x1F,0x00,0x4F,0x0D,0x0E,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x14,0x1F,0x96,0xB9,0xA3,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x0C,0x00,0x0F,0x08
    db 0x00,0x00,0x00,0x00,0x00,0x10,0x0E,0x0F,0xFF
%endmacro
vpt_mode00:
    EMIT_VPT_MODE01
vpt_mode01:
    EMIT_VPT_MODE01

%macro EMIT_VPT_MODE03 0
    db 80,24,16
    dw 0x1000
    db 0x00,0x03,0x00,0x02,0x67
    db 0x5F,0x4F,0x50,0x82,0x55,0x81,0xBF,0x1F,0x00,0x4F,0x0D,0x0E,0x00,0x00,0x00,0x50,0x9C,0x8E,0x8F,0x28,0x1F,0x96,0xB9,0xA3,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x0C,0x00,0x0F,0x08
    db 0x00,0x00,0x00,0x00,0x00,0x10,0x0E,0x00,0xFF
%endmacro
vpt_mode02:
    EMIT_VPT_MODE03
vpt_mode03:
    EMIT_VPT_MODE03

%macro EMIT_VPT_MODE05 0
    db 40,24,8
    dw 0x4000
    db 0x09,0x03,0x00,0x02,0x63
    db 0x2D,0x27,0x28,0x90,0x2B,0x80,0xBF,0x1F,0x00,0xC1,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x14,0x00,0x96,0xB9,0xA2,0xFF
    db 0x00,0x13,0x15,0x17,0x02,0x04,0x06,0x07,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x01,0x00,0x03,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x30,0x0F,0x0F,0xFF
%endmacro
vpt_mode04:
    EMIT_VPT_MODE05
vpt_mode05:
    EMIT_VPT_MODE05

vpt_mode06:
    db 80,24,8
    dw 0x4000
    db 0x01,0x01,0x00,0x06,0x63
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0xC1,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x00,0x96,0xB9,0xC2,0xFF
    db 0x00,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x17,0x01,0x00,0x01,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x0D,0x0F,0xFF

vpt_mode07:
    db 80,24,16
    dw 0x1000
    db 0x00,0x03,0x00,0x02,0x66
    db 0x5F,0x4F,0x50,0x82,0x55,0x81,0xBF,0x1F,0x00,0x4F,0x0D,0x0E,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x0F,0x96,0xB9,0xA3,0xFF
    db 0x00,0x08,0x08,0x08,0x08,0x08,0x08,0x08,0x10,0x18,0x18,0x18,0x18,0x18,0x18,0x18,0x0E,0x00,0x0F,0x08
    db 0x00,0x00,0x00,0x00,0x00,0x10,0x0A,0x0F,0xFF

    times 5*64 db 0                 ; modes 08h-0Ch reserved by IBM

vpt_mode0d:
    db 40,24,8
    dw 0x2000
    db 0x09,0x0F,0x00,0x06,0x63
    db 0x2D,0x27,0x28,0x90,0x2B,0x80,0xBF,0x1F,0x00,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x14,0x00,0x96,0xB9,0xE3,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x01,0x00,0x0F,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF

vpt_mode0e:
    db 80,24,8
    dw 0x4000
    db 0x01,0x0F,0x00,0x06,0x63
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x00,0x96,0xB9,0xE3,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x01,0x00,0x0F,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF

vpt_mode0f:
    db 80,24,14
    dw 0x8000
    db 0x01,0x0F,0x00,0x06,0xA3
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0x83,0x85,0x5D,0x28,0x0F,0x63,0xBA,0xE3,0xFF
    db 0x00,0x08,0x00,0x00,0x18,0x18,0x00,0x00,0x00,0x08,0x00,0x00,0x00,0x18,0x00,0x00,0x01,0x00,0x01,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF

vpt_mode10:
    db 80,24,14
    dw 0x8000
    db 0x01,0x0F,0x00,0x06,0xA3
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0x83,0x85,0x5D,0x28,0x0F,0x63,0xBA,0xE3,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x01,0x00,0x0F,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF

vpt_mode11:
    db 80,29,16
    dw 0x9600
    db 0x01,0x0F,0x00,0x06,0xE3
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0x0B,0x3E,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0xEA,0x8C,0xDF,0x28,0x00,0xE7,0x04,0xE3,0xFF
    db 0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x00,0x3F,0x01,0x00,0x0F,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF

vpt_mode12:
    db 80,29,16
    dw 0x9600
    db 0x01,0x0F,0x00,0x06,0xE3
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0x0B,0x3E,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0xEA,0x8C,0xDF,0x28,0x00,0xE7,0x04,0xE3,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x14,0x07,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,0x01,0x00,0x0F,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x00,0x05,0x0F,0xFF

vpt_mode13:
    db 40,24,8
    dw 0xFA00
    db 0x01,0x0F,0x00,0x0E,0x63
    db 0x5F,0x4F,0x50,0x82,0x54,0x80,0xBF,0x1F,0x00,0x41,0x00,0x00,0x00,0x00,0x00,0x00,0x9C,0x8E,0x8F,0x28,0x40,0x96,0xB9,0xA3,0xFF
    db 0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,0x41,0x00,0x0F,0x00
    db 0x00,0x00,0x00,0x00,0x00,0x40,0x05,0x0F,0xFF
