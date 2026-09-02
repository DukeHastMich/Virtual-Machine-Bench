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

start:
    cli
    cld
    xor ax, ax
    mov ds, ax
    mov es, ax
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
    mov word [0x15*4], int15
    mov word [0x16*4], int16
    mov word [0x19*4], int19
    mov word [0x1A*4], int1a

    ; BIOS data area.
    mov ax, BDA_SEG
    mov es, ax
    xor di, di
    xor ax, ax
    mov cx, 128
    rep stosw
    mov word [es:0x0E], 0x9FC0       ; 1 KiB EBDA segment
    mov word [es:0x10], 0x0061       ; CGA 80x25 plus two floppy drives
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
    mov al, 0xBC                    ; timer, keyboard, cascade unmasked
    out 0x21, al
    mov al, 0xFF
    out 0xA1, al
    mov al, 0x36
    out 0x43, al
    xor al, al
    out 0x40, al
    out 0x40, al

    ; Detect the ATA master and cache a CHS geometry that matches the
    ; controller's 16-head/63-sector translation.
    call ata_detect

    call reset_cga_start
    call clear_screen
    push cs
    pop ds                         ; embedded POST strings live in the ROM segment
    mov si, logo
    call print
    mov si, post_text
    call print
    sti
    int 0x19
.halt: hlt
    jmp .halt

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

; DS:SI zero-terminated ROM string, direct standard color text memory.
print:
    push ax
    push bx
    push dx
    push di
    push es
    mov ax, BDA_SEG
    mov es, ax
    mov di, [es:0x50]
    mov ax, VIDEO_SEG
    mov es, ax
.next:
    lodsb
    test al, al
    J286_E .done
    cmp al, 13
    J286_E .next
    cmp al, 10
    J286_NE .put
    mov ax, di
    xor dx, dx
    mov bx, 160
    div bx
    inc ax
    mul bx
    mov di, ax
    jmp .next
.put:
    mov ah, 0x07
    stosw
    jmp .next
.done:
    mov ax, BDA_SEG
    mov es, ax
    mov [es:0x50], di
    pop es
    pop di
    pop dx
    pop bx
    pop ax
    ret

default_int: iret

; Temporary visible boot-path tracer.
; AL = marker character, DI = byte offset in B800 text memory.
; Preserves AX, BX and ES and does not intentionally modify FLAGS.
boot_debug_mark:
    push ax
    push bx
    push es
    mov bl, al
    mov bh, 0x0E                    ; bright yellow on black
    mov ax, VIDEO_SEG
    mov es, ax
    mov [es:di], bx
    pop es
    pop bx
    pop ax
    ret

int08:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    inc word [0x6C]
    J286_NE .eoi
    inc word [0x6E]
.eoi:
    mov al, 0x20
    out 0x20, al
    pop ds
    pop ax
    iret

int09:
    push ax
    push bx
    push cx
    push dx
    push ds
    in al, 0x60
    mov dl, al
    mov ax, BDA_SEG
    mov ds, ax
    mov al, dl
    cmp al, 0xE0
    J286_NE .not_extended
    or byte [0x96], 2
    jmp .eoi
.not_extended:
    test al, 0x80
    J286_E .make_code
    jmp .break_code
.make_code:
    cmp al, 0x2A
    J286_NE .not_lshift
    jmp .left_shift_down
.not_lshift:
    cmp al, 0x36
    J286_NE .not_rshift
    jmp .right_shift_down
.not_rshift:
    cmp al, 0x1D
    J286_NE .not_ctrl
    jmp .ctrl_down
.not_ctrl:
    cmp al, 0x38
    J286_NE .not_alt
    jmp .alt_down
.not_alt:
    cmp al, 0x3A
    J286_NE .not_caps
    jmp .caps_toggle
.not_caps:
    cmp al, 0x45
    J286_NE .not_num
    jmp .num_toggle
.not_num:
    cmp al, 0x46
    J286_NE .not_scroll
    jmp .scroll_toggle
.not_scroll:
    cmp al, 0x3A
    J286_BE .translate
    cmp al, 0x44                   ; F1-F10
    J286_A .not_f1_f10
    jmp .enqueue_scan_only
