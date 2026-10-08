Imports System
Imports System.Collections.Generic

' Headland HT12/A reusable silicon worker.
'
' This file intentionally implements only behavior that is presently supported by
' extracted Headland/HT12 evidence.  Unknown register bits remain stored/visible
' without invented side effects.  The FOX motherboard owns straps and wiring.
Public NotInheritable Class Ht12PowerOnStraps
    Public Property RamConfigurationCode As Byte
    Public Property AtBusLowSpeed As Boolean
    Public Property ZeroWaitMainMemory As Boolean
    Public Property ExtraWaitStateMode As Byte
    Public Property TcStrapHigh As Boolean

    Public Function SystemConfigurationValue() As Byte
        Dim valueInBed As Integer = RamConfigurationCode And &H7
        If AtBusLowSpeed Then valueInBed = valueInBed Or &H8
        If ZeroWaitMainMemory Then valueInBed = valueInBed Or &H10
        valueInBed = valueInBed Or ((CInt(ExtraWaitStateMode) And &H3) << 5)
        If TcStrapHigh Then valueInBed = valueInBed Or &H80
        Return CByte(valueInBed And &HFF)
    End Function
End Class

Public NotInheritable Class Ht12ConfigurationWorker
    Private ReadOnly _registersInBed(255) As Byte
    Private _indexInBed As Byte

    Public Event RegisterWritten(indexInBed As Byte, oldValueInBed As Byte, newValueInBed As Byte)

    Public ReadOnly Property SelectedIndex As Byte
        Get
            Return _indexInBed
        End Get
    End Property

    Public Sub Reset(strapsInBed As Ht12PowerOnStraps)
        Array.Clear(_registersInBed, 0, _registersInBed.Length)
        _indexInBed = 0
        If strapsInBed IsNot Nothing Then _registersInBed(&H10) = strapsInBed.SystemConfigurationValue()
        ' HT12 datasheet reset defaults: Quiet Bus enabled and 40000h-9FFFFh
        ' motherboard RAM enabled in Misc Feature Register 14h.
        _registersInBed(&H14) = &H9
        ' Headland documents 3Fh as the reset/default Top-of-Extended-Memory value.
        _registersInBed(&H18) = &H3F
    End Sub

    Public Sub SelectIndex(valueInBed As Byte)
        _indexInBed = valueInBed
    End Sub

    Public Function ReadSelected() As Byte
        Return _registersInBed(_indexInBed)
    End Function

    Public Sub WriteSelected(valueInBed As Byte)
        Dim indexValueInBed As Byte = _indexInBed
        ' 15h-17h are status/information registers in the documented map.  Until
        ' their write behavior is sourced, do not allow firmware to manufacture
        ' state by writing them.
        If indexValueInBed >= &H15 AndAlso indexValueInBed <= &H17 Then Return
        If Not IsKnownIndexedRegister(indexValueInBed) Then Return

        Dim oldValueInBed As Byte = _registersInBed(indexValueInBed)
        If oldValueInBed = valueInBed Then Return
        _registersInBed(indexValueInBed) = valueInBed
        RaiseEvent RegisterWritten(indexValueInBed, oldValueInBed, valueInBed)
    End Sub

    Public Function ReadRegister(indexInBed As Byte) As Byte
        Return _registersInBed(indexInBed)
    End Function

    Public Shared Function IsKnownIndexedRegister(indexInBed As Byte) As Boolean
        Return indexInBed = &H10 OrElse
               (indexInBed >= &H12 AndAlso indexInBed <= &H19) OrElse
               (indexInBed >= &H20 AndAlso indexInBed <= &H23)
    End Function
End Class

