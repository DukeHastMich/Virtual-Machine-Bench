Imports System
Imports System.Diagnostics
Imports System.Threading
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

' Diamond Stealth Pro ISA revision C5 (1993), factory 2 MiB upgrade
' configuration: S3 86C928, 60 ns VRAM, Brooktree Bt485 RAMDAC.
'
' This device owns the VGA/S3 I/O registers, the legacy A0000h-BFFFFh video
' aperture, the C0000h-C7FFFh option-ROM window, and the S3 linear window.
' The host renderer consumes only emulated VRAM/register state; guest software
' never writes a host bitmap directly.
Public NotInheritable Class DiamondStealthPro928BoardProfile
    Public Const Manufacturer As String = "Diamond Computer Systems"
    Public Const ProductFamily As String = "Stealth Pro"
    Public Const Controller As String = "S3 86C928"
    Public Const Bus As String = "16-bit ISA"
    Public Const InstalledVramBytes As Integer = 2 * 1024 * 1024
    Public Const FccId As String = "FTU1SA928C"
    Public Const ProductSku As String = "DIA005"
    Public Const VramAccessTimeNanoseconds As Integer = 60

    ' Concrete catalogue configuration selected for this virtual machine.
    ' The 1 MiB C5 used Diamond's SS2410/MU9C1880-compatible DAC; Diamond's
    ' contemporary 2 MiB upgrade supplied the high-refresh Brooktree Bt485.
    Public Property RamdacModel As String = "Brooktree Bt485 (2 MiB upgrade configuration)"
    Public Property ClockGeneratorModel As String = "IC Designs ICD2061A-compatible programmable clock"
    Public Property VramOrganization As String = "2 MiB, 60 ns VRAM"
    Public Property Revision As String = "Revision C5, ISA, 2 MiB upgrade configuration"
    Public Property IrqJumper As String = "JP1: IRQ 2/9 selectable; open by default"
    Public Property FeatureConnector As String = "26-pin VGA feature connector"
    Public Property VideoOutput As String = "15-pin analog VGA"
    Public Property OptionRomIdentity As String = "32 KiB clean-room replacement; target OEM BIOS v2.03"
End Class

' Board-level ICD2061A-compatible clock-generator boundary.  The selected
' output frequency is real board state.  Serial programming is tracked as an
' explicit implementation defect until the board wiring and programming word
' are both verified; silently accepting writes here would invent hardware.
Public NotInheritable Class Icd2061AClockSource928
    Private _pixelClockHzInBed As Long

    Public Property PixelClockHz As Long
        Get
            Return _pixelClockHzInBed
        End Get
        Set(value As Long)
            _pixelClockHzInBed = Math.Max(0L, value)
        End Set
    End Property

    Public Sub PowerCycle()
        ' The ICD2061A's programmed divider state is volatile.  A cold supply
        ' transition returns this board boundary to the fixed VGA oscillator
        ' fallback until firmware serially programs a new frequency.
        Reset()
    End Sub

    Public Sub Reset()
        ' Preserve the pre-refit reset contract until the ICD2061A serial/reset
        ' behavior is backed by verified board documentation.
        _pixelClockHzInBed = 0L
    End Sub

    Friend Sub CopyPresentationStateTo(targetInBed As Icd2061AClockSource928)
        If targetInBed Is Nothing Then Throw New ArgumentNullException(NameOf(targetInBed))
        targetInBed._pixelClockHzInBed = _pixelClockHzInBed
    End Sub
End Class