.not_f1_f10:
    cmp al, 0x47
    J286_AE .maybe_navigation
    jmp .eoi
.maybe_navigation:
    cmp al, 0x53                   ; keypad/navigation including Delete
    J286_A .not_navigation
    jmp .enqueue_scan_only
.not_navigation:
    cmp al, 0x57                   ; F11
    J286_NE .not_f11
    jmp .enqueue_scan_only
.not_f11:
    cmp al, 0x58                   ; F12
    J286_E .enqueue_scan_only
    jmp .eoi
.translate:
    xor bx, bx
    mov bl, al
    mov dl, [cs:key_ascii+bx]
    mov cl, [0x17]
    test cl, 3
    J286_E .caps
    mov dl, [cs:key_shift_ascii+bx]
.caps:
    test cl, 0x40
    J286_E .control
    cmp dl, 'A'
    J286_B .caps_lower
    cmp dl, 'Z'
    J286_A .caps_lower
    or dl, 0x20
    jmp .control
.caps_lower:
    cmp dl, 'a'
    J286_B .control
    cmp dl, 'z'
    J286_A .control
    and dl, 0xDF
.control:
    test cl, 4
    J286_E .alternate
    mov dh, dl
    and dh, 0xDF
    cmp dh, 'A'
    J286_B .alternate
    cmp dh, 'Z'
    J286_A .alternate
    mov dl, dh
    sub dl, 0x40
.alternate:
    test cl, 8
    J286_E .enqueue
    xor dl, dl
.enqueue:
    mov ah, al
    mov al, dl
.queue_word:
    mov bx, [0x1C]                 ; tail offset
    mov dx, bx
    add dx, 2
    cmp dx, 0x3E
    J286_B .check_full
    mov dx, 0x1E
.check_full:
    cmp dx, [0x1A]                 ; full if next tail equals head
    J286_E .eoi
    mov [bx], ax
    mov [0x1C], dx
    jmp .eoi
.enqueue_scan_only:
    mov ah, al
    xor al, al
    jmp .queue_word
.left_shift_down:
    or byte [0x17], 2
    jmp .eoi
.right_shift_down:
    or byte [0x17], 1
    jmp .eoi
.ctrl_down:
    or byte [0x17], 4
    jmp .eoi
.alt_down:
    or byte [0x17], 8
    jmp .eoi
.caps_toggle:
    xor byte [0x17], 0x40
    jmp .eoi
.num_toggle:
    xor byte [0x17], 0x20
    jmp .eoi
.scroll_toggle:
    xor byte [0x17], 0x10
    jmp .eoi
.break_code:
    and al, 0x7F
    cmp al, 0x2A
    J286_E .left_shift_up
    cmp al, 0x36
    J286_E .right_shift_up
    cmp al, 0x1D
    J286_E .ctrl_up
    cmp al, 0x38
    J286_E .alt_up
    jmp .eoi
.left_shift_up:
    and byte [0x17], 0xFD
    jmp .eoi
.right_shift_up:
    and byte [0x17], 0xFE
    jmp .eoi
.ctrl_up:
    and byte [0x17], 0xFB
    jmp .eoi
.alt_up:
    and byte [0x17], 0xF7
.eoi:
    mov al, 0x20
    out 0x20, al
    pop ds
    pop dx
    pop cx
    pop bx
    pop ax
    iret

; IBM set-1 make-code translation used by the ROM IRQ1 handler.
key_ascii:
    db 0,27,'1234567890-=',8,9
    db 'qwertyuiop[]',13,0,'asdfghjkl',59,39,96,0,92
    db 'zxcvbnm,./',0,'*',0,' ',0,0,0,0,0,0,0,0,0,0
