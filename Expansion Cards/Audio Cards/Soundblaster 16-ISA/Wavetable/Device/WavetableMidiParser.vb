Option Strict On
Option Explicit On

Imports System
Imports System.Text

' Immutable decoded MIDI message produced by WavetableMidiParser.
' Brick 4 keeps this on the emulated-device side.  A later host-render boundary may
' wrap/copy these messages with machine-time metadata; do not put host renderer
' objects or filesystem state in this structure.
Public Structure WavetableMidiMessage
    Public Sub New(statusInBed As Byte,
                   dataLengthInBed As Integer,
                   data1InBed As Byte,
                   data2InBed As Byte)
        Status = statusInBed
        DataLength = Math.Max(0, Math.Min(2, dataLengthInBed))
        Data1 = data1InBed
        Data2 = data2InBed
    End Sub

    Public ReadOnly Property Status As Byte
    Public ReadOnly Property DataLength As Integer
    Public ReadOnly Property Data1 As Byte
    Public ReadOnly Property Data2 As Byte

    Public ReadOnly Property IsChannelMessage As Boolean
        Get
            Return Status >= &H80 AndAlso Status <= &HEF
        End Get
    End Property

    Public ReadOnly Property ChannelZeroBased As Integer
        Get
            If Not IsChannelMessage Then Return -1
            Return CInt(Status And &HF)
        End Get
    End Property

    Public ReadOnly Property CommandNibble As Byte
        Get
            Return CByte(Status And &HF0)
        End Get
    End Property

    Public Function Summary() As String
        Dim builderInBed As New StringBuilder()
        builderInBed.Append(Status.ToString("X2"))
        If DataLength >= 1 Then builderInBed.Append(" ").Append(Data1.ToString("X2"))
        If DataLength >= 2 Then builderInBed.Append(" ").Append(Data2.ToString("X2"))
        Return builderInBed.ToString()
    End Function
End Structure