' Board-level Brooktree Bt485 RAMDAC.  S3 CR55/CR43 supply RS3:RS2 and the VGA
' DAC ports supply RS1:RS0, so both the compatible palette path and the Bt485
' extended command/overlay/cursor register space are guest-visible.
Public NotInheritable Class BrooktreeBt485Ramdac928
    Private _pelMask As Byte = &HFF
    Private _readIndex As Byte
    Private _writeIndex As Byte
    Private _component As Integer
    Private _reading As Boolean
    Private ReadOnly _palette(255, 2) As Byte
    Private ReadOnly _overlayPalette(15, 2) As Byte
    Private ReadOnly _cursorRam(1023) As Byte
    Private _overlayReadIndex As Byte
    Private _overlayWriteIndex As Byte
    Private _overlayComponent As Integer
    Private _command0 As Byte
    Private _command1 As Byte
    Private _command2 As Byte
    Private _command3 As Byte
    Private _command4 As Byte
    Private _cursorAddress As Integer
    Private _cursorXWrite As UInt16
    Private _cursorYWrite As UInt16
    Private _cursorX As UInt16
    Private _cursorY As UInt16
    Private ReadOnly _signature(2) As Byte

    Public Property PelMask As Byte
        Get
            Return _pelMask
        End Get
        Set(value As Byte)
            _pelMask = value
        End Set
    End Property

    Public Property ReadIndex As Byte
        Get
            Return _readIndex
        End Get
        Set(value As Byte)
            _readIndex = value
            _component = 0
            _reading = True
        End Set
    End Property

    Public Property WriteIndex As Byte
        Get
            Return _writeIndex
        End Get
        Set(value As Byte)
            _writeIndex = value
            _component = 0
            _reading = False
        End Set
    End Property

    Public ReadOnly Property IsReading As Boolean
        Get
            Return _reading
        End Get
    End Property

    Public ReadOnly Property ComponentPhase As Integer
        Get
            Return _component
        End Get
    End Property

    Public ReadOnly Property EightBitComponents As Boolean
        Get
            Return (_command0 And &H2) <> 0
        End Get
    End Property

    ' Brooktree Bt485A, Command Register 0 (CR00), p. 25:
    ' https://www.dosdays.co.uk/media/brooktree/BT485_Datasheet.pdf
    Public ReadOnly Property OutputPoweredDown As Boolean
        Get
            Return (_command0 And 1) <> 0
        End Get
    End Property

    ' Brooktree Bt485A, Command Register 2 CR25 and VGA Port, pp. 15, 27.
    ' The Stealth's S3 pixel-port wiring asserts PORTSEL for enhanced output;
    ' CR25 still has to unmask it.  Power-up therefore remains on the VGA port.
    Public ReadOnly Property PixelPortSelected As Boolean
        Get
            Return (_command2 And &H20) <> 0
        End Get
    End Property

    ' Brooktree Bt485A, Command Registers 1/3 and Modes of Operation, pp. 9-15,
    ' 26, 28.  This is the display input format at the RAMDAC, not S3 CR50's
    ' graphics-engine pixel length.
    Public ReadOnly Property DisplayBitsPerPixel As Integer
        Get
            If Not PixelPortSelected Then Return 8
            Select Case (_command3 >> 5) And 3
                Case 1 : Return 24             ' packed 24-bit, 4/3:1
                Case 3 : Return 8              ' 8-bit, 2:1
            End Select
            Select Case (_command1 >> 5) And 3
                Case 0 : Return 24
                Case 1 : Return 16
                Case 2 : Return 8
                Case Else : Return 4
            End Select
        End Get
    End Property

    Public ReadOnly Property TrueColorPaletteBypassed As Boolean
        Get
            Return (_command1 And &H10) <> 0
        End Get
    End Property

    Public ReadOnly Property SixteenBitRgb565 As Boolean
        Get
            Return (_command1 And &H8) <> 0
        End Get
    End Property

    Public ReadOnly Property TrueColorPaletteIndexesContiguous As Boolean
        Get
            ' Brooktree Bt485A Command Register 2 CR22, p. 27.
            Return (_command2 And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property FourBitLowNibbleFirst As Boolean
        Get
            Return (_command1 And &H80) <> 0
        End Get
    End Property

    Public ReadOnly Property CursorMode As Integer
        Get
            Return _command2 And 3
        End Get
    End Property

    Public ReadOnly Property CursorSize As Integer
        Get
            Return If((_command3 And 4) <> 0, 64, 32)
        End Get
    End Property

    Public ReadOnly Property CursorX As Integer
        Get
            Return _cursorX And &HFFFUS
        End Get
    End Property

    Public ReadOnly Property CursorY As Integer
        Get
            Return _cursorY And &HFFFUS
        End Get
    End Property

    Public Function CursorPlaneValue(xInBed As Integer, yInBed As Integer) As Integer
        Dim sizeInBed As Integer = CursorSize
        If xInBed < 0 OrElse yInBed < 0 OrElse xInBed >= sizeInBed OrElse yInBed >= sizeInBed Then Return 0
        Dim bytesPerRowInBed As Integer = sizeInBed \ 8
        Dim planeSizeInBed As Integer = sizeInBed * bytesPerRowInBed
        Dim byteOffsetInBed As Integer = yInBed * bytesPerRowInBed + (xInBed \ 8)
        Dim maskInBed As Integer = &H80 >> (xInBed And 7)
        Dim plane0InBed As Integer = If((_cursorRam(byteOffsetInBed) And maskInBed) <> 0, 1, 0)
        Dim plane1InBed As Integer = If((_cursorRam(planeSizeInBed + byteOffsetInBed) And maskInBed) <> 0, 2, 0)
        Return plane1InBed Or plane0InBed
    End Function

    Public Function OverlayComponentAsEightBit(indexInBed As Integer, componentInBed As Integer) As Integer
        Dim rawInBed As Integer = _overlayPalette(indexInBed And &HF, componentInBed Mod 3)
        Return If(EightBitComponents, rawInBed, rawInBed * 255 \ 63)
    End Function

    Public Sub Reset()
        _pelMask = &HFF
        _readIndex = 0
        _writeIndex = 0
        _component = 0
        _reading = False
        Array.Clear(_palette, 0, _palette.Length)
        Array.Clear(_overlayPalette, 0, _overlayPalette.Length)
        Array.Clear(_cursorRam, 0, _cursorRam.Length)
        _overlayReadIndex = 0 : _overlayWriteIndex = 0 : _overlayComponent = 0
        _command0 = 0 : _command1 = 0 : _command2 = 0 : _command3 = 0 : _command4 = 0
        _cursorAddress = 0
        _cursorXWrite = 0US : _cursorYWrite = 0US
        _cursorX = 0US : _cursorY = 0US
        Array.Clear(_signature, 0, _signature.Length)
        Dim ega(,) As Byte = {
            {0, 0, 0}, {0, 0, 42}, {0, 42, 0}, {0, 42, 42},
            {42, 0, 0}, {42, 0, 42}, {42, 21, 0}, {42, 42, 42},
            {21, 21, 21}, {21, 21, 63}, {21, 63, 21}, {21, 63, 63},
            {63, 21, 21}, {63, 21, 63}, {63, 63, 21}, {63, 63, 63}}
        For i As Integer = 0 To 15
            _palette(i, 0) = ega(i, 0)
            _palette(i, 1) = ega(i, 1)
            _palette(i, 2) = ega(i, 2)
        Next
    End Sub

    Public Function ReadData() As Byte
        Dim result As Byte = _palette(_readIndex, _component)
        _component += 1
        If _component >= 3 Then
            _component = 0
            _readIndex = CByte((_readIndex + 1) And &HFF)
        End If
        Return result
    End Function

    Public Sub WriteData(value As Byte)
        _palette(_writeIndex, _component) = NormalizeComponent(value)
        _component += 1
        If _component >= 3 Then
            _component = 0
            _writeIndex = CByte((_writeIndex + 1) And &HFF)
        End If
    End Sub

    Private Function NormalizeComponent(valueInBed As Byte) As Byte
        Return If(EightBitComponents, valueInBed, CByte(valueInBed And &H3F))
    End Function

    Public Function ReadRegister(registerSelectInBed As Integer) As Byte
        Select Case registerSelectInBed And &HF
            Case 0 : Return _writeIndex
            Case 1 : Return ReadData()
            Case 2 : Return _pelMask
            Case 3 : Return If(_reading, CByte(3), CByte(0))
            Case 4 : Return _overlayWriteIndex
            Case 5
                Dim resultInBed As Byte = _overlayPalette(_overlayReadIndex And &HF, _overlayComponent)
                _overlayComponent += 1
                If _overlayComponent = 3 Then
                    _overlayComponent = 0
                    _overlayReadIndex = CByte((_overlayReadIndex + 1) And &HF)
                End If
                Return resultInBed
            Case 6 : Return _command0
            Case 7 : Return _overlayReadIndex
            Case 8 : Return _command1
            Case 9 : Return _command2
            Case &HA
                If (_command0 And &H80) <> 0 Then
                    Select Case _writeIndex
                        Case 1 : Return _command3
                        Case 2 : Return _command4
                        Case &H20, &H21, &H22 : Return _signature(_writeIndex - &H20)
                    End Select
                End If
                ' Bt485A ID=00, revision=10, attached CRT sense asserted,
                ' followed by the live read/write and RGB component phase.
                Return CByte(&H28 Or If(_reading, 4, 0) Or (_component And 3))
            Case &HB
                Dim resultInBed As Byte = _cursorRam(_cursorAddress And &H3FF)
                _cursorAddress = (_cursorAddress + 1) And &H3FF
                _command3 = CByte((_command3 And &HFC) Or ((_cursorAddress >> 8) And 3))
                Return resultInBed
            ' Brooktree Bt485A, Cursor (x,y) Registers, p. 32: reads return
            ' the last MPU-written values, which need not yet be displayed.
            Case &HC : Return CByte(_cursorXWrite And &HFFUS)
            Case &HD : Return CByte((_cursorXWrite >> 8) And &HFUS)
            Case &HE : Return CByte(_cursorYWrite And &HFFUS)
            Case Else : Return CByte((_cursorYWrite >> 8) And &HFUS)
        End Select
    End Function

    Public Sub WriteRegister(registerSelectInBed As Integer, valueInBed As Byte)
        Select Case registerSelectInBed And &HF
            Case 0
                WriteIndex = valueInBed
                _cursorAddress = (_cursorAddress And &H300) Or valueInBed
            Case 1 : WriteData(valueInBed)
            Case 2 : _pelMask = valueInBed
            Case 3
                ReadIndex = valueInBed
                _cursorAddress = (_cursorAddress And &H300) Or valueInBed
            Case 4
                _overlayWriteIndex = CByte(valueInBed And &HF)
                _overlayComponent = 0
            Case 5
                _overlayPalette(_overlayWriteIndex And &HF, _overlayComponent) = NormalizeComponent(valueInBed)
                _overlayComponent += 1
                If _overlayComponent = 3 Then
                    _overlayComponent = 0
                    _overlayWriteIndex = CByte((_overlayWriteIndex + 1) And &HF)
                End If
            Case 6 : _command0 = valueInBed
            Case 7
                _overlayReadIndex = CByte(valueInBed And &HF)
                _overlayComponent = 0
            Case 8 : _command1 = valueInBed
            Case 9 : _command2 = valueInBed
            Case &HA
                If (_command0 And &H80) <> 0 Then
                    If _writeIndex = 1 Then
                        _command3 = CByte(valueInBed And &H7F)
                        _cursorAddress = ((_command3 And 3) << 8) Or (_cursorAddress And &HFF)
                    ElseIf _writeIndex = 2 Then
                        _command4 = CByte(valueInBed And &H7)
                    ElseIf _writeIndex >= &H20 AndAlso _writeIndex <= &H22 AndAlso (_command4 And 4) = 0 Then
                        _signature(_writeIndex - &H20) = valueInBed
                    End If
                End If
            Case &HB
                _cursorRam(_cursorAddress And &H3FF) = valueInBed
                _cursorAddress = (_cursorAddress + 1) And &H3FF
                _command3 = CByte((_command3 And &HFC) Or ((_cursorAddress >> 8) And 3))
            Case &HC : _cursorXWrite = CUShort((_cursorXWrite And &HF00US) Or valueInBed)
            Case &HD : _cursorXWrite = CUShort((_cursorXWrite And &HFFUS) Or (CUShort(valueInBed And &HF) << 8))
            Case &HE : _cursorYWrite = CUShort((_cursorYWrite And &HF00US) Or valueInBed)
            Case &HF
                _cursorYWrite = CUShort((_cursorYWrite And &HFFUS) Or (CUShort(valueInBed And &HF) << 8))
                ' Brooktree Bt485A Cursor Operation, p. 16 and cursor registers,
                ' p. 32: both displayed coordinates load only after CYHR.
                _cursorX = _cursorXWrite
                _cursorY = _cursorYWrite
        End Select
    End Sub

    Public Function Component(indexInBed As Integer, componentInBed As Integer) As Byte
        Return _palette(indexInBed And &HFF, componentInBed Mod 3)
    End Function

    Public Function ComponentAsEightBit(indexInBed As Integer, componentInBed As Integer) As Integer
        Dim rawInBed As Integer = Component(indexInBed, componentInBed)
        Return If(EightBitComponents, rawInBed, rawInBed * 255 \ 63)
    End Function

    Friend Sub CopyPresentationStateTo(targetInBed As BrooktreeBt485Ramdac928)
        If targetInBed Is Nothing Then Throw New ArgumentNullException(NameOf(targetInBed))
        targetInBed._pelMask = _pelMask
        targetInBed._readIndex = _readIndex
        targetInBed._writeIndex = _writeIndex
        targetInBed._component = _component
        targetInBed._reading = _reading
        targetInBed._command0 = _command0
        targetInBed._command1 = _command1
        targetInBed._command2 = _command2
        targetInBed._command3 = _command3
        targetInBed._command4 = _command4
        targetInBed._overlayReadIndex = _overlayReadIndex
        targetInBed._overlayWriteIndex = _overlayWriteIndex
        targetInBed._overlayComponent = _overlayComponent
        targetInBed._cursorAddress = _cursorAddress
        targetInBed._cursorXWrite = _cursorXWrite
        targetInBed._cursorYWrite = _cursorYWrite
        targetInBed._cursorX = _cursorX
        targetInBed._cursorY = _cursorY
        Array.Copy(_signature, targetInBed._signature, _signature.Length)
        For indexInBed As Integer = 0 To 255
            For componentInBed As Integer = 0 To 2
                targetInBed._palette(indexInBed, componentInBed) = _palette(indexInBed, componentInBed)
            Next
        Next
        For indexInBed As Integer = 0 To 15
            For componentInBed As Integer = 0 To 2
                targetInBed._overlayPalette(indexInBed, componentInBed) = _overlayPalette(indexInBed, componentInBed)
            Next
        Next
        Array.Copy(_cursorRam, targetInBed._cursorRam, _cursorRam.Length)
    End Sub
End Class

Public Class DiamondStealthPro928
    Implements IPortDevice, IWordPortDevice, IClockedDevice, IClockBatchSafeDevice, IPortDecodeCandidateProvider, IMemoryDecodeChangeSource, IMemoryMappedDevice, IPageCoherentMemoryDecode, IResettableDevice, IPowerCycleDevice, IDisposable

    Public Event MemoryDecodeChanged() Implements IMemoryDecodeChangeSource.MemoryDecodeChanged

    Private Const VramSize As Integer = 2 * 1024 * 1024
    Private Const PicosecondsPerSecond As Long = 1000000000000L
    Private Const OptionRomBase As UInteger = &HC0000UI
    Private Const OptionRomWindowSize As UInteger = &H8000UI

    Private ReadOnly _boardProfile As New DiamondStealthPro928BoardProfile()
    Private ReadOnly _ramdac As New BrooktreeBt485Ramdac928()
    Private ReadOnly _clockGenerator As New Icd2061AClockSource928()

    Private ReadOnly _vram(VramSize - 1) As Byte
    Private ReadOnly _latches(3) As Byte

    Private ReadOnly _sequencer(255) As Byte
    Private ReadOnly _crtc(255) As Byte
    Private ReadOnly _graphics(255) As Byte
    Private ReadOnly _attribute(31) As Byte
    Private _sequencerIndex As Byte
    Private _crtcIndex As Byte
    Private _graphicsIndex As Byte
    Private _attributeIndex As Byte
    Private _attributeDataPhase As Boolean
    Private _attributeVideoEnabled As Boolean = True
    ' CROMWELL VGA MODE TRANSITION TRACE
    ' Host-only forensic state.  Nothing here is mapped to guest I/O or memory.
    Private Const DiagnosticVgaTraceCapacity As Integer = 8192
    Private ReadOnly _diagnosticVgaTrace As New System.Collections.Generic.Queue(Of String)()
    Private _diagnosticVgaTraceSequence As ULong
    ' Always-on activity must be cheap enough for software VGA rasterizers.
    ' QuickBASIC PAINT changes GC registers for individual pixel masks; storing
    ' formatted strings for every OUT caused hundreds of thousands of temporary
    ' allocations.  This fixed numeric ring preserves exact recent bus writes
    ' without changing guest-visible timing or allocating on the hot path.
    Private Const DiagnosticVgaPortRingCapacityInBed As Integer = 8192
    Private Structure DiagnosticVgaPortWriteInBed
        Public Sequence As ULong
        Public Port As UInt16
        Public RegisterIndex As Byte
        Public Value As Byte
    End Structure
    Private ReadOnly _diagnosticVgaPortRingInBed(DiagnosticVgaPortRingCapacityInBed - 1) As DiagnosticVgaPortWriteInBed
    Private _diagnosticVgaPortRingIndexInBed As Integer
    Private _diagnosticVgaPortRingCountInBed As Integer
    Private _diagnosticVgaPortSequenceInBed As ULong
    ' Dedicated RAMDAC recorder.  Mode-X software can issue hundreds of
    ' thousands of Sequencer map-mask writes after loading its palette; those
    ' must never evict the much smaller 3C6h-3C9h transaction history needed to
    ' diagnose color corruption.  This is host-only instrumentation, not a
    ' register, FIFO, or timing feature of the Bt485/86C928 board.
    Private Const DiagnosticDacTraceCapacity As Integer = 4096
    Private ReadOnly _diagnosticDacTrace As New System.Collections.Generic.Queue(Of String)()
    Private _diagnosticDacTraceSequence As ULong
    Private _diagnosticVgaTraceEnabled As Boolean
    Private _diagnosticVgaStatusReadCount As ULong
    Private _diagnosticVgaDacDataWriteCount As ULong
    Private _diagnosticVgaMemoryReadCount As ULong
    Private _diagnosticVgaLatchLoadCount As ULong
    Private ReadOnly _diagnosticVgaWriteModeCounts(3) As ULong
    Private ReadOnly _diagnosticVgaMapMaskCounts(15) As ULong
    Private ReadOnly _diagnosticVgaPlaneBitsSet(3, 3) As ULong
    Private ReadOnly _diagnosticVgaPlaneBitsCleared(3, 3) As ULong
    Private ReadOnly _diagnosticVgaMode2Inputs(3, 15) As ULong
    Private _diagnosticVgaChainedWriteCount As ULong
    Private _diagnosticVgaUnchainedWriteCount As ULong
    Private _diagnosticLastRenderClass As String = String.Empty
    Private _diagnosticLastGraphicsState As String = String.Empty

    Private _miscOutput As Byte = &H1
    Private _featureControl As Byte

    Private _setupRegister As Byte = 0          ' 46E8h power-on default: decode disabled
    Private _setupOptionSelect As Byte = 0      ' 0102h power-on default: asleep until POST setup
    Private _optionRom() As Byte = Array.Empty(Of Byte)()

    ' Beam timing is maintained in the adapter's own pixel-clock domain.
    ' Host presentation may render at any cadence; guest-visible status never
    ' derives from a WinForms timer.
    Private _beamDotPhase As Double
    Private _frameCounter As ULong
    Private _horizontalRetrace As Boolean
    Private _verticalRetrace As Boolean
    Private _displayEnable As Boolean

    ' Enhanced 86C928 register file.
    Private _subsystemControl As UInt16
    Private _advancedFunction As UInt16
    Private _curY As UInt16
    Private _curX As UInt16
    Private _destY As UInt16
    Private _destX As UInt16
    Private _errorTerm As UInt16
    Private _majorAxisCount As UInt16
    Private _minorAxisCount As UInt16
    Private _topScissors As UInt16
    Private _leftScissors As UInt16
    Private _bottomScissors As UInt16 = &HFFFUS
    Private _rightScissors As UInt16 = &HFFFUS
    Private _pixelControl As UInt16
    Private _multifunctionMisc As UInt16
    Private _readRegisterSelect As Byte
    Private _backgroundColor As UInteger
    Private _foregroundColor As UInteger = &HFFFFFFFFUI
    Private _writeMask As UInteger = &HFFFFFFFFUI
    Private _readMask As UInteger = &HFFFFFFFFUI
    Private _colorCompare As UInteger
    Private _backgroundMix As UInt16
    Private _foregroundMix As UInt16 = &H27US
    Private _pixelTransfer As UInt16
    Private _pixelTransferExtension As UInt16
    Private _graphicsEngineBusy As Boolean
    Private _verticalSyncInterruptPending As Boolean
    Private _engineIdleInterruptPending As Boolean
    Private _fifoOverflowInterruptPending As Boolean
    Private _fifoEmptyInterruptPending As Boolean

    ' 86C928 hardware cursor state (CR45-CR4F). Color stacks are modeled as
    ' 24-bit RGB values and the 64x64 AND/XOR cursor pattern lives in VRAM.
    Private _cursorForeground As UInteger = &HFFFFFFUI
    Private _cursorBackground As UInteger
    Private _cursorForegroundWriteByte As Integer
    Private _cursorBackgroundWriteByte As Integer
    Private _nativeCursorX As Integer
    Private _nativeCursorY As Integer

    Private NotInheritable Class EngineCommand928
        Public Command As UInt16
        Public PixelPhasePicoseconds As Long
        Public Progress As Long
        Public TotalPixels As Long
        Public LineX As Integer
        Public LineY As Integer
        Public LineEndX As Integer
        Public LineEndY As Integer
        Public LineDx As Integer
        Public LineDy As Integer
        Public LineStepX As Integer
        Public LineStepY As Integer
        Public LineError As Integer
        Public LineMajorIsY As Boolean
        Public LineRadial As Boolean
        Public Initialized As Boolean
        Public IsShortStroke As Boolean
        Public ShortStrokeValue As UInt16
        Public ShortStrokeVectorIndex As Integer
        Public ShortStrokePixelsRemaining As Integer

        ' The 86C928 command FIFO captures the programming state that belongs to
        ' a queued operation.  Without this snapshot, a driver preparing the next
        ' FIFO entry would retroactively change an already-issued command.
        Public CurX As UInt16
        Public CurY As UInt16
        Public DestX As UInt16
        Public DestY As UInt16
        Public ErrorTerm As UInt16
        Public MajorAxisCount As UInt16
        Public MinorAxisCount As UInt16
        Public TopScissors As UInt16
        Public LeftScissors As UInt16
        Public BottomScissors As UInt16
        Public RightScissors As UInt16
        Public PixelControl As UInt16
        Public MultifunctionMisc As UInt16
        Public BackgroundColor As UInteger
        Public ForegroundColor As UInteger
        Public WriteMask As UInteger
        Public ReadMask As UInteger
        Public ColorCompare As UInteger
        Public BackgroundMix As UInt16
        Public ForegroundMix As UInt16
        Public PixelTransfer As UInt16
        Public PixelTransferExtension As UInt16
        Public BytesPerPixel As Integer
        Public PitchBytes As Integer
        Public SourceBaseAddress As Long
        Public DestinationBaseAddress As Long
        Public TransferInitialized As Boolean
        Public TransferAcrossPlanes As Boolean
        Public TransferWrite As Boolean
        Public TransferWordWidth As Boolean
        Public TransferPixelsPerByte As Integer
        Public TransferUnitsPerRow As Integer
        Public TransferUnitIndex As Long
        Public TransferPixelAccumulator As UInteger
        Public TransferAccumulatorBytes As Integer
        Public TransferReadLowByte As Integer = -1
    End Class
    Private ReadOnly _engineQueue As New System.Collections.Generic.Queue(Of EngineCommand928)()
    Private Const EngineFifoDepth As Integer = 8
    Private Const EnginePixelPeriodPicoseconds As Long = 25000L
    Private _engineActiveCommand As EngineCommand928
    Private _pixelTransferReadLatch As UInt16
    Private ReadOnly _pixelTransferWriteBytes As New System.Collections.Generic.Queue(Of Byte)()
    Private ReadOnly _pixelTransferReadBytes As New System.Collections.Generic.Queue(Of Byte)()
    Private _engineForcedBytesPerPixel As Integer
    Private _engineForcedPitchBytes As Integer
    Private _lastDrawingCommand As UInt16

    Private _frameBitmap As Bitmap
    Private _framePixels() As Integer = Array.Empty(Of Integer)()
    Private _frameHandle As GCHandle
    Private _frameWidth As Integer
    Private _frameHeight As Integer

    Public Sub New()
        ResetDevice()
        ' The allocation-free numeric port ring is always armed.  Verbose text
        ' capture remains available for a short deliberate experiment.
        _diagnosticVgaTraceEnabled = False
    End Sub

    ' ISA RESET is a board reset, not a reconstruction of the host object.
    ' ROM contents, physical VRAM contents, and installed font RAM survive as
    ' storage devices; register/latch/state-machine state returns to reset.
    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        ResetRegisters()

        _setupRegister = 0
        _setupOptionSelect = 0
        _featureControl = 0
        _miscOutput = &H1
        _sequencerIndex = 0
        _crtcIndex = 0
        _graphicsIndex = 0
        _attributeIndex = 0
        _attributeDataPhase = False
        ' ISA RESET blanks the adapter output until BIOS/video firmware explicitly
        ' re-enables display through the Attribute Controller PAS bit.  Leaving
        ' this True exposes stale VRAM through reset-state graphics registers.
        _attributeVideoEnabled = False

        _ramdac.Reset()
        _clockGenerator.Reset()
        InitializeDefaultPalette()

        ResetEnhancedEngine()
        _beamDotPhase = 0.0R
        _frameCounter = 0UL
        _horizontalRetrace = False
        _verticalRetrace = False
        _displayEnable = False
        _diagnosticVgaMemoryReadCount = 0UL
        _diagnosticVgaLatchLoadCount = 0UL
        Array.Clear(_diagnosticVgaWriteModeCounts, 0, _diagnosticVgaWriteModeCounts.Length)
        Array.Clear(_diagnosticVgaMapMaskCounts, 0, _diagnosticVgaMapMaskCounts.Length)
        Array.Clear(_diagnosticVgaPlaneBitsSet, 0, _diagnosticVgaPlaneBitsSet.Length)
        Array.Clear(_diagnosticVgaPlaneBitsCleared, 0, _diagnosticVgaPlaneBitsCleared.Length)
        Array.Clear(_diagnosticVgaMode2Inputs, 0, _diagnosticVgaMode2Inputs.Length)
        _diagnosticVgaChainedWriteCount = 0UL
        _diagnosticVgaUnchainedWriteCount = 0UL
    End Sub

    Public Sub PowerCycleDevice() Implements IPowerCycleDevice.PowerCycleDevice
        ' ISA RESET resets card logic but does not erase fitted VRAM. Removing
        ' chassis power does: begin from deterministic uncharged storage while
        ' preserving immutable option ROM and physical board configuration.
        Array.Clear(_vram, 0, _vram.Length)
        Array.Clear(_latches, 0, _latches.Length)
        ResetDevice()
        _clockGenerator.PowerCycle()
    End Sub

    Private Sub ResetEnhancedEngine()
        _subsystemControl = 0
        _advancedFunction = 0
        _curY = 0
        _curX = 0
        _destY = 0
        _destX = 0
        _errorTerm = 0
        _majorAxisCount = 0
        _minorAxisCount = 0
        _topScissors = 0
        _leftScissors = 0
        _bottomScissors = &HFFFUS
        _rightScissors = &HFFFUS
        _pixelControl = 0
        _multifunctionMisc = 0
        _readRegisterSelect = 0
        _backgroundColor = 0UI
        _foregroundColor = &HFFFFFFFFUI
        _writeMask = &HFFFFFFFFUI
        _readMask = &HFFFFFFFFUI
        _colorCompare = 0UI
        _backgroundMix = 0
        _foregroundMix = &H27US
        _pixelTransfer = 0
        _pixelTransferExtension = 0
        _graphicsEngineBusy = False
        _verticalSyncInterruptPending = False
        _engineIdleInterruptPending = False
        _fifoOverflowInterruptPending = False
        _fifoEmptyInterruptPending = False
        _cursorForeground = &HFFFFFFUI
        _cursorBackground = 0UI
        _cursorForegroundWriteByte = 0
        _cursorBackgroundWriteByte = 0
        _nativeCursorX = 0
        _nativeCursorY = 0
        _engineQueue.Clear()
        _engineActiveCommand = Nothing
        _pixelTransferWriteBytes.Clear()
        _pixelTransferReadBytes.Clear()
        _pixelTransferReadLatch = 0
        _engineForcedBytesPerPixel = 0
        _engineForcedPitchBytes = 0
        _lastDrawingCommand = 0US
    End Sub

    ' The motherboard can sample this just like the output pin of the ISA card.
    ' The bus currently has no interrupt-source interface, so the electrical
    ' connection to a selected PIC input belongs in HardwareBus/MotherboardDevices.
    Public ReadOnly Property InterruptLineAsserted As Boolean
        Get
            Return ((_subsystemControl And &H100US) <> 0 AndAlso _verticalSyncInterruptPending) OrElse
                   ((_subsystemControl And &H200US) <> 0 AndAlso _engineIdleInterruptPending) OrElse
                   ((_subsystemControl And &H400US) <> 0 AndAlso _fifoOverflowInterruptPending) OrElse
                   ((_subsystemControl And &H800US) <> 0 AndAlso _fifoEmptyInterruptPending)
        End Get
    End Property

    Public Sub SetExternalPixelClockHz(clockHzInBed As Long)
        ' Board clock-generator layer supplies programmable clocks. A value <= 0
        ' removes that output and falls back to the fixed VGA oscillator pair.
        _clockGenerator.PixelClockHz = clockHzInBed
    End Sub

    Public ReadOnly Property ModelName As String
        Get
            Return "Diamond Stealth Pro ISA rev C5 / S3 86C928 / 2 MB / Bt485"
        End Get
    End Property

    Public ReadOnly Property BoardProfile As DiamondStealthPro928BoardProfile
        Get
            Return _boardProfile
        End Get
    End Property

    Public Function GetBoardIdentificationText() As String
        Return DiamondStealthPro928BoardProfile.Manufacturer & " " & DiamondStealthPro928BoardProfile.ProductFamily &
               " | " & _boardProfile.Revision &
               " | GPU=" & DiamondStealthPro928BoardProfile.Controller &
               " | bus=" & DiamondStealthPro928BoardProfile.Bus &
               " | FCC=" & DiamondStealthPro928BoardProfile.FccId &
               " | SKU=" & DiamondStealthPro928BoardProfile.ProductSku &
               " | VRAM=" & _boardProfile.VramOrganization &
               " | RAMDAC=" & _boardProfile.RamdacModel &
               " | clock=" & _boardProfile.ClockGeneratorModel &
               " | IRQ=" & _boardProfile.IrqJumper &
               " | ROM=" & _boardProfile.OptionRomIdentity
    End Function

    Public ReadOnly Property StartAddress As UInt16
        Get
            Return CUShort((CUShort(_crtc(&HC)) << 8) Or _crtc(&HD))
        End Get
    End Property

    Public Sub BeginDiagnosticVgaTrace()
        _diagnosticVgaTrace.Clear()
        _diagnosticVgaTraceSequence = 0UL
        _diagnosticDacTrace.Clear()
        _diagnosticDacTraceSequence = 0UL
        _diagnosticVgaStatusReadCount = 0UL
        _diagnosticVgaDacDataWriteCount = 0UL
        _diagnosticLastRenderClass = String.Empty
        _diagnosticVgaTraceEnabled = True
        TraceVgaDiagnostic("TRACE BEGIN")
        TraceVgaDiagnostic("STATE " & DiagnosticVgaStateOneLine())
    End Sub

    Public Sub EndDiagnosticVgaTrace()
        If _diagnosticVgaTraceEnabled Then
            TraceVgaDiagnostic("STATE " & DiagnosticVgaStateOneLine())
            TraceVgaDiagnostic("TRACE END")
        End If
        _diagnosticVgaTraceEnabled = False
    End Sub

    Public ReadOnly Property DiagnosticVgaTraceEnabled As Boolean
        Get
            Return _diagnosticVgaTraceEnabled
        End Get
    End Property

    Public Function GetDiagnosticVgaTrace() As String
        Dim reportInBed As New System.Text.StringBuilder()
        reportInBed.AppendLine("Diamond Stealth Pro / S3 86C928 VGA mode-transition trace")
        reportInBed.AppendLine("Trace enabled: " & If(_diagnosticVgaTraceEnabled, "yes", "no"))
        reportInBed.AppendLine("3BA/3DA input-status reads while tracing: " & _diagnosticVgaStatusReadCount.ToString())
        reportInBed.AppendLine("3C9 DAC data writes while tracing: " & _diagnosticVgaDacDataWriteCount.ToString())
        reportInBed.AppendLine()
        reportInBed.AppendLine(GetDiagnosticVgaStateSnapshot())
        reportInBed.AppendLine()
        reportInBed.AppendLine("--- bounded register trace ---")
        If _diagnosticVgaTrace.Count = 0 Then
            reportInBed.AppendLine("(trace empty)")
        Else
            For Each lineInBed As String In _diagnosticVgaTrace
                reportInBed.AppendLine(lineInBed)
            Next
        End If
        reportInBed.AppendLine()
        AppendDiagnosticVgaPortRingInBed(reportInBed)
        reportInBed.AppendLine()
        AppendDiagnosticDacTraceInBed(reportInBed)
        Return reportInBed.ToString()
    End Function

    Public Function GetDiagnosticVgaStateSnapshot() As String
        Dim snapshotInBed As New System.Text.StringBuilder()
        snapshotInBed.AppendLine(GetBoardIdentificationText())
        snapshotInBed.AppendLine("Renderer class: " & DiagnosticRenderClass())
        snapshotInBed.AppendLine("Attribute video/PAS: " & If(_attributeVideoEnabled, "enabled", "disabled") &
                                 "   AC phase: " & If(_attributeDataPhase, "data", "index") &
                                 "   AC index=" & (_attributeIndex And &H1F).ToString("X2"))
        snapshotInBed.AppendLine("MISC=" & _miscOutput.ToString("X2") &
                                 "  SEQ1=" & _sequencer(1).ToString("X2") &
                                 " SEQ2=" & _sequencer(2).ToString("X2") &
                                 " SEQ4=" & _sequencer(4).ToString("X2"))
        snapshotInBed.AppendLine("GC0=" & _graphics(0).ToString("X2") &
                                 " GC1=" & _graphics(1).ToString("X2") &
                                 " GC2=" & _graphics(2).ToString("X2") &
                                 " GC3=" & _graphics(3).ToString("X2") &
                                 " GC4=" & _graphics(4).ToString("X2") &
                                 " GC5=" & _graphics(5).ToString("X2") &
                                 " GC6=" & _graphics(6).ToString("X2") &
                                 " GC7=" & _graphics(7).ToString("X2") &
                                 " GC8=" & _graphics(8).ToString("X2") &
                                 "  chain4=" & If(Chain4Enabled(), "yes", "no"))
        snapshotInBed.AppendLine("AC10=" & _attribute(&H10).ToString("X2") &
                                 " AC12=" & _attribute(&H12).ToString("X2") &
                                 " AC13=" & _attribute(&H13).ToString("X2") &
                                 " AC14=" & _attribute(&H14).ToString("X2"))
        snapshotInBed.Append("AC palette 00-0F:")
        For attributePaletteIndexInBed As Integer = 0 To &HF
            snapshotInBed.Append(" " & _attribute(attributePaletteIndexInBed).ToString("X2"))
        Next
        snapshotInBed.AppendLine()
        snapshotInBed.Append("AC resolved DAC/RGB:")
        For logicalColorInBed As Integer = 0 To &HF
            Dim resolvedDacIndexInBed As Integer = MapAttributeColor(logicalColorInBed) And &HFF
            snapshotInBed.Append(" " & logicalColorInBed.ToString("X1") & "->" &
                                 resolvedDacIndexInBed.ToString("X2") & "/" &
                                 DiagnosticDacRgb(resolvedDacIndexInBed))
        Next
        snapshotInBed.AppendLine()
        AppendDiagnosticPlanarColorCountsInBed(snapshotInBed)
        snapshotInBed.AppendLine("CR00=" & _crtc(0).ToString("X2") &
                                 " CR01=" & _crtc(1).ToString("X2") &
                                 " CR06=" & _crtc(6).ToString("X2") &
                                 " CR07=" & _crtc(7).ToString("X2") &
                                 " CR09=" & _crtc(9).ToString("X2"))
        snapshotInBed.AppendLine("CR0C=" & _crtc(&HC).ToString("X2") &
                                 " CR0D=" & _crtc(&HD).ToString("X2") &
                                 " CR11=" & _crtc(&H11).ToString("X2") &
                                 " CR13=" & _crtc(&H13).ToString("X2") &
                                 " CR14=" & _crtc(&H14).ToString("X2") &
                                 " CR17=" & _crtc(&H17).ToString("X2"))
        snapshotInBed.AppendLine("StartAddress=" & StartAddress.ToString("X4") &
                                 "  AdvancedFunction=" & _advancedFunction.ToString("X4") &
                                 "  DAC mask=" & _ramdac.PelMask.ToString("X2"))
        snapshotInBed.AppendLine("Engine: busy=" & If(_graphicsEngineBusy, "1", "0") &
                                 " fifo=" & (_engineQueue.Count + If(_engineActiveCommand Is Nothing, 0, 1)).ToString() & "/" & EngineFifoDepth.ToString())
        snapshotInBed.AppendLine("S3: CR31=" & _crtc(&H31).ToString("X2") &
                                 " CR35=" & _crtc(&H35).ToString("X2") &
                                 " CR40=" & _crtc(&H40).ToString("X2") &
                                 " CR45=" & _crtc(&H45).ToString("X2") &
                                 " CR58=" & _crtc(&H58).ToString("X2") &
                                 " CR5D=" & _crtc(&H5D).ToString("X2") &
                                 " CR5E=" & _crtc(&H5E).ToString("X2"))
        snapshotInBed.AppendLine("VRAM: one 2 MiB backing store; bank=" & (GetLegacyBankBaseBytes() \ &H10000).ToString() &
                                 " CR31=" & _crtc(&H31).ToString("X2") &
                                 " CR35=" & _crtc(&H35).ToString("X2") &
                                 " CR51=" & _crtc(&H51).ToString("X2"))
        snapshotInBed.AppendLine("VGA memory: reads=" & _diagnosticVgaMemoryReadCount.ToString("N0") &
                                 " latch-loads=" & _diagnosticVgaLatchLoadCount.ToString("N0") &
                                 " writes chained/unchained=" & _diagnosticVgaChainedWriteCount.ToString("N0") &
                                 "/" & _diagnosticVgaUnchainedWriteCount.ToString("N0"))
        snapshotInBed.AppendLine("VGA write modes 0/1/2/3: " &
                                 _diagnosticVgaWriteModeCounts(0).ToString("N0") & "/" &
                                 _diagnosticVgaWriteModeCounts(1).ToString("N0") & "/" &
                                 _diagnosticVgaWriteModeCounts(2).ToString("N0") & "/" &
                                 _diagnosticVgaWriteModeCounts(3).ToString("N0"))
        Dim usedMasksInBed As New System.Text.StringBuilder()
        For maskInBed As Integer = 0 To 15
            If _diagnosticVgaMapMaskCounts(maskInBed) = 0UL Then Continue For
            If usedMasksInBed.Length <> 0 Then usedMasksInBed.Append(" ")
            usedMasksInBed.Append(maskInBed.ToString("X1")).Append("=").Append(_diagnosticVgaMapMaskCounts(maskInBed).ToString("N0"))
        Next
        snapshotInBed.AppendLine("VGA effective map masks: " & If(usedMasksInBed.Length = 0, "<none>", usedMasksInBed.ToString()))
        For writeModeInBed As Integer = 0 To 3
            snapshotInBed.Append("Mode " & writeModeInBed.ToString() & " plane bit transitions set/clear:")
            For planeInBed As Integer = 0 To 3
                snapshotInBed.Append(" P" & planeInBed.ToString() & "=" &
                                     _diagnosticVgaPlaneBitsSet(writeModeInBed, planeInBed).ToString("N0") & "/" &
                                     _diagnosticVgaPlaneBitsCleared(writeModeInBed, planeInBed).ToString("N0"))
            Next
            snapshotInBed.AppendLine()
        Next
        For logicalOpInBed As Integer = 0 To 3
            snapshotInBed.Append("Mode 2 CPU low nibbles, ROP " & logicalOpInBed.ToString() & ":")
            Dim anyMode2InputInBed As Boolean = False
            For nibbleInBed As Integer = 0 To &HF
                Dim countInBed As ULong = _diagnosticVgaMode2Inputs(logicalOpInBed, nibbleInBed)
                If countInBed = 0UL Then Continue For
                anyMode2InputInBed = True
                snapshotInBed.Append(" " & nibbleInBed.ToString("X1") & "=" & countInBed.ToString("N0"))
            Next
            If Not anyMode2InputInBed Then snapshotInBed.Append(" <none>")
            snapshotInBed.AppendLine()
        Next
        snapshotInBed.AppendLine("Beam: HRETRACE=" & If(_horizontalRetrace, "1", "0") &
                                 " VRETRACE=" & If(_verticalRetrace, "1", "0") &
                                 " DISPLAY=" & If(_displayEnable, "1", "0") &
                                 " dotClock=" & GetDotClockHz().ToString() & " Hz")
        snapshotInBed.AppendLine()
        snapshotInBed.AppendLine("--- most recently preserved graphics state ---")
        snapshotInBed.AppendLine(If(String.IsNullOrEmpty(_diagnosticLastGraphicsState),
                                    "(no graphics-to-text transition observed)",
                                    _diagnosticLastGraphicsState))
        snapshotInBed.AppendLine()
        AppendDiagnosticPaletteInBed(snapshotInBed, "current DAC palette")
        snapshotInBed.AppendLine()
        AppendDiagnosticDacTraceInBed(snapshotInBed)
        Return snapshotInBed.ToString()
    End Function

    Private Sub AppendDiagnosticPlanarColorCountsInBed(targetInBed As System.Text.StringBuilder)
        If targetInBed Is Nothing OrElse DiagnosticRenderClass() <> "VGA planar graphics" Then Return

        Dim widthInBed As Integer = Math.Max(320, Math.Min(2048, GetHorizontalDisplayDots()))
        Dim heightInBed As Integer = Math.Max(200, Math.Min(1200, GetVerticalDisplayLines()))
        Dim stridePlaneBytesInBed As Integer = GetCrtcRowAddressAdvance()
        Dim startPlaneOffsetInBed As Integer =
            NormalizeDisplayPlaneOffsetInBed(CLng(GetCrtcDisplayStartAddressCounterInBed()) * 2L)
        Dim scanlinesInBed() As GraphicsScanlineAddressInBed =
            BuildGraphicsScanlineAddressesInBed(heightInBed,
                                                startPlaneOffsetInBed,
                                                stridePlaneBytesInBed,
                                                packedAddressingInBed:=False)
        Dim countsInBed(15) As Long
        Dim enabledPlanesInBed As Integer = _attribute(&H12) And &HF
        Dim programmedPixelPanInBed As Integer = _attribute(&H13) And &H7

        For yInBed As Integer = 0 To heightInBed - 1
            Dim lowerScreenInBed As Boolean = scanlinesInBed(yInBed).LowerScreen
            Dim pixelPanInBed As Integer =
                If(lowerScreenInBed AndAlso (_attribute(&H10) And &H20) <> 0,
                   0,
                   programmedPixelPanInBed)
            Dim rowBaseInBed As Long = scanlinesInBed(yInBed).Address
            For xInBed As Integer = 0 To widthInBed - 1
                Dim sourceXInBed As Integer = xInBed + pixelPanInBed
                Dim planeOffsetInBed As Long = rowBaseInBed + (sourceXInBed \ 8)
                Dim bitInBed As Integer = &H80 >> (sourceXInBed And 7)
                Dim logicalColorInBed As Integer = 0
                If (enabledPlanesInBed And 1) <> 0 AndAlso
                   (ReadDisplayPlaneByteInBed(0, planeOffsetInBed) And bitInBed) <> 0 Then logicalColorInBed = logicalColorInBed Or 1
                If (enabledPlanesInBed And 2) <> 0 AndAlso
                   (ReadDisplayPlaneByteInBed(1, planeOffsetInBed) And bitInBed) <> 0 Then logicalColorInBed = logicalColorInBed Or 2
                If (enabledPlanesInBed And 4) <> 0 AndAlso
                   (ReadDisplayPlaneByteInBed(2, planeOffsetInBed) And bitInBed) <> 0 Then logicalColorInBed = logicalColorInBed Or 4
                If (enabledPlanesInBed And 8) <> 0 AndAlso
                   (ReadDisplayPlaneByteInBed(3, planeOffsetInBed) And bitInBed) <> 0 Then logicalColorInBed = logicalColorInBed Or 8
                countsInBed(logicalColorInBed) += 1
            Next
        Next

        targetInBed.Append("Visible planar logical-color counts:")
        For logicalColorInBed As Integer = 0 To &HF
            targetInBed.Append(" " & logicalColorInBed.ToString("X1") & "=" & countsInBed(logicalColorInBed).ToString())
        Next
        targetInBed.AppendLine()
    End Sub

    Private Sub AppendDiagnosticPaletteInBed(targetInBed As System.Text.StringBuilder,
                                             titleInBed As String)
        targetInBed.AppendLine("--- " & titleInBed & " (index:RRGGBB raw DAC components) ---")
        For firstIndexInBed As Integer = 0 To 255 Step 8
            targetInBed.Append(firstIndexInBed.ToString("X2")).Append(":")
            For indexInBed As Integer = firstIndexInBed To firstIndexInBed + 7
                targetInBed.Append(" ").Append(indexInBed.ToString("X2")).Append(":")
                targetInBed.Append(_ramdac.Component(indexInBed, 0).ToString("X2"))
                targetInBed.Append(_ramdac.Component(indexInBed, 1).ToString("X2"))
                targetInBed.Append(_ramdac.Component(indexInBed, 2).ToString("X2"))
            Next
            targetInBed.AppendLine()
        Next
    End Sub

    Private Sub PreserveGraphicsStateBeforeTextTransitionInBed(reasonInBed As String)
        If IsTextMode() Then Return
        Dim preservedInBed As New System.Text.StringBuilder()
        preservedInBed.AppendLine("Reason: " & reasonInBed)
        preservedInBed.AppendLine(DiagnosticVgaStateOneLine())
        preservedInBed.AppendLine("DAC mask=" & _ramdac.PelMask.ToString("X2") &
                                  " read/write state=" & If(_ramdac.IsReading, "read", "write") &
                                  " component=" & _ramdac.ComponentPhase.ToString())
        AppendDiagnosticPaletteInBed(preservedInBed, "preserved pre-text DAC palette")
        _diagnosticLastGraphicsState = preservedInBed.ToString()
        TraceVgaDiagnostic("PRESERVED GRAPHICS STATE before " & reasonInBed)
    End Sub

    Private Function DiagnosticRenderClass() As String
        If Not VideoOutputEnabledInBed() Then Return "BLANK (video output gated)"
        If IsTextMode() Then Return "TEXT"
        If EnhancedDisplayEnabled() Then
            Return "S3 enhanced / Bt485 " & _ramdac.DisplayBitsPerPixel.ToString() & " bpp"
        End If
        If (_graphics(5) And &H40) <> 0 Then
            Return If(Chain4Enabled(),
                      "VGA 256-color / chain-4",
                      "VGA 256-color / unchained")
        End If
        If IsCgaCompatibilityScanoutInBed() Then Return "VGA CGA-compatible graphics"
        Return "VGA planar graphics"
    End Function

    Private Function DiagnosticVgaStateOneLine() As String
        Return "class=" & DiagnosticRenderClass() &
               " PAS=" & If(_attributeVideoEnabled, "1", "0") &
               " ACphase=" & If(_attributeDataPhase, "D", "I") &
               " SEQ4=" & _sequencer(4).ToString("X2") &
               " GC5=" & _graphics(5).ToString("X2") &
               " GC6=" & _graphics(6).ToString("X2") &
               " AC10=" & _attribute(&H10).ToString("X2") &
               " CR11=" & _crtc(&H11).ToString("X2") &
               " CR13=" & _crtc(&H13).ToString("X2")
    End Function

    Private Sub TraceVgaDiagnostic(messageInBed As String)
        If Not _diagnosticVgaTraceEnabled Then Return
        _diagnosticVgaTraceSequence += 1UL
        While _diagnosticVgaTrace.Count >= DiagnosticVgaTraceCapacity
            _diagnosticVgaTrace.Dequeue()
        End While
        _diagnosticVgaTrace.Enqueue("#" & _diagnosticVgaTraceSequence.ToString("000000") & " " & messageInBed)
    End Sub

    Private Sub TraceVgaPortWriteInBed(portInBed As UInt16, valueInBed As Byte)
        Dim registerIndexInBed As Byte = &HFF
        Select Case portInBed
            Case &H3C0US : registerIndexInBed = CByte(_attributeIndex And &H1F)
            Case &H3C5US : registerIndexInBed = _sequencerIndex
            Case &H3CFUS : registerIndexInBed = _graphicsIndex
            Case &H3B5US, &H3D5US : registerIndexInBed = _crtcIndex
        End Select

        _diagnosticVgaPortSequenceInBed += 1UL
        _diagnosticVgaPortRingInBed(_diagnosticVgaPortRingIndexInBed) =
            New DiagnosticVgaPortWriteInBed With {
                .Sequence = _diagnosticVgaPortSequenceInBed,
                .Port = portInBed,
                .RegisterIndex = registerIndexInBed,
                .Value = valueInBed
            }
        _diagnosticVgaPortRingIndexInBed =
            (_diagnosticVgaPortRingIndexInBed + 1) Mod DiagnosticVgaPortRingCapacityInBed
        If _diagnosticVgaPortRingCountInBed < DiagnosticVgaPortRingCapacityInBed Then
            _diagnosticVgaPortRingCountInBed += 1
        End If
    End Sub

    Private Sub AppendDiagnosticVgaPortRingInBed(targetInBed As System.Text.StringBuilder)
        targetInBed.AppendLine("--- allocation-free VGA port-write flight recorder ---")
        targetInBed.AppendLine("Retained " & _diagnosticVgaPortRingCountInBed.ToString() &
                               " of " & _diagnosticVgaPortSequenceInBed.ToString() &
                               " writes since board construction")
        If _diagnosticVgaPortRingCountInBed = 0 Then
            targetInBed.AppendLine("(no VGA port writes retained)")
            Return
        End If
        Dim firstInBed As Integer =
            (_diagnosticVgaPortRingIndexInBed - _diagnosticVgaPortRingCountInBed +
             DiagnosticVgaPortRingCapacityInBed) Mod DiagnosticVgaPortRingCapacityInBed
        For ordinalInBed As Integer = 0 To _diagnosticVgaPortRingCountInBed - 1
            Dim entryInBed As DiagnosticVgaPortWriteInBed =
                _diagnosticVgaPortRingInBed(
                    (firstInBed + ordinalInBed) Mod DiagnosticVgaPortRingCapacityInBed)
            targetInBed.Append("P#").Append(entryInBed.Sequence.ToString("000000000")).
                Append(" OUT ").Append(entryInBed.Port.ToString("X4"))
            If entryInBed.RegisterIndex <> &HFF Then
                targetInBed.Append("[").Append(entryInBed.RegisterIndex.ToString("X2")).Append("]")
            End If
            targetInBed.Append(" <- ").Append(entryInBed.Value.ToString("X2")).AppendLine()
        Next
    End Sub

    Private Sub TraceDacDiagnosticInBed(messageInBed As String)
        If Not _diagnosticVgaTraceEnabled Then Return
        _diagnosticDacTraceSequence += 1UL
        While _diagnosticDacTrace.Count >= DiagnosticDacTraceCapacity
            _diagnosticDacTrace.Dequeue()
        End While
        _diagnosticDacTrace.Enqueue("D#" & _diagnosticDacTraceSequence.ToString("000000") & " " & messageInBed)
    End Sub

    Private Sub AppendDiagnosticDacTraceInBed(targetInBed As System.Text.StringBuilder)
        targetInBed.AppendLine("--- dedicated Bt485 3C6h-3C9h flight recorder ---")
        If _diagnosticDacTrace.Count = 0 Then
            targetInBed.AppendLine("(no DAC port activity retained)")
            Return
        End If
        For Each lineInBed As String In _diagnosticDacTrace
            targetInBed.AppendLine(lineInBed)
        Next
    End Sub

    Private Sub TraceVgaRenderClassIfChanged()
        If Not _diagnosticVgaTraceEnabled Then Return
        Dim currentClassInBed As String = DiagnosticRenderClass()
        If String.Equals(currentClassInBed, _diagnosticLastRenderClass, StringComparison.Ordinal) Then Return
        _diagnosticLastRenderClass = currentClassInBed
        TraceVgaDiagnostic("RENDER -> " & currentClassInBed & "   " & DiagnosticVgaStateOneLine())
    End Sub

    Public Sub LoadOptionRom(data As Byte())
        If data Is Nothing Then Throw New ArgumentNullException(NameOf(data))
        If data.Length = 0 OrElse data.Length > CInt(OptionRomWindowSize) Then
            Throw New ArgumentException("Diamond VGA BIOS ROM must fit within C0000h-C7FFFh.", NameOf(data))
        End If
        Dim hadOptionRomInBed As Boolean = _optionRom.Length > 0
        _optionRom = CType(data.Clone(), Byte())
        If Not hadOptionRomInBed Then RaiseEvent MemoryDecodeChanged()
    End Sub

    Private Sub ResetRegisters()
        Array.Clear(_sequencer, 0, _sequencer.Length)
        Array.Clear(_crtc, 0, _crtc.Length)
        Array.Clear(_graphics, 0, _graphics.Length)
        Array.Clear(_attribute, 0, _attribute.Length)
        _sequencer(2) = &HF
        _sequencer(4) = &H6
        _graphics(6) = &H5
        _graphics(7) = &HF
        _graphics(8) = &HFF
        _crtc(&H30) = &H90          ' S3 86C928 chip ID/revision reset value
        ' Board-strap values identify the ISA/2 MiB Diamond profile.  The
        ' option ROM programs operational display timings after POST scans C000h.
        ' Read-only reset-state straps for the chosen Diamond-style ISA/2 MiB profile.
        ' CR36: ISA bus, 16-bit ROM path, full C0000-C7FFF decode, LA23:17 MEMCS16,
        '        2 MiB VRAM.  CR37: setup through 46E8 bit4, reserved bit9=1,
        '        NOWS enabled, MEMCS16 generated by the 86C928.
        _crtc(&H36) = &H9B
        _crtc(&H37) = &H1B
        _crtc(&H38) = 0
        _crtc(&H39) = 0
        _crtc(&H3A) = &HD0
        _attributeDataPhase = False
        _attributeVideoEnabled = True
        _graphicsEngineBusy = False
    End Sub

    Private Sub InitializeDefaultPalette()
        For i As Integer = 0 To 15
            _attribute(i) = CByte(i)
        Next
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        ' 46E8h is the ISA subsystem setup latch.  0102h is decoded only while
        ' the board is in setup mode; normal operation ignores 0102h.
        If port = &H46E8US Then Return True
        If port = &H102US Then Return SetupModeSelected()
        If Not BoardAddressDecodeEnabled() Then Return False
        If IsLegacyPortDecoded(port) Then Return True
        If IsEnhancedPort(port) Then Return (_crtc(&H40) And 1) <> 0
        Return False
    End Function

    Private Function IsLegacyPortDecoded(port As UInt16) As Boolean
        ' 3C0h-3CFh is common VGA decode. The CRTC index/data and Input Status 1
        ' blocks are selected by Misc Output bit 0 exactly as on VGA hardware.
        If port >= &H3C0US AndAlso port <= &H3CFUS Then Return True

        Dim colorDecode As Boolean = (_miscOutput And 1) <> 0
        If colorDecode Then
            Return port >= &H3D0US AndAlso port <= &H3DFUS
        End If
        Return port >= &H3B0US AndAlso port <= &H3BFUS
    End Function

    Private Function GetBt485RegisterSelectInBed(portInBed As UInt16) As Integer
        Dim lowSelectInBed As Integer
        Select Case portInBed
            Case &H3C8US : lowSelectInBed = 0
            Case &H3C9US : lowSelectInBed = 1
            Case &H3C6US : lowSelectInBed = 2
            Case Else : lowSelectInBed = 3
        End Select

        ' 86C928 CR55[1:0] drive DAC RS3:2.  When both are zero the legacy
        ' CR43[1] extension drives RS2, exactly as specified by the S3 manual.
        Dim highSelectInBed As Integer = (_crtc(&H55) And 3) << 2
        If highSelectInBed = 0 AndAlso (_crtc(&H43) And 2) <> 0 Then highSelectInBed = 4
        Return highSelectInBed Or lowSelectInBed
    End Function

    Private Function ReadBt485PortInBed(portInBed As UInt16) As Byte
        Dim registerSelectInBed As Integer = GetBt485RegisterSelectInBed(portInBed)
        Dim readIndexBeforeInBed As Byte = _ramdac.ReadIndex
        Dim writeIndexBeforeInBed As Byte = _ramdac.WriteIndex
        Dim componentBeforeInBed As Integer = _ramdac.ComponentPhase
        Dim readingBeforeInBed As Boolean = _ramdac.IsReading
        Dim resultInBed As Byte = _ramdac.ReadRegister(registerSelectInBed)
        TraceDacDiagnosticInBed("IN  " & portInBed.ToString("X4") &
                                " RS=" & registerSelectInBed.ToString("X1") &
                                " -> " & resultInBed.ToString("X2") &
                                " pre=" & If(readingBeforeInBed, "R", "W") &
                                " ri=" & readIndexBeforeInBed.ToString("X2") &
                                " wi=" & writeIndexBeforeInBed.ToString("X2") &
                                " c=" & componentBeforeInBed.ToString())
        Return resultInBed
    End Function

    Public Function PotentiallyHandlesPort(port As UInt16) As Boolean Implements IPortDecodeCandidateProvider.PotentiallyHandlesPort
        ' Physical chip-select candidates are broader than current enable state.
        ' The bus compiles these candidates once, then HandlesPort still decides
        ' whether the 86C928 actually responds on each live bus cycle.
        If port = &H46E8US OrElse port = &H102US Then Return True
        If port >= &H3B0US AndAlso port <= &H3DFUS Then Return True

        ' CR43 can XOR-remap the enhanced x2E8/x6E8/xAE8/xEE8 families.
        ' Test both electrical aliases so either legal strap/register state is
        ' represented in the compiled PCB candidate map.
        Return IsEnhancedPort(port) OrElse
               IsEnhancedPort(CUShort(port Xor &H3A0US))
    End Function

    Private Function IsEnhancedPort(port As UInt16) As Boolean
        Dim canonical As UInt16 = CanonicalEnhancedPort(port)
        Select Case canonical
            Case &H42E8US, &H42E9US, &H4AE8US, &H4AE9US,
                 &H82E8US, &H82E9US, &H86E8US, &H86E9US,
                 &H8AE8US, &H8AE9US, &H8EE8US, &H8EE9US,
                 &H92E8US, &H92E9US, &H96E8US, &H96E9US,
                 &H9AE8US, &H9AE9US, &H9EE8US, &H9EE9US,
                 &HA2E8US, &HA2E9US, &HA6E8US, &HA6E9US,
                 &HAAE8US, &HAAE9US, &HAEE8US, &HAEE9US,
                 &HB2E8US, &HB2E9US, &HB6E8US, &HB6E9US,
                 &HBAE8US, &HBAE9US, &HBEE8US, &HBEE9US,
                 &HE2E8US, &HE2E9US, &HE2EAUS, &HE2EBUS
                Return True
        End Select
        Return False
    End Function

    Private Function CanonicalEnhancedPort(port As UInt16) As UInt16
        ' CR43 bit 4 remaps x2E8/x6E8/xAE8/xEE8 families by XOR 03A0h.
        If (_crtc(&H43) And &H10) <> 0 Then Return CUShort(port Xor &H3A0US)
        Return port
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If port = &H102US Then Return _setupOptionSelect
        If port = &H46E8US Then Return _setupRegister
        Dim enhancedInBed As Boolean = IsEnhancedPort(port) AndAlso (_crtc(&H40) And 1) <> 0
        If Not enhancedInBed AndAlso Not IsLegacyPortDecoded(port) Then Return &HFF

        Select Case port
            Case &H3C0US
                Return CByte((_attributeIndex And &H1F) Or If(_attributeVideoEnabled, &H20, 0))
            Case &H3C1US
                ' IBM VGA Hardware Technical Reference, Attribute Controller:
                ' PAS=1 enables display output and protects palette registers 00-0F
                ' from CPU access; control registers 10-14 remain accessible.
                ' https://bitsavers.org/pdf/ibm/pc/cards/IBM_VGA_XGA_Technical_Reference_Manual_May92.pdf
                If (_attributeIndex And &H1F) <= &HF AndAlso _attributeVideoEnabled Then Return &HFF
                Return _attribute(_attributeIndex And &H1F)
            Case &H3C2US
                ' S3 86C928 Data Book, Input Status Register 0 (3C2h), p. 6-2:
                ' bit 7 is active-low vertical-retrace interrupt and bit 4 is
                ' the board's monitor-sense input.  This profile has a CRT attached.
                ' https://www.dosdays.co.uk/media/s3/928/86C928_Datasheet.pdf
                Return CByte(&H10 Or If(Not _verticalRetrace, &H80, 0))
            Case &H3C4US
                Return _sequencerIndex
            Case &H3C5US
                Return _sequencer(_sequencerIndex)
            Case &H3C6US
                Return ReadBt485PortInBed(port)
            Case &H3C7US
                Return ReadBt485PortInBed(port)
            Case &H3C8US
                Return ReadBt485PortInBed(port)
            Case &H3C9US
                Return ReadBt485PortInBed(port)
            Case &H3CAUS
                Return _featureControl
            Case &H3CCUS
                Return _miscOutput
            Case &H3CEUS
                Return _graphicsIndex
            Case &H3CFUS
                Return _graphics(_graphicsIndex)
            Case &H3BAUS, &H3DAUS
                _diagnosticVgaStatusReadCount += 1UL
                If _diagnosticVgaTraceEnabled Then
                    If _attributeDataPhase Then
                        TraceVgaDiagnostic("IN " & port.ToString("X4") & " status: AC flip-flop data->index")
                    End If
                End If
                _attributeDataPhase = False
                ' S3 86C928 Data Book, Input Status Register 1 (3BA/3DA),
                ' pp. 6-44/6-45: bit 2 is the fixed diagnostic-comparator result.
                Dim status As Byte = &H4
                If _verticalRetrace Then status = CByte(status Or &H8)
                ' VGA Input Status Register 1 bit 0 is the inverted Display Enable
                ' signal: 1 during horizontal/vertical blanking, 0 while the active
                ' raster is being displayed.  This polarity is relied upon by BIOS
                ' and software retrace polling loops.
                If Not _displayEnable Then status = CByte(status Or 1)
                Return status
            Case &H3B4US, &H3D4US
                Return _crtcIndex
            Case &H3B5US, &H3D5US
                Return ReadCrtc(_crtcIndex)
        End Select

        If IsEnhancedPort(port) Then
            Dim canonical As UInt16 = CanonicalEnhancedPort(port)
            Dim basePort As UInt16 = CUShort(canonical And &HFFFEUS)
            If basePort = &HE2E8US Then Return ReadPixelTransferByteInBed()
            Dim value As UInt16 = ReadEnhancedWord(basePort)
            If (canonical And 1US) <> 0 Then Return CByte(value >> 8)
            Return CByte(value And &HFFUS)
        End If
        Return &HFF
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        TraceVgaPortWriteInBed(port, value)
        If port = &H102US Then
            TraceVgaDiagnostic("OUT 0102 <- " & value.ToString("X2") & "  setup option")
            Dim decodeBeforeInBed As Boolean = BoardAddressDecodeEnabled()
            _setupOptionSelect = value
            If BoardAddressDecodeEnabled() <> decodeBeforeInBed Then RaiseEvent MemoryDecodeChanged()
            Return
        End If
        If port = &H46E8US Then
            TraceVgaDiagnostic("OUT 46E8 <- " & value.ToString("X2") & "  subsystem setup")
            Dim decodeBeforeInBed As Boolean = BoardAddressDecodeEnabled()
            _setupRegister = value
            If BoardAddressDecodeEnabled() <> decodeBeforeInBed Then RaiseEvent MemoryDecodeChanged()
            Return
        End If
        Dim enhancedInBed As Boolean = IsEnhancedPort(port) AndAlso (_crtc(&H40) And 1) <> 0
        If Not enhancedInBed AndAlso Not IsLegacyPortDecoded(port) Then Return

        Select Case port
            Case &H3C0US
                If Not _attributeDataPhase Then
                    TraceVgaDiagnostic("OUT 3C0 AC index <- " & (value And &H1F).ToString("X2") &
                                       " PAS=" & If((value And &H20) <> 0, "1", "0"))
                    _attributeIndex = CByte(value And &H1F)
                    _attributeVideoEnabled = (value And &H20) <> 0
                    _attributeDataPhase = True
                Else
                    TraceVgaDiagnostic("OUT 3C0 AC[" & (_attributeIndex And &H1F).ToString("X2") &
                                       "] <- " & value.ToString("X2"))
                    Dim attributeRegisterInBed As Integer = _attributeIndex And &H1F
                    ' IBM VGA Hardware Technical Reference, Attribute Controller:
                    ' PAS protects palette registers 00-0F while video is enabled.
                    Dim paletteLockedInBed As Boolean =
                        (_crtc(&H33) And &H40) <> 0 AndAlso
                        (attributeRegisterInBed <= &HF OrElse attributeRegisterInBed = &H11)
                    ' S3 86C928 Data Book BKWD_2 CR33 bit 6, p. 7-3, locks
                    ' Attribute palette and overscan writes independently of PAS.
                    If Not paletteLockedInBed AndAlso
                       (attributeRegisterInBed > &HF OrElse Not _attributeVideoEnabled) Then
                        _attribute(attributeRegisterInBed) =
                            If(attributeRegisterInBed <= &HF, CByte(value And &H3F), value)
                    End If
                    _attributeDataPhase = False
                End If
                Return
            Case &H3C2US
                TraceVgaDiagnostic("OUT 3C2 MISC <- " & value.ToString("X2"))
                Dim memoryDecodeBeforeInBed As Boolean = VideoMemoryDecodeEnabled()
                ' S3 86C928 Data Book BKWD_3 CR34 bit 7, p. 7-4: lock only
                ' Misc Output clock-select bits 3:2; all other pins remain writable.
                If (_crtc(&H34) And &H80) <> 0 Then
                    value = CByte((value And Not &HC) Or (_miscOutput And &HC))
                End If
                _miscOutput = value
                If VideoMemoryDecodeEnabled() <> memoryDecodeBeforeInBed Then RaiseEvent MemoryDecodeChanged()
                Return
            Case &H3C4US
                If _diagnosticVgaTraceEnabled Then TraceVgaDiagnostic("OUT 3C4 SEQ index <- " & value.ToString("X2"))
                _sequencerIndex = value
                Return
            Case &H3C5US
                If _diagnosticVgaTraceEnabled Then TraceVgaDiagnostic("OUT 3C5 SEQ[" & _sequencerIndex.ToString("X2") & "] <- " & value.ToString("X2"))
                ' S3 86C928 Data Book BKWD_3 CR34 bit 5, p. 7-4: only SR1
                ' bit 0 (8/9-dot character width) is locked.
                If _sequencerIndex = 1 AndAlso (_crtc(&H34) And &H20) <> 0 Then
                    value = CByte((value And Not 1) Or (_sequencer(1) And 1))
                End If
                _sequencer(_sequencerIndex) = value
                Return
            Case &H3C6US
                TraceDacDiagnosticInBed("OUT 3C6 RS=" & GetBt485RegisterSelectInBed(port).ToString("X1") &
                                        " <- " & value.ToString("X2") &
                                        " pre=" & If(_ramdac.IsReading, "R", "W") &
                                        " ri=" & _ramdac.ReadIndex.ToString("X2") &
                                        " wi=" & _ramdac.WriteIndex.ToString("X2") &
                                        " c=" & _ramdac.ComponentPhase.ToString())
                TraceVgaDiagnostic("OUT 3C6 DAC RS=" & GetBt485RegisterSelectInBed(port).ToString("X1") & " <- " & value.ToString("X2"))
                If (_crtc(&H33) And &H10) = 0 Then _ramdac.WriteRegister(GetBt485RegisterSelectInBed(port), value)
                Return
            Case &H3C7US
                TraceDacDiagnosticInBed("OUT 3C7 RS=" & GetBt485RegisterSelectInBed(port).ToString("X1") &
                                        " <- " & value.ToString("X2") &
                                        " pre=" & If(_ramdac.IsReading, "R", "W") &
                                        " ri=" & _ramdac.ReadIndex.ToString("X2") &
                                        " wi=" & _ramdac.WriteIndex.ToString("X2") &
                                        " c=" & _ramdac.ComponentPhase.ToString())
                TraceVgaDiagnostic("OUT 3C7 DAC RS=" & GetBt485RegisterSelectInBed(port).ToString("X1") & " <- " & value.ToString("X2"))
                If (_crtc(&H33) And &H10) = 0 Then _ramdac.WriteRegister(GetBt485RegisterSelectInBed(port), value)
                Return
            Case &H3C8US
                TraceDacDiagnosticInBed("OUT 3C8 RS=" & GetBt485RegisterSelectInBed(port).ToString("X1") &
                                        " <- " & value.ToString("X2") &
                                        " pre=" & If(_ramdac.IsReading, "R", "W") &
                                        " ri=" & _ramdac.ReadIndex.ToString("X2") &
                                        " wi=" & _ramdac.WriteIndex.ToString("X2") &
                                        " c=" & _ramdac.ComponentPhase.ToString())
                TraceVgaDiagnostic("OUT 3C8 DAC RS=" & GetBt485RegisterSelectInBed(port).ToString("X1") & " <- " & value.ToString("X2"))
                If (_crtc(&H33) And &H10) = 0 Then _ramdac.WriteRegister(GetBt485RegisterSelectInBed(port), value)
                Return
            Case &H3C9US
                If _diagnosticVgaTraceEnabled Then _diagnosticVgaDacDataWriteCount += 1UL
                TraceDacDiagnosticInBed("OUT 3C9 RS=" & GetBt485RegisterSelectInBed(port).ToString("X1") &
                                        " <- " & value.ToString("X2") &
                                        " pre=" & If(_ramdac.IsReading, "R", "W") &
                                        " ri=" & _ramdac.ReadIndex.ToString("X2") &
                                        " wi=" & _ramdac.WriteIndex.ToString("X2") &
                                        " c=" & _ramdac.ComponentPhase.ToString())
                TraceVgaDiagnostic("OUT 3C9 DAC[" & _ramdac.WriteIndex.ToString("X2") &
                                   "]." & _ramdac.ComponentPhase.ToString() &
                                   " <- " & value.ToString("X2"))
                ' S3 86C928 Data Book BKWD_2 CR33 bit 4, p. 7-3: all video
                ' DAC register writes are blocked while reads remain live.
                If (_crtc(&H33) And &H10) = 0 Then _ramdac.WriteRegister(GetBt485RegisterSelectInBed(port), value)
                Return
            Case &H3CEUS
                If _diagnosticVgaTraceEnabled Then TraceVgaDiagnostic("OUT 3CE GC index <- " & value.ToString("X2"))
                _graphicsIndex = value
                Return
            Case &H3CFUS
                If _diagnosticVgaTraceEnabled Then TraceVgaDiagnostic("OUT 3CF GC[" & _graphicsIndex.ToString("X2") & "] <- " & value.ToString("X2"))
                Dim oldGraphicsValueInBed As Byte = _graphics(_graphicsIndex)
                If _graphicsIndex = &H6 AndAlso
                   (oldGraphicsValueInBed And 1) <> 0 AndAlso
                   (value And 1) = 0 Then
                    PreserveGraphicsStateBeforeTextTransitionInBed("GC06 graphics-to-text write")
                End If
                _graphics(_graphicsIndex) = value
                If _graphicsIndex = &H6 AndAlso
                   ((oldGraphicsValueInBed Xor value) And &HC) <> 0 Then
                    RaiseEvent MemoryDecodeChanged()
                End If
                Return
            Case &H3BAUS, &H3DAUS
                TraceVgaDiagnostic("OUT " & port.ToString("X4") & " feature <- " & value.ToString("X2"))
                _featureControl = value
                Return
            Case &H3B4US, &H3D4US
                TraceVgaDiagnostic("OUT " & port.ToString("X4") & " CRTC index <- " & value.ToString("X2"))
                _crtcIndex = value
                Return
            Case &H3B5US, &H3D5US
                WriteCrtc(_crtcIndex, value)
                Return
        End Select

        If IsEnhancedPort(port) Then
            Dim canonical As UInt16 = CanonicalEnhancedPort(port)
            Dim basePort As UInt16 = CUShort(canonical And &HFFFEUS)
            WriteEnhancedByte(basePort, (canonical And 1US) <> 0, value)
        End If
    End Sub

    Private Sub WriteEnhancedByte(basePortInBed As UInt16, highByteInBed As Boolean, valueInBed As Byte)
        ' ISA byte-lane writes do not perform an implicit read.  In particular,
        ' 42E8 and 9AE8 return status on reads but accept control/command on writes.
        If basePortInBed = &HE2E8US Then
            _pixelTransfer = CUShort(valueInBed)
            FeedPixelTransferByteInBed(valueInBed)
            Return
        End If
        Dim writeShadowInBed As UInt16 = ReadEnhancedWriteShadow(basePortInBed)
        If highByteInBed Then
            writeShadowInBed = CUShort((writeShadowInBed And &HFFUS) Or (CUShort(valueInBed) << 8))
        Else
            writeShadowInBed = CUShort((writeShadowInBed And &HFF00US) Or valueInBed)
        End If
        WriteEnhancedWord(basePortInBed, writeShadowInBed)
    End Sub

    Private Function ReadEnhancedWriteShadow(portInBed As UInt16) As UInt16
        Select Case portInBed
            Case &H42E8US : Return _subsystemControl
            Case &H4AE8US : Return _advancedFunction
            Case &H82E8US : Return _curY
            Case &H86E8US : Return _curX
            Case &H8AE8US : Return _destY
            Case &H8EE8US : Return _destX
            Case &H92E8US : Return _errorTerm
            Case &H96E8US : Return _majorAxisCount
            Case &HA2E8US : Return PeekEngineDwordHalfInBed(_backgroundColor)
            Case &HA6E8US : Return PeekEngineDwordHalfInBed(_foregroundColor)
            Case &HAAE8US : Return PeekEngineDwordHalfInBed(_writeMask)
            Case &HAEE8US : Return PeekEngineDwordHalfInBed(_readMask)
            Case &HB2E8US : Return PeekEngineDwordHalfInBed(_colorCompare)
            Case &HB6E8US : Return _backgroundMix
            Case &HBAE8US : Return _foregroundMix
            Case &HE2E8US : Return _pixelTransfer
            Case &HE2EAUS : Return _pixelTransferExtension
            Case Else : Return 0US
        End Select
    End Function

    Public Function ReadPortWord(port As UInt16) As UInt16 Implements IWordPortDevice.ReadPortWord
        If IsEnhancedPort(port) Then Return ReadEnhancedWord(CanonicalEnhancedPort(port))
        Return CUShort(ReadPort(port) Or (CUShort(ReadPort(CUShort(port + 1))) << 8))
    End Function

    Public Sub WritePortWord(port As UInt16, value As UInt16) Implements IWordPortDevice.WritePortWord
        If IsEnhancedPort(port) Then
            WriteEnhancedWord(CanonicalEnhancedPort(port), value)
            Return
        End If
        WritePort(port, CByte(value And &HFFUS))
        WritePort(CUShort(port + 1), CByte(value >> 8))
    End Sub

    Private Function ReadCrtc(index As Byte) As Byte
        ' CR38 is the lock key itself and must remain reachable at reset.  The
        ' other S3 VGA-extension registers are hidden until that key is loaded.
        If index >= &H30 AndAlso index <= &H3C AndAlso index <> &H38 Then
            If Not S3VgaRegistersUnlocked() Then Return 0
        End If
        If index >= &H40 AndAlso index <= &H5F Then
            If Not S3SystemRegistersUnlocked() Then Return 0
        End If
        Select Case index
            Case &H24
                Return CByte((If(_attributeDataPhase, &H80, 0)) Or
                             (If(_attributeVideoEnabled, &H20, 0)) Or
                             (_attributeIndex And &H1F))
            Case &H30
                Return &H90                    ' 86C928 ID/revision
            Case &H36
                Return &H9B                    ' ISA, 16-bit ROM, full ROM decode, 2 MiB
            Case &H37
                Return &H1B                    ' bit4 setup select, NOWS, internal MEMCS16
            Case &H45
                _cursorForegroundWriteByte = 0
                _cursorBackgroundWriteByte = 0
                Return _crtc(&H45)
            Case &H4A
                Return ReadCursorColorStack(True)
            Case &H4B
                Return ReadCursorColorStack(False)
        End Select
        Return _crtc(index)
    End Function

    Private Sub WriteCrtc(index As Byte, value As Byte)
        If index = &H38 Then
            TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " APPLIED lock key")
            _crtc(index) = value
            Return
        End If
        If index >= &H30 AndAlso index <= &H3C AndAlso Not S3VgaRegistersUnlocked() Then
            TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " BLOCKED S3 VGA lock")
            Return
        End If
        If index >= &H40 AndAlso index <= &H5F AndAlso Not S3SystemRegistersUnlocked() Then
            TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " BLOCKED S3 system lock")
            Return
        End If
        If index = &H4A Then
            WriteCursorColorStack(True, value)
            Return
        End If
        If index = &H4B Then
            WriteCursorColorStack(False, value)
            Return
        End If
        If index = &H30 OrElse index = &H36 OrElse index = &H37 Then
            TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " BLOCKED read-only strap")
            Return
        End If
        ' S3 86C928 Data Book CR35, p. 7-5: timing locks are register/bit
        ' granular.  Preserve only the protected fields so unrelated control
        ' bits in the same CRTC register still follow the bus write.
        If (_crtc(&H35) And &H20) <> 0 Then
            If index <= 5 Then
                TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " BLOCKED CR35 horizontal lock")
                Return
            ElseIf index = &H17 Then
                value = CByte((value And Not &H4) Or (_crtc(&H17) And &H4))
            End If
        End If
        If (_crtc(&H35) And &H10) <> 0 Then
            Select Case index
                Case &H6, &H10, &H15, &H16
                    TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " BLOCKED CR35 vertical lock")
                    Return
                Case &H7
                    Const verticalOverflowLockMaskInBed As Integer = &HAD ' bits 7,5,3,2,0
                    value = CByte((value And Not verticalOverflowLockMaskInBed) Or
                                  (_crtc(7) And verticalOverflowLockMaskInBed))
                Case &H9
                    value = CByte((value And Not &H20) Or (_crtc(9) And &H20))
                Case &H11
                    value = CByte((value And &HF0) Or (_crtc(&H11) And &HF))
            End Select
        End If
        If index <= 7 AndAlso (_crtc(&H11) And &H80) <> 0 Then
            If index = 7 Then
                ' VGA CR11 protection normally leaves only CR7 bit 4 writable.
                ' S3 CR33 bit 1 additionally releases CR7 bits 6 and 1.
                Dim writableMaskInBed As Integer = &H10
                If (_crtc(&H33) And &H2) <> 0 Then writableMaskInBed = writableMaskInBed Or &H42
                value = CByte((_crtc(7) And Not writableMaskInBed) Or (value And writableMaskInBed))
            Else
                TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " BLOCKED CR11 protect")
                Return
            End If
        End If
        TraceVgaDiagnostic("CRTC[" & index.ToString("X2") & "] <- " & value.ToString("X2") & " APPLIED")
        Dim oldValueInBed As Byte = _crtc(index)
        Dim normalizedValueInBed As Byte = NormalizeExtendedCrtcWrite(index, value)
        _crtc(index) = normalizedValueInBed
        If index = &H48 Then
            ' S3 86C928 Data Book CR46-CR49, p. 8-6: both displayed cursor
            ' coordinates are registered by the write to Origin-Y High (CR48).
            _nativeCursorX = ((_crtc(&H46) And &H7) << 8) Or _crtc(&H47)
            _nativeCursorY = ((normalizedValueInBed And &H7) << 8) Or _crtc(&H49)
        End If
        If (index = &H53 OrElse index = &H58 OrElse index = &H59 OrElse index = &H5A) AndAlso
           normalizedValueInBed <> oldValueInBed Then
            RaiseEvent MemoryDecodeChanged()
        End If
    End Sub

    Private Function NormalizeExtendedCrtcWrite(indexInBed As Byte, valueInBed As Byte) As Byte
        Select Case indexInBed
            Case &H45
                _cursorForegroundWriteByte = 0
                _cursorBackgroundWriteByte = 0
                ' S3 86C928 Data Book HGC_MODE (CR45), p. 8-5.  Preserve the
                ' native enable, x2/x3 stretch, right-storage, and Bt485-enable
                ' controls; bits 1, 6, and 7 are reserved.
                Return CByte(valueInBed And &H3D)
            Case &H46, &H48
                Return CByte(valueInBed And &H7)
            Case &H4C
                Return CByte(valueInBed And &HF)
            Case &H4E, &H4F
                Return CByte(valueInBed And &H3F)
            Case &H58
                Return CByte(valueInBed And &H9B)
        End Select
        Return valueInBed
    End Function

    Private Shared Function IsHorizontalTimingRegister(indexInBed As Byte) As Boolean
        Select Case indexInBed
            Case &H0, &H1, &H2, &H3, &H4, &H5
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function IsVerticalTimingRegister(indexInBed As Byte) As Boolean
        Select Case indexInBed
            Case &H6, &H7, &H9, &H10, &H11, &H15, &H16
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Sub WriteCursorColorStack(foregroundInBed As Boolean, valueInBed As Byte)
        Dim shiftInBed As Integer
        If foregroundInBed Then
            shiftInBed = _cursorForegroundWriteByte * 8
            _cursorForeground = (_cursorForeground And Not (CUInt(&HFF) << shiftInBed)) Or (CUInt(valueInBed) << shiftInBed)
            _cursorForegroundWriteByte = (_cursorForegroundWriteByte + 1) Mod 3
        Else
            shiftInBed = _cursorBackgroundWriteByte * 8
            _cursorBackground = (_cursorBackground And Not (CUInt(&HFF) << shiftInBed)) Or (CUInt(valueInBed) << shiftInBed)
            _cursorBackgroundWriteByte = (_cursorBackgroundWriteByte + 1) Mod 3
        End If
    End Sub

    Private Function ReadCursorColorStack(foregroundInBed As Boolean) As Byte
        Dim indexInBed As Integer = If(foregroundInBed, _cursorForegroundWriteByte, _cursorBackgroundWriteByte)
        Dim colorInBed As UInteger = If(foregroundInBed, _cursorForeground, _cursorBackground)
        Dim resultInBed As Byte = CByte((colorInBed >> (indexInBed * 8)) And &HFFUI)
        If foregroundInBed Then
            _cursorForegroundWriteByte = (_cursorForegroundWriteByte + 1) Mod 3
        Else
            _cursorBackgroundWriteByte = (_cursorBackgroundWriteByte + 1) Mod 3
        End If
        Return resultInBed
    End Function

    Private Function S3VgaRegistersUnlocked() As Boolean
        Return (_crtc(&H38) And &HCC) = &H48
    End Function

    Private Function S3SystemRegistersUnlocked() As Boolean
        Return (_crtc(&H39) And &HE0) = &HA0
    End Function

    Private Function ReadDacData() As Byte
        Return _ramdac.ReadData()
    End Function

    Private Sub WriteDacData(value As Byte)
        _ramdac.WriteData(value)
    End Sub

    Public Function HandlesMemory(address As UInteger) As Boolean Implements IMemoryMappedDevice.HandlesMemory
        If address >= OptionRomBase AndAlso address < OptionRomBase + OptionRomWindowSize AndAlso _optionRom.Length > 0 Then Return True
        If VideoMemoryDecodeEnabled() Then
            If MemoryMappedIoContainsInBed(address) Then Return True
            Dim apertureOffset As Integer
            If TryTranslateLegacyAperture(address, apertureOffset) Then Return True
            If LinearWindowContains(address) Then Return True
        End If
        Return False
    End Function

    Public Function ReadMemoryByte(address As UInteger) As Byte Implements IMemoryMappedDevice.ReadMemoryByte
        If address >= OptionRomBase AndAlso address < OptionRomBase + OptionRomWindowSize AndAlso _optionRom.Length > 0 Then
            Dim offset As Integer = CInt(address - OptionRomBase)
            If offset < _optionRom.Length Then Return _optionRom(offset)
            Return &HFF
        End If

        If MemoryMappedIoContainsInBed(address) Then Return ReadMemoryMappedIoByteInBed(address)

        Dim apertureOffset As Integer
        If TryTranslateLegacyAperture(address, apertureOffset) Then Return ReadVgaMemory(apertureOffset)

        Dim linearOffset As Integer
        If TryTranslateLinearWindow(address, linearOffset) Then Return _vram(linearOffset Mod VramSize)
        Return &HFF
    End Function

    Public Sub WriteMemoryByte(address As UInteger, value As Byte) Implements IMemoryMappedDevice.WriteMemoryByte
        If address >= OptionRomBase AndAlso address < OptionRomBase + OptionRomWindowSize Then Return
        If MemoryMappedIoContainsInBed(address) Then
            WriteMemoryMappedIoByteInBed(address, value)
            Return
        End If
        Dim apertureOffset As Integer
        If TryTranslateLegacyAperture(address, apertureOffset) Then
            WriteVgaMemory(apertureOffset, value)
            Return
        End If
        Dim linearOffset As Integer
        If TryTranslateLinearWindow(address, linearOffset) Then _vram(linearOffset Mod VramSize) = value
    End Sub


    Private Function SetupModeSelected() As Boolean
        ' CR37 bit0 samples PD8.  Our ISA profile straps PD8 high, selecting
        ' SETUP_MO bit4 as the setup-mode control.
        Return (_setupRegister And &H10) <> 0
    End Function

    Private Function BoardAddressDecodeEnabled() As Boolean
        ' In operational mode, 46E8h bit 3 enables normal board address decode.
        ' The subsystem must also have been awakened through 0102h bit 0.
        Return Not SetupModeSelected() AndAlso (_setupRegister And &H8) <> 0 AndAlso (_setupOptionSelect And 1) <> 0
    End Function

    Private Function VideoMemoryDecodeEnabled() As Boolean
        ' VGA Miscellaneous Output bit 1 is RAM Enable. It gates CPU access to
        ' display memory but does not disable the adapter I/O register block.
        Return BoardAddressDecodeEnabled() AndAlso (_miscOutput And &H2) <> 0
    End Function

    Private Function MemoryMappedIoEnabledInBed() As Boolean
        ' S3 86C928 Data Book section 3.4.3: CR53 bit 4 and ADVFUNC bit 5
        ' are equivalent enables for the two 32-KByte MMIO regions.
        Return VideoMemoryDecodeEnabled() AndAlso
               (((_crtc(&H53) And &H10) <> 0) OrElse ((_advancedFunction And &H20US) <> 0))
    End Function

    Private Function MemoryMappedIoContainsInBed(addressInBed As UInteger) As Boolean
        Return MemoryMappedIoEnabledInBed() AndAlso
               addressInBed >= &HA0000UI AndAlso addressInBed <= &HAFFFFUI
    End Function

    Private Function ReadMemoryMappedIoByteInBed(addressInBed As UInteger) As Byte
        ' Every address in the lower MMIO half aliases PIX_TRANS.  Enhanced
        ' command registers in the upper half are write-only through MMIO.
        If addressInBed < &HA8000UI Then Return ReadPixelTransferByteInBed()
        Return &HFF
    End Function

    Private Sub WriteMemoryMappedIoByteInBed(addressInBed As UInteger, valueInBed As Byte)
        If addressInBed < &HA8000UI Then
            _pixelTransfer = CUShort(valueInBed)
            FeedPixelTransferByteInBed(valueInBed)
            Return
        End If

        ' Table 3-1 preserves the low sixteen address bits: for example,
        ' I/O 82E8h is written at memory A82E8h and BAE8h at ABAE8h.
        Dim mappedPortInBed As UInt16 = CUShort(addressInBed And &HFFFFUI)
        If mappedPortInBed = &HBEE8US Then Return ' Read Register Select has no MMIO alias.
        If IsEnhancedPort(mappedPortInBed) Then
            Dim canonicalInBed As UInt16 = CanonicalEnhancedPort(mappedPortInBed)
            WriteEnhancedByte(CUShort(canonicalInBed And &HFFFEUS),
                              (canonicalInBed And 1US) <> 0US,
                              valueInBed)
        End If
    End Sub

    Private Function TryTranslateLegacyAperture(address As UInteger, ByRef offset As Integer) As Boolean
        Dim mapSelect As Integer = (_graphics(6) >> 2) And 3
        Dim baseAddress As UInteger
        Dim length As UInteger
        Select Case mapSelect
            Case 0 : baseAddress = &HA0000UI : length = &H20000UI
            Case 1 : baseAddress = &HA0000UI : length = &H10000UI
            Case 2 : baseAddress = &HB0000UI : length = &H8000UI
            Case Else : baseAddress = &HB8000UI : length = &H8000UI
        End Select
        If address < baseAddress OrElse address >= baseAddress + length Then Return False
        offset = CInt(address - baseAddress)
        Return True
    End Function

    Private Function LinearWindowContains(address As UInteger) As Boolean
        Dim ignored As Integer
        Return TryTranslateLinearWindow(address, ignored)
    End Function

    Private Function TryTranslateLinearWindow(address As UInteger, ByRef offset As Integer) As Boolean
        If ((_crtc(&H58) And &H10) = 0) AndAlso ((_advancedFunction And &H10US) = 0) Then Return False

        Dim sizeCode As Integer = _crtc(&H58) And 3
        Dim windowSize As UInteger
        Select Case sizeCode
            Case 0 : windowSize = &H10000UI
            Case 1 : windowSize = &H100000UI
            Case Else : windowSize = &H200000UI   ' 86C928 is limited to the fitted 2 MiB board aperture
        End Select

        ' CR59/CR5A are the high address bytes of the linear address window.
        ' The physical comparator ignores low bits required by window alignment.
        Dim requestedBase As UInteger = (CUInt(_crtc(&H59)) << 24) Or (CUInt(_crtc(&H5A)) << 16)
        Dim baseAddress As UInteger = requestedBase And Not (windowSize - 1UI)
        If address < baseAddress OrElse address >= baseAddress + windowSize Then Return False

        Dim translated As UInteger = address - baseAddress
        If translated >= CUInt(VramSize) Then Return False
        offset = CInt(translated)
        Return True
    End Function

    Private Function ReadVgaMemory(apertureOffset As Integer) As Byte
        _diagnosticVgaMemoryReadCount += 1UL
        Dim planeOffset As Integer
        Dim selectedPlaneInBed As Integer
        If Chain4Enabled() Then
            selectedPlaneInBed = apertureOffset And 3
            planeOffset = (apertureOffset >> 2) And &HFFFF
        ElseIf OddEvenReadEnabled() Then
            ' S3 86C928 Data Book GR4/GR5/GR6, pp. 6-26..6-28:
            ' A0 selects odd/even within the plane pair selected by GR4 bit 1.
            selectedPlaneInBed = (_graphics(4) And 2) Or (apertureOffset And 1)
            planeOffset = OddEvenPlaneOffset(apertureOffset)
        Else
            selectedPlaneInBed = _graphics(4) And 3
            planeOffset = apertureOffset And &HFFFF
        End If

        LoadLatches(planeOffset)
        If (_graphics(5) And &H8) = 0 Then
            Return ReadPlaneByte(selectedPlaneInBed, planeOffset)
        End If

        ' VGA read mode 1: compare latched pixels with Color Compare using the
        ' Color Don't Care mask.  Each result bit corresponds to one pixel.
        Dim compare As Integer = _graphics(2) And &HF
        Dim care As Integer = _graphics(7) And &HF
        Dim result As Integer = &HFF
        For plane As Integer = 0 To 3
            If (care And (1 << plane)) <> 0 Then
                If (compare And (1 << plane)) <> 0 Then
                    result = result And _latches(plane)
                Else
                    result = result And (Not _latches(plane) And &HFF)
                End If
            End If
        Next
        Return CByte(result And &HFF)
    End Function

    Private Sub WriteVgaMemory(apertureOffset As Integer, value As Byte)
        Dim planeOffset As Integer
        Dim forcedPlaneMask As Integer = &HF
        If Chain4Enabled() Then
            forcedPlaneMask = 1 << (apertureOffset And 3)
            planeOffset = (apertureOffset >> 2) And &HFFFF
        ElseIf OddEvenWriteEnabled() Then
            ' S3 86C928 Data Book SR4 bit 2, pp. 6-7/6-8: even addresses
            ' enable planes 0+2 and odd addresses planes 1+3.  SR2 then masks
            ' that pair; a write is not restricted to one plane.
            forcedPlaneMask = If((apertureOffset And 1) = 0, &H5, &HA)
            planeOffset = OddEvenPlaneOffset(apertureOffset)
        Else
            planeOffset = apertureOffset And &HFFFF
        End If

        Dim mapMask As Integer = (_sequencer(2) And &HF) And forcedPlaneMask
        Dim writeMode As Integer = _graphics(5) And 3
        _diagnosticVgaWriteModeCounts(writeMode) += 1UL
        _diagnosticVgaMapMaskCounts(mapMask And &HF) += 1UL
        If Chain4Enabled() Then
            _diagnosticVgaChainedWriteCount += 1UL
        Else
            _diagnosticVgaUnchainedWriteCount += 1UL
        End If
        Dim rotateCount As Integer = _graphics(3) And 7
        Dim logicalOp As Integer = (_graphics(3) >> 3) And 3
        Dim bitMask As Integer = _graphics(8)
        Dim rotated As Integer = RotateRight8(value, rotateCount)
        If writeMode = 2 Then _diagnosticVgaMode2Inputs(logicalOp, value And &HF) += 1UL

        For plane As Integer = 0 To 3
            If (mapMask And (1 << plane)) = 0 Then Continue For
            Dim source As Integer
            Dim effectiveMask As Integer = bitMask
            Select Case writeMode
                Case 0
                    If (_graphics(1) And (1 << plane)) <> 0 Then
                        source = If((_graphics(0) And (1 << plane)) <> 0, &HFF, 0)
                    Else
                        source = rotated
                    End If
                    source = ApplyLogical(source, _latches(plane), logicalOp)
                Case 1
                    source = _latches(plane)
                    effectiveMask = &HFF
                Case 2
                    source = If((value And (1 << plane)) <> 0, &HFF, 0)
                    source = ApplyLogical(source, _latches(plane), logicalOp)
                Case Else
                    ' VGA write mode 3 uses Set/Reset as the source, applies
                    ' the Data Rotate raster operation against the addressed
                    ' latch, then merges through (rotated CPU data AND GC8).
                    ' Omitting the raster operation leaves colour planes set
                    ' during masked Windows/GDI drawing (notably Mode 12h).
                    source = If((_graphics(0) And (1 << plane)) <> 0, &HFF, 0)
                    source = ApplyLogical(source, _latches(plane), logicalOp)
                    effectiveMask = RotateRight8(value, rotateCount) And bitMask
            End Select
            Dim finalValue As Byte = CByte(((source And effectiveMask) Or (_latches(plane) And (Not effectiveMask And &HFF))) And &HFF)
            Dim oldValueInBed As Integer = ReadPlaneByte(plane, planeOffset)
            _diagnosticVgaPlaneBitsSet(writeMode, plane) +=
                CULng(System.Numerics.BitOperations.PopCount(CUInt((Not oldValueInBed) And finalValue And &HFF)))
            _diagnosticVgaPlaneBitsCleared(writeMode, plane) +=
                CULng(System.Numerics.BitOperations.PopCount(CUInt(oldValueInBed And (Not finalValue) And &HFF)))
            WritePlaneByte(plane, planeOffset, finalValue)
        Next
    End Sub

    Private Sub LoadLatches(offset As Integer)
        _diagnosticVgaLatchLoadCount += 1UL
        For plane As Integer = 0 To 3
            _latches(plane) = ReadPlaneByte(plane, offset)
        Next
    End Sub

    Private Function PlaneLinearAddress(planeInBed As Integer, planeOffsetInBed As Integer) As Integer
        Dim bankBase As Integer = GetLegacyBankBaseBytes()
        Dim withinBank As Integer = ((planeOffsetInBed And &HFFFF) << 2) Or (planeInBed And 3)
        Return (bankBase + withinBank) Mod VramSize
    End Function

    Private Function ReadPlaneByte(planeInBed As Integer, planeOffsetInBed As Integer) As Byte
        Return _vram(PlaneLinearAddress(planeInBed, planeOffsetInBed))
    End Function

    Private Sub WritePlaneByte(planeInBed As Integer, planeOffsetInBed As Integer, valueInBed As Byte)
        _vram(PlaneLinearAddress(planeInBed, planeOffsetInBed)) = valueInBed
    End Sub

    ' CROMWELL STEALTH PRO 928 SCANOUT ADDRESS BRICK 10.2
    '
    ' CPU banking and CRT scan-out are separate electrical paths.  The legacy
    ' A0000/B0000 CPU aperture uses CR35/CR51 CPU-base bits through
    ' PlaneLinearAddress().  The CRTC instead owns its display-start and logical
    ' screen-width counters.  Never let a CPU bank selection move the monitor.
    Private Function NormalizeDisplayPlaneOffsetInBed(offsetInBed As Long) As Integer
        Dim planeBytesInBed As Long = VramSize \ 4L
        Dim normalizedInBed As Long = offsetInBed Mod planeBytesInBed
        If normalizedInBed < 0 Then normalizedInBed += planeBytesInBed
        Return CInt(normalizedInBed)
    End Function

    Private Function ReadDisplayPlaneByteInBed(planeInBed As Integer, planeOffsetInBed As Long) As Byte
        Dim normalizedInBed As Integer = NormalizeDisplayPlaneOffsetInBed(planeOffsetInBed)
        Dim linearInBed As Integer = (normalizedInBed << 2) Or (planeInBed And 3)
        Return _vram(linearInBed)
    End Function

    Private Function NormalizeDisplayLinearAddressInBed(addressInBed As Long) As Integer
        Dim normalizedInBed As Long = addressInBed Mod CLng(VramSize)
        If normalizedInBed < 0 Then normalizedInBed += VramSize
        Return CInt(normalizedInBed)
    End Function

    Private Function ReadDisplayLinearByteInBed(addressInBed As Long) As Byte
        Return _vram(NormalizeDisplayLinearAddressInBed(addressInBed))
    End Function

    Private Function GetLegacyBankBaseBytes() As Integer
        ' S3 86C928 CPU base address: CR35 bits 3-0 are address bits 17-14
        ' and CR51 bits 3-2 are address bits 19-18. CR31 bit 0 enables the
        ' base-address offset. This is therefore a 64 KiB-granularity aperture.
        If (_crtc(&H31) And 1) = 0 Then Return 0
        Dim bank As Integer = (_crtc(&H35) And &HF) Or ((_crtc(&H51) And &HC) << 2)
        Return (bank * &H10000) Mod VramSize
    End Function

    Private Function Chain4Enabled() As Boolean
        Return (_sequencer(4) And &H8) <> 0
    End Function

    Private Function OddEvenReadEnabled() As Boolean
        ' S3 86C928 Data Book GR5 bit 4: affects CPU reads only.
        Return Not Chain4Enabled() AndAlso (_graphics(5) And &H10) <> 0
    End Function

    Private Function OddEvenWriteEnabled() As Boolean
        ' S3 86C928 Data Book SR4 bit 2: affects CPU writes only.
        Return Not Chain4Enabled() AndAlso (_sequencer(4) And &H4) = 0
    End Function

    Private Function OddEvenPlaneOffset(apertureOffsetInBed As Integer) As Integer
        ' GR6 bit 1 replaces host A0 with a higher address bit; A0 remains the
        ' plane-pair selector.  With chaining disabled A0 is not removed.
        If (_graphics(6) And 2) <> 0 Then Return (apertureOffsetInBed >> 1) And &HFFFF
        Return apertureOffsetInBed And &HFFFF
    End Function

    Private Shared Function RotateRight8(value As Integer, count As Integer) As Integer
        count = count And 7
        If count = 0 Then Return value And &HFF
        Return ((value >> count) Or ((value << (8 - count)) And &HFF)) And &HFF
    End Function

    Private Shared Function ApplyLogical(source As Integer, latch As Integer, op As Integer) As Integer
        Select Case op
            Case 1 : Return source And latch
            Case 2 : Return source Or latch
            Case 3 : Return source Xor latch
            Case Else : Return source
        End Select
    End Function

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds <= 0 Then Return

        AdvanceGraphicsEngine(elapsedPicoseconds)

        Dim dotClockHz As Long = GetDotClockHz()
        Dim horizontalTotalDots As Integer = GetHorizontalTotalDots()
        Dim verticalTotalLines As Integer = GetVerticalTotalLines()
        Dim dotsPerFrame As Long = Math.Max(1L, CLng(horizontalTotalDots) * verticalTotalLines)
        Dim frameDotsInBed As Double = CDbl(dotsPerFrame)

        Dim retraceStart As Integer = GetVerticalRetraceStart() Mod verticalTotalLines
        Dim retraceStartDotInBed As Double = CDbl(retraceStart) * CDbl(horizontalTotalDots)
        Dim advancedDots As Double = (CDbl(elapsedPicoseconds) * CDbl(dotClockHz)) / CDbl(PicosecondsPerSecond)

        ' IClockBatchSafe means an arbitrarily large motherboard batch must not
        ' erase guest-visible beam edges.  Comparing only the old/final Boolean
        ' retrace state loses a complete retrace interval when both endpoints lie
        ' in active display.  Latch the S3 vertical-sync event whenever the beam
        ' crosses the retrace-start dot anywhere inside this elapsed interval.
        If CrossedFramePositionInBed(_beamDotPhase,
                                     advancedDots,
                                     retraceStartDotInBed,
                                     frameDotsInBed) Then
            _verticalSyncInterruptPending = True
        End If

        Dim accumulatedPhaseInBed As Double = _beamDotPhase + advancedDots
        If accumulatedPhaseInBed >= frameDotsInBed Then
            Dim completedFrames As ULong = CULng(Math.Floor(accumulatedPhaseInBed / frameDotsInBed))
            _frameCounter += completedFrames
            accumulatedPhaseInBed -= CDbl(completedFrames) * frameDotsInBed
        End If
        _beamDotPhase = accumulatedPhaseInBed

        Dim dotInFrame As Long = CLng(Math.Floor(_beamDotPhase))
        Dim currentLine As Integer = CInt(dotInFrame \ horizontalTotalDots)
        Dim dotInLine As Integer = CInt(dotInFrame Mod horizontalTotalDots)

        Dim horizontalDisplayDots As Integer = GetHorizontalDisplayDots()
        Dim hRetraceStart As Integer = GetHorizontalRetraceStartDots() Mod horizontalTotalDots
        Dim hRetraceEnd As Integer = GetHorizontalRetraceEndDots(hRetraceStart, horizontalTotalDots)
        _horizontalRetrace = IsWrappedRangeMember(dotInLine, hRetraceStart, hRetraceEnd, horizontalTotalDots)

        Dim retraceLength As Integer = (_crtc(&H11) And &HF) + 1
        Dim retraceEnd As Integer = (retraceStart + retraceLength) Mod verticalTotalLines
        _verticalRetrace = IsWrappedRangeMember(currentLine, retraceStart, retraceEnd, verticalTotalLines)

        Dim verticalDisplayLines As Integer = Math.Min(verticalTotalLines, Math.Max(1, GetVerticalDisplayLines()))
        Dim horizontalBlankInBed As Boolean = IsHorizontalBlankInBed(dotInLine, horizontalTotalDots)
        Dim verticalBlankInBed As Boolean = IsVerticalBlankInBed(currentLine, verticalTotalLines)
        Dim displaySkewDotsInBed As Integer = ((_crtc(3) >> 5) And 3) * GetCharacterDots()
        _displayEnable = VideoOutputEnabledInBed() AndAlso
                         Not horizontalBlankInBed AndAlso
                         Not verticalBlankInBed AndAlso
                         dotInLine >= displaySkewDotsInBed AndAlso
                         dotInLine < horizontalDisplayDots + displaySkewDotsInBed AndAlso
                         currentLine < verticalDisplayLines
    End Sub

    Private Function IsHorizontalBlankInBed(dotInLineInBed As Integer,
                                             totalDotsInBed As Integer) As Boolean
        ' S3 86C928 Data Book CR2/CR3/CR5 and CR5D, pp. 6-10/6-11/9-8.
        Dim characterDotsInBed As Integer = GetCharacterDots()
        Dim totalCharactersInBed As Integer = Math.Max(1, totalDotsInBed \ characterDotsInBed)
        Dim startCharacterInBed As Integer = _crtc(2)
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5D) And &H4) <> 0 Then
            startCharacterInBed = startCharacterInBed Or &H100
        End If
        Dim endLowInBed As Integer = (_crtc(3) And &H1F) Or ((_crtc(5) And &H80) >> 2)
        Dim endCharacterInBed As Integer = (startCharacterInBed And Not &H3F) Or endLowInBed
        If endCharacterInBed <= startCharacterInBed Then endCharacterInBed += &H40
        Dim currentCharacterInBed As Integer = dotInLineInBed \ characterDotsInBed
        Return IsWrappedRangeMember(currentCharacterInBed,
                                    startCharacterInBed,
                                    endCharacterInBed,
                                    totalCharactersInBed)
    End Function

    Private Function IsVerticalBlankInBed(currentLineInBed As Integer,
                                           totalLinesInBed As Integer) As Boolean
        ' S3 86C928 Data Book CR7/CR9/CR15/CR16 and CR5E, pp. 6-12, 6-13,
        ' 6-19, 9-9.  EVB supplies the low eight bits and wraps in that field.
        Dim startLineInBed As Integer = _crtc(&H15)
        If (_crtc(7) And &H8) <> 0 Then startLineInBed = startLineInBed Or &H100
        If (_crtc(9) And &H20) <> 0 Then startLineInBed = startLineInBed Or &H200
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5E) And &H4) <> 0 Then
            startLineInBed = startLineInBed Or &H400
        End If
        Dim endLineInBed As Integer = (startLineInBed And Not &HFF) Or _crtc(&H16)
        If endLineInBed <= startLineInBed Then endLineInBed += &H100
        Return IsWrappedRangeMember(currentLineInBed,
                                    startLineInBed,
                                    endLineInBed,
                                    totalLinesInBed)
    End Function

    Private Shared Function CrossedFramePositionInBed(startPhaseInBed As Double,
                                                       advanceDotsInBed As Double,
                                                       eventPositionInBed As Double,
                                                       frameDotsInBed As Double) As Boolean
        If advanceDotsInBed <= 0.0R OrElse frameDotsInBed <= 0.0R Then Return False
        If advanceDotsInBed >= frameDotsInBed Then Return True

        startPhaseInBed = startPhaseInBed Mod frameDotsInBed
        If startPhaseInBed < 0.0R Then startPhaseInBed += frameDotsInBed
        eventPositionInBed = eventPositionInBed Mod frameDotsInBed
        If eventPositionInBed < 0.0R Then eventPositionInBed += frameDotsInBed

        Dim distanceInBed As Double = eventPositionInBed - startPhaseInBed
        If distanceInBed <= 0.0R Then distanceInBed += frameDotsInBed
        Return advanceDotsInBed >= distanceInBed
    End Function

    Private Shared Function IsWrappedRangeMember(valueInBed As Integer,
                                                  startInBed As Integer,
                                                  endInBed As Integer,
                                                  modulusInBed As Integer) As Boolean
        If modulusInBed <= 0 Then Return False
        startInBed = ((startInBed Mod modulusInBed) + modulusInBed) Mod modulusInBed
        endInBed = ((endInBed Mod modulusInBed) + modulusInBed) Mod modulusInBed
        If startInBed = endInBed Then Return False
        If startInBed < endInBed Then Return valueInBed >= startInBed AndAlso valueInBed < endInBed
        Return valueInBed >= startInBed OrElse valueInBed < endInBed
    End Function

    Private Function GetDotClockHz() As Long
        Dim baseClockInBed As Long
        Select Case (_miscOutput >> 2) And 3
            Case 0 : baseClockInBed = 25175000L
            Case 1 : baseClockInBed = 28322000L
            Case Else
                baseClockInBed = If(_clockGenerator.PixelClockHz > 0,
                                    _clockGenerator.PixelClockHz,
                                    25175000L)
        End Select

        ' VGA Sequencer Clocking Mode bit 3 divides the selected dot clock by
        ' two.  This affects the electrical beam clock, not merely host scaling.
        If (_sequencer(1) And &H8) <> 0 Then Return Math.Max(1L, baseClockInBed \ 2L)
        Return baseClockInBed
    End Function

    Private Function GetCharacterDots() As Integer
        Return If((_sequencer(1) And 1) <> 0, 8, 9)
    End Function

    Private Function GetHorizontalTotalDots() As Integer
        Dim totalChars As Integer = CInt(_crtc(0)) + 5
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5D) And 1) <> 0 Then totalChars += &H100
        Return Math.Max(GetCharacterDots(), totalChars * GetCharacterDots())
    End Function

    Private Function GetHorizontalDisplayDots() As Integer
        Dim displayEnd As Integer = CInt(_crtc(1))
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5D) And &H2) <> 0 Then displayEnd = displayEnd Or &H100
        Return Math.Max(GetCharacterDots(), (displayEnd + 1) * GetCharacterDots())
    End Function

    Private Function GetHorizontalRetraceStartDots() As Integer
        Dim startCharacter As Integer = CInt(_crtc(4))
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5D) And &H10) <> 0 Then startCharacter = startCharacter Or &H100
        Return startCharacter * GetCharacterDots()
    End Function

    Private Function GetHorizontalRetraceEndDots(startDotsInBed As Integer, totalDotsInBed As Integer) As Integer
        Dim charDots As Integer = GetCharacterDots()
        Dim startChar As Integer = startDotsInBed \ charDots
        Dim endLow As Integer = _crtc(5) And &H1F
        Dim endChar As Integer = (startChar And Not &H1F) Or endLow
        If endChar <= startChar Then endChar += &H20
        Return (endChar * charDots) Mod Math.Max(1, totalDotsInBed)
    End Function

    Private Function GetFramePeriodPicoseconds() As Long
        Dim dotsPerFrame As Long = CLng(GetHorizontalTotalDots()) * GetVerticalTotalLines()
        Return Math.Max(1L, CLng((CDbl(PicosecondsPerSecond) * CDbl(dotsPerFrame)) / CDbl(GetDotClockHz())))
    End Function

    Private Function GetVerticalTotalLines() As Integer
        Dim value As Integer = _crtc(6)
        If (_crtc(7) And &H1) <> 0 Then value = value Or &H100
        If (_crtc(7) And &H20) <> 0 Then value = value Or &H200
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5E) And 1) <> 0 Then value = value Or &H400
        Return Math.Max(2, value + 2)
    End Function

    Private Function GetVerticalRetraceStart() As Integer
        Dim value As Integer = _crtc(&H10)
        If (_crtc(7) And &H4) <> 0 Then value = value Or &H100
        If (_crtc(7) And &H80) <> 0 Then value = value Or &H200
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5E) And &H4) <> 0 Then value = value Or &H400
        Return value
    End Function

    Public Function RenderFrame() As Bitmap
        TraceVgaRenderClassIfChanged()
        Dim frameInBed As Bitmap
        If Not VideoOutputEnabledInBed() Then
            frameInBed = RenderBlank(640, 400)
        ElseIf IsTextMode() Then
            frameInBed = RenderTextMode()
        ElseIf EnhancedDisplayEnabled() Then
            ' S3 Advanced Function Control bit 0 selects the enhanced display
            ' path. It takes precedence over residual VGA serializer bits left
            ' programmed in GR5 during a driver mode transition.
            frameInBed = RenderEnhancedMode()
        ElseIf (_graphics(5) And &H40) <> 0 Then
            ' GC05 bit 6 selects the VGA 256-color shift-register display format.
            ' Chain-4 controls CPU memory addressing; disabling chain-4 does not
            ' turn Mode-X-style scan-out back into 16-color bit-plane graphics.
            frameInBed = Render256ColorMode()
        ElseIf IsCgaCompatibilityScanoutInBed() Then
            ' GR5 bit 5 selects the CGA-compatible interleaved shift mode used
            ' by BIOS modes 04h-06h.  It is neither ordinary four-plane EGA
            ' scan-out nor VGA 256-colour shift mode.
            frameInBed = RenderCgaCompatibilityMode()
        Else
            frameInBed = RenderPlanarGraphicsMode()
        End If
        If VideoOutputEnabledInBed() Then
            ApplyHardwareCursor(_framePixels, _frameWidth, _frameHeight)
            ApplyBt485Cursor(_framePixels, _frameWidth, _frameHeight)
        End If
        Return frameInBed
    End Function

    Private Function VideoOutputEnabledInBed() As Boolean
        ' S3 86C928 Data Book SR0 and SR1, pp. 6-3..6-6: both sequencer-reset
        ' bits must be released and SR1 bit 5 blanks the screen without stopping
        ' sync. Brooktree Bt485A Command Register 0 CR00 powers down its output.
        Return _attributeVideoEnabled AndAlso
               (_sequencer(0) And 3) = 3 AndAlso
               (_sequencer(1) And &H20) = 0 AndAlso
               Not _ramdac.OutputPoweredDown
    End Function

    Private Sub ApplyBt485Cursor(pixelsInBed As Integer(), widthInBed As Integer, heightInBed As Integer)
        If pixelsInBed Is Nothing OrElse widthInBed <= 0 OrElse heightInBed <= 0 Then Return
        ' S3 86C928 Data Book CR45 bit 5 and CR55 bit 5, pp. 8-5/9-5:
        ' both switches are required to route CDE/ODF cursor control to Bt485.
        If (_crtc(&H45) And &H20) = 0 OrElse (_crtc(&H55) And &H20) = 0 Then Return
        Dim modeInBed As Integer = _ramdac.CursorMode
        If modeInBed = 0 Then Return
        Dim sizeInBed As Integer = _ramdac.CursorSize
        Dim originXInBed As Integer = _ramdac.CursorX - sizeInBed
        Dim originYInBed As Integer = _ramdac.CursorY - sizeInBed
        For cursorYInBed As Integer = 0 To sizeInBed - 1
            Dim displayYInBed As Integer = originYInBed + cursorYInBed
            If displayYInBed < 0 OrElse displayYInBed >= heightInBed Then Continue For
            For cursorXInBed As Integer = 0 To sizeInBed - 1
                Dim displayXInBed As Integer = originXInBed + cursorXInBed
                If displayXInBed < 0 OrElse displayXInBed >= widthInBed Then Continue For
                Dim planesInBed As Integer = _ramdac.CursorPlaneValue(cursorXInBed, cursorYInBed)
                Dim colorRegisterInBed As Integer
                Dim complementInBed As Boolean
                Select Case modeInBed
                    Case 1
                        colorRegisterInBed = planesInBed
                    Case 2
                        Select Case planesInBed
                            Case 0 : colorRegisterInBed = 1
                            Case 1 : colorRegisterInBed = 2
                            Case 3 : complementInBed = True
                        End Select
                    Case 3
                        If planesInBed = 2 Then colorRegisterInBed = 1
                        If planesInBed = 3 Then colorRegisterInBed = 2
                End Select
                Dim pixelIndexInBed As Integer = displayYInBed * widthInBed + displayXInBed
                If complementInBed Then
                    pixelsInBed(pixelIndexInBed) = pixelsInBed(pixelIndexInBed) Xor &HFFFFFF
                ElseIf colorRegisterInBed <> 0 Then
                    pixelsInBed(pixelIndexInBed) = Color.FromArgb(255,
                        _ramdac.OverlayComponentAsEightBit(colorRegisterInBed, 0),
                        _ramdac.OverlayComponentAsEightBit(colorRegisterInBed, 1),
                        _ramdac.OverlayComponentAsEightBit(colorRegisterInBed, 2)).ToArgb()
                End If
            Next
        Next
    End Sub

    Private Sub ApplyHardwareCursor(pixelsInBed As Integer(), widthInBed As Integer, heightInBed As Integer)
        If pixelsInBed Is Nothing OrElse widthInBed <= 0 OrElse heightInBed <= 0 Then Return
        ' S3 86C928 Data Book HGC_MODE CR45 bit 0: the native sprite is enabled
        ' only in Enhanced mode.  When the Bt485 path is selected, CDE/ODF owns
        ' cursor composition instead of this internal AND/XOR compositor.
        If Not EnhancedDisplayEnabled() OrElse (_crtc(&H45) And 1) = 0 Then Return
        If (_crtc(&H45) And &H20) <> 0 AndAlso (_crtc(&H55) And &H20) <> 0 Then Return

        Dim cursorX As Integer = _nativeCursorX
        Dim cursorY As Integer = _nativeCursorY
        Dim originX As Integer = _crtc(&H4E) And &H3F
        Dim originY As Integer = _crtc(&H4F) And &H3F
        Dim mapAddress As Integer = (((_crtc(&H4C) And &HF) << 8) Or _crtc(&H4D)) * 1024
        Dim horizontalStretchInBed As Integer = If((_crtc(&H45) And &H8) <> 0, 3,
                                                   If((_crtc(&H45) And &H4) <> 0, 2, 1))
        Dim useTrueColorStacksInBed As Boolean = horizontalStretchInBed > 1
        Dim backgroundArgbInBed As Integer =
            If(useTrueColorStacksInBed,
               Color.FromArgb(255, CInt((_cursorBackground >> 16) And &HFFUI), CInt((_cursorBackground >> 8) And &HFFUI), CInt(_cursorBackground And &HFFUI)).ToArgb(),
               PaletteArgb(_crtc(&HF)))
        Dim foregroundArgbInBed As Integer =
            If(useTrueColorStacksInBed,
               Color.FromArgb(255, CInt((_cursorForeground >> 16) And &HFFUI), CInt((_cursorForeground >> 8) And &HFFUI), CInt(_cursorForeground And &HFFUI)).ToArgb(),
               PaletteArgb(_crtc(&HE)))

        ' The S3 cursor is a 64x64 two-plane monochrome sprite. Each scan line
        ' alternates an AND word and XOR word four times.  See 86C928 Data Book
        ' Enhanced Mode Programming 11.5, pp. 11-16..11-18.
        For cy As Integer = originY To 63
            Dim dy As Integer = cursorY + cy - originY
            If dy < 0 OrElse dy >= heightInBed Then Continue For
            Dim lineBase As Integer = (mapAddress + cy * 16) Mod VramSize
            For cx As Integer = originX To 63
                Dim bitMask As Integer = &H80 >> (cx And 7)
                Dim wordPairInBed As Integer = cx >> 4
                Dim byteWithinWordInBed As Integer = (cx >> 3) And 1
                Dim andSet As Boolean = (_vram((lineBase + wordPairInBed * 4 + byteWithinWordInBed) Mod VramSize) And bitMask) <> 0
                Dim xorSet As Boolean = (_vram((lineBase + wordPairInBed * 4 + 2 + byteWithinWordInBed) Mod VramSize) And bitMask) <> 0
                For stretchedPixelInBed As Integer = 0 To horizontalStretchInBed - 1
                    Dim dx As Integer = cursorX + (cx - originX) * horizontalStretchInBed + stretchedPixelInBed
                    If dx < 0 OrElse dx >= widthInBed Then Continue For
                    Dim pixelIndex As Integer = dy * widthInBed + dx
                    If Not andSet AndAlso Not xorSet Then
                        pixelsInBed(pixelIndex) = backgroundArgbInBed
                    ElseIf Not andSet AndAlso xorSet Then
                        pixelsInBed(pixelIndex) = foregroundArgbInBed
                    ElseIf andSet AndAlso xorSet Then
                        pixelsInBed(pixelIndex) = pixelsInBed(pixelIndex) Xor &HFFFFFF
                    End If
                Next
            Next
        Next
    End Sub

    Private Function IsTextMode() As Boolean
        Return (_graphics(6) And 1) = 0
    End Function

    Private Function EnhancedDisplayEnabled() As Boolean
        ' Enhanced pixel interpretation is selected by the 86C928 Advanced
        ' Function Control register.  Backward-compatibility CR3A does not by
        ' itself place the display engine into an enhanced packed-pixel mode.
        Return (_advancedFunction And 1US) <> 0
    End Function

    Private Function EnsureFrame(width As Integer, height As Integer) As Bitmap
        ' Never silently clamp here.  Every renderer indexes its output using the
        ' dimensions it requested; changing only the allocation dimensions turns
        ' a legal high-resolution CRTC mode into an out-of-range host write.
        width = Math.Max(1, width)
        height = Math.Max(1, height)
        Dim pixelCountInBed As Long = CLng(width) * CLng(height)
        If pixelCountInBed > Integer.MaxValue Then
            Throw New InvalidOperationException("Requested host video frame is too large to allocate.")
        End If
        If _frameBitmap Is Nothing OrElse _frameWidth <> width OrElse _frameHeight <> height Then
            If _frameBitmap IsNot Nothing Then
                _frameBitmap.Dispose()
                _frameBitmap = Nothing
            End If
            If _frameHandle.IsAllocated Then _frameHandle.Free()

            _framePixels = New Integer(width * height - 1) {}
            _frameHandle = GCHandle.Alloc(_framePixels, GCHandleType.Pinned)
            _frameBitmap = New Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, _frameHandle.AddrOfPinnedObject())
            _frameWidth = width
            _frameHeight = height
        End If
        Return _frameBitmap
    End Function

    Private Function FramePixels(width As Integer, height As Integer) As Integer()
        EnsureFrame(width, height)
        Return _framePixels
    End Function

    Private Function RenderBlank(width As Integer, height As Integer) As Bitmap
        Dim pixels As Integer() = FramePixels(width, height)
        Array.Fill(pixels, Color.Black.ToArgb())
        Return _frameBitmap
    End Function

    ' Render the VGA alphanumeric display from the CRTC scan-out state rather
    ' than pretending text memory is a flat columns*rows array.
    '
    ' S3 86C928 / VGA CRTC state used here:
    '   CR08  preset row scan / byte panning
    '   CR09  maximum scan line / double scan / line-compare bit 9
    '   CR0C-D display start address
    '   CR13  screen offset (logical line width)
    '   CR14  count-by-4 / doubleword mode
    '   CR17  count-by-2 / byte-word addressing
    '   CR18  split-screen line compare
    '
    ' When line compare fires, the VGA clears the display address generator
    ' and row-scan counter. The lower screen therefore begins at display
    ' memory address 0; it is not a host-side overlay or retained bitmap.
    Private Function RenderTextMode() As Bitmap
        Dim charWidth As Integer = If((_sequencer(1) And 1) <> 0, 8, 9)
        Dim maxScan As Integer = _crtc(9) And &H1F
        Dim charHeight As Integer = maxScan + 1
        If charHeight < 1 OrElse charHeight > 32 Then
            maxScan = 15
            charHeight = 16
        End If

        Dim columns As Integer = CInt(_crtc(1)) + 1
        If columns < 1 OrElse columns > 256 Then columns = 80

        Dim visibleScanLines As Integer = GetVerticalDisplayLines()
        visibleScanLines = Math.Max(1, Math.Min(2048, visibleScanLines))

        Dim width As Integer = columns * charWidth
        Dim height As Integer = visibleScanLines
        Dim pixels As Integer() = FramePixels(width, height)

        Dim lineAddress As Integer = GetTextDisplayStartAddress()
        Dim rowAddressAdvance As Integer = GetCrtcRowAddressAdvance()
        Dim addressClockDivisor As Integer = GetCrtcAddressClockDivisor()

        Dim presetRowScan As Integer = _crtc(8) And &H1F
        Dim rowScan As Integer = presetRowScan
        If rowScan > maxScan Then rowScan = maxScan

        Dim programmedBytePan As Integer = (_crtc(8) >> 5) And 3
        Dim lineCompare As Integer = GetCrtcLineCompare()
        Dim splitScreen As Boolean = False

        Dim doubleScan As Boolean = (_crtc(9) And &H80) <> 0
        Dim doubleScanPhase As Integer = 0

        Dim cursorCell As Integer = GetTextCursorAddress()
        Dim cursorDisabled As Boolean = (_crtc(&HA) And &H20) <> 0
        ' Cursor blink divider intentionally left alone in this patch.
        Dim cursorVisible As Boolean = Not cursorDisabled AndAlso ((_frameCounter And &H10UL) = 0)
        Dim cursorStart As Integer = _crtc(&HA) And &H1F
        Dim cursorEnd As Integer = _crtc(&HB) And &H1F

        Dim blinkEnabled As Boolean = (_attribute(&H10) And &H8) <> 0
        Dim blinkOn As Boolean = (_frameCounter And &H10UL) = 0
        ' VGA SR3 uses deliberately non-contiguous select bits.  Attribute bit
        ' 3 selects map A (SR3 5/3/2); a clear bit selects map B (SR3 4/1/0).
        ' The resulting 3-bit value addresses one of eight 8 KiB font regions
        ' in plane 2, in the VGA's interleaved physical order.
        Dim characterMapAInBed As Integer =
            (((_sequencer(3) >> 2) And &H3) Or ((_sequencer(3) And &H20) >> 3)) * 8192
        Dim characterMapBInBed As Integer =
            ((_sequencer(3) And &H3) Or ((_sequencer(3) And &H10) >> 2)) * 8192
        Dim dacPaletteInBed() As Integer = BuildPaletteArgbTableInBed()
        Dim textPaletteInBed(15) As Integer
        For logicalIndexInBed As Integer = 0 To 15
            textPaletteInBed(logicalIndexInBed) = dacPaletteInBed(MapAttributeColor(logicalIndexInBed) And &HFF)
        Next

        ' Diagnostic-only source-address probes.  These record the actual CRTC
        ' cell address used for the four displayed corner pixels.  They do not
        ' write guest VRAM and therefore cannot change the program being tested.
        Dim diagTopLeft As Integer = -1
        Dim diagTopRight As Integer = -1
        Dim diagBottomLeft As Integer = -1
        Dim diagBottomRight As Integer = -1

        For y As Integer = 0 To visibleScanLines - 1
            Dim lowerScreenUnpanned As Boolean =
                splitScreen AndAlso (_attribute(&H10) And &H20) <> 0

            Dim bytePan As Integer =
                If(lowerScreenUnpanned, 0, programmedBytePan)

            Dim pixelPan As Integer =
                If(lowerScreenUnpanned, 0, GetHorizontalPixelPan(charWidth))

            Dim cachedCellInBed As Integer = -1
            Dim cachedCharacterInBed As Integer = 0
            Dim cachedForegroundInBed As Integer = 0
            Dim cachedBackgroundInBed As Integer = 0
            Dim cachedGlyphBitsInBed As Integer = 0
            Dim cachedDrawGlyphInBed As Boolean = False
            Dim cachedCursorLineInBed As Boolean = False

            For x As Integer = 0 To width - 1
                Dim sourceX As Integer = x + pixelPan
                Dim sourceColumn As Integer = sourceX \ charWidth
                Dim pixelInCharacter As Integer = sourceX Mod charWidth

                Dim characterClockAddress As Integer =
                    sourceColumn \ addressClockDivisor

                Dim cell As Integer =
                    NormalizeDisplayPlaneOffsetInBed(
                        CLng(lineAddress) + bytePan + characterClockAddress)

                If y = 0 Then
                    If x = 0 Then
                        diagTopLeft = cell
                    ElseIf x = width - 1 Then
                        diagTopRight = cell
                    End If
                ElseIf y = visibleScanLines - 1 Then
                    If x = 0 Then
                        diagBottomLeft = cell
                    ElseIf x = width - 1 Then
                        diagBottomRight = cell
                    End If
                End If

                ' A character cell normally feeds 8 or 9 adjacent host pixels.
                ' Fetch plane 0/1 text data, plane-2 glyph data, blink and color
                ' decode once per cell instead of repeating all of it per pixel.
                If cell <> cachedCellInBed Then
                    cachedCellInBed = cell
                    cachedCharacterInBed = ReadDisplayPlaneByteInBed(0, cell)
                    Dim attrInBed As Integer = ReadDisplayPlaneByteInBed(1, cell)

                    Dim fgIndexInBed As Integer = attrInBed And &HF
                    Dim bgIndexInBed As Integer = (attrInBed >> 4) And 7
                    Dim charBlinkInBed As Boolean = blinkEnabled AndAlso (attrInBed And &H80) <> 0
                    If Not blinkEnabled AndAlso (attrInBed And &H80) <> 0 Then bgIndexInBed = bgIndexInBed Or 8

                    cachedForegroundInBed = textPaletteInBed(fgIndexInBed And &HF)
                    cachedBackgroundInBed = textPaletteInBed(bgIndexInBed And &HF)
                    cachedDrawGlyphInBed = Not charBlinkInBed OrElse blinkOn
                    Dim fontMapOffsetInBed As Integer =
                        If((attrInBed And &H8) <> 0,
                           characterMapAInBed,
                           characterMapBInBed)
                    cachedGlyphBitsInBed =
                        ReadDisplayPlaneByteInBed(
                            2,
                            (fontMapOffsetInBed +
                             (cachedCharacterInBed And &HFF) * 32 +
                             rowScan) And &HFFFF)
                    cachedCursorLineInBed = cursorVisible AndAlso
                                            cell = cursorCell AndAlso
                                            rowScan >= cursorStart AndAlso
                                            rowScan <= cursorEnd
                End If

                Dim setPixelInBed As Boolean = False
                If cachedDrawGlyphInBed AndAlso pixelInCharacter < 8 Then
                    setPixelInBed = (cachedGlyphBitsInBed And (&H80 >> pixelInCharacter)) <> 0
                ElseIf cachedDrawGlyphInBed AndAlso
                       pixelInCharacter = 8 AndAlso
                       charWidth = 9 AndAlso
                       cachedCharacterInBed >= &HC0 AndAlso cachedCharacterInBed <= &HDF AndAlso
                       (_attribute(&H10) And &H4) <> 0 Then
                    setPixelInBed = (cachedGlyphBitsInBed And 1) <> 0
                End If

                If cachedCursorLineInBed Then setPixelInBed = True
                pixels(y * width + x) = If(setPixelInBed, cachedForegroundInBed, cachedBackgroundInBed)
            Next

            If y = lineCompare Then
                lineAddress = 0
                rowScan = 0
                splitScreen = True
                doubleScanPhase = 0
                Continue For
            End If

            If doubleScan Then
                If doubleScanPhase = 0 Then
                    doubleScanPhase = 1
                    Continue For
                End If
                doubleScanPhase = 0
            End If

            If rowScan >= maxScan Then
                rowScan = 0
                lineAddress = NormalizeDisplayPlaneOffsetInBed(CLng(lineAddress) + rowAddressAdvance)
            Else
                rowScan += 1
            End If
        Next

        DrawPageCornerDiagnostics(
            pixels,
            width,
            height,
            diagTopLeft,
            diagTopRight,
            diagBottomLeft,
            diagBottomRight)
        DrawAttributeControllerDiagnostics(pixels, width, height)
        Return _frameBitmap
    End Function

    Private Function GetCrtcDisplayStartAddressCounterInBed() As Integer
        Dim addressInBed As Integer = CInt(StartAddress)

        ' S3 86C928 extensions: CR31[5:4] are display-start bits 17:16 and
        ' CR51[1:0] are bits 19:18.  CPU-base selection lives in different bits
        ' and must never be folded into this CRTC address.
        addressInBed = addressInBed Or ((CInt(_crtc(&H31)) And &H30) << 12)
        addressInBed = addressInBed Or ((CInt(_crtc(&H51)) And &H3) << 18)

        Return addressInBed And &HFFFFF
    End Function

    Private Function GetTextDisplayStartAddress() As Integer
        Return NormalizeDisplayPlaneOffsetInBed(GetCrtcDisplayStartAddressCounterInBed())
    End Function

    Private Function GetTextCursorAddress() As Integer
        Dim address As Integer =
            ((CInt(_crtc(&HE)) << 8) Or CInt(_crtc(&HF)))

        address = address Or ((CInt(_crtc(&H31)) And &H30) << 12)
        Return address And &H3FFFF
    End Function

    Private Function GetCrtcLineCompare() As Integer
        Dim value As Integer = CInt(_crtc(&H18))
        If (_crtc(7) And &H10) <> 0 Then value = value Or &H100
        If (_crtc(9) And &H40) <> 0 Then value = value Or &H200

        If (_crtc(&H5E) And &H40) <> 0 Then value = value Or &H400
        Return value
    End Function

    Public Function GetScanoutTiming() As VideoScanoutTiming
        Dim timingInBed As New VideoScanoutTiming()
        timingInBed.PixelClockHz = GetDotClockHz()
        timingInBed.HorizontalTotalDots = GetHorizontalTotalDots()
        timingInBed.HorizontalActiveDots = GetHorizontalDisplayDots()
        timingInBed.HorizontalSyncStartDots = GetHorizontalRetraceStartDots()
        timingInBed.HorizontalSyncEndDots =
            GetHorizontalRetraceEndDots(timingInBed.HorizontalSyncStartDots,
                                       timingInBed.HorizontalTotalDots)
        timingInBed.VerticalTotalLines = GetVerticalTotalLines()
        timingInBed.VerticalActiveLines = GetVerticalDisplayLines()
        timingInBed.VerticalSyncStartLine = GetVerticalRetraceStart()
        Dim verticalEndLowInBed As Integer = _crtc(&H11) And &HF
        timingInBed.VerticalSyncEndLine =
            (timingInBed.VerticalSyncStartLine And Not &HF) Or verticalEndLowInBed
        If timingInBed.VerticalSyncEndLine <= timingInBed.VerticalSyncStartLine Then
            timingInBed.VerticalSyncEndLine += &H10
        End If
        timingInBed.DoubleScan = (_crtc(9) And &H80) <> 0
        timingInBed.PixelRepeat =
            If(Not EnhancedDisplayEnabled() AndAlso (_graphics(5) And &H40) <> 0, 2, 1)
        timingInBed.HorizontalSyncPositive = (_miscOutput And &H40) = 0
        timingInBed.VerticalSyncPositive = (_miscOutput And &H80) = 0
        Return timingInBed
    End Function

    Private Function IsCgaCompatibilityScanoutInBed() As Boolean
        ' Modes 04h/05h select the CGA interleaved serializer with GR5 bit 5.
        ' Mode 06h uses one enabled plane and the B800 aperture while retaining
        ' CGA's 8-KiB odd/even scan-line layout; it does not set GR5 bit 5.
        Dim cgaShiftInBed As Boolean = (_graphics(5) And &H20) <> 0
        Dim mode6OnePlaneInBed As Boolean =
            (_graphics(6) And &HC) = &HC AndAlso
            (_sequencer(2) And &HF) = 1 AndAlso
            (_attribute(&H10) And 1) <> 0
        Return cgaShiftInBed OrElse mode6OnePlaneInBed
    End Function

    Private Structure GraphicsScanlineAddressInBed
        Public Address As Long
        Public LowerScreen As Boolean
    End Structure

    ' The CRTC memory-address counter does not advance once per host bitmap row.
    ' It advances only when the row-scan counter wraps, and CR09 bit 7 can make
    ' every row-scan count consume two physical scan lines. Graphics modes use
    ' the same address generator as text mode.
    Private Function BuildGraphicsScanlineAddressesInBed(physicalHeightInBed As Integer,
                                                         startAddressInBed As Long,
                                                         rowAddressAdvanceInBed As Integer,
                                                         packedAddressingInBed As Boolean) As GraphicsScanlineAddressInBed()
        Dim resultInBed(Math.Max(1, physicalHeightInBed) - 1) As GraphicsScanlineAddressInBed
        Dim maxScanInBed As Integer = _crtc(9) And &H1F
        Dim rowScanInBed As Integer = Math.Min(_crtc(8) And &H1F, maxScanInBed)
        Dim doubleScanInBed As Boolean = (_crtc(9) And &H80) <> 0
        Dim doubleScanPhaseInBed As Integer = 0
        Dim lineCompareInBed As Integer = GetCrtcLineCompare()
        Dim lineAddressInBed As Long = startAddressInBed
        Dim lowerScreenInBed As Boolean = False

        For physicalYInBed As Integer = 0 To resultInBed.Length - 1
            resultInBed(physicalYInBed).Address = lineAddressInBed
            resultInBed(physicalYInBed).LowerScreen = lowerScreenInBed

            If physicalYInBed = lineCompareInBed Then
                lineAddressInBed = 0
                rowScanInBed = 0
                doubleScanPhaseInBed = 0
                lowerScreenInBed = True
                Continue For
            End If

            If doubleScanInBed Then
                If doubleScanPhaseInBed = 0 Then
                    doubleScanPhaseInBed = 1
                    Continue For
                End If
                doubleScanPhaseInBed = 0
            End If

            If rowScanInBed >= maxScanInBed Then
                rowScanInBed = 0
                lineAddressInBed += rowAddressAdvanceInBed
                lineAddressInBed =
                    If(packedAddressingInBed,
                       NormalizeDisplayLinearAddressInBed(lineAddressInBed),
                       NormalizeDisplayPlaneOffsetInBed(lineAddressInBed))
            Else
                rowScanInBed += 1
            End If
        Next

        Return resultInBed
    End Function

    Private Function GetCrtcScreenOffsetUnits() As Integer
        Dim offsetInBed As Integer = CInt(_crtc(&H13))
        Dim upperInBed As Integer = (CInt(_crtc(&H51)) And &H30) << 4
        If upperInBed <> 0 Then
            offsetInBed = offsetInBed Or upperInBed
        ElseIf (_crtc(&H43) And &H4) <> 0 Then
            offsetInBed = offsetInBed Or &H100
        End If
        Return offsetInBed
    End Function

    Private Function GetCrtcRowAddressAdvance() As Integer
        ' Text/planar plane addressing advances two plane bytes per CR13 unit.
        ' A programmed zero offset is preserved: real hardware can repeat/overlap
        ' rows and the host renderer must not silently "repair" guest registers.
        Return GetCrtcScreenOffsetUnits() * 2
    End Function

    Private Function GetCrtcPackedRowAddressAdvanceInBed() As Integer
        ' Chained 256-color and S3 packed-pixel display fetches use the packed
        ' linear organization represented by this card's interleaved VRAM.
        Return GetCrtcScreenOffsetUnits() * 8
    End Function

    Private Function GetCrtcAddressClockDivisor() As Integer
        If (_crtc(&H14) And &H20) <> 0 Then Return 4
        If (_crtc(&H17) And &H8) <> 0 Then Return 2
        Return 1
    End Function

    Private Function GetHorizontalPixelPan(charWidth As Integer) As Integer
        Dim setting As Integer = _attribute(&H13) And &HF

        If charWidth = 9 Then
            If setting = 8 Then Return 0
            If setting <= 7 Then Return setting + 1
            Return 0
        End If

        Return Math.Min(7, setting)
    End Function
    ' -----------------------------------------------------------------------
    ' TEMPORARY PCB-1V PAGE/CELL DIAGNOSTIC OVERLAY
    '
    ' Each corner label reports the CRTC source cell that produced that corner
    ' of the host framebuffer:
    '
    '   TL/TR/BL/BR = displayed corner
    '   Pn          = 4 KiB BIOS text page containing that CRTC cell
    '   Cxxxxx      = CRTC cell/word address
    '   Vxxxxx      = equivalent interleaved B800 byte offset (cell * 2)
    '   xx/yy       = character byte / attribute byte fetched from planes 0/1
    '
    ' The overlay is drawn AFTER scan-out directly into the host Integer()
    ' framebuffer.  It never changes VGA VRAM, BDA state, CRTC registers, or
    ' guest memory.  Set this constant False after diagnosis.
    ' TEMPORARY ATTRIBUTE-CONTROLLER DIAGNOSTIC.
    '
    ' Host-only. Does not change guest VRAM, BDA state, CRTC state, or DAC state.
    ' CURx = current emulator palette decode.
    ' VGAx = forensic VGA palette decode using P54S + Color Select rules.
    Private Sub DrawAttributeControllerDiagnostics(
        pixels As Integer(),
        width As Integer,
        height As Integer)

        If Not DiagnosticOverlayJumperClosed Then Return
        If pixels Is Nothing OrElse width <= 0 OrElse height <= 0 Then Return

        Dim ac10 As Integer = _attribute(&H10)
        Dim cs As Integer = _attribute(&H14)
        Dim p7 As Integer = _attribute(7) And &H3F
        Dim pf As Integer = _attribute(&HF) And &H3F

        Dim cur7 As Integer = MapAttributeColor(7)
        Dim curF As Integer = MapAttributeColor(&HF)
        Dim vga7 As Integer = MapAttributeColorForensic(7)
        Dim vgaF As Integer = MapAttributeColorForensic(&HF)

        Dim line1 As String = String.Format(
            "AC10={0:X2} CS={1:X2} P7={2:X2} PF={3:X2} CUR7={4:X2} VGA7={5:X2} CURF={6:X2} VGAF={7:X2} VE={8}",
            ac10, cs, p7, pf, cur7, vga7, curF, vgaF,
            If(_attributeVideoEnabled, 1, 0))

        Dim line2 As String = String.Format(
            "DAC7={0} DACF={1} DAC{2:X2}={3} DAC{4:X2}={5}",
            DiagnosticDacRgb(7),
            DiagnosticDacRgb(&HF),
            vga7, DiagnosticDacRgb(vga7),
            vgaF, DiagnosticDacRgb(vgaF))

        Dim sb As New System.Text.StringBuilder("PAL")
        For i As Integer = 0 To 15
            sb.AppendFormat(" {0:X2}", _attribute(i) And &H3F)
        Next

        DrawDiagnosticText(pixels, width, height, 0, 18, line1, Color.White.ToArgb())
        DrawDiagnosticText(pixels, width, height, 0, 36, line2, Color.Orange.ToArgb())
        DrawDiagnosticText(pixels, width, height, 0, 54, sb.ToString(), Color.LightGreen.ToArgb())
    End Sub

    Private Function MapAttributeColorForensic(index As Integer) As Integer
        Dim paletteValue As Integer = _attribute(index And &HF) And &H3F
        Dim colorSelect As Integer = _attribute(&H14) And &HF

        ' Attribute Mode Control 10h bit 7 = P54S.
        ' 0: DAC P5/P4 come from palette register bits 5/4.
        ' 1: DAC P5/P4 come from Color Select bits 1/0.
        If (_attribute(&H10) And &H80) <> 0 Then
            paletteValue =
                (paletteValue And &HF) Or
                ((colorSelect And &H3) << 4)
        End If

        ' Color Select bits 3/2 supply DAC P7/P6.
        paletteValue =
            paletteValue Or
            ((colorSelect And &HC) << 4)

        Return paletteValue And _ramdac.PelMask
    End Function

    Private Function DiagnosticDacRgb(index As Integer) As String
        index = index And &HFF

        Dim r As Integer = _ramdac.Component(index, 0) * 255 \ 63
        Dim g As Integer = _ramdac.Component(index, 1) * 255 \ 63
        Dim b As Integer = _ramdac.Component(index, 2) * 255 \ 63

        Return String.Format("{0:X2}{1:X2}{2:X2}", r, g, b)
    End Function
    ' Host-only service jumper JP-DIAG.
    ' OPEN/False = normal display (default). CLOSED/True = draw the forensic overlays.
    ' This is deliberately not guest-visible hardware; it only gates host diagnostics.
    Public Property DiagnosticOverlayJumperClosed As Boolean = False

    Private Sub DrawPageCornerDiagnostics(
        pixels As Integer(),
        width As Integer,
        height As Integer,
        topLeftCell As Integer,
        topRightCell As Integer,
        bottomLeftCell As Integer,
        bottomRightCell As Integer)

        If Not DiagnosticOverlayJumperClosed Then Return
        If pixels Is Nothing OrElse width <= 0 OrElse height <= 0 Then Return

        Dim tl As String = BuildPageCornerLabel("TL", topLeftCell)
        Dim tr As String = BuildPageCornerLabel("TR", topRightCell)
        Dim bl As String = BuildPageCornerLabel("BL", bottomLeftCell)
        Dim br As String = BuildPageCornerLabel("BR", bottomRightCell)

        DrawDiagnosticText(pixels, width, height, 0, 0, tl, Color.Lime.ToArgb())

        Dim trX As Integer = Math.Max(0, width - tr.Length * 8)
        DrawDiagnosticText(pixels, width, height, trX, 0, tr, Color.Yellow.ToArgb())

        Dim bottomY As Integer = Math.Max(0, height - 16)
        DrawDiagnosticText(pixels, width, height, 0, bottomY, bl, Color.Cyan.ToArgb())

        Dim brX As Integer = Math.Max(0, width - br.Length * 8)
        DrawDiagnosticText(pixels, width, height, brX, bottomY, br, Color.Magenta.ToArgb())
    End Sub

    Private Function BuildPageCornerLabel(tag As String, cell As Integer) As String
        If cell < 0 Then Return tag & " P? C????? V????? ??/??"

        Dim normalized As Integer = cell And &HFFFFF
        ' 80x25 BIOS text pages occupy 1000h interleaved bytes.  With VGA
        ' odd/even addressing that is 800h CRTC cells per page.
        Dim page As Integer = normalized \ &H800
        Dim vramByteOffset As Integer = NormalizeDisplayLinearAddressInBed(CLng(normalized) * 2L)
        Dim planeOffset As Integer = NormalizeDisplayPlaneOffsetInBed(normalized)
        Dim ch As Integer = ReadDisplayPlaneByteInBed(0, planeOffset)
        Dim attr As Integer = ReadDisplayPlaneByteInBed(1, planeOffset)

        Return String.Format(
            "{0} P{1:X1} C{2:X5} V{3:X5} {4:X2}/{5:X2}",
            tag,
            page,
            normalized,
            vramByteOffset,
            ch,
            attr)
    End Function

    Private Sub DrawDiagnosticText(
        pixels As Integer(),
        width As Integer,
        height As Integer,
        x As Integer,
        y As Integer,
        text As String,
        foreground As Integer)

        Const glyphWidth As Integer = 8
        Const glyphHeight As Integer = 16

        If String.IsNullOrEmpty(text) Then Return
        If x >= width OrElse y >= height Then Return

        Dim background As Integer = Color.Black.ToArgb()
        Dim boxWidth As Integer = Math.Min(width - x, text.Length * glyphWidth)
        Dim boxHeight As Integer = Math.Min(height - y, glyphHeight)

        ' Opaque black diagnostic backing makes the numbers readable regardless
        ' of the guest palette.  This touches only the host framebuffer.
        For py As Integer = 0 To boxHeight - 1
            Dim rowBase As Integer = (y + py) * width + x
            For px As Integer = 0 To boxWidth - 1
                pixels(rowBase + px) = background
            Next
        Next

        For characterIndex As Integer = 0 To text.Length - 1
            Dim charX As Integer = x + characterIndex * glyphWidth
            If charX + glyphWidth > width Then Exit For

            Dim code As Integer = AscW(text(characterIndex)) And &HFF
            Dim fontBase As Integer = code * 32

            For py As Integer = 0 To glyphHeight - 1
                If y + py >= height Then Exit For

                Dim bits As Integer = ReadDisplayPlaneByteInBed(2, (fontBase + py) And &HFFFF)
                Dim rowBase As Integer = (y + py) * width + charX

                For px As Integer = 0 To 7
                    If (bits And (&H80 >> px)) <> 0 Then
                        pixels(rowBase + px) = foreground
                    End If
                Next
            Next
        Next
    End Sub
    Private Function MapAttributeColor(index As Integer) As Integer
        Dim paletteValue As Integer = _attribute(index And &HF) And &H3F
        Dim colorSelect As Integer = _attribute(&H14) And &HF

        ' VGA Attribute Mode Control register 10h bit 7 (P54S):
        '   0 = DAC P5/P4 come from palette-register bits 5/4
        '   1 = DAC P5/P4 come from Color Select bits 1/0
        If (_attribute(&H10) And &H80) <> 0 Then
            paletteValue =
                (paletteValue And &HF) Or
                ((colorSelect And &H3) << 4)
        End If

        ' Color Select bits 3/2 always supply DAC P7/P6.
        paletteValue =
            paletteValue Or
            ((colorSelect And &HC) << 4)

        Return paletteValue And _ramdac.PelMask
    End Function
    Private Function Render256ColorMode() As Bitmap
        Dim width As Integer = 320
        Dim height As Integer = 200
        Dim inferredWidth As Integer = GetHorizontalDisplayDots()
        Dim inferredHeight As Integer = GetVerticalDisplayLines()
        If inferredWidth >= 256 AndAlso inferredWidth <= 1024 Then width = inferredWidth
        If inferredHeight >= 100 AndAlso inferredHeight <= 768 Then height = inferredHeight

        ' In VGA 256-colour shift mode the CRTC timing is expressed in dot
        ' clocks while one address-clock fetch serializes two 8-bit pixels.
        ' Consequently both BIOS mode 13h (640 timing dots -> 320 pixels) and
        ' Scorched Earth's unchained mode (720 timing dots -> 360 pixels) halve
        ' the CRTC horizontal display value.  SEQ01 bit 3 is a separate clock
        ' control and is not the scan-out width selector.
        width = Math.Max(1, width \ 2)

        Dim pixels As Integer() = FramePixels(width, height)
        Dim dacPaletteInBed() As Integer = BuildPaletteArgbTableInBed()

        ' VGA 256-color shift scan-out is a CRTC display path independent of the
        ' CPU's chain-4 choice. In this interleaved VRAM backing store, both chained
        ' mode 13h and unchained/Mode-X scan-out serialize plane 0..3 bytes into the
        ' 8-bit DAC stream. CR13 plus S3 extensions defines the logical row pitch.
        Dim startLinearInBed As Integer =
            NormalizeDisplayLinearAddressInBed(CLng(GetCrtcDisplayStartAddressCounterInBed()) * 4L)
        Dim pitchInBed As Integer = GetCrtcPackedRowAddressAdvanceInBed()
        Dim scanlinesInBed() As GraphicsScanlineAddressInBed =
            BuildGraphicsScanlineAddressesInBed(height,
                                                startLinearInBed,
                                                pitchInBed,
                                                packedAddressingInBed:=True)
        Dim programmedPixelPanInBed As Integer = (_attribute(&H13) And &H7) \ 2

        For yInBed As Integer = 0 To height - 1
            Dim outputRowInBed As Integer = yInBed * width
            Dim lowerScreenInBed As Boolean = scanlinesInBed(yInBed).LowerScreen
            Dim rowBaseInBed As Long = scanlinesInBed(yInBed).Address
            Dim pixelPanInBed As Integer =
                If(lowerScreenInBed AndAlso (_attribute(&H10) And &H20) <> 0,
                   0,
                   programmedPixelPanInBed)
            For xInBed As Integer = 0 To width - 1
                Dim colorIndexInBed As Integer =
                    ReadDisplayLinearByteInBed(rowBaseInBed + xInBed + pixelPanInBed) And _ramdac.PelMask
                pixels(outputRowInBed + xInBed) = dacPaletteInBed(colorIndexInBed And &HFF)
            Next
        Next
        Return _frameBitmap
    End Function

    Private Function RenderCgaCompatibilityMode() As Bitmap
        ' IBM VGA/XGA Technical Reference, VGA Function pp. 2-18..2-20:
        ' BIOS modes 04h/05h retain the CGA 320x200 four-pixel-per-byte format;
        ' mode 06h retains the 640x200 eight-pixel-per-byte format.  Successive
        ' source rows alternate between the two 8 KiB CGA banks.  VGA odd/even
        ' addressing stores successive CPU bytes in maps 0 and 1, so reconstruct
        ' the CPU-visible B800 byte stream before unpacking its pixels.
        ' https://bitsavers.org/pdf/ibm/pc/cards/IBM_VGA_XGA_Technical_Reference_Manual_May92.pdf
        Dim oneBitInBed As Boolean = (_sequencer(2) And &H3) = 1
        Dim widthInBed As Integer = If(oneBitInBed, 640, 320)
        Dim heightInBed As Integer =
            Math.Max(1, Math.Min(1200, GetVerticalDisplayLines()))
        Dim bytesPerRowInBed As Integer = widthInBed \ If(oneBitInBed, 8, 4)
        Dim startCpuByteInBed As Integer = (GetCrtcDisplayStartAddressCounterInBed() * 2) And &H7FFF
        Dim scanlinesInBed() As GraphicsScanlineAddressInBed =
            BuildGraphicsScanlineAddressesInBed(heightInBed,
                                                0,
                                                1,
                                                packedAddressingInBed:=True)

        Dim pixelsInBed As Integer() = FramePixels(widthInBed, heightInBed)
        Dim dacPaletteInBed() As Integer = BuildPaletteArgbTableInBed()
        Dim logicalPaletteInBed(3) As Integer
        For logicalInBed As Integer = 0 To 3
            logicalPaletteInBed(logicalInBed) =
                dacPaletteInBed(MapAttributeColor(logicalInBed) And &HFF)
        Next

        For yInBed As Integer = 0 To heightInBed - 1
            Dim sourceYInBed As Integer = CInt(scanlinesInBed(yInBed).Address)
            Dim cgaRowInBed As Integer =
                ((sourceYInBed And 1) * &H2000) +
                ((sourceYInBed \ 2) * bytesPerRowInBed)
            Dim outputRowInBed As Integer = yInBed * widthInBed
            For byteXInBed As Integer = 0 To bytesPerRowInBed - 1
                Dim cpuOffsetInBed As Integer = (startCpuByteInBed + cgaRowInBed + byteXInBed) And &H7FFF
                Dim planeInBed As Integer = If(oneBitInBed, 0, cpuOffsetInBed And 1)
                Dim planeOffsetInBed As Integer = If(oneBitInBed, cpuOffsetInBed, cpuOffsetInBed \ 2)
                Dim packedInBed As Integer = ReadDisplayPlaneByteInBed(planeInBed, planeOffsetInBed)
                If oneBitInBed Then
                    For bitInBed As Integer = 0 To 7
                        Dim logicalInBed As Integer = (packedInBed >> (7 - bitInBed)) And 1
                        pixelsInBed(outputRowInBed + byteXInBed * 8 + bitInBed) = logicalPaletteInBed(logicalInBed)
                    Next
                Else
                    For pairInBed As Integer = 0 To 3
                        Dim logicalInBed As Integer = (packedInBed >> (6 - pairInBed * 2)) And 3
                        pixelsInBed(outputRowInBed + byteXInBed * 4 + pairInBed) = logicalPaletteInBed(logicalInBed)
                    Next
                End If
            Next
        Next
        Return _frameBitmap
    End Function

    Private Function RenderPlanarGraphicsMode() As Bitmap
        Dim width As Integer = Math.Max(320, Math.Min(2048, GetHorizontalDisplayDots()))
        ' The C5/2 MiB board is not intrinsically limited to 768 visible lines in planar modes.
        ' Keep the same conservative host safety ceiling as enhanced scan-out so 1024/1200-line
        ' programmed modes are not silently cropped by the renderer.
        Dim height As Integer = Math.Max(200, Math.Min(1200, GetVerticalDisplayLines()))
        ' CR13 is extended by the S3 logical-screen-width bits.  Each planar
        ' CRTC offset unit advances two bytes within a plane.  The display start
        ' uses the independent CR31/CR51 CRTC extension bits.
        Dim stridePlaneBytesInBed As Integer = GetCrtcRowAddressAdvance()
        Dim startPlaneOffsetInBed As Integer =
            NormalizeDisplayPlaneOffsetInBed(CLng(GetCrtcDisplayStartAddressCounterInBed()) * 2L)

        Dim pixels As Integer() = FramePixels(width, height)
        Dim dacPaletteInBed() As Integer = BuildPaletteArgbTableInBed()
        Dim planarPaletteInBed(15) As Integer
        For logicalIndexInBed As Integer = 0 To 15
            planarPaletteInBed(logicalIndexInBed) = dacPaletteInBed(MapAttributeColor(logicalIndexInBed) And &HFF)
        Next

        Dim scanlinesInBed() As GraphicsScanlineAddressInBed =
            BuildGraphicsScanlineAddressesInBed(height,
                                                startPlaneOffsetInBed,
                                                stridePlaneBytesInBed,
                                                packedAddressingInBed:=False)
        Dim programmedPixelPanInBed As Integer = _attribute(&H13) And &H7
        Dim enabledPlanesInBed As Integer = _attribute(&H12) And &HF
        For yInBed As Integer = 0 To height - 1
            Dim outputRowInBed As Integer = yInBed * width
            Dim lowerScreenInBed As Boolean = scanlinesInBed(yInBed).LowerScreen
            Dim rowBaseInBed As Long = scanlinesInBed(yInBed).Address
            Dim pixelPanInBed As Integer =
                If(lowerScreenInBed AndAlso (_attribute(&H10) And &H20) <> 0,
                   0,
                   programmedPixelPanInBed)

            Dim cachedPlaneOffsetInBed As Long = Long.MinValue
            Dim plane0InBed As Integer
            Dim plane1InBed As Integer
            Dim plane2InBed As Integer
            Dim plane3InBed As Integer

            For xInBed As Integer = 0 To width - 1
                Dim sourceXInBed As Integer = xInBed + pixelPanInBed
                Dim planeOffsetInBed As Long = rowBaseInBed + (sourceXInBed \ 8)
                If planeOffsetInBed <> cachedPlaneOffsetInBed Then
                    cachedPlaneOffsetInBed = planeOffsetInBed
                    plane0InBed = If((enabledPlanesInBed And 1) <> 0,
                                     ReadDisplayPlaneByteInBed(0, planeOffsetInBed), 0)
                    plane1InBed = If((enabledPlanesInBed And 2) <> 0,
                                     ReadDisplayPlaneByteInBed(1, planeOffsetInBed), 0)
                    plane2InBed = If((enabledPlanesInBed And 4) <> 0,
                                     ReadDisplayPlaneByteInBed(2, planeOffsetInBed), 0)
                    plane3InBed = If((enabledPlanesInBed And 8) <> 0,
                                     ReadDisplayPlaneByteInBed(3, planeOffsetInBed), 0)
                End If
                Dim bitInBed As Integer = &H80 >> (sourceXInBed And 7)
                Dim colorIndexInBed As Integer = 0
                If (plane0InBed And bitInBed) <> 0 Then
                    colorIndexInBed = colorIndexInBed Or 1
                End If
                If (plane1InBed And bitInBed) <> 0 Then
                    colorIndexInBed = colorIndexInBed Or 2
                End If
                If (plane2InBed And bitInBed) <> 0 Then
                    colorIndexInBed = colorIndexInBed Or 4
                End If
                If (plane3InBed And bitInBed) <> 0 Then
                    colorIndexInBed = colorIndexInBed Or 8
                End If
                pixels(outputRowInBed + xInBed) = planarPaletteInBed(colorIndexInBed)
            Next
        Next
        Return _frameBitmap
    End Function

    Private Function RenderEnhancedMode() As Bitmap
        Dim width As Integer = Math.Max(320, Math.Min(2048, GetHorizontalDisplayDots()))
        Dim height As Integer = Math.Max(200, Math.Min(1200, GetVerticalDisplayLines()))
        ' Brooktree Bt485A Command Registers 1/3 and Tables 3-8, pp. 8-15:
        ' the RAMDAC selects display depth and packing.  S3 CR50[5:4] describes
        ' graphics-engine pixel length and must never be substituted for it.
        ' https://www.dosdays.co.uk/media/brooktree/BT485_Datasheet.pdf
        Dim displayBitsPerPixelInBed As Integer = _ramdac.DisplayBitsPerPixel

        ' Display scan-out obeys the programmed CRTC pitch exactly.  Do not
        ' substitute host-visible width when software intentionally programs a
        ' wider, narrower, wrapping or page-flipped logical surface.
        Dim pitchInBed As Integer = GetCrtcPackedRowAddressAdvanceInBed()
        Dim startInBed As Integer =
            NormalizeDisplayLinearAddressInBed(CLng(GetCrtcDisplayStartAddressCounterInBed()) * 4L)

        Dim pixels As Integer() = FramePixels(width, height)
        Dim dacPaletteInBed() As Integer =
            If(displayBitsPerPixelInBed = 4 OrElse displayBitsPerPixelInBed = 8,
               BuildPaletteArgbTableInBed(),
               Nothing)
        Dim scanlinesInBed() As GraphicsScanlineAddressInBed =
            BuildGraphicsScanlineAddressesInBed(height,
                                                startInBed,
                                                pitchInBed,
                                                packedAddressingInBed:=True)

        For yInBed As Integer = 0 To height - 1
            Dim outputRowInBed As Integer = yInBed * width
            Dim rowBaseInBed As Long = scanlinesInBed(yInBed).Address

            For xInBed As Integer = 0 To width - 1
                Select Case displayBitsPerPixelInBed
                    Case 4
                        Dim packedAddressInBed As Long = rowBaseInBed + (xInBed \ 2)
                        Dim packedInBed As Integer = ReadDisplayLinearByteInBed(packedAddressInBed)
                        Dim lowNibbleFirstInBed As Boolean = _ramdac.FourBitLowNibbleFirst
                        Dim useLowNibbleInBed As Boolean = ((xInBed And 1) = 0) = lowNibbleFirstInBed
                        Dim paletteIndexInBed As Integer =
                            If(useLowNibbleInBed, packedInBed And &HF, (packedInBed >> 4) And &HF)
                        paletteIndexInBed = paletteIndexInBed And (_ramdac.PelMask And &HF)
                        pixels(outputRowInBed + xInBed) = dacPaletteInBed(paletteIndexInBed)

                    Case 8
                        Dim addressInBed As Long = rowBaseInBed + xInBed
                        Dim paletteIndexInBed As Integer =
                            ReadDisplayLinearByteInBed(addressInBed) And _ramdac.PelMask
                        pixels(outputRowInBed + xInBed) = dacPaletteInBed(paletteIndexInBed And &HFF)

                    Case 16
                        Dim addressInBed As Long = rowBaseInBed + CLng(xInBed) * 2L
                        Dim lowInBed As Integer = ReadDisplayLinearByteInBed(addressInBed)
                        Dim highInBed As Integer = ReadDisplayLinearByteInBed(addressInBed + 1L)
                        Dim wordValueInBed As Integer = lowInBed Or (highInBed << 8)
                        Dim rRawInBed As Integer = (wordValueInBed >> If(_ramdac.SixteenBitRgb565, 11, 10)) And &H1F
                        Dim gRawInBed As Integer = (wordValueInBed >> 5) And If(_ramdac.SixteenBitRgb565, &H3F, &H1F)
                        Dim bRawInBed As Integer = wordValueInBed And &H1F
                        Dim rInBed As Integer
                        Dim gInBed As Integer
                        Dim bInBed As Integer
                        If _ramdac.TrueColorPaletteBypassed Then
                            rInBed = rRawInBed * 255 \ 31
                            gInBed = gRawInBed * 255 \ If(_ramdac.SixteenBitRgb565, 63, 31)
                            bInBed = bRawInBed * 255 \ 31
                        Else
                            rInBed = TrueColorPaletteComponentInBed(rRawInBed, 5, 0)
                            gInBed = TrueColorPaletteComponentInBed(gRawInBed, If(_ramdac.SixteenBitRgb565, 6, 5), 1)
                            bInBed = TrueColorPaletteComponentInBed(bRawInBed, 5, 2)
                        End If
                        pixels(outputRowInBed + xInBed) = Color.FromArgb(255, rInBed, gInBed, bInBed).ToArgb()

                    Case Else
                        ' Bt485A packed 24-bit Table 8: B, G, R byte order and
                        ' three bytes of VRAM per pixel (not a padded dword).
                        Dim addressInBed As Long = rowBaseInBed + CLng(xInBed) * 3L
                        Dim bInBed As Integer = ReadDisplayLinearByteInBed(addressInBed)
                        Dim gInBed As Integer = ReadDisplayLinearByteInBed(addressInBed + 1L)
                        Dim rInBed As Integer = ReadDisplayLinearByteInBed(addressInBed + 2L)
                        If Not _ramdac.TrueColorPaletteBypassed Then
                            rInBed = _ramdac.ComponentAsEightBit(rInBed And _ramdac.PelMask, 0)
                            gInBed = _ramdac.ComponentAsEightBit(gInBed And _ramdac.PelMask, 1)
                            bInBed = _ramdac.ComponentAsEightBit(bInBed And _ramdac.PelMask, 2)
                        End If
                        pixels(outputRowInBed + xInBed) = Color.FromArgb(255, rInBed, gInBed, bInBed).ToArgb()
                End Select
            Next
        Next
        Return _frameBitmap
    End Function

    Private Function TrueColorPaletteComponentInBed(rawComponentInBed As Integer,
                                                     componentBitsInBed As Integer,
                                                     paletteComponentInBed As Integer) As Integer
        ' Bt485A Pixel Read Mask and Table 9, pp. 15-16: in non-bypass true
        ' color, each component independently addresses its palette RAM using
        ' sparse (MSB-aligned) or contiguous (LSB-aligned) indexing.
        Dim paletteIndexInBed As Integer
        If _ramdac.TrueColorPaletteIndexesContiguous Then
            paletteIndexInBed = rawComponentInBed
        Else
            paletteIndexInBed = rawComponentInBed << (8 - componentBitsInBed)
        End If
        paletteIndexInBed = paletteIndexInBed And _ramdac.PelMask
        Return _ramdac.ComponentAsEightBit(paletteIndexInBed, paletteComponentInBed)
    End Function

    Private Function GetVerticalDisplayLines() As Integer
        Dim value As Integer = _crtc(&H12)
        If (_crtc(7) And &H2) <> 0 Then value = value Or &H100
        If (_crtc(7) And &H40) <> 0 Then value = value Or &H200
        If S3SystemRegistersUnlocked() AndAlso (_crtc(&H5E) And &H2) <> 0 Then value = value Or &H400
        Return value + 1
    End Function

    Private Function BuildPaletteArgbTableInBed() As Integer()
        Dim resultInBed(255) As Integer
        For indexInBed As Integer = 0 To 255
            Dim rInBed As Integer = _ramdac.ComponentAsEightBit(indexInBed, 0)
            Dim gInBed As Integer = _ramdac.ComponentAsEightBit(indexInBed, 1)
            Dim bInBed As Integer = _ramdac.ComponentAsEightBit(indexInBed, 2)
            resultInBed(indexInBed) = Color.FromArgb(255, rInBed, gInBed, bInBed).ToArgb()
        Next
        Return resultInBed
    End Function

    Private Function PaletteArgb(index As Integer) As Integer
        index = index And &HFF
        Dim r As Integer = _ramdac.ComponentAsEightBit(index, 0)
        Dim g As Integer = _ramdac.ComponentAsEightBit(index, 1)
        Dim b As Integer = _ramdac.ComponentAsEightBit(index, 2)
        Return Color.FromArgb(255, r, g, b).ToArgb()
    End Function


    Private Function ReadEnhancedWord(port As UInt16) As UInt16
        Select Case port
            Case &H42E8US
                Dim value As UInt16 = 0
                If _verticalSyncInterruptPending Then value = CUShort(value Or 1US)
                If _engineIdleInterruptPending Then value = CUShort(value Or 2US)
                If _fifoOverflowInterruptPending Then value = CUShort(value Or 4US)
                If _fifoEmptyInterruptPending Then value = CUShort(value Or 8US)
                ' MID2..0 are external monitor-sense pins.  Until a monitor
                ' profile drives them, pulled-high/open is the physical default.
                value = CUShort(value Or &H70US)
                If SubsystemReportsEightBitPlanes() Then value = CUShort(value Or &H80US)
                Return value
            Case &H4AE8US : Return _advancedFunction
            Case &H82E8US : Return _curY
            Case &H86E8US : Return _curX
            Case &H8AE8US : Return _destY
            Case &H8EE8US : Return _destX
            Case &H92E8US : Return _errorTerm
            Case &H96E8US : Return _majorAxisCount
            Case &H9AE8US
                Dim status As UInt16 = 0
                Dim occupiedSlotsInBed As Integer = Math.Min(EngineFifoDepth, _engineQueue.Count)
                If occupiedSlotsInBed > 0 Then
                    status = CUShort((1 << occupiedSlotsInBed) - 1)
                End If
                If _engineQueue.Count = 0 Then status = CUShort(status Or &H400US)
                If _graphicsEngineBusy Then status = CUShort(status Or &H200US)
                ' GP_STAT bit 8 is DATA_AVAIL for CPU reads from PIX_TRANS.
                If PixelTransferReadDataAvailableInBed() Then status = CUShort(status Or &H100US)
                Return status
            Case &HA2E8US : Return ReadEngineDwordHalfInBed(_backgroundColor)
            Case &HA6E8US : Return ReadEngineDwordHalfInBed(_foregroundColor)
            Case &HAAE8US : Return ReadEngineDwordHalfInBed(_writeMask)
            Case &HAEE8US : Return ReadEngineDwordHalfInBed(_readMask)
            Case &HB2E8US : Return ReadEngineDwordHalfInBed(_colorCompare)
            Case &HB6E8US : Return _backgroundMix
            Case &HBAE8US : Return _foregroundMix
            Case &HBEE8US
                Dim selectedInBed As Integer = _readRegisterSelect And 7
                Dim selectedValueInBed As UInt16
                Select Case selectedInBed
                    Case 0 : selectedValueInBed = _minorAxisCount
                    Case 1 : selectedValueInBed = _topScissors
                    Case 2 : selectedValueInBed = _leftScissors
                    Case 3 : selectedValueInBed = _bottomScissors
                    Case 4 : selectedValueInBed = _rightScissors
                    Case 5 : selectedValueInBed = _pixelControl
                    Case 6 : selectedValueInBed = _multifunctionMisc
                    Case Else : selectedValueInBed = CUShort(ReadEnhancedWord(&H9AE8US) And &H1FFFUS)
                End Select
                _readRegisterSelect = CByte((selectedInBed + 1) And 7)
                Return selectedValueInBed
            Case &HE2E8US
                Return ReadPixelTransferWordInBed()
            Case &HE2EAUS : Return _pixelTransferExtension
        End Select
        Return 0
    End Function

    Private Function SubsystemReportsEightBitPlanes() As Boolean
        ' SUBSYS_STAT bit 7 is the legacy 4/8-plane indication.  Explicit
        ' 16/32-bit engine lengths are not encoded in this one-bit field.
        If ((_crtc(&H50) >> 4) And 3) <> 0 Then Return False
        Return VramSize >= 1024 * 1024
    End Function

    Private Sub WriteEnhancedWord(port As UInt16, value As UInt16)
        Select Case port
            Case &H42E8US
                TraceVgaDiagnostic("OUT 42E8 S3 subsystem <- " & value.ToString("X4"))
                ' Bits 0-3 are write-one clear strobes.  Bits 8-11 are the
                ' persistent enables; clear strobes must never be read back by
                ' the byte-write shadow or replayed by a later high-byte write.
                If (value And &H1US) <> 0 Then _verticalSyncInterruptPending = False
                If (value And &H2US) <> 0 Then _engineIdleInterruptPending = False
                If (value And &H4US) <> 0 Then _fifoOverflowInterruptPending = False
                If (value And &H8US) <> 0 Then _fifoEmptyInterruptPending = False
                _subsystemControl = CUShort(value And &HF00US)
                If (value And &HC000US) = &H8000US Then
                    _engineQueue.Clear()
                    _engineActiveCommand = Nothing
                    _pixelTransferWriteBytes.Clear()
                    _pixelTransferReadBytes.Clear()
                    _graphicsEngineBusy = False
                    _engineIdleInterruptPending = True
                    _fifoEmptyInterruptPending = True
                End If
            Case &H4AE8US
                TraceVgaDiagnostic("OUT 4AE8 S3 advanced function <- " & value.ToString("X4"))
                Dim oldAdvancedFunctionInBed As UInt16 = _advancedFunction
                _advancedFunction = value
                If ((oldAdvancedFunctionInBed Xor value) And &H30US) <> 0US Then
                    RaiseEvent MemoryDecodeChanged()
                End If
            Case &H82E8US : _curY = CUShort(value And &HFFFUS)
            Case &H86E8US : _curX = CUShort(value And &HFFFUS)
            Case &H8AE8US : _destY = CUShort(value And &H3FFFUS)
            Case &H8EE8US : _destX = CUShort(value And &H3FFFUS)
            Case &H92E8US : _errorTerm = CUShort(value And &H3FFFUS)
            Case &H96E8US : _majorAxisCount = CUShort(value And &HFFFUS)
            Case &H9AE8US : QueueEnhancedCommand(value)
            Case &H9EE8US : QueueShortStroke(value)
            Case &HA2E8US : WriteEngineDwordHalfInBed(_backgroundColor, value)
            Case &HA6E8US : WriteEngineDwordHalfInBed(_foregroundColor, value)
            Case &HAAE8US : WriteEngineDwordHalfInBed(_writeMask, value)
            Case &HAEE8US : WriteEngineDwordHalfInBed(_readMask, value)
            Case &HB2E8US : WriteEngineDwordHalfInBed(_colorCompare, value)
            Case &HB6E8US : _backgroundMix = value
            Case &HBAE8US : _foregroundMix = value
            Case &HBEE8US
                Dim index As Integer = (value >> 12) And &HF
                Dim data As UInt16 = CUShort(value And &HFFFUS)
                Select Case index
                    Case 0 : _minorAxisCount = data
                    Case 1 : _topScissors = data
                    Case 2 : _leftScissors = data
                    Case 3 : _bottomScissors = data
                    Case 4 : _rightScissors = data
                    Case &HA : _pixelControl = data
                    Case &HE : _multifunctionMisc = data
                    Case &HF : _readRegisterSelect = CByte(data And &H7)
                End Select
            Case &HE2E8US
                _pixelTransfer = value
                FeedPixelTransferWordInBed(value)
            Case &HE2EAUS : _pixelTransferExtension = value
        End Select
    End Sub

    Private Function EngineUsesDwordRegistersInBed() As Boolean
        Return EnhancedBytesPerPixel() = 4
    End Function

    Private Function PeekEngineDwordHalfInBed(valueInBed As UInteger) As UInt16
        If EngineUsesDwordRegistersInBed() AndAlso (_multifunctionMisc And &H10US) <> 0 Then
            Return CUShort((valueInBed >> 16) And &HFFFFUI)
        End If
        Return CUShort(valueInBed And &HFFFFUI)
    End Function

    Private Function ReadEngineDwordHalfInBed(valueInBed As UInteger) As UInt16
        Dim resultInBed As UInt16 = PeekEngineDwordHalfInBed(valueInBed)
        If EngineUsesDwordRegistersInBed() Then
            _multifunctionMisc = CUShort(_multifunctionMisc Xor &H10US)
        End If
        Return resultInBed
    End Function

    Private Sub WriteEngineDwordHalfInBed(ByRef targetInBed As UInteger,
                                          valueInBed As UInt16)
        If EngineUsesDwordRegistersInBed() AndAlso (_multifunctionMisc And &H10US) <> 0 Then
            targetInBed = (targetInBed And &HFFFFUI) Or (CUInt(valueInBed) << 16)
        Else
            targetInBed = (targetInBed And &HFFFF0000UI) Or valueInBed
        End If
        If EngineUsesDwordRegistersInBed() Then
            _multifunctionMisc = CUShort(_multifunctionMisc Xor &H10US)
        End If
    End Sub

    Private Function CaptureEngineCommandInBed(commandInBed As UInt16) As EngineCommand928
        Return New EngineCommand928() With {
            .Command = commandInBed,
            .PixelPhasePicoseconds = 0,
            .Progress = 0,
            .TotalPixels = Math.Max(1L, CLng((_majorAxisCount And &HFFFUS) + 1) * CLng((_minorAxisCount And &HFFFUS) + 1)),
            .CurX = _curX,
            .CurY = _curY,
            .DestX = _destX,
            .DestY = _destY,
            .ErrorTerm = _errorTerm,
            .MajorAxisCount = _majorAxisCount,
            .MinorAxisCount = _minorAxisCount,
            .TopScissors = _topScissors,
            .LeftScissors = _leftScissors,
            .BottomScissors = _bottomScissors,
            .RightScissors = _rightScissors,
            .PixelControl = _pixelControl,
            .MultifunctionMisc = _multifunctionMisc,
            .BackgroundColor = _backgroundColor,
            .ForegroundColor = _foregroundColor,
            .WriteMask = _writeMask,
            .ReadMask = _readMask,
            .ColorCompare = _colorCompare,
            .BackgroundMix = _backgroundMix,
            .ForegroundMix = _foregroundMix,
            .PixelTransfer = _pixelTransfer,
            .PixelTransferExtension = _pixelTransferExtension,
            .BytesPerPixel = EnhancedBytesPerPixel(),
            .PitchBytes = EnhancedPitch(),
            .SourceBaseAddress = CLng((_multifunctionMisc >> 2) And 3US) << 20,
            .DestinationBaseAddress = CLng(_multifunctionMisc And 3US) << 20
        }
    End Function

    Private Function EngineFifoCanAcceptInBed() As Boolean
        If _engineQueue.Count + If(_engineActiveCommand Is Nothing, 0, 1) < EngineFifoDepth Then Return True
        _fifoOverflowInterruptPending = True
        Return False
    End Function

    Private Sub EnqueueEngineCommandInBed(commandInBed As EngineCommand928)
        If commandInBed Is Nothing Then Return
        _engineQueue.Enqueue(commandInBed)
        If _engineActiveCommand Is Nothing Then StartNextEngineCommand()
    End Sub

    Private Sub QueueEnhancedCommand(command As UInt16)
        If (_crtc(&H40) And 1) = 0 Then Return
        _lastDrawingCommand = command
        If Not EngineFifoCanAcceptInBed() Then Return
        EnqueueEngineCommandInBed(CaptureEngineCommandInBed(command))
    End Sub

    Private Sub QueueShortStroke(valueInBed As UInt16)
        If (_crtc(&H40) And 1) = 0 Then Return
        If Not EngineFifoCanAcceptInBed() Then Return

        Dim commandInBed As EngineCommand928 = CaptureEngineCommandInBed(_lastDrawingCommand)
        commandInBed.IsShortStroke = True
        commandInBed.ShortStrokeValue = valueInBed
        commandInBed.TotalPixels = 0
        For vectorIndexInBed As Integer = 0 To 1
            Dim byteIndexInBed As Integer = If((_lastDrawingCommand And &H1000US) <> 0,
                                                vectorIndexInBed, 1 - vectorIndexInBed)
            Dim vectorInBed As Integer = (CInt(valueInBed) >> (byteIndexInBed * 8)) And &HFF
            commandInBed.TotalPixels += (vectorInBed And &HF) + 1L
        Next

        ' A word containing no enabled short-stroke vector consumes no engine
        ' time and does not manufacture BUSY/FIFO activity.
        If commandInBed.TotalPixels <= 0 Then Return
        EnqueueEngineCommandInBed(commandInBed)
    End Sub

    Private Sub StartNextEngineCommand()
        If _engineQueue.Count = 0 Then
            If _graphicsEngineBusy Then _engineIdleInterruptPending = True
            _engineActiveCommand = Nothing
            _graphicsEngineBusy = False
            Return
        End If
        _engineActiveCommand = _engineQueue.Dequeue()
        If _engineQueue.Count = 0 Then _fifoEmptyInterruptPending = True
        _graphicsEngineBusy = True
    End Sub

    Private Sub AdvanceGraphicsEngine(elapsedPicosecondsInBed As Long)
        Dim remainingInBed As Long = elapsedPicosecondsInBed
        While remainingInBed > 0 AndAlso _engineActiveCommand IsNot Nothing
            Dim neededInBed As Long = EnginePixelPeriodPicoseconds - _engineActiveCommand.PixelPhasePicoseconds
            If remainingInBed < neededInBed Then
                _engineActiveCommand.PixelPhasePicoseconds += remainingInBed
                Exit While
            End If
            remainingInBed -= neededInBed
            _engineActiveCommand.PixelPhasePicoseconds = 0
            If StepEnhancedCommand(_engineActiveCommand) Then
                Dim completedInBed As EngineCommand928 = _engineActiveCommand
                _engineActiveCommand = Nothing
                _curX = CUShort(completedInBed.CurX And &HFFFUS)
                _curY = CUShort(completedInBed.CurY And &HFFFUS)
                _errorTerm = CUShort(completedInBed.ErrorTerm And &H3FFFUS)
                StartNextEngineCommand()
            End If
        End While
    End Sub

    Private Function StepEnhancedCommand(commandInBed As EngineCommand928) As Boolean
        If commandInBed.IsShortStroke Then Return StepShortStrokeCommandInBed(commandInBed)

        Dim commandTypeInBed As Integer = (commandInBed.Command >> 13) And 7

        ' A rectangle command with WAIT=1 is a host image-transfer operation.
        ' The engine advances only when the CPU supplies/accepts the next transfer
        ' unit; it must not render using the last latched PIX_TRANS word.
        If commandTypeInBed = 2 AndAlso (commandInBed.Command And &H100US) <> 0 Then
            Return StepPixelTransferCommandInBed(commandInBed)
        End If

        Select Case commandTypeInBed
            Case 1
                If Not commandInBed.Initialized Then
                    commandInBed.LineX = commandInBed.CurX
                    commandInBed.LineY = commandInBed.CurY
                    commandInBed.TotalPixels = (commandInBed.MajorAxisCount And &HFFFUS) + 1L
                    Dim directionInBed As Integer = (commandInBed.Command >> 5) And 7
                    commandInBed.LineRadial = (commandInBed.Command And &H8US) <> 0
                    If commandInBed.LineRadial Then
                        Dim dxsInBed() As Integer = {1, 1, 0, -1, -1, -1, 0, 1}
                        Dim dysInBed() As Integer = {0, -1, -1, -1, 0, 1, 1, 1}
                        commandInBed.LineStepX = dxsInBed(directionInBed)
                        commandInBed.LineStepY = dysInBed(directionInBed)
                    Else
                        ' Axial lines consume the guest-supplied S3 Bresenham
                        ' state. DEST_Y is AXSTP and DEST_X is DIASTP.
                        commandInBed.LineStepX = If((directionInBed And 1) <> 0, 1, -1)
                        commandInBed.LineStepY = If((directionInBed And 4) <> 0, 1, -1)
                        ' CMD bit 6 set selects X-major; clear selects Y-major.
                        commandInBed.LineMajorIsY = (directionInBed And 2) = 0
                        commandInBed.LineDx = SignExtend14(commandInBed.DestY)
                        commandInBed.LineDy = SignExtend14(commandInBed.DestX)
                        commandInBed.LineError = SignExtend14(commandInBed.ErrorTerm)
                    End If
                    commandInBed.Initialized = True
                End If
                Dim finalPixelInBed As Boolean = commandInBed.Progress = commandInBed.TotalPixels - 1
                Dim drawPixelInBed As Boolean = (commandInBed.Command And &H10US) <> 0 AndAlso
                    Not (finalPixelInBed AndAlso (commandInBed.Command And &H4US) <> 0)
                If drawPixelInBed Then
                    PutEnhancedPixelForCommand(commandInBed, commandInBed.LineX, commandInBed.LineY,
                                               commandInBed.ForegroundColor, commandInBed.ForegroundMix)
                End If
                commandInBed.Progress += 1
                If commandInBed.Progress >= commandInBed.TotalPixels Then
                    commandInBed.CurX = CUShort(commandInBed.LineX And &HFFF)
                    commandInBed.CurY = CUShort(commandInBed.LineY And &HFFF)
                    commandInBed.ErrorTerm = CUShort(commandInBed.LineError And &H3FFF)
                    Return True
                End If
                If commandInBed.LineRadial Then
                    commandInBed.LineX += commandInBed.LineStepX
                    commandInBed.LineY += commandInBed.LineStepY
                ElseIf commandInBed.LineError >= 0 Then
                    commandInBed.LineX += commandInBed.LineStepX
                    commandInBed.LineY += commandInBed.LineStepY
                    commandInBed.LineError += commandInBed.LineDy
                Else
                    If commandInBed.LineMajorIsY Then
                        commandInBed.LineY += commandInBed.LineStepY
                    Else
                        commandInBed.LineX += commandInBed.LineStepX
                    End If
                    commandInBed.LineError += commandInBed.LineDx
                End If
                commandInBed.CurX = CUShort(commandInBed.LineX And &HFFF)
                commandInBed.CurY = CUShort(commandInBed.LineY And &HFFF)
                commandInBed.ErrorTerm = CUShort(commandInBed.LineError And &H3FFF)
                Return False

            Case 2, 6, 7
                Dim widthInBed As Integer = (commandInBed.MajorAxisCount And &HFFFUS) + 1
                Dim heightInBed As Integer = (commandInBed.MinorAxisCount And &HFFFUS) + 1
                commandInBed.TotalPixels = CLng(widthInBed) * heightInBed
                Dim xxInBed As Integer = CInt(commandInBed.Progress Mod widthInBed)
                Dim yyInBed As Integer = CInt(commandInBed.Progress \ widthInBed)
                Dim xDirectionInBed As Integer = If((commandInBed.Command And &H20US) <> 0, 1, -1)
                Dim yDirectionInBed As Integer = If((commandInBed.Command And &H80US) <> 0, 1, -1)
                Dim sourceInBed As UInteger
                Dim destinationXInBed As Integer
                Dim destinationYInBed As Integer
                If commandTypeInBed = 2 Then
                    sourceInBed = commandInBed.ForegroundColor
                    destinationXInBed = CInt(commandInBed.CurX) + xxInBed * xDirectionInBed
                    destinationYInBed = CInt(commandInBed.CurY) + yyInBed * yDirectionInBed
                ElseIf commandTypeInBed = 6 Then
                    sourceInBed = GetEnhancedPixelForCommand(commandInBed,
                        CInt(commandInBed.CurX) + xxInBed * xDirectionInBed,
                        CInt(commandInBed.CurY) + yyInBed * yDirectionInBed,
                        sourceAddressInBed:=True)
                    destinationXInBed = CInt(commandInBed.DestX) + xxInBed * xDirectionInBed
                    destinationYInBed = CInt(commandInBed.DestY) + yyInBed * yDirectionInBed
                Else
                    Dim patternXInBed As Integer = (CInt(commandInBed.CurX) And Not 7) + (xxInBed And 7)
                    Dim patternYInBed As Integer = CInt(commandInBed.CurY) + (yyInBed And 7)
                    sourceInBed = GetEnhancedPixelForCommand(commandInBed,
                                                             patternXInBed,
                                                             patternYInBed,
                                                             sourceAddressInBed:=True)
                    destinationXInBed = CInt(commandInBed.DestX) + xxInBed
                    destinationYInBed = CInt(commandInBed.DestY) + yyInBed
                End If
                If (commandInBed.Command And &H10US) <> 0 Then
                    PutEnhancedPixelForCommand(commandInBed, destinationXInBed, destinationYInBed, sourceInBed, commandInBed.ForegroundMix)
                End If
                commandInBed.Progress += 1
                If commandInBed.Progress >= commandInBed.TotalPixels Then
                    commandInBed.CurX = commandInBed.DestX
                    commandInBed.CurY = commandInBed.DestY
                    Return True
                End If
                Return False
        End Select
        Return True
    End Function

    Private Function StepShortStrokeCommandInBed(commandInBed As EngineCommand928) As Boolean
        If Not commandInBed.Initialized Then
            commandInBed.LineX = CInt(commandInBed.CurX)
            commandInBed.LineY = CInt(commandInBed.CurY)
            commandInBed.ShortStrokeVectorIndex = 0
            commandInBed.ShortStrokePixelsRemaining = 0
            commandInBed.Initialized = True
        End If

        While commandInBed.ShortStrokePixelsRemaining <= 0 AndAlso
              commandInBed.ShortStrokeVectorIndex < 2
            Dim byteIndexInBed As Integer = If((commandInBed.Command And &H1000US) <> 0,
                                                commandInBed.ShortStrokeVectorIndex,
                                                1 - commandInBed.ShortStrokeVectorIndex)
            Dim shiftInBed As Integer = byteIndexInBed * 8
            Dim vectorInBed As Integer = (CInt(commandInBed.ShortStrokeValue) >> shiftInBed) And &HFF
            commandInBed.ShortStrokeVectorIndex += 1
            commandInBed.ShortStrokePixelsRemaining = (vectorInBed And &HF) + 1
            commandInBed.Command = CUShort((commandInBed.Command And Not &H10US) Or CUShort(vectorInBed And &H10))
            Dim directionInBed As Integer = (vectorInBed >> 5) And 7
            Dim dxsInBed() As Integer = {1, 1, 0, -1, -1, -1, 0, 1}
            Dim dysInBed() As Integer = {0, -1, -1, -1, 0, 1, 1, 1}
            commandInBed.LineStepX = dxsInBed(directionInBed)
            commandInBed.LineStepY = dysInBed(directionInBed)
        End While

        If commandInBed.ShortStrokePixelsRemaining <= 0 Then
            commandInBed.CurX = CUShort(commandInBed.LineX And &HFFF)
            commandInBed.CurY = CUShort(commandInBed.LineY And &HFFF)
            Return True
        End If

        If (commandInBed.Command And &H10US) <> 0 Then
            PutEnhancedPixelForCommand(commandInBed,
                                       commandInBed.LineX,
                                       commandInBed.LineY,
                                       commandInBed.ForegroundColor,
                                       commandInBed.ForegroundMix)
        End If
        commandInBed.LineX += commandInBed.LineStepX
        commandInBed.LineY += commandInBed.LineStepY
        commandInBed.ShortStrokePixelsRemaining -= 1
        commandInBed.Progress += 1
        commandInBed.CurX = CUShort(commandInBed.LineX And &HFFF)
        commandInBed.CurY = CUShort(commandInBed.LineY And &HFFF)

        If commandInBed.Progress >= commandInBed.TotalPixels Then Return True
        Return False
    End Function

    Private Shared Function SignExtend14(valueInBed As UInt16) As Integer
        Dim resultInBed As Integer = valueInBed And &H3FFFUS
        If (resultInBed And &H2000) <> 0 Then resultInBed -= &H4000
        Return resultInBed
    End Function

    Private Sub PutEnhancedPixelForCommand(commandInBed As EngineCommand928,
                                           xInBed As Integer,
                                           yInBed As Integer,
                                           colorInBed As UInteger,
                                           mixInBed As UInt16)
        Dim insideClipInBed As Boolean = xInBed >= commandInBed.LeftScissors AndAlso
                                          xInBed <= commandInBed.RightScissors AndAlso
                                          yInBed >= commandInBed.TopScissors AndAlso
                                          yInBed <= commandInBed.BottomScissors
        If (commandInBed.MultifunctionMisc And &H20US) = 0 Then
            If Not insideClipInBed Then Return
        ElseIf insideClipInBed Then
            Return
        End If
        If xInBed < 0 OrElse yInBed < 0 Then Return
        Dim destinationInBed As UInteger =
            GetEnhancedPixelForCommand(commandInBed,
                                       xInBed,
                                       yInBed,
                                       sourceAddressInBed:=False)
        If (commandInBed.MultifunctionMisc And &H100US) <> 0 Then
            Dim equalInBed As Boolean = ((colorInBed Xor commandInBed.ColorCompare) And commandInBed.ReadMask) = 0UI
            Dim rejectInBed As Boolean = If((commandInBed.MultifunctionMisc And &H80US) <> 0,
                                             Not equalInBed, equalInBed)
            If rejectInBed Then Return
        End If
        Dim mixedInBed As UInteger = ApplyCommandMix(commandInBed, colorInBed, destinationInBed, mixInBed)
        Dim finalInBed As UInteger = (mixedInBed And commandInBed.WriteMask) Or (destinationInBed And Not commandInBed.WriteMask)
        Dim addressInBed As Long = commandInBed.DestinationBaseAddress +
            CLng(yInBed) * commandInBed.PitchBytes +
            CLng(xInBed) * commandInBed.BytesPerPixel
        If addressInBed < 0 OrElse addressInBed + commandInBed.BytesPerPixel > VramSize Then Return
        For byteInBed As Integer = 0 To Math.Min(3, commandInBed.BytesPerPixel - 1)
            _vram(CInt(addressInBed + byteInBed)) = CByte((finalInBed >> (byteInBed * 8)) And &HFFUI)
        Next
    End Sub

    Private Function ApplyCommandMix(commandInBed As EngineCommand928,
                                     operationSourceInBed As UInteger,
                                     destinationInBed As UInteger,
                                     requestedMixInBed As UInt16) As UInteger
        Dim foregroundSourceInBed As UInteger = SelectCommandMixColor(commandInBed, requestedMixInBed,
                                                                      operationSourceInBed, destinationInBed)
        Dim foregroundResultInBed As UInteger = ApplyMixFunction(foregroundSourceInBed, destinationInBed, requestedMixInBed)
        Select Case (commandInBed.PixelControl >> 6) And 3US
            Case 2US, 3US
                Dim selectorInBed As UInteger
                If ((commandInBed.PixelControl >> 6) And 3US) = 2US Then
                    selectorInBed = CUInt(commandInBed.PixelTransfer) Or (CUInt(commandInBed.PixelTransferExtension) << 16)
                Else
                    selectorInBed = destinationInBed
                End If
                Dim backgroundSourceInBed As UInteger = SelectCommandMixColor(commandInBed, commandInBed.BackgroundMix,
                                                                              operationSourceInBed, destinationInBed)
                Dim backgroundResultInBed As UInteger = ApplyMixFunction(backgroundSourceInBed, destinationInBed,
                                                                          commandInBed.BackgroundMix)
                Return (foregroundResultInBed And selectorInBed) Or (backgroundResultInBed And Not selectorInBed)
            Case Else
                Return foregroundResultInBed
        End Select
    End Function

    Private Shared Function SelectCommandMixColor(commandInBed As EngineCommand928,
                                                   mixInBed As UInt16,
                                                   operationSourceInBed As UInteger,
                                                   destinationInBed As UInteger) As UInteger
        Select Case (mixInBed >> 5) And 3US
            Case 0US : Return commandInBed.BackgroundColor
            Case 1US : Return commandInBed.ForegroundColor
            Case 2US : Return CUInt(commandInBed.PixelTransfer) Or (CUInt(commandInBed.PixelTransferExtension) << 16)
            Case Else : Return operationSourceInBed
        End Select
    End Function

    Private Function GetEnhancedPixelForCommand(commandInBed As EngineCommand928,
                                                xInBed As Integer,
                                                yInBed As Integer,
                                                sourceAddressInBed As Boolean) As UInteger
        If xInBed < 0 OrElse yInBed < 0 Then Return 0UI
        Dim baseAddressInBed As Long =
            If(sourceAddressInBed,
               commandInBed.SourceBaseAddress,
               commandInBed.DestinationBaseAddress)
        Dim addressInBed As Long = baseAddressInBed +
            CLng(yInBed) * commandInBed.PitchBytes +
            CLng(xInBed) * commandInBed.BytesPerPixel
        If addressInBed < 0 OrElse addressInBed + commandInBed.BytesPerPixel > VramSize Then Return 0UI
        Dim resultInBed As UInteger
        For byteInBed As Integer = 0 To Math.Min(3, commandInBed.BytesPerPixel - 1)
            resultInBed = resultInBed Or (CUInt(_vram(CInt(addressInBed + byteInBed))) << (byteInBed * 8))
        Next
        Return resultInBed
    End Function

    Private Sub FeedPixelTransferByteInBed(valueInBed As Byte)
        _pixelTransferWriteBytes.Enqueue(valueInBed)
    End Sub

    Private Sub FeedPixelTransferWordInBed(valueInBed As UInt16)
        _pixelTransferReadLatch = valueInBed
        _pixelTransferWriteBytes.Enqueue(CByte(valueInBed And &HFFUS))
        _pixelTransferWriteBytes.Enqueue(CByte((valueInBed >> 8) And &HFFUS))
    End Sub

    Private Function ReadPixelTransferByteInBed() As Byte
        If _pixelTransferReadBytes.Count = 0 Then Return CByte(_pixelTransferReadLatch And &HFFUS)
        Dim valueInBed As Byte = _pixelTransferReadBytes.Dequeue()
        _pixelTransferReadLatch = CUShort(valueInBed)
        Return valueInBed
    End Function

    Private Function ReadPixelTransferWordInBed() As UInt16
        If _pixelTransferReadBytes.Count = 0 Then Return _pixelTransferReadLatch
        Dim lowInBed As Byte = _pixelTransferReadBytes.Dequeue()
        Dim highInBed As Byte = If(_pixelTransferReadBytes.Count > 0, _pixelTransferReadBytes.Dequeue(), CByte(0))
        _pixelTransferReadLatch = CUShort(lowInBed Or (CUShort(highInBed) << 8))
        Return _pixelTransferReadLatch
    End Function

    Private Function PixelTransferReadDataAvailableInBed() As Boolean
        If _pixelTransferReadBytes.Count = 0 Then Return False
        If _engineActiveCommand Is Nothing OrElse Not _engineActiveCommand.TransferWordWidth Then Return True
        Return _pixelTransferReadBytes.Count >= 2
    End Function

    Private Sub InitializePixelTransferCommandInBed(commandInBed As EngineCommand928)
        commandInBed.TransferAcrossPlanes = (commandInBed.Command And &H2US) <> 0
        commandInBed.TransferWrite = (commandInBed.Command And &H1US) <> 0
        commandInBed.TransferWordWidth = (commandInBed.Command And &H200US) <> 0
        Dim transferBytesInBed As Integer = If(commandInBed.TransferWordWidth, 2, 1)
        Dim widthInBed As Integer = (commandInBed.MajorAxisCount And &HFFFUS) + 1
        If commandInBed.TransferAcrossPlanes Then
            commandInBed.TransferPixelsPerByte = transferBytesInBed * 8
            commandInBed.TransferUnitsPerRow = (widthInBed + commandInBed.TransferPixelsPerByte - 1) \ commandInBed.TransferPixelsPerByte
        Else
            ' Through-plane transfers are a byte stream.  A 16-bit ISA transfer
            ' can therefore be only half of one 32-bpp pixel; it must not be
            ' mistaken for a complete pixel and advance X prematurely.
            commandInBed.TransferPixelsPerByte = Math.Max(1, transferBytesInBed \ Math.Max(1, commandInBed.BytesPerPixel))
            Dim rowBytesInBed As Integer = widthInBed * Math.Max(1, commandInBed.BytesPerPixel)
            commandInBed.TransferUnitsPerRow = (rowBytesInBed + transferBytesInBed - 1) \ transferBytesInBed
        End If
        commandInBed.TotalPixels = CLng(commandInBed.TransferUnitsPerRow) * ((commandInBed.MinorAxisCount And &HFFFUS) + 1L)
        commandInBed.TransferUnitIndex = 0
        commandInBed.TransferPixelAccumulator = 0UI
        commandInBed.TransferAccumulatorBytes = 0
        commandInBed.TransferInitialized = True
    End Sub

    Private Function StepPixelTransferCommandInBed(commandInBed As EngineCommand928) As Boolean
        If Not commandInBed.TransferInitialized Then InitializePixelTransferCommandInBed(commandInBed)
        Dim transferBytesInBed As Integer = If(commandInBed.TransferWordWidth, 2, 1)
        If commandInBed.TransferWrite Then
            If _pixelTransferWriteBytes.Count < transferBytesInBed Then Return False
        ElseIf _pixelTransferReadBytes.Count <> 0 Then
            ' DATA_AVAIL remains asserted until the CPU consumes this unit.
            Return False
        End If

        Dim packedInBed As UInteger
        If commandInBed.TransferWrite Then
            For byteInBed As Integer = 0 To transferBytesInBed - 1
                packedInBed = packedInBed Or (CUInt(_pixelTransferWriteBytes.Dequeue()) << (byteInBed * 8))
            Next
            ' CMD bit 12 selects the order presented to the engine.  Clear means
            ' high byte first; set means the ISA little-endian low byte first.
            If commandInBed.TransferWordWidth AndAlso (commandInBed.Command And &H1000US) = 0 Then
                packedInBed = ((packedInBed And &HFFUI) << 8) Or ((packedInBed >> 8) And &HFFUI)
            End If
        End If

        Dim widthInBed As Integer = (commandInBed.MajorAxisCount And &HFFFUS) + 1
        Dim rowInBed As Integer = CInt(commandInBed.TransferUnitIndex \ commandInBed.TransferUnitsPerRow)
        Dim unitInRowInBed As Integer = CInt(commandInBed.TransferUnitIndex Mod commandInBed.TransferUnitsPerRow)
        Dim firstPixelInBed As Integer = unitInRowInBed * commandInBed.TransferPixelsPerByte
        Dim xDirectionInBed As Integer = If((commandInBed.Command And &H20US) <> 0, 1, -1)
        Dim yDirectionInBed As Integer = If((commandInBed.Command And &H80US) <> 0, 1, -1)

        If commandInBed.TransferWrite Then
            For pixelInUnitInBed As Integer = 0 To commandInBed.TransferPixelsPerByte - 1
                Dim rowPixelInBed As Integer = firstPixelInBed + pixelInUnitInBed
                If rowPixelInBed >= widthInBed Then Exit For
                Dim xInBed As Integer = CInt(commandInBed.CurX) + rowPixelInBed * xDirectionInBed
                Dim yInBed As Integer = CInt(commandInBed.CurY) + rowInBed * yDirectionInBed
                If commandInBed.TransferAcrossPlanes Then
                    Dim bitNumberInBed As Integer = transferBytesInBed * 8 - 1 - pixelInUnitInBed
                    Dim foregroundInBed As Boolean = ((packedInBed >> bitNumberInBed) And 1UI) <> 0UI
                    Dim sourceInBed As UInteger = If(foregroundInBed, commandInBed.ForegroundColor, commandInBed.BackgroundColor)
                    Dim mixInBed As UInt16 = If(foregroundInBed, commandInBed.ForegroundMix, commandInBed.BackgroundMix)
                    PutEnhancedPixelForCommand(commandInBed, xInBed, yInBed, sourceInBed, mixInBed)
                End If
            Next
            If Not commandInBed.TransferAcrossPlanes Then
                Dim rowBytesInBed As Integer = widthInBed * commandInBed.BytesPerPixel
                For byteInUnitInBed As Integer = 0 To transferBytesInBed - 1
                    Dim rowByteInBed As Integer = unitInRowInBed * transferBytesInBed + byteInUnitInBed
                    If rowByteInBed >= rowBytesInBed Then Exit For
                    Dim pixelInBed As Integer = rowByteInBed \ commandInBed.BytesPerPixel
                    Dim componentInBed As Integer = rowByteInBed Mod commandInBed.BytesPerPixel
                    If componentInBed = 0 Then
                        commandInBed.TransferPixelAccumulator = 0UI
                        commandInBed.TransferAccumulatorBytes = 0
                    End If
                    commandInBed.TransferPixelAccumulator = commandInBed.TransferPixelAccumulator Or
                        (CUInt((packedInBed >> (byteInUnitInBed * 8)) And &HFFUI) << (componentInBed * 8))
                    commandInBed.TransferAccumulatorBytes += 1
                    If commandInBed.TransferAccumulatorBytes = commandInBed.BytesPerPixel Then
                        Dim xInBed As Integer = CInt(commandInBed.CurX) + pixelInBed * xDirectionInBed
                        Dim yInBed As Integer = CInt(commandInBed.CurY) + rowInBed * yDirectionInBed
                        PutEnhancedPixelForCommand(commandInBed, xInBed, yInBed,
                                                   commandInBed.TransferPixelAccumulator,
                                                   commandInBed.ForegroundMix)
                    End If
                Next
            End If
        Else
            Dim outputInBed As UInteger
            If commandInBed.TransferAcrossPlanes Then
                For pixelInUnitInBed As Integer = 0 To commandInBed.TransferPixelsPerByte - 1
                    Dim rowPixelInBed As Integer = firstPixelInBed + pixelInUnitInBed
                    If rowPixelInBed >= widthInBed Then Exit For
                    Dim xInBed As Integer = CInt(commandInBed.CurX) + rowPixelInBed * xDirectionInBed
                    Dim yInBed As Integer = CInt(commandInBed.CurY) + rowInBed * yDirectionInBed
                    Dim sourceInBed As UInteger =
                        GetEnhancedPixelForCommand(commandInBed,
                                                   xInBed,
                                                   yInBed,
                                                   sourceAddressInBed:=True)
                    Dim bitNumberInBed As Integer = transferBytesInBed * 8 - 1 - pixelInUnitInBed
                    If (sourceInBed And commandInBed.ReadMask) = commandInBed.ReadMask Then
                        outputInBed = outputInBed Or (1UI << bitNumberInBed)
                    End If
                Next
            Else
                Dim rowBytesInBed As Integer = widthInBed * commandInBed.BytesPerPixel
                For byteInUnitInBed As Integer = 0 To transferBytesInBed - 1
                    Dim rowByteInBed As Integer = unitInRowInBed * transferBytesInBed + byteInUnitInBed
                    If rowByteInBed >= rowBytesInBed Then Exit For
                    Dim pixelInBed As Integer = rowByteInBed \ commandInBed.BytesPerPixel
                    Dim componentInBed As Integer = rowByteInBed Mod commandInBed.BytesPerPixel
                    Dim xInBed As Integer = CInt(commandInBed.CurX) + pixelInBed * xDirectionInBed
                    Dim yInBed As Integer = CInt(commandInBed.CurY) + rowInBed * yDirectionInBed
                    Dim sourceInBed As UInteger =
                        GetEnhancedPixelForCommand(commandInBed,
                                                   xInBed,
                                                   yInBed,
                                                   sourceAddressInBed:=True)
                    outputInBed = outputInBed Or
                        (((sourceInBed >> (componentInBed * 8)) And &HFFUI) << (byteInUnitInBed * 8))
                Next
            End If
            If commandInBed.TransferWordWidth AndAlso (commandInBed.Command And &H1000US) = 0 Then
                outputInBed = ((outputInBed And &HFFUI) << 8) Or ((outputInBed >> 8) And &HFFUI)
            End If
            For byteInBed As Integer = 0 To transferBytesInBed - 1
                _pixelTransferReadBytes.Enqueue(CByte((outputInBed >> (byteInBed * 8)) And &HFFUI))
            Next
        End If

        commandInBed.TransferUnitIndex += 1
        commandInBed.Progress = commandInBed.TransferUnitIndex
        If commandInBed.TransferUnitIndex < commandInBed.TotalPixels Then Return False
        commandInBed.CurX = CUShort((CInt(commandInBed.CurX) + (widthInBed - 1) * xDirectionInBed) And &HFFF)
        commandInBed.CurY = CUShort((CInt(commandInBed.CurY) + (((commandInBed.MinorAxisCount And &HFFFUS))) * yDirectionInBed) And &HFFF)
        Return True
    End Function

    Private Function EnhancedBytesPerPixel() As Integer
        If _engineForcedBytesPerPixel > 0 Then Return _engineForcedBytesPerPixel
        Select Case (_crtc(&H50) >> 4) And 3
            Case 1 : Return 2
            Case 3 : Return 4
            Case Else : Return 1
        End Select
    End Function

    Private Function GetEngineScreenWidthPixels() As Integer
        ' CR50[7:6] is the 86C928 graphics-engine screen-width field.
        ' CR50 bit 0 is not an additional width bit; treating it as one invents
        ' undocumented 1152/1600-pixel modes and changes the engine pitch when
        ' software modifies an unrelated control.
        Dim codeInBed As Integer = (_crtc(&H50) >> 6) And 3
        Select Case codeInBed
            Case 1 : Return 640
            Case 2 : Return 800
            Case 3 : Return 1280
            Case Else
                ' The zero encoding selects the large virtual-screen stride;
                ' CR31 bit 1 distinguishes the 1K and 2K organizations.
                Return If((_crtc(&H31) And &H2) <> 0, 2048, 1024)
        End Select
    End Function

    Private Function EnhancedPitch() As Integer
        If _engineForcedPitchBytes > 0 Then Return _engineForcedPitchBytes
        ' Drawing-engine address generation is controlled by CR50.  CR13 plus
        ' CR51/CR43 controls display fetch pitch and must not silently override
        ' the engine's programmed virtual-screen width.
        Return GetEngineScreenWidthPixels() * EnhancedBytesPerPixel()
    End Function

    Private Shared Function ApplyMixFunction(sourceInBed As UInteger, destinationInBed As UInteger, mixInBed As UInt16) As UInteger
        ' 86C928 MIX-TYPE truth table ("current" is the destination and "new"
        ' is the selected color source).  This ordering is not the conventional
        ' generic ROP2 ordering.
        Select Case mixInBed And &HFUS
            Case &H0 : Return Not destinationInBed
            Case &H1 : Return 0UI
            Case &H2 : Return &HFFFFFFFFUI
            Case &H3 : Return destinationInBed
            Case &H4 : Return Not sourceInBed
            Case &H5 : Return destinationInBed Xor sourceInBed
            Case &H6 : Return Not destinationInBed Xor sourceInBed
            Case &H7 : Return sourceInBed
            Case &H8 : Return Not destinationInBed Or Not sourceInBed
            Case &H9 : Return destinationInBed Or Not sourceInBed
            Case &HA : Return Not destinationInBed Or sourceInBed
            Case &HB : Return destinationInBed Or sourceInBed
            Case &HC : Return destinationInBed And sourceInBed
            Case &HD : Return Not destinationInBed And sourceInBed
            Case &HE : Return destinationInBed And Not sourceInBed
            Case Else : Return Not destinationInBed And Not sourceInBed
        End Select
    End Function

    ' Host presentation snapshot.  The caller must invoke this while it owns the
    ' live machine.  Copying 2 MiB of VRAM and the scan-out register file is much
    ' shorter than rasterizing a complete frame while the CPU/bus ownership gate
    ' is held.  The target is a private renderer replica and is never guest-visible.
    Public Sub CopyPresentationStateTo(targetInBed As DiamondStealthPro928)
        If targetInBed Is Nothing Then Throw New ArgumentNullException(NameOf(targetInBed))
        If Object.ReferenceEquals(Me, targetInBed) Then Return

        Buffer.BlockCopy(_vram, 0, targetInBed._vram, 0, _vram.Length)
        Buffer.BlockCopy(_sequencer, 0, targetInBed._sequencer, 0, _sequencer.Length)
        Buffer.BlockCopy(_crtc, 0, targetInBed._crtc, 0, _crtc.Length)
        Buffer.BlockCopy(_graphics, 0, targetInBed._graphics, 0, _graphics.Length)
        Buffer.BlockCopy(_attribute, 0, targetInBed._attribute, 0, _attribute.Length)

        targetInBed._miscOutput = _miscOutput
        targetInBed._attributeVideoEnabled = _attributeVideoEnabled
        targetInBed._frameCounter = _frameCounter
        targetInBed._advancedFunction = _advancedFunction
        targetInBed._cursorForeground = _cursorForeground
        targetInBed._cursorBackground = _cursorBackground
        _clockGenerator.CopyPresentationStateTo(targetInBed._clockGenerator)
        targetInBed.DiagnosticOverlayJumperClosed = DiagnosticOverlayJumperClosed
        _ramdac.CopyPresentationStateTo(targetInBed._ramdac)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _frameBitmap IsNot Nothing Then
            _frameBitmap.Dispose()
            _frameBitmap = Nothing
        End If
        If _frameHandle.IsAllocated Then _frameHandle.Free()
        _framePixels = Array.Empty(Of Integer)()
        _frameWidth = 0
        _frameHeight = 0
    End Sub