key_shift_ascii:
    db 0,27,'!@#$%^&*()_+',8,9
    db 'QWERTYUIOP{}',13,0,'ASDFGHJKL:',34,126,0,124
    db 'ZXCVBNM<>?',0,'*',0,' ',0,0,0,0,0,0,0,0,0,0

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
    mov ah, 7
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
    mov dx, 0x184F
    int 0x10
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
    push ax
    push di
    mov di, 0x05A0
    mov al, 'I'                     ; entered INT 13h handler
    call boot_debug_mark
    pop di
    pop ax

    push bp
    mov bp, sp
    sub sp, 2
    mov [ss:bp-2], dx               ; preserve the original drive number

    push ax
    push di
    mov di, 0x05A2
    mov al, 'P'                     ; INT 13h stack frame/prologue completed
    call boot_debug_mark
    pop di
    pop ax

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
    push ax
    push di
    mov di, 0x05A4
    mov al, 'D'                     ; AH=02 dispatch reached
    call boot_debug_mark
    pop di
    pop ax

    mov dx, [ss:bp-2]
    test dl, 0x80
    J286_NE .read_hard_disk
    cmp dl, 2
    J286_AE .drive_not_ready

    push ax
    push di
    mov di, 0x05A6
    mov al, 'B'                     ; about to CALL floppy_read
    call boot_debug_mark
    pop di
    pop ax

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

; AT system services used during DOS kernel initialization.
int15:
    push bp
    mov bp, sp
    cmp ah, 0x88                   ; extended memory above 1 MiB, in KiB
    J286_E .extended_memory
    mov ah, 0x86                   ; unsupported function
    or word [ss:bp+6], 1
    pop bp
    iret
.extended_memory:
    mov ax, 0x3C00                 ; 15 MiB = 15360 KiB
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret

; Read AL sectors from floppy CHS CH/DH/CL into ES:BX via DMA channel 2.
floppy_read:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    push ds

    push ax
    push di
    mov di, 0x05A8
    mov al, 'R'                     ; entered floppy_read
    call boot_debug_mark
    pop di
    pop ax

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

    push ax
    push di
    mov di, 0x05AA
    mov al, 'C'                     ; FDC command completed
    call boot_debug_mark
    pop di
    pop ax

    ; Copy the bounce sector to the caller's buffer.
    push ds
    mov ax, 0x9FC0
    mov ds, ax
    xor si, si
    mov cx, 256
    cld
    rep movsw
    pop ds

    push ax
    push di
    mov di, 0x05AC
    mov al, 'M'                     ; bounce sector copied to 0000:7C00
    call boot_debug_mark
    pop di
    pop ax

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

    push ax
    push di
    mov di, 0x05B0
    mov al, 'A'                     ; entered ATA transfer
    call boot_debug_mark
    pop di
    pop ax

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

    push ax
    push di
    mov di, 0x05B2
    mov al, 'Q'                     ; ATA command issued
    call boot_debug_mark
    pop di
    pop ax

    call ata_wait_drq
    J286_NC .data_phase
    mov [HDW_ERROR], al
    jmp .bad

.data_phase:
    push ax
    push di
    mov di, 0x05B4
    mov al, 'D'                     ; DRQ observed
    call boot_debug_mark
    pop di
    pop ax

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
    push ax
    push di
    mov di, 0x05B6
    mov al, 'I'                     ; PIO data phase completed
    call boot_debug_mark
    pop di
    pop ax

    call ata_wait_complete
    J286_NC .sector_done
    mov [HDW_ERROR], al
    jmp .bad

.sector_done:
    push ax
    push di
    mov di, 0x05B8
    mov al, 'Z'                     ; ATA command reached ready state
    call boot_debug_mark
    pop di
    pop ax

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

int16:
    cmp ah, 0
    J286_E .read
    cmp ah, 1
    J286_E .peek
    cmp ah, 2
    J286_E .shift_status
    cmp ah, 0x10
    J286_E .read
    cmp ah, 0x11
    J286_E .peek
    cmp ah, 0x12
    J286_E .shift_status
    xor ah, ah
    iret
.read:
    push bx
    push ds
.wait_key:
    mov ax, BDA_SEG
    mov ds, ax
    mov bx, [0x1A]
    cmp bx, [0x1C]
    J286_NE .dequeue
    sti
    hlt
    jmp .wait_key
.dequeue:
    mov ax, [bx]
    add bx, 2
    cmp bx, 0x3E
    J286_B .save_head
    mov bx, 0x1E
.save_head:
    mov [0x1A], bx
    pop ds
    pop bx
    iret
