Imports System
Imports System.Collections.Generic

' OPTi 82C602 companion/buffer state used by the QDI V4P895P3/SMT board.
'
' The 82C601/602 data book defines F7h-FAh as WRITE-ONLY indexed registers.
' They are reached here through the 82C895 motherboard index/data fabric; reads
' therefore float high rather than exposing our host-side diagnostic latches.
Public NotInheritable Class Opti602Companion486
    Private _generalChipSelectAddressHigh As Byte
    Private _generalChipSelectAddressLow As Byte
    Private _generalChipSelectControl As Byte
    Private _greenPowerPort As Byte

    Public Sub New()
        ResetDevice()
    End Sub

    Public Sub ResetDevice()
        _generalChipSelectAddressHigh = &H0
        _generalChipSelectAddressLow = &H0
        _generalChipSelectControl = &H0
        _greenPowerPort = &H7 ' PP2#/PP1#/PP0# all inactive/high after reset.
    End Sub

    Public Function HandlesIndex(indexInBed As Byte) As Boolean
        Return indexInBed >= &HF7 AndAlso indexInBed <= &HFA
    End Function

    Public Function ReadIndexedRegister(indexInBed As Byte) As Byte
        If Not HandlesIndex(indexInBed) Then Return &HFF
        ' F7h-FAh are documented WO.  Do not turn implementation state into a
        ' guest-visible readback feature the physical part does not provide.
        Return &HFF
    End Function

    Public Sub WriteIndexedRegister(indexInBed As Byte, valueInBed As Byte)
        Select Case indexInBed
            Case &HF7
                _generalChipSelectAddressHigh = valueInBed
            Case &HF8
                _generalChipSelectAddressLow = valueInBed
            Case &HF9
                ' Mask selectors 110/111 are reserved.  Preserve the actual bus
                ' value in the latch; behavior using those encodings is simply not
                ' synthesized by this milestone.
                _generalChipSelectControl = valueInBed
            Case &HFA
                ' Bits 7:3 are reserved; only physical PP2#/PP1#/PP0# exist.
                _greenPowerPort = CByte(valueInBed And &H7)
        End Select
    End Sub

    Public ReadOnly Property UpperCmosBankEnabled As Boolean
        Get
            Return (_generalChipSelectControl And &H20) <> 0
        End Get
    End Property

    Public ReadOnly Property GeneralPurposeChipSelectAddress As UInt16
        Get
            Return CUShort((CUShort(_generalChipSelectAddressHigh) << 8) Or _generalChipSelectAddressLow)
        End Get
    End Property

    Public ReadOnly Property GeneralPurposeChipSelectControlForDiagnostics As Byte
        Get
            Return _generalChipSelectControl
        End Get
    End Property

    Public ReadOnly Property GreenPowerPortForDiagnostics As Byte
        Get
            Return _greenPowerPort
        End Get
    End Property
End Class