' Hardware EMS address translator.  Four 16 KiB windows are selected by 19h and
' translated through page registers 20h-23h.  It contains no backing RAM; the
' motherboard memory worker remains the owner of physical DRAM.
Public NotInheritable Class Ht12EmsWorker
    Private ReadOnly _configurationInBed As Ht12ConfigurationWorker

    Public Sub New(configurationInBed As Ht12ConfigurationWorker)
        If configurationInBed Is Nothing Then Throw New ArgumentNullException(NameOf(configurationInBed))
        _configurationInBed = configurationInBed
    End Sub

    Public Function TryTranslate(cpuAddressInBed As UInteger, ByRef physicalAddressInBed As UInteger) As Boolean
        Dim emsConfigInBed As Byte = _configurationInBed.ReadRegister(&H19)
        If (emsConfigInBed And &H80) = 0 Then Return False

        Dim frameSelectorInBed As Integer = (emsConfigInBed >> 4) And &H7
        Dim frameBaseInBed As UInteger = &HC0000UI + CUInt(frameSelectorInBed * &H4000)
        If cpuAddressInBed < frameBaseInBed OrElse cpuAddressInBed >= frameBaseInBed + &H10000UI Then Return False

        Dim pageIndexInBed As Integer = CInt((cpuAddressInBed - frameBaseInBed) \ &H4000UI)
        If pageIndexInBed < 0 OrElse pageIndexInBed > 3 Then Return False
        If (emsConfigInBed And (1 << pageIndexInBed)) = 0 Then Return False

        Dim pageRegisterInBed As Byte = _configurationInBed.ReadRegister(CByte(&H20 + pageIndexInBed))
        physicalAddressInBed = (CUInt(pageRegisterInBed) << 14) Or (cpuAddressInBed And &H3FFFUI)
        Return True
    End Function
End Class

' Headland HT12 Port 92h worker.  The primary datasheet defines only bits 0/1:
'   bit 0 = alternate hot CPU reset (latched until BIOS clears it)
'   bit 1 = alternate Gate A20 (1 = active)
'   bits 2-7 are reserved, read as zero.
' Reset timing belongs to the board/processor binding; this worker publishes the
' sourced request edge and timing constants instead of performing a host reset.
Public NotInheritable Class Ht12Port92Worker
    Public Const HotResetMask As Byte = &H1
    Public Const GateA20Mask As Byte = &H2
    Public Const HotResetMinimumDelayPicoseconds As Long = 6720000L
    Public Const HotResetOperationPicoseconds As Long = 13400000L

    Private _controlInBed As Byte

    Public Event GateA20Changed(enabledInBed As Boolean)
    Public Event HotResetRequested()

    Public Sub Reset()
        _controlInBed = 0
    End Sub

    Public ReadOnly Property GateA20Active As Boolean
        Get
            Return (_controlInBed And GateA20Mask) <> 0
        End Get
    End Property

    Public ReadOnly Property HotResetLatched As Boolean
        Get
            Return (_controlInBed And HotResetMask) <> 0
        End Get
    End Property

    Public Function ReadValue() As Byte
        Return CByte(_controlInBed And (HotResetMask Or GateA20Mask))
    End Function

    Public Sub WriteValue(valueInBed As Byte)
        Dim oldInBed As Byte = _controlInBed
        Dim nextInBed As Byte = CByte(valueInBed And (HotResetMask Or GateA20Mask))
        _controlInBed = nextInBed

        If ((oldInBed Xor nextInBed) And GateA20Mask) <> 0 Then
            RaiseEvent GateA20Changed((nextInBed And GateA20Mask) <> 0)
        End If

        ' The HT12 latch remains set after the pulse until software clears it.
        ' Only the 0->1 request edge starts a new alternate reset operation.
        If (oldInBed And HotResetMask) = 0 AndAlso (nextInBed And HotResetMask) <> 0 Then
            RaiseEvent HotResetRequested()
        End If
    End Sub
End Class