.peek:
    push bp
    mov bp, sp
    push bx
    push ds
    mov bx, BDA_SEG
    mov ds, bx
    mov bx, [0x1A]
    cmp bx, [0x1C]
    J286_E .none
    mov ax, [bx]
    and word [ss:bp+6], 0xFFBF
    jmp .peek_done
.none:
    or word [ss:bp+6], 0x0040
.peek_done:
    pop ds
    pop bx
    pop bp
    iret
.shift_status:
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov al, [0x17]
    xor ah, ah
    pop ds
    iret

int1a:
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    mov dx, [0x6C]
    mov cx, [0x6E]
    xor al, al
    pop ds
    iret

int19:
    cli

    ; Clear the boot buffer before each attempt. This prevents a failed read
    ; from accidentally booting a stale 55AA sector left in RAM.
    xor ax, ax
    mov es, ax
    mov di, 0x7C00
    mov cx, 256
    cld
    rep stosw

    mov di, 0x0500
    mov al, 'F'                     ; about to call floppy INT 13h read
    call boot_debug_mark

    mov bx, 0x7C00
    mov ax, 0x0201
    xor cx, cx
    mov cl, 1
    xor dx, dx
    int 0x13
    J286_C .floppy_error

    mov di, 0x0502
    mov al, 'f'                     ; floppy INT 13h returned success
    call boot_debug_mark
    cmp word [es:0x7DFE], 0xAA55
    J286_NE .floppy_bad_signature

    mov di, 0x0504
    mov al, 'S'                     ; valid floppy boot signature
    call boot_debug_mark
    jmp .floppy_go

.floppy_error:
    mov di, 0x0502
    mov al, '!'                     ; floppy INT 13h returned CF=1
    call boot_debug_mark
    jmp .harddisk

.floppy_bad_signature:
    mov di, 0x0504
    mov al, 'x'                     ; read returned, but no 55AA signature
    call boot_debug_mark

.harddisk:
    xor ax, ax
    mov es, ax
    mov di, 0x7C00
    mov cx, 256
    cld
    rep stosw

    mov di, 0x0508
    mov al, 'H'                     ; about to call HDD INT 13h read
    call boot_debug_mark

    mov bx, 0x7C00
    mov ax, 0x0201
    mov cx, 1
    xor dh, dh
    mov dl, 0x80
    int 0x13
    J286_C .hard_error

    mov di, 0x050A
    mov al, 'h'                     ; HDD INT 13h returned success
    call boot_debug_mark
    cmp word [es:0x7DFE], 0xAA55
    J286_NE .hard_bad_signature

    mov di, 0x050C
    mov al, 'S'                     ; valid HDD boot signature
    call boot_debug_mark
    jmp .go

.hard_error:
    mov di, 0x050A
    mov al, '!'                     ; HDD INT 13h returned CF=1
    call boot_debug_mark
    jmp .failed

.hard_bad_signature:
    mov di, 0x050C
    mov al, 'x'                     ; HDD read returned, no 55AA signature
    call boot_debug_mark
    jmp .failed

.floppy_go:
    xor dl, dl
.go:
    call clear_screen                ; successful boot removes debug markers
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

floppy_dpt:
    db 0xDF,0x02,0x25,0x02,18,0x1B,0xFF,0x6C,0xF6,15,8

dummy db 0
logo db 10,'  +----------------------------------------------------------+',10
     db '  |             V I R T U A L   C O M P U T E R              |',10
     db '  |                    PC/AT SYSTEM BIOS                      |',10
     db '  +----------------------------------------------------------+',10,10,0
post_text db '  80286 processor test ........ OK',10
          db '  639 KB conventional memory .. OK',10
          db '  Initializing system hardware  ...',10
          db '  Searching for boot media',10,10,0
boot_fail db '  No bootable disk was found.',10,10
          db '  Mount a bootable .IMA/.IMG floppy or hard-disk image,',10
          db '  or an ISO CD-ROM, then reset the machine.',10,0

times 0xFFF0-($-$$) db 0xFF
reset_vector: jmp 0xF000:start
times 0x10000-($-$$) db 0xFF
