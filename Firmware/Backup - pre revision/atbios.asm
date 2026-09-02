; Virtual Computer clean-room PC/AT firmware
; 64 KiB system ROM mapped at physical F0000h.
bits 16
org 0

%define BDA_SEG 0x0040
%define VIDEO_SEG 0xB800

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
    mov word [es:0x10], 0x0021       ; equipment: display + floppy
    mov word [es:0x13], 639          ; conventional KiB (top 1 KiB reserved EBDA)
    mov word [es:0x1A], 0x001E       ; keyboard ring head
    mov word [es:0x1C], 0x001E       ; keyboard ring tail
    mov byte [es:0x49], 3            ; 80x25 color
    mov word [es:0x4A], 80
    mov word [es:0x4C], 0x1000       ; bytes per text page
    mov word [es:0x4E], 0            ; active-page display offset
    mov word [es:0x60], 0x0607       ; cursor start/end scan lines
    mov byte [es:0x62], 0            ; active display page

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
    jz .done
    cmp al, 13
    je .next
    cmp al, 10
    jne .put
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

int08:
    push ax
    push ds
    mov ax, BDA_SEG
    mov ds, ax
    inc word [0x6C]
    jnz .eoi
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
    jne .not_extended
    or byte [0x96], 2
    jmp .eoi
.not_extended:
    test al, 0x80
    jz short .make_code
    jmp .break_code
.make_code:
    cmp al, 0x2A
    jne short .not_lshift
    jmp .left_shift_down
.not_lshift:
    cmp al, 0x36
    jne short .not_rshift
    jmp .right_shift_down
.not_rshift:
    cmp al, 0x1D
    jne short .not_ctrl
    jmp .ctrl_down
.not_ctrl:
    cmp al, 0x38
    jne short .not_alt
    jmp .alt_down
.not_alt:
    cmp al, 0x3A
    jne short .not_caps
    jmp .caps_toggle
.not_caps:
    cmp al, 0x45
    jne short .not_num
    jmp .num_toggle
.not_num:
    cmp al, 0x46
    jne short .not_scroll
    jmp .scroll_toggle
.not_scroll:
    cmp al, 0x3A
    jbe .translate
    cmp al, 0x44                   ; F1-F10
    ja short .not_f1_f10
    jmp .enqueue_scan_only
.not_f1_f10:
    cmp al, 0x47
    jae short .maybe_navigation
    jmp .eoi
.maybe_navigation:
    cmp al, 0x53                   ; keypad/navigation including Delete
    ja short .not_navigation
    jmp .enqueue_scan_only
.not_navigation:
    cmp al, 0x57                   ; F11
    jne short .not_f11
    jmp .enqueue_scan_only
.not_f11:
    cmp al, 0x58                   ; F12
    je .enqueue_scan_only
    jmp .eoi
.translate:
    xor bx, bx
    mov bl, al
    mov dl, [cs:key_ascii+bx]
    mov cl, [0x17]
    test cl, 3
    jz .caps
    mov dl, [cs:key_shift_ascii+bx]
.caps:
    test cl, 0x40
    jz .control
    cmp dl, 'A'
    jb .caps_lower
    cmp dl, 'Z'
    ja .caps_lower
    or dl, 0x20
    jmp .control
.caps_lower:
    cmp dl, 'a'
    jb .control
    cmp dl, 'z'
    ja .control
    and dl, 0xDF
.control:
    test cl, 4
    jz .alternate
    mov dh, dl
    and dh, 0xDF
    cmp dh, 'A'
    jb .alternate
    cmp dh, 'Z'
    ja .alternate
    mov dl, dh
    sub dl, 0x40
.alternate:
    test cl, 8
    jz .enqueue
    xor dl, dl
.enqueue:
    mov ah, al
    mov al, dl
.queue_word:
    mov bx, [0x1C]                 ; tail offset
    mov dx, bx
    add dx, 2
    cmp dx, 0x3E
    jb .check_full
    mov dx, 0x1E
.check_full:
    cmp dx, [0x1A]                 ; full if next tail equals head
    je .eoi
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
    je .left_shift_up
    cmp al, 0x36
    je .right_shift_up
    cmp al, 0x1D
    je .ctrl_up
    cmp al, 0x38
    je .alt_up
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
    je video_set_mode
    cmp ah, 0x01
    jne short .not01
    jmp video_set_shape
.not01:
    cmp ah, 0x02
    jne short .not02
    jmp video_set_cursor
.not02:
    cmp ah, 0x03
    jne short .not03
    jmp video_get_cursor
.not03:
    cmp ah, 0x05
    jne short .not05
    jmp video_set_page
.not05:
    cmp ah, 0x06
    jne short .not06
    jmp video_scroll_up
.not06:
    cmp ah, 0x07
    jne short .not07
    jmp video_scroll_down
.not07:
    cmp ah, 0x08
    jne short .not08
    jmp video_read_cell
.not08:
    cmp ah, 0x09
    jne short .not09
    jmp video_write_cell
.not09:
    cmp ah, 0x0A
    jne short .not0a
    jmp video_write_char
.not0a:
    cmp ah, 0x0E
    jne short .not0e
    jmp video_tty