' MIDI 1.0 byte-stream parser for the daughterboard input connector.
'
' Important stream rules modeled here:
' - channel voice running status is retained across channel messages;
' - system-common status cancels running status;
' - realtime bytes F8-FF may appear between any two bytes and do not disturb the
'   message currently being assembled;
' - SysEx payload is framed/countable but deliberately not interpreted in Brick 4.
'
' The parser never throws for malformed guest MIDI.  Bad/stray input is counted and
' discarded so a guest cannot kill the machine thread with a broken byte stream.
Public NotInheritable Class WavetableMidiParser
    Private _runningStatusInBed As Byte
    Private _currentStatusInBed As Byte
    Private _expectedDataBytesInBed As Integer
    Private _dataBytesReceivedInBed As Integer
    Private _data1InBed As Byte
    Private _data2InBed As Byte
    Private _inSystemExclusiveInBed As Boolean
    Private _currentSystemExclusiveBytesInBed As ULong

    Private _decodedMessagesInBed As ULong
    Private _realtimeMessagesInBed As ULong
    Private _systemExclusiveMessagesInBed As ULong
    Private _systemExclusivePayloadBytesInBed As ULong
    Private _strayDataBytesInBed As ULong
    Private _malformedStatusBytesInBed As ULong

    Public ReadOnly Property InSystemExclusive As Boolean
        Get
            Return _inSystemExclusiveInBed
        End Get
    End Property

    Public ReadOnly Property DecodedMessages As ULong
        Get
            Return _decodedMessagesInBed
        End Get
    End Property

    Public ReadOnly Property StrayDataBytes As ULong
        Get
            Return _strayDataBytesInBed
        End Get
    End Property

    Public Sub Reset()
        _runningStatusInBed = 0
        _currentStatusInBed = 0
        _expectedDataBytesInBed = 0
        _dataBytesReceivedInBed = 0
        _data1InBed = 0
        _data2InBed = 0
        _inSystemExclusiveInBed = False
        _currentSystemExclusiveBytesInBed = 0UL
    End Sub

    ' Returns a decoded message only when this byte completes one.  SysEx content is
    ' consumed and counted internally in Brick 4; it is not forwarded as invented
    ' GM/GS/XG behavior.
    Public Function FeedByte(valueInBed As Byte) As Nullable(Of WavetableMidiMessage)
        ' MIDI realtime status may be interleaved anywhere, including inside SysEx
        ' and between the data bytes of a channel message.  It does not cancel
        ' running status or the partially assembled message.
        If valueInBed >= &HF8 Then
            If RuntimeDiagnostics.Enabled Then
                _decodedMessagesInBed += 1UL
                _realtimeMessagesInBed += 1UL
            End If
            Return New WavetableMidiMessage(valueInBed, 0, 0, 0)
        End If

        If _inSystemExclusiveInBed Then
            If valueInBed = &HF7 Then
                _inSystemExclusiveInBed = False
                If RuntimeDiagnostics.Enabled Then
                    _systemExclusiveMessagesInBed += 1UL
                    _systemExclusivePayloadBytesInBed += _currentSystemExclusiveBytesInBed
                End If
                _currentSystemExclusiveBytesInBed = 0UL
                _currentStatusInBed = 0
                _expectedDataBytesInBed = 0
                _dataBytesReceivedInBed = 0
                Return Nothing
            End If

            If valueInBed < &H80 Then
                If RuntimeDiagnostics.Enabled Then _currentSystemExclusiveBytesInBed += 1UL
                Return Nothing
            End If

            ' Any non-realtime status other than EOX inside SysEx is malformed.
            ' Abort the frame and then interpret the new status normally so the
            ' parser can recover without losing the rest of the stream.
            If RuntimeDiagnostics.Enabled Then _malformedStatusBytesInBed += 1UL
            _inSystemExclusiveInBed = False
            _currentSystemExclusiveBytesInBed = 0UL
        End If

        If valueInBed >= &H80 Then
            Return BeginStatus(valueInBed)
        End If

        If _currentStatusInBed = 0 Then
            If _runningStatusInBed = 0 Then
                If RuntimeDiagnostics.Enabled Then _strayDataBytesInBed += 1UL
                Return Nothing
            End If

            _currentStatusInBed = _runningStatusInBed
            _expectedDataBytesInBed = DataLengthForStatus(_runningStatusInBed)
            _dataBytesReceivedInBed = 0
        End If

        If _expectedDataBytesInBed <= 0 Then
            If RuntimeDiagnostics.Enabled Then _strayDataBytesInBed += 1UL
            _currentStatusInBed = 0
            Return Nothing
        End If

        If _dataBytesReceivedInBed = 0 Then
            _data1InBed = valueInBed
        Else
            _data2InBed = valueInBed
        End If
        _dataBytesReceivedInBed += 1

        If _dataBytesReceivedInBed < _expectedDataBytesInBed Then Return Nothing

        Dim completedStatusInBed As Byte = _currentStatusInBed
        Dim completedLengthInBed As Integer = _expectedDataBytesInBed
        Dim completedInBed As New WavetableMidiMessage(completedStatusInBed,
                                                       completedLengthInBed,
                                                       _data1InBed,
                                                       _data2InBed)
        _decodedMessagesInBed += 1UL

        If completedStatusInBed >= &H80 AndAlso completedStatusInBed <= &HEF Then
            ' Running status remains active.  Leave the channel status armed for the
            ' next data byte pair/single without requiring a repeated status byte.
            _currentStatusInBed = _runningStatusInBed
            _expectedDataBytesInBed = DataLengthForStatus(_runningStatusInBed)
        Else
            _currentStatusInBed = 0
            _expectedDataBytesInBed = 0
        End If
        _dataBytesReceivedInBed = 0
        _data1InBed = 0
        _data2InBed = 0

        Return completedInBed
    End Function

    Private Function BeginStatus(statusInBed As Byte) As Nullable(Of WavetableMidiMessage)
        _dataBytesReceivedInBed = 0
        _data1InBed = 0
        _data2InBed = 0

        If statusInBed >= &H80 AndAlso statusInBed <= &HEF Then
            _runningStatusInBed = statusInBed
            _currentStatusInBed = statusInBed
            _expectedDataBytesInBed = DataLengthForStatus(statusInBed)
            Return Nothing
        End If

        ' System common cancels running status.  Realtime was already handled above.
        _runningStatusInBed = 0
        _currentStatusInBed = 0
        _expectedDataBytesInBed = 0

        Select Case statusInBed
            Case &HF0 ' System Exclusive start
                _inSystemExclusiveInBed = True
                _currentSystemExclusiveBytesInBed = 0UL
                Return Nothing

            Case &HF1, &HF3 ' MTC quarter-frame / Song Select
                _currentStatusInBed = statusInBed
                _expectedDataBytesInBed = 1
                Return Nothing

            Case &HF2 ' Song Position Pointer
                _currentStatusInBed = statusInBed
                _expectedDataBytesInBed = 2
                Return Nothing

            Case &HF6, &HF7 ' Tune Request / EOX outside a SysEx frame
                If RuntimeDiagnostics.Enabled Then _decodedMessagesInBed += 1UL
                Return New WavetableMidiMessage(statusInBed, 0, 0, 0)

            Case &HF4, &HF5
                ' Undefined system-common status values.  Consume them as standalone
                ' statuses, count the malformed/unsupported condition, and recover.
                If RuntimeDiagnostics.Enabled Then _malformedStatusBytesInBed += 1UL
                Return Nothing

            Case Else
                If RuntimeDiagnostics.Enabled Then _malformedStatusBytesInBed += 1UL
                Return Nothing
        End Select
    End Function

    Private Shared Function DataLengthForStatus(statusInBed As Byte) As Integer
        If statusInBed >= &H80 AndAlso statusInBed <= &HEF Then
            Select Case statusInBed And &HF0
                Case &HC0, &HD0
                    Return 1
                Case Else
                    Return 2
            End Select
        End If

        Select Case statusInBed
            Case &HF1, &HF3
                Return 1
            Case &HF2
                Return 2
            Case Else
                Return 0
        End Select
    End Function

    Public Function DiagnosticText() As String
        Return "    MIDI parser          : " & If(_inSystemExclusiveInBed, "inside SysEx", "idle") & Environment.NewLine &
               "    decoded messages     : " & _decodedMessagesInBed.ToString("N0") & Environment.NewLine &
               "    realtime messages    : " & _realtimeMessagesInBed.ToString("N0") & Environment.NewLine &
               "    SysEx frames/bytes   : " & _systemExclusiveMessagesInBed.ToString("N0") & " / " & _systemExclusivePayloadBytesInBed.ToString("N0") & Environment.NewLine &
               "    stray data bytes     : " & _strayDataBytesInBed.ToString("N0") & Environment.NewLine &
               "    malformed statuses   : " & _malformedStatusBytesInBed.ToString("N0")
    End Function
End Class
