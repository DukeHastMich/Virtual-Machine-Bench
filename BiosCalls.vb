' BiosCalls.vb — Fully restored BIOS interrupt system
Module BiosCalls
    Private BiosCursorColumn As Integer
    Private BiosCursorRow As Integer
    Private ReadOnly BiosKeyboardBuffer As New Queue(Of UInt16)()

    Public Sub QueueBiosKey(scanCode As Byte, asciiCode As Byte)
        SyncLock BiosKeyboardBuffer
            BiosKeyboardBuffer.Enqueue(CUShort((CUShort(scanCode) << 8) Or asciiCode))
        End SyncLock
    End Sub
    Public Sub IRQ(ByVal IRQ As Byte)
        Select Case IRQ
            Case &H8 : HandleTimerInterrupt()
            Case &H9 : HandleKeyboardInterrupt()
            Case &H10 : HandleVideoServices(10)
            Case &H11 : CPU0.AX = &H4222US ' AT, color display, two floppy drives, 80287 present.
            Case &H12 : CPU0.AX = 640 ' Conventional memory size in KiB.
            Case &H13 : HandleDiskServices()
            Case &H15 : HandleSystemServices()
            Case &H16 : HandleKeyboardServices()
            Case &H17 : HandlePrinterServices()
            Case &H18 : Debug.Print("INT 18h: ROM BASIC not implemented")
            Case &H19 : Debug.Print("INT 19h: Bootstrap loader") : CPU0.CS = &H0 : CPU0.IP = &H7C00
            Case &H1A : HandleTimeServices()
            Case &H1B : Debug.Print("INT 1Bh: CTRL-BREAK (stub)")
            Case &H1C : Debug.Print("INT 1Ch: Timer tick (stub)")
            Case &H1D : CPU0.ES = &H40 : CPU0.BX = &H8E : Debug.Print("INT 1Dh: Diskette parameter pointer")
            Case &H1E : Debug.Print("INT 1Eh: Diskette IRQ handler (stub)")
            Case &H1F : CPU0.ES = &H40 : CPU0.BX = &HA8 : Debug.Print("INT 1Fh: Video save pointer")
            Case &H20 : Debug.Print("INT 20h: Terminate program") : CPU0.CS = &H0 : CPU0.IP = &H100
            Case &H21 : HandleDosServices()
            Case &H25 : Debug.Print("INT 25h: Absolute disk read (stub)")
            Case &H26 : Debug.Print("INT 26h: Absolute disk write (stub)")
            Case &H27 : Debug.Print("INT 27h: Terminate and stay resident (stub)")
            Case &H2A : Debug.Print("INT 2Ah: Network control block (stub)")
            Case &H2B To &HFF : Debug.Print("INT " & Hex(IRQ) & "h: Reserved or vendor-specific (stub)")
            Case Else : Debug.Print("INT " & Hex(IRQ) & "h: Unhandled interrupt")
        End Select
    End Sub

    Private Sub HandleTimerInterrupt()
        Dim low As UInteger = CPU0.ReadWord(&H46CUI)
        Dim high As UInteger = CPU0.ReadWord(&H46EUI)
        Dim ticks As UInteger = low Or (high << 16)
        ticks += 1UI
        If ticks >= &H1800B0UI Then
            ticks = 0
            CPU0.WriteByte(&H470UI, 1)
        End If
        CPU0.WriteWord(&H46CUI, CUShort(ticks And &HFFFFUI))
        CPU0.WriteWord(&H46EUI, CUShort(ticks >> 16))
        SystemBus.WriteByte(&H20, &H20)
    End Sub

    Private Sub HandleKeyboardInterrupt()
        ' Reading port 60h acknowledges the controller's output buffer. The
        ' WinForms key path has already placed the corresponding BIOS key word
        ' in BiosKeyboardBuffer.
        SystemBus.ReadByte(&H60)
        SystemBus.WriteByte(&H20, &H20)
    End Sub

    Private Sub HandleSystemServices()
        Select Case CPU0.AH
            Case &H24 ' A20 gate services
                Select Case CPU0.AL
                    Case 0 : CPU0.A20Enabled = False : CPU0.AH = 0 : CPU0.Flags = CUShort(CPU0.Flags And Not 1US)
                    Case 1 : CPU0.A20Enabled = True : CPU0.AH = 0 : CPU0.Flags = CUShort(CPU0.Flags And Not 1US)
                    Case 2 : CPU0.AL = If(CPU0.A20Enabled, CByte(1), CByte(0)) : CPU0.AH = 0 : CPU0.Flags = CUShort(CPU0.Flags And Not 1US)
                    Case Else : CPU0.AH = &H86 : CPU0.Flags = CUShort(CPU0.Flags Or 1US)
                End Select
            Case &H86 ' BIOS wait; timing is represented by the emulated PIT.
                CPU0.AH = 0 : CPU0.Flags = CUShort(CPU0.Flags And Not 1US)
            Case &H88 ' Extended memory above 1 MiB in KiB.
                CPU0.AX = 15 * 1024 : CPU0.Flags = CUShort(CPU0.Flags And Not 1US)
            Case Else
                CPU0.AH = &H86 : CPU0.Flags = CUShort(CPU0.Flags Or 1US)
        End Select
    End Sub

    Private Sub HandleDiskServices()
        Dim drive As Integer = CPU0.DL
        Dim success As Boolean = False
        Dim status As Byte = 0
        Select Case CPU0.AH
            Case &H0
                success = (drive < &H80 AndAlso FloppyController.IsMounted(drive)) OrElse
                          (drive = &H80 AndAlso Declares.IdeController.HardDiskSectorCount > 0)
            Case &H2, &H3
                Dim count As Integer = CPU0.AL
                Dim cylinder As Integer = CPU0.CH Or ((CPU0.CL And &HC0) << 2)
                Dim sector As Integer = CPU0.CL And &H3F
                Dim buffer As UInteger = (CUInt(CPU0.ES) << 4) + CPU0.BX
                Dim data As Byte() = Nothing
                Dim lba As Long = -1
                If drive < &H80 AndAlso CPU0.AH = &H2 Then
                    data = FloppyController.BiosRead(drive, cylinder, CPU0.DH, sector, count)
                ElseIf drive = &H80 Then
                    Const heads As Integer = 16, sectors As Integer = 63
                    lba = (CLng(cylinder) * heads + CPU0.DH) * sectors + sector - 1
                    If CPU0.AH = &H2 Then data = Declares.IdeController.BiosRead(lba, count)
                End If
                If CPU0.AH = &H3 Then
                    ReDim data(count * 512 - 1)
                    For index As Integer = 0 To data.Length - 1 : data(index) = CPU0.ReadByte(buffer + CUInt(index)) : Next
                    If drive < &H80 Then success = FloppyController.BiosWrite(drive, cylinder, CPU0.DH, sector, data)
                    If drive = &H80 Then success = Declares.IdeController.BiosWrite(lba, data)
                ElseIf data IsNot Nothing Then
                    For index As Integer = 0 To data.Length - 1 : CPU0.WriteByte(buffer + CUInt(index), data(index)) : Next
                    success = True
                End If
                If success Then CPU0.AL = CByte(count)
            Case &H8
                If drive < &H80 Then
                    Dim geometry As Integer() = FloppyController.GetGeometry(drive)
                    If geometry IsNot Nothing Then
                        Dim maxCylinder As Integer = geometry(0) - 1
                        CPU0.CH = CByte(maxCylinder And &HFF)
                        CPU0.CL = CByte(geometry(2) Or ((maxCylinder >> 2) And &HC0))
                        CPU0.DH = CByte(geometry(1) - 1) : CPU0.DL = 2
                        CPU0.BL = If(geometry(2) = 18, CByte(4), If(geometry(2) = 15, CByte(2), CByte(3)))
                        success = True
                    End If
                ElseIf drive = &H80 AndAlso Declares.IdeController.HardDiskSectorCount > 0 Then
                    Const heads As Integer = 16, sectors As Integer = 63
                    Dim cylinders As Long = Math.Min(1024, Math.Max(1, Declares.IdeController.HardDiskSectorCount \ (heads * sectors)))
                    Dim maxCylinder As Integer = CInt(cylinders - 1)
                    CPU0.CH = CByte(maxCylinder And &HFF)
                    CPU0.CL = CByte(sectors Or ((maxCylinder >> 2) And &HC0))
                    CPU0.DH = heads - 1 : CPU0.DL = 1 : success = True
                End If
            Case Else
                status = 1
        End Select
        If Not success AndAlso status = 0 Then status = &H20
        CPU0.AH = status
        If success Then CPU0.Flags = CUShort(CPU0.Flags And Not 1US) Else CPU0.Flags = CUShort(CPU0.Flags Or 1US)
    End Sub

    Private Sub HandleVideoServices(IRQ As Byte)
        Dim RegAH As Byte = CPU0.AH
        Select Case RegAH
            Case &H0 ' set video mode
                Mode = CPU0.AL
                Form1.Current.Mode2.Enabled = False
                Form1.Current.Mode3.Enabled = False
                Form1.Current.Mode4.Enabled = False
                Select Case Mode
                    Case &H0  ' 40x25 monochrome
                    Case &H1  ' 40x25 16-color
                    Case &H2 : Form1.Current.Mode2.Enabled = True : OffSet = 32769 ' 80x25 mono
                    Case &H3 : Form1.Current.Mode3.Enabled = True : OffSet = 32769 ' 80x25 color
                    Case &H4 : Form1.Current.Mode4.Enabled = True : OffSet = 32769 ' 320x200 graphics
                    Case &H5 : OffSet = 32769 ' 320x200 color burst off
                    Case &H6  ' 640x200 graphics 2-color
                    Case 7 To 19 : OffSet = 32769 ' Various VGA modes
                End Select

            Case &H6 ' scroll up
                Dim AL = CPU0.AL, CH = CPU0.CH, CL = CPU0.CL, DH = CPU0.DH, DL = CPU0.DL, BH = CPU0.BH
                If AL = 0 Then AL = 25 : CH = 1 : CL = 1 : DH = 25 : DL = 80
                For b As UInt32 = 1 To AL
                    For y As UInt32 = CH To DH
                        For x = CL * 2 To DL * 2 + 1
                            VrMem(11, OffSet + ((y - 1) * 160) + x - 3) = VrMem(11, OffSet + ((y - 1) * 160) + x - 3 + 160)
                        Next
                    Next
                    For c = CL * 2 To DL * 2 + 1
                        If c Mod 2 = 0 Then
                            VrMem(11, OffSet - 2 + (DH - 1) * 160 + c) = 32
                        Else
                            VrMem(11, OffSet - 4 + (DH - 1) * 160 + c) = BH
                        End If
                    Next
                Next

            Case &H7 ' scroll down
                Dim AL = CPU0.AL, CH = CPU0.CH, CL = CPU0.CL, DH = CPU0.DH, DL = CPU0.DL, BH = CPU0.BH
                If AL = 0 Then AL = 25 : CH = 1 : CL = 1 : DH = 25 : DL = 80
                For b = 1 To AL
                    For y = DH - 1 To CH Step -1
                        For x = CL * 2 To DL * 2 + 1
                            VrMem(11, OffSet + ((y - 1) * 160) + x - 3 + 160) = VrMem(11, OffSet + ((y - 1) * 160) + x - 3)
                        Next
                    Next
                    For c = CL * 2 To DL * 2 + 1
                        If c Mod 2 = 0 Then
                            VrMem(11, OffSet - 2 + (CH - 1) * 160 + c) = 32
                        Else
                            VrMem(11, OffSet - 4 + (CH - 1) * 160 + c) = BH
                        End If
                    Next
                Next

            Case &H2 ' set cursor position
                BiosCursorRow = Math.Min(24, CPU0.DH) : BiosCursorColumn = Math.Min(79, CPU0.DL)
            Case &H3 ' get cursor position
                CPU0.DH = CByte(BiosCursorRow) : CPU0.DL = CByte(BiosCursorColumn) : CPU0.CX = &H607
            Case &HE ' teletype output
                BiosTeletype(CPU0.AL, CPU0.BL)
            Case Else
                Debug.Print("INT 10h AH=" & Hex(RegAH) & ": Not implemented")
        End Select
    End Sub

    Private Sub BiosTeletype(character As Byte, attribute As Byte)
        Select Case character
            Case 8 : If BiosCursorColumn > 0 Then BiosCursorColumn -= 1
            Case 10 : BiosCursorRow += 1
            Case 13 : BiosCursorColumn = 0
            Case Else
                Dim cell As UInteger = &HB8000UI + CUInt((BiosCursorRow * 80 + BiosCursorColumn) * 2)
                CPU0.WriteByte(cell, character) : CPU0.WriteByte(cell + 1UI, If(attribute = 0, CByte(7), attribute))
                BiosCursorColumn += 1
                If BiosCursorColumn >= 80 Then BiosCursorColumn = 0 : BiosCursorRow += 1
        End Select
        If BiosCursorRow >= 25 Then
            For row As Integer = 1 To 24
                For column As Integer = 0 To 159
                    CPU0.WriteByte(&HB8000UI + CUInt((row - 1) * 160 + column), CPU0.ReadByte(&HB8000UI + CUInt(row * 160 + column)))
                Next
            Next
            For column As Integer = 0 To 79
                CPU0.WriteByte(&HB8000UI + CUInt(24 * 160 + column * 2), 32)
                CPU0.WriteByte(&HB8001UI + CUInt(24 * 160 + column * 2), 7)
            Next
            BiosCursorRow = 24
        End If
    End Sub

    Private Sub HandleKeyboardServices()
        Dim AH = CPU0.AH
        Select Case AH
            Case &H0
                SyncLock BiosKeyboardBuffer
                    ' The fallback Enter keeps unattended boot media moving in
                    ' the absence of a physical ROM's blocking keyboard loop.
                    CPU0.AX = If(BiosKeyboardBuffer.Count > 0, BiosKeyboardBuffer.Dequeue(), CUShort(&H1C0D))
                End SyncLock
            Case &H1
                SyncLock BiosKeyboardBuffer
                    If BiosKeyboardBuffer.Count = 0 Then
                        CPU0.Flags = CPU0.Flags Or &H40US
                    Else
                        CPU0.AX = BiosKeyboardBuffer.Peek()
                        CPU0.Flags = CPU0.Flags And Not &H40US
                    End If
                End SyncLock
            Case Else : Debug.Print("INT 16h: AH=" & Hex(AH) & ": Unhandled")
        End Select
    End Sub

    Private Sub HandlePrinterServices()
        Select Case CPU0.AH
            Case &H0 : CPU0.AX = &H9000
            Case &H1 : CPU0.AX = &H9000
            Case &H2 : Debug.Print("Printer char: " & ChrW(CPU0.AL))
            Case Else : Debug.Print("INT 17h AH=" & Hex(CPU0.AH))
        End Select
    End Sub

    Private Sub HandleTimeServices()
        Dim ticks = CUInt((DateTime.Now - DateTime.Today).TotalMilliseconds / 55)
        CPU0.CX = CUShort((ticks >> 16) And &HFFFF)
        CPU0.DX = CUShort(ticks And &HFFFF)
        CPU0.AL = 0
    End Sub

    Private Sub HandleDosServices()
        Select Case CPU0.AH
            Case &H0 : CPU0.CS = 0 : CPU0.IP = &H100
            Case &H1 : CPU0.AL = &H41 ' 'A'
            Case &H2 : Debug.Print("Output char: " & ChrW(CPU0.DL))
            Case &H9 : Debug.Print("Display string (stub)")
            Case Else : Debug.Print("INT 21h AH=" & Hex(CPU0.AH))
        End Select
    End Sub
    ' Additional Reserved Interrupt Stubs (INT 2Bh - INT FFh)
    Private Sub HandleReservedInterrupts(ByVal IRQ As Byte)
        Debug.Print("INT " & Hex(IRQ) & "h: Reserved or vendor-specific interrupt (stub)")
    End Sub

    ' Dispatch for missing but initialized INTs
    Private Sub InitializeAllStubs()
        Dim ref_i 'Reference iterator
        For i = &H2B To &HFF
            ref_i = i
            If Not IRQHandled(i) Then
                IRQMap(i) = Sub() HandleReservedInterrupts(ref_i)
            End If
        Next
    End Sub

    ' Simulated interrupt vector mapping
    Private ReadOnly IRQMap As New Dictionary(Of Byte, Action)
    Private Function IRQHandled(ByVal irq As Byte) As Boolean
        Return irq = &H10 Or irq = &H11 Or irq = &H12 Or irq = &H13 Or irq = &H15 Or irq = &H16 Or irq = &H17 Or irq = &H18 Or irq = &H19 Or irq = &H1A Or irq = &H1B Or irq = &H1C Or irq = &H1D Or irq = &H1E Or irq = &H1F Or irq = &H20 Or irq = &H21 Or irq = &H25 Or irq = &H26 Or irq = &H27 Or irq = &H2A
    End Function

End Module