.not0e:
    cmp ah, 0x0F
    jne short .not0f
    jmp video_get_mode
.not0f:
    cmp ah, 0x12
    jne short .not12
    jmp video_ega_query
.not12:
    cmp ah, 0x1A
    jne short .unknown
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
    jnz .done
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
    je .save
    cmp cl, 8
    je .backspace
    cmp cl, 13
    je .carriage
    cmp cl, 10
    je .linefeed
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
    jb .save
.carriage:
    xor dl, dl
    cmp cl, 13
    je .save
.linefeed:
    inc dh
    cmp dh, 25
    jb .save
    mov ax, 0x0601
    mov bh, 7
    xor cx, cx
    mov dx, 0x184F
    int 0x10
    mov dh, 24
    jmp .save
.backspace:
    test dl, dl
    jz .save
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
    jz .clear_all
    cmp bl, al
    jae .clear_all
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
    jbe .copy_row
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
    jbe .fill_row
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
    jz .clear_all
    cmp bl, al
    jae .clear_all
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
    je .copy_done
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
    jbe .fill_row
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
    jne .done
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

; Core disk BIOS. AH=00 reset, 02 read, 08 parameters.
int13:
    push bp
    mov bp, sp
    cmp ah, 0x00
    je .success
    cmp ah, 0x02
    je .read
    cmp ah, 0x08
    je .params
    mov ah, 0x01
    jmp .failure
.params:
    test dl, 0x80
    jnz .hd_params
    mov ch, 79
    mov cl, 18
    mov dh, 1
    mov dl, 2
    xor ah, ah
    jmp .success
.hd_params:
    mov ch, 255
    mov cl, 63
    mov dh, 15
    mov dl, 1
    xor ah, ah
    jmp .success
.read:
    test dl, 0x80
    jnz .ata_read
    call floppy_read
    jc .failure
    xor ah, ah
    jmp .success
.ata_read:
    call ata_read_chs
    jc .failure
    xor ah, ah
.success:
    and word [ss:bp+6], 0xFFFE
    pop bp
    iret
.failure:
    or word [ss:bp+6], 1
    pop bp
    iret

; AT system services used during DOS kernel initialization.
int15:
    push bp
    mov bp, sp
    cmp ah, 0x88                   ; extended memory above 1 MiB, in KiB
    je .extended_memory
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
    jne short .do_sector
    jmp .success

.do_sector:
    ; DMA channel 2: device -> memory, 512 bytes at physical 9FC0:0000.
    xor al, al
    out 0x0C, al
    mov al, 2
    out 0x0A, al
    mov al, 0x46
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
    mov al, 0x1C
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
    jnz .discard_bad
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
    jb .advance_done
    mov byte [0x202], 1
    xor byte [0x201], 1
    cmp byte [0x201], 0
    jne .advance_done
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

; Minimal LBA-compatible CHS path for the emulated ATA master.
ata_read_chs:
    push ax
    push bx
    push cx
    push dx
    push si
    push di
    mov si, ax
    ; LBA = ((cylinder * 16 + head) * 63) + sector - 1
    xor ax, ax
    mov al, ch
    mov di, 16
    mul di
    xor di, di
    mov di, dx
    and di, 0x000F
    add ax, di
    mov di, 63
    mul di
    mov di, cx
    and di, 0x003F
    dec di
    add ax, di
    mov di, ax
    mov dx, 0x1F6
    mov al, 0xE0
    out dx, al
    mov dx, 0x1F2
    mov ax, si
    out dx, al
    inc dx
    mov ax, di
    out dx, al
    inc dx
    mov al, ah
    out dx, al
    inc dx
    xor al, al
    out dx, al
    add dx, 2
    mov al, 0x20
    out dx, al
    in al, dx
    test al, 1
    jnz .bad
    mov di, bx
    mov cx, si
    and cx, 0x00FF
    mov ax, 256
    mul cx
    mov cx, ax
    mov dx, 0x1F0
    rep insw
    clc
    jmp .done
.bad: stc
.done:
    pop di
    pop si
    pop dx
    pop cx
    pop bx
    pop ax
    ret

int16:
    cmp ah, 0
    je .read
    cmp ah, 1
    je .peek
    cmp ah, 2
    je .shift_status
    cmp ah, 0x10
    je .read
    cmp ah, 0x11
    je .peek
    cmp ah, 0x12
    je .shift_status
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
    jne .dequeue
    sti
    hlt
    jmp .wait_key
.dequeue:
    mov ax, [bx]
    add bx, 2
    cmp bx, 0x3E
    jb .save_head
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
    je .none
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
    xor ax, ax
    mov es, ax
    mov bx, 0x7C00
    mov ax, 0x0201
    xor cx, cx
    mov cl, 1
    xor dx, dx
    int 0x13
    jc .harddisk
    cmp word [es:0x7DFE], 0xAA55
    je .floppy_go
.harddisk:
    mov ax, 0x0201
    mov cx, 1
    xor dh, dh
    mov dl, 0x80
    int 0x13
    jc .failed
    cmp word [es:0x7DFE], 0xAA55
    jne .failed
    jmp .go
.floppy_go:
    xor dl, dl
.go:
    call clear_screen                ; clean boot handoff, as period BIOSes did
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