' OPTi 82C895 System/Power Management Controller indexed-register model.
'
' QDI V4P895P3/SMT uses the documented dual data-port arrangement:
'   22h = shared index latch
'   23h = integrated peripheral-controller data (IPC index 01h)
'   24h = 82C895 main control/power-management data
'
' Main registers 20h-2Fh (except 2Ch) and E0h-EFh are represented.  The
' companion 82C602's F7h-FAh write-only registers are routed through the same
' board-side indexed path.  CP04 intentionally models the silicon register
' surface before attaching it to a powered 486 machine fabric.
Public NotInheritable Class Opti895Chipset486
    Implements IPortDevice, IPortDecodeCandidateProvider, IResettableDevice, IMotherboardLocalPortDevice

    Public Const IndexPort As UInt16 = &H22US
    Public Const IpcDataPort As UInt16 = &H23US
    Public Const MainDataPort As UInt16 = &H24US

    Private ReadOnly _mainRegisters(255) As Byte
    Private ReadOnly _ipcRegisters(255) As Byte
    Private ReadOnly _companion As Opti602Companion486
    Private _index As Byte

    Public Sub New(Optional companionInBed As Opti602Companion486 = Nothing)
        _companion = If(companionInBed, New Opti602Companion486())
        ResetDevice()
    End Sub

    Public ReadOnly Property Companion602 As Opti602Companion486
        Get
            Return _companion
        End Get
    End Property

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        Array.Clear(_mainRegisters, 0, _mainRegisters.Length)
        Array.Clear(_ipcRegisters, 0, _ipcRegisters.Length)
        _index = 0
        _companion.ResetDevice()

        ' OPTi 82C895 Rev. 1.0, September 1994 reset/default values.
        _mainRegisters(&H20) = &H0
        _mainRegisters(&H21) = &H0
        _mainRegisters(&H22) = &HE4
        _mainRegisters(&H23) = &H0
        _mainRegisters(&H24) = &H0
        _mainRegisters(&H25) = &H7C
        _mainRegisters(&H26) = &H10
        _mainRegisters(&H27) = &HDE
        _mainRegisters(&H28) = &HF8
        _mainRegisters(&H29) = &H10 ' table marks low nibble don't-care while disabled
        _mainRegisters(&H2A) = &HE0
        _mainRegisters(&H2B) = &H10 ' table marks low nibble don't-care while disabled
        _mainRegisters(&H2D) = &HC0
        _mainRegisters(&H2E) = &H0
        _mainRegisters(&H2F) = &H0

        For indexInBed As Integer = &HE0 To &HE7
            _mainRegisters(indexInBed) = &H0
        Next
        _mainRegisters(&HE8) = &H8
        _mainRegisters(&HE9) = &H8
        _mainRegisters(&HEA) = &H0
        _mainRegisters(&HEB) = &HFF
        _mainRegisters(&HEC) = &H0
        _mainRegisters(&HED) = &H0
        _mainRegisters(&HEE) = &H0
        ' The EFh table prints reset=00 while its reserved-bit prose asks BIOS
        ' to program bit6=1 and bits1:0=01.  Reset follows the printed default;
        ' writes are normalized to the documented required reserved values.
        _mainRegisters(&HEF) = &H0

        ' Internal IPC configuration register (index 01h) reset/default = C0h.
        _ipcRegisters(&H1) = &HC0
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port = IndexPort OrElse port = IpcDataPort OrElse port = MainDataPort
    End Function

    Public Function PotentiallyHandlesPort(port As UInt16) As Boolean Implements IPortDecodeCandidateProvider.PotentiallyHandlesPort
        Return HandlesPort(port)
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Select Case port
            Case IndexPort
                ' The data book specifies 22h as the index register but does not
                ' define useful readback semantics.  Model an undriven ISA read.
                Return &HFF
            Case IpcDataPort
                Return ReadIpcData()
            Case MainDataPort
                Return ReadMainData()
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Select Case port
            Case IndexPort
                _index = value
            Case IpcDataPort
                WriteIpcData(value)
            Case MainDataPort
                WriteMainData(value)
        End Select
    End Sub

    Private Function ReadIpcData() As Byte
        Select Case _index
            Case &H1
                Return _ipcRegisters(&H1)
            Case Else
                Return &HFF
        End Select
    End Function

    Private Sub WriteIpcData(valueInBed As Byte)
        Select Case _index
            Case &H1
                _ipcRegisters(&H1) = valueInBed
        End Select
    End Sub

    Private Function ReadMainData() As Byte
        If _companion.HandlesIndex(_index) Then
            Return _companion.ReadIndexedRegister(_index)
        End If
        If Not IsMainIndexDecoded(_index) Then Return &HFF
        Return _mainRegisters(_index)
    End Function

    Private Sub WriteMainData(valueInBed As Byte)
        If _companion.HandlesIndex(_index) Then
            _companion.WriteIndexedRegister(_index, valueInBed)
            Return
        End If
        If Not IsMainIndexDecoded(_index) Then Return

        Select Case _index
            Case &H20
                ' Revision bits 7:6 are RO and currently 00. Bit 6 is also a
                ' documented must-remain-zero debug bit.
                _mainRegisters(_index) = CByte(valueInBed And &H3F)
            Case &H21
                ' Bit 6 is reserved/must remain zero.
                _mainRegisters(_index) = CByte(valueInBed And &HBF)
            Case &H22
                ' Reserved debug bits 6:5 must remain 11.
                _mainRegisters(_index) = CByte((valueInBed And &H9F) Or &H60)
            Case &H28, &H2A
                ' Bits 4:3 are unused and read as 11 in the reset table.
                _mainRegisters(_index) = CByte((valueInBed And &HE7) Or &H18)
            Case &HEF
                ' Datasheet programming requirement for reserved bits: bit6=1,
                ' bits5:4=00, bit7=0 and bits1:0=01. Preserve only mode bits 3:2.
                _mainRegisters(_index) = CByte(&H41 Or (valueInBed And &HC))
            Case Else
                _mainRegisters(_index) = valueInBed
        End Select
    End Sub

    Public Shared Function IsMainIndexDecoded(indexInBed As Byte) As Boolean
        If indexInBed >= &H20 AndAlso indexInBed <= &H2F Then
            Return indexInBed <> &H2C
        End If
        Return indexInBed >= &HE0 AndAlso indexInBed <= &HEF
    End Function

    Public ReadOnly Property SelectedIndexForDiagnostics As Byte
        Get
            Return _index
        End Get
    End Property

    Public Function PeekMainRegisterForDiagnostics(indexInBed As Byte) As Byte
        If Not IsMainIndexDecoded(indexInBed) Then Return &HFF
        Return _mainRegisters(indexInBed)
    End Function

    Public Function PeekIpcRegisterForDiagnostics(indexInBed As Byte) As Byte
        If indexInBed <> &H1 Then Return &HFF
        Return _ipcRegisters(indexInBed)
    End Function

    Public ReadOnly Property ExternalL2CacheEnabled As Boolean
        Get
            Return (_mainRegisters(&H21) And &H10) <> 0
        End Get
    End Property

    Public ReadOnly Property L2CacheKilobytes As Integer
        Get
            Select Case (_mainRegisters(&H21) >> 2) And &H3
                Case 0 : Return 64
                Case 1 : Return 128
                Case 2 : Return 256
                Case Else : Return 512
            End Select
        End Get
    End Property

    Public ReadOnly Property FastGateA20Enabled As Boolean
        Get
            Return (_mainRegisters(&H22) And &H2) <> 0
        End Get
    End Property

    Public ReadOnly Property GlobalCacheEnabled As Boolean
        Get
            ' Register 27h bit7 is active-low: 0 globally enables cache.
            Return (_mainRegisters(&H27) And &H80) = 0
        End Get
    End Property

    Public ReadOnly Property TurboNormalMode As Boolean
        Get
            Return (_mainRegisters(&H27) And &H8) <> 0
        End Get
    End Property

    Public ReadOnly Property AtClockSelection As Integer
        Get
            Return _mainRegisters(&H25) And &H3
        End Get
    End Property

    Public Function DiagnosticText() As String
        Return "OPTi 82C895 indexed chipset" & Environment.NewLine &
               "  index/data              : 22h / 24h" & Environment.NewLine &
               "  integrated IPC data     : 23h (index 01h=" & _ipcRegisters(&H1).ToString("X2") & "h)" & Environment.NewLine &
               "  companion               : OPTi 82C602 F7h-FAh" & Environment.NewLine &
               "  upper CMOS bank         : " & If(_companion.UpperCmosBankEnabled, "selected", "normal/lower") & Environment.NewLine &
               "  L2 cache register       : " & If(ExternalL2CacheEnabled, "enabled", "disabled") & ", " & L2CacheKilobytes.ToString() & " KB encoding"
    End Function

    Public Sub ValidateResetDefaultsOrThrow()
        Dim expectedInBed As New Dictionary(Of Byte, Byte) From {
            {&H20, &H0}, {&H21, &H0}, {&H22, &HE4}, {&H23, &H0},
            {&H24, &H0}, {&H25, &H7C}, {&H26, &H10}, {&H27, &HDE},
            {&H28, &HF8}, {&H29, &H10}, {&H2A, &HE0}, {&H2B, &H10},
            {&H2D, &HC0}, {&H2E, &H0}, {&H2F, &H0},
            {&HE0, &H0}, {&HE1, &H0}, {&HE2, &H0}, {&HE3, &H0},
            {&HE4, &H0}, {&HE5, &H0}, {&HE6, &H0}, {&HE7, &H0},
            {&HE8, &H8}, {&HE9, &H8}, {&HEA, &H0}, {&HEB, &HFF},
            {&HEC, &H0}, {&HED, &H0}, {&HEE, &H0}, {&HEF, &H0}
        }

        For Each pairInBed As KeyValuePair(Of Byte, Byte) In expectedInBed
            If _mainRegisters(pairInBed.Key) <> pairInBed.Value Then
                Throw New InvalidOperationException("OPTi 82C895 reset mismatch at index " & pairInBed.Key.ToString("X2") & "h.")
            End If
        Next
        If _ipcRegisters(&H1) <> &HC0 Then
            Throw New InvalidOperationException("OPTi 82C895 IPC index 01h reset mismatch.")
        End If
        If _companion.UpperCmosBankEnabled OrElse _companion.GreenPowerPortForDiagnostics <> &H7 Then
            Throw New InvalidOperationException("OPTi 82C602 reset mismatch.")
        End If
    End Sub
End Class