End Class

' CROMWELL STEALTH PRO 928 PRESENTATION BRICK
'
' This is host presentation machinery, not an ISA device.  A private 86C928
' replica receives a short, machine-owned state copy; all expensive raster work
' then happens here without holding the CPU/motherboard ownership gate.  WinForms
' consumes only already-rendered frames through a separate presentation lock.
Public NotInheritable Class DiamondStealthPro928PresentationWorker
    Implements IDisposable

    Public Event PresentationFaulted(faultInBed As Exception)

    ' _rendererInBed has explicit ownership:
    '   snapshotBusy=1 from machine-boundary state copy through worker raster finish.
    ' The machine thread never overwrites it while the raster worker is reading it.
    Private ReadOnly _rendererInBed As New DiamondStealthPro928()
    Private ReadOnly _crtPresenterInBed As New CrtPresenter()
    Private ReadOnly _requestInBed As New AutoResetEvent(False)
    Private ReadOnly _frameGateInBed As New Object()
    Private ReadOnly _recycledFramesInBed As New System.Collections.Generic.List(Of Bitmap)()

    Private _workerInBed As Thread
    Private _stopRequestedInBed As Integer
    Private _runningInBed As Integer
    Private _requestOutstandingInBed As Integer
    Private _snapshotBusyInBed As Integer
    Private _snapshotReadyInBed As Integer
    Private _powerOffFrameRequestInBed As Integer
    Private _disposedInBed As Boolean

    Private _latestFrameInBed As Bitmap
    Private _frameWidthInBed As Integer
    Private _frameHeightInBed As Integer
    Private _publishedGenerationInBed As Long

    Private _requestCountInBed As Long
    Private _coalescedRequestCountInBed As Long
    Private _boundaryCaptureCountInBed As Long
    Private _boundaryBusyDeferralCountInBed As Long
    Private _renderedFrameCountInBed As Long
    Private _lastCaptureTicksInBed As Long
    Private _lastRenderTicksInBed As Long
    Private _lastPublishTicksInBed As Long
    Private _lastFaultInBed As String = String.Empty

    Public Sub New()
    End Sub

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return Volatile.Read(_runningInBed) <> 0
        End Get
    End Property

    Public Sub Start()
        If _disposedInBed Then Throw New ObjectDisposedException(NameOf(DiamondStealthPro928PresentationWorker))
        If Interlocked.CompareExchange(_runningInBed, 1, 0) <> 0 Then Return
        Interlocked.Exchange(_stopRequestedInBed, 0)
        ' This worker is only the final host CRT transducer. Guest VRAM,
        ' CRTC timing and scanout state have already been captured at a
        ' coordinated machine boundary. Do not let an expensive cosmetic
        ' raster compete at equal priority with the machine timeline; when
        ' the host is saturated, presentation may coalesce a frame while
        ' guest-visible hardware continues deterministically.
        _workerInBed = New Thread(AddressOf WorkerLoopInBed) With {
            .IsBackground = True,
            .Name = "Diamond Stealth Pro 928 host rasterizer",
            .Priority = ThreadPriority.BelowNormal
        }
        _workerInBed.Start()
    End Sub

    Public Sub RequestFrame()
        If _disposedInBed OrElse Volatile.Read(_stopRequestedInBed) <> 0 Then Return
        Interlocked.Increment(_requestCountInBed)
        If Interlocked.Exchange(_requestOutstandingInBed, 1) <> 0 Then
            Interlocked.Increment(_coalescedRequestCountInBed)
        End If
    End Sub

    ' Host chassis power-off presentation. No guest/device state is sampled.
    ' The raster worker owns CrtPresenter, so the synthetic dark CRT is queued to
    ' that same worker instead of touching its mutable presentation surface on UI.
    Public Sub RequestPowerOffFrame()
        If _disposedInBed OrElse Volatile.Read(_stopRequestedInBed) <> 0 Then Return
        Interlocked.Exchange(_requestOutstandingInBed, 0)
        Interlocked.Exchange(_powerOffFrameRequestInBed, 1)
        _requestInBed.Set()
    End Sub

    ' Called only by MachineRuntime286 while the machine thread already owns the
    ' CPU/bus/device gate at the end of a bounded slice. It never waits for the
    ' raster worker. If the previous snapshot is still being rendered, the newest
    ' request remains coalesced and a later slice boundary will service it.
    Public Sub ServiceCaptureAtMachineBoundary(sourceInBed As DiamondStealthPro928)
        If sourceInBed Is Nothing Then Return
        If _disposedInBed OrElse Volatile.Read(_stopRequestedInBed) <> 0 Then Return
        If Volatile.Read(_requestOutstandingInBed) = 0 Then Return

        If Interlocked.CompareExchange(_snapshotBusyInBed, 1, 0) <> 0 Then
            Interlocked.Increment(_boundaryBusyDeferralCountInBed)
            Return
        End If

        Dim ownsSnapshotInBed As Boolean = True
        Try
            ' Re-check after claiming the replica in case the request was consumed
            ' by an earlier boundary on another lifecycle transition.
            If Interlocked.Exchange(_requestOutstandingInBed, 0) = 0 Then
                Interlocked.Exchange(_snapshotBusyInBed, 0)
                ownsSnapshotInBed = False
                Return
            End If

            Dim captureStartInBed As Long = Stopwatch.GetTimestamp()
            sourceInBed.CopyPresentationStateTo(_rendererInBed)
            Interlocked.Exchange(_lastCaptureTicksInBed,
                                 Stopwatch.GetTimestamp() - captureStartInBed)
            Interlocked.Increment(_boundaryCaptureCountInBed)
            Interlocked.Exchange(_snapshotReadyInBed, 1)
            _requestInBed.Set()
            ownsSnapshotInBed = False
        Catch ex As Exception
            If ownsSnapshotInBed Then Interlocked.Exchange(_snapshotBusyInBed, 0)
            FailPresentationInBed(ex)
        End Try
    End Sub

    Public Function TakeLatestFrame(ByRef lastGenerationInBed As Long) As Bitmap
        If _disposedInBed Then Return Nothing
        SyncLock _frameGateInBed
            If _latestFrameInBed Is Nothing OrElse
               _publishedGenerationInBed = lastGenerationInBed Then Return Nothing

            Dim resultInBed As Bitmap = _latestFrameInBed
            _latestFrameInBed = Nothing
            lastGenerationInBed = _publishedGenerationInBed
            Return resultInBed
        End SyncLock
    End Function

    Public Sub RecycleFrame(frameInBed As Bitmap)
        If frameInBed Is Nothing Then Return
        SyncLock _frameGateInBed
            If _disposedInBed OrElse _recycledFramesInBed.Count >= 3 Then
                frameInBed.Dispose()
            Else
                _recycledFramesInBed.Add(frameInBed)
            End If
        End SyncLock
    End Sub

    Private Sub WorkerLoopInBed()
        Try
            While Volatile.Read(_stopRequestedInBed) = 0
                _requestInBed.WaitOne()
                If Volatile.Read(_stopRequestedInBed) <> 0 Then Exit While

                If Interlocked.Exchange(_powerOffFrameRequestInBed, 0) <> 0 Then
                    ' If a captured guest snapshot was queued but not yet rasterized,
                    ' discard it now that the chassis is dark and release replica
                    ' ownership so the next power-on can capture immediately.
                    If Interlocked.Exchange(_snapshotReadyInBed, 0) <> 0 Then
                        Interlocked.Exchange(_snapshotBusyInBed, 0)
                    End If
                    Try
                        Using blankInBed As New Bitmap(640, 400, PixelFormat.Format32bppArgb)
                            Using graphicsInBed As Graphics = Graphics.FromImage(blankInBed)
                                graphicsInBed.Clear(Color.Black)
                            End Using
                            Dim powerOffTimingInBed As New VideoScanoutTiming With {
                                .HorizontalActiveDots = 640,
                                .HorizontalTotalDots = 800,
                                .VerticalActiveLines = 400,
                                .VerticalTotalLines = 449,
                                .PixelRepeat = 1
                            }
                            Dim presentedOffInBed As Bitmap =
                                _crtPresenterInBed.Present(blankInBed, powerOffTimingInBed)
                            PublishFrameInBed(presentedOffInBed)
                        End Using
                    Catch ex As Exception
                        FailPresentationInBed(ex)
                    End Try
                    Continue While
                End If

                If Interlocked.Exchange(_snapshotReadyInBed, 0) = 0 Then Continue While

                Try
                    Dim renderStartInBed As Long = Stopwatch.GetTimestamp()
                    Dim renderedInBed As Bitmap = _rendererInBed.RenderFrame()
                    Dim presentedInBed As Bitmap =
                        _crtPresenterInBed.Present(renderedInBed,
                                                   _rendererInBed.GetScanoutTiming())
                    Interlocked.Exchange(_lastRenderTicksInBed,
                                         Stopwatch.GetTimestamp() - renderStartInBed)

                    Dim publishStartInBed As Long = Stopwatch.GetTimestamp()
                    PublishFrameInBed(presentedInBed)
                    Interlocked.Exchange(_lastPublishTicksInBed,
                                         Stopwatch.GetTimestamp() - publishStartInBed)
                    Interlocked.Increment(_renderedFrameCountInBed)
                Finally
                    ' Only now may a future machine boundary overwrite the replica.
                    Interlocked.Exchange(_snapshotBusyInBed, 0)
                End Try
            End While
        Catch ex As Exception
            FailPresentationInBed(ex)
        Finally
            Interlocked.Exchange(_runningInBed, 0)
            Interlocked.Exchange(_snapshotBusyInBed, 0)
        End Try
    End Sub

    Private Sub FailPresentationInBed(faultInBed As Exception)
        If faultInBed Is Nothing Then Return
        _lastFaultInBed = faultInBed.ToString()
        If Interlocked.Exchange(_stopRequestedInBed, 1) = 0 Then
            Try
                _requestInBed.Set()
            Catch
            End Try
            RaiseEvent PresentationFaulted(faultInBed)
        End If
    End Sub

    Private Function AcquirePublishFrameInBed(widthInBed As Integer,
                                                heightInBed As Integer) As Bitmap
        SyncLock _frameGateInBed
            For indexInBed As Integer = _recycledFramesInBed.Count - 1 To 0 Step -1
                Dim candidateInBed As Bitmap = _recycledFramesInBed(indexInBed)
                _recycledFramesInBed.RemoveAt(indexInBed)
                If candidateInBed.Width = widthInBed AndAlso
                   candidateInBed.Height = heightInBed Then
                    Return candidateInBed
                End If
                candidateInBed.Dispose()
            Next
        End SyncLock
        Return New Bitmap(widthInBed, heightInBed, PixelFormat.Format32bppArgb)
    End Function

    Private Sub PublishFrameInBed(sourceInBed As Bitmap)
        If sourceInBed Is Nothing Then Return

        ' Full-frame GDI work remains outside the publication lock.
        Dim publishInBed As Bitmap =
            AcquirePublishFrameInBed(sourceInBed.Width, sourceInBed.Height)
        Using graphicsInBed As Graphics = Graphics.FromImage(publishInBed)
            graphicsInBed.CompositingMode =
                System.Drawing.Drawing2D.CompositingMode.SourceCopy
            graphicsInBed.DrawImageUnscaled(sourceInBed, 0, 0)
        End Using

        SyncLock _frameGateInBed
            Dim staleInBed As Bitmap = _latestFrameInBed
            _latestFrameInBed = publishInBed
            _frameWidthInBed = publishInBed.Width
            _frameHeightInBed = publishInBed.Height
            _publishedGenerationInBed += 1L

            If staleInBed IsNot Nothing Then
                If _recycledFramesInBed.Count < 3 Then
                    _recycledFramesInBed.Add(staleInBed)
                Else
                    staleInBed.Dispose()
                End If
            End If
        End SyncLock
    End Sub

    Public Function DiagnosticText() As String
        Dim frequencyInBed As Double = CDbl(Stopwatch.Frequency)
        Dim captureMillisecondsInBed As Double =
            CDbl(Interlocked.Read(_lastCaptureTicksInBed)) * 1000.0R / frequencyInBed
        Dim renderMillisecondsInBed As Double =
            CDbl(Interlocked.Read(_lastRenderTicksInBed)) * 1000.0R / frequencyInBed
        Dim publishMillisecondsInBed As Double =
            CDbl(Interlocked.Read(_lastPublishTicksInBed)) * 1000.0R / frequencyInBed
        Dim dimensionsInBed As String
        SyncLock _frameGateInBed
            dimensionsInBed = If(_frameWidthInBed <= 0 OrElse _frameHeightInBed <= 0,
                                 "none",
                                 _frameWidthInBed.ToString() & "x" & _frameHeightInBed.ToString())
        End SyncLock

        Return "S3 host presentation worker" & Environment.NewLine &
               "  running           : " & If(IsRunning, "yes", "no") & Environment.NewLine &
               "  frame requests    : " & Interlocked.Read(_requestCountInBed).ToString("N0") & Environment.NewLine &
               "  coalesced requests: " & Interlocked.Read(_coalescedRequestCountInBed).ToString("N0") & Environment.NewLine &
               "  boundary captures : " & Interlocked.Read(_boundaryCaptureCountInBed).ToString("N0") & Environment.NewLine &
               "  render deferrals  : " & Interlocked.Read(_boundaryBusyDeferralCountInBed).ToString("N0") & Environment.NewLine &
               "  rendered frames   : " & Interlocked.Read(_renderedFrameCountInBed).ToString("N0") & Environment.NewLine &
               "  latest frame      : " & dimensionsInBed & Environment.NewLine &
               "  capture ms        : " & captureMillisecondsInBed.ToString("0.000") & Environment.NewLine &
               "  raster ms         : " & renderMillisecondsInBed.ToString("0.000") & Environment.NewLine &
               "  publish ms        : " & publishMillisecondsInBed.ToString("0.000") & Environment.NewLine &
               "  fault             : " & If(String.IsNullOrEmpty(_lastFaultInBed), "none", _lastFaultInBed)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposedInBed Then Return
        _disposedInBed = True
        Interlocked.Exchange(_stopRequestedInBed, 1)
        _requestInBed.Set()

        Dim workerInBed As Thread = _workerInBed
        If workerInBed IsNot Nothing AndAlso workerInBed IsNot Thread.CurrentThread Then
            workerInBed.Join()
        End If

        SyncLock _frameGateInBed
            If _latestFrameInBed IsNot Nothing Then _latestFrameInBed.Dispose()
            _latestFrameInBed = Nothing
            For Each frameInBed As Bitmap In _recycledFramesInBed
                frameInBed.Dispose()
            Next
            _recycledFramesInBed.Clear()
        End SyncLock

        _crtPresenterInBed.Dispose()
        _rendererInBed.Dispose()
        _requestInBed.Dispose()
    End Sub
End Class