Public NotInheritable Class HeadlandHt12
    Implements IPortDevice, IResettableDevice, IMotherboardLocalPortDevice

    Public Const ConfigurationIndexPort As UInt16 = &H1EDUS
    Public Const ConfigurationDataPort As UInt16 = &H1EFUS
    Public Const Port92 As UInt16 = &H92US

    Private ReadOnly _strapsInBed As Ht12PowerOnStraps
    Private ReadOnly _configurationInBed As Ht12ConfigurationWorker
    Private ReadOnly _emsInBed As Ht12EmsWorker
    Private ReadOnly _port92InBed As Ht12Port92Worker

    Public Event ConfigurationChanged(indexInBed As Byte, oldValueInBed As Byte, newValueInBed As Byte)
    Public Event FastA20GateChanged(enabledInBed As Boolean)
    Public Event AlternateCpuResetRequested()

    Public Sub New(strapsInBed As Ht12PowerOnStraps)
        _strapsInBed = If(strapsInBed, New Ht12PowerOnStraps())
        _configurationInBed = New Ht12ConfigurationWorker()
        _emsInBed = New Ht12EmsWorker(_configurationInBed)
        _port92InBed = New Ht12Port92Worker()
        AddHandler _configurationInBed.RegisterWritten, AddressOf ConfigurationRegisterWrittenInBed
        AddHandler _port92InBed.GateA20Changed, AddressOf Port92A20ChangedInBed
        AddHandler _port92InBed.HotResetRequested, AddressOf Port92HotResetRequestedInBed
        ResetDevice()
    End Sub

    Public ReadOnly Property Identity As String
        Get
            Return "Headland Technology HT12/A"
        End Get
    End Property

    Public ReadOnly Property Configuration As Ht12ConfigurationWorker
        Get
            Return _configurationInBed
        End Get
    End Property

    Public ReadOnly Property Ems As Ht12EmsWorker
        Get
            Return _emsInBed
        End Get
    End Property

    Public ReadOnly Property Port92Control As Ht12Port92Worker
        Get
            Return _port92InBed
        End Get
    End Property

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        _configurationInBed.Reset(_strapsInBed)
        _port92InBed.Reset()
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port = ConfigurationIndexPort OrElse port = ConfigurationDataPort OrElse port = Port92
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Select Case port
            Case ConfigurationIndexPort
                Return _configurationInBed.SelectedIndex
            Case ConfigurationDataPort
                Return _configurationInBed.ReadSelected()
            Case Port92
                Return _port92InBed.ReadValue()
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Select Case port
            Case ConfigurationIndexPort
                _configurationInBed.SelectIndex(value)
            Case ConfigurationDataPort
                _configurationInBed.WriteSelected(value)
            Case Port92
                _port92InBed.WriteValue(value)
        End Select
    End Sub

    Public Function TryTranslateEmsAddress(cpuAddressInBed As UInteger, ByRef physicalAddressInBed As UInteger) As Boolean
        Return _emsInBed.TryTranslate(cpuAddressInBed, physicalAddressInBed)
    End Function

    Private Sub ConfigurationRegisterWrittenInBed(indexInBed As Byte, oldValueInBed As Byte, newValueInBed As Byte)
        RaiseEvent ConfigurationChanged(indexInBed, oldValueInBed, newValueInBed)
    End Sub

    Private Sub Port92A20ChangedInBed(enabledInBed As Boolean)
        RaiseEvent FastA20GateChanged(enabledInBed)
    End Sub

    Private Sub Port92HotResetRequestedInBed()
        RaiseEvent AlternateCpuResetRequested()
    End Sub

    Public Function DiagnosticText() As String
        Return "Headland HT12/A" & Environment.NewLine &
               "  config index/data        : 01EDh / 01EFh" & Environment.NewLine &
               "  selected index           : " & _configurationInBed.SelectedIndex.ToString("X2") & "h" & Environment.NewLine &
               "  system configuration 10h : " & _configurationInBed.ReadRegister(&H10).ToString("X2") & "h" & Environment.NewLine &
               "  top ext memory 18h       : " & _configurationInBed.ReadRegister(&H18).ToString("X2") & "h" & Environment.NewLine &
               "  EMS configuration 19h    : " & _configurationInBed.ReadRegister(&H19).ToString("X2") & "h" & Environment.NewLine &
               "  port 92 control          : " & _port92InBed.ReadValue().ToString("X2") & "h" & Environment.NewLine &
               "  fast A20 / hot reset     : " & If(_port92InBed.GateA20Active, "1", "0") & " / " & If(_port92InBed.HotResetLatched, "1", "0")
    End Function
End Class
