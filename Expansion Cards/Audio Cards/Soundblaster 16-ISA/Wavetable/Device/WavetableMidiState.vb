Option Strict On
Option Explicit On

Imports System
Imports System.Text

' Device-side MIDI channel state.  This is deliberately renderer-independent.
' It represents the musical state a generic Wave Blaster-compatible daughterboard
' has accepted from its MIDI input; later bricks may snapshot this state/events for
' a host SoundFont renderer without putting host synth objects here.
Public NotInheritable Class WavetableMidiState
    Private Const ChannelCount As Integer = 16
    Private Const NoteCount As Integer = 128

    Private NotInheritable Class ChannelState
        Public ReadOnly Controllers(127) As Byte
        Public ReadOnly KeyDown(127) As Boolean
        Public ReadOnly Sustained(127) As Boolean
        Public ReadOnly Velocity(127) As Byte
        Public ReadOnly PolyPressure(127) As Byte
        Public Program As Byte
        Public ChannelPressure As Byte
        Public PitchBend As Integer
        Public ActiveNotes As Integer

        Public Sub Reset()
            Array.Clear(Controllers, 0, Controllers.Length)
            Array.Clear(KeyDown, 0, KeyDown.Length)
            Array.Clear(Sustained, 0, Sustained.Length)
            Array.Clear(Velocity, 0, Velocity.Length)
            Array.Clear(PolyPressure, 0, PolyPressure.Length)
            Program = 0
            ChannelPressure = 0
            PitchBend = 8192

            ' General MIDI power-on/controller defaults commonly expected by MIDI
            ' receivers.  This is state bookkeeping only; no GM reset SysEx is
            ' invented or injected into the guest stream.
            Controllers(7) = 100   ' channel volume
            Controllers(10) = 64  ' pan center
            Controllers(11) = 127 ' expression
            ActiveNotes = 0
        End Sub
    End Class

    Private ReadOnly _channelsInBed(ChannelCount - 1) As ChannelState
    Private _activeNotesInBed As Integer
    Private _messagesAppliedInBed As ULong
    Private _noteOnMessagesInBed As ULong
    Private _noteOffMessagesInBed As ULong
    Private _controlChangeMessagesInBed As ULong
    Private _programChangeMessagesInBed As ULong
    Private _pitchBendMessagesInBed As ULong
    Private _systemResetMessagesInBed As ULong

    Public Sub New()
        For indexInBed As Integer = 0 To ChannelCount - 1
            _channelsInBed(indexInBed) = New ChannelState()
        Next
        Reset()
    End Sub

    Public ReadOnly Property ActiveNoteCount As Integer
        Get
            Return _activeNotesInBed
        End Get
    End Property

    Public ReadOnly Property MessagesApplied As ULong
        Get
            Return _messagesAppliedInBed
        End Get
    End Property

    Public Sub Reset()
        For Each channelInBed As ChannelState In _channelsInBed
            channelInBed.Reset()
        Next
        _activeNotesInBed = 0
    End Sub

    Public Sub ApplyMessage(messageInBed As WavetableMidiMessage)
        _messagesAppliedInBed += 1UL

        If messageInBed.IsChannelMessage Then
            ApplyChannelMessage(messageInBed)
            Return
        End If

        ' FFh arriving through the MIDI data stream is MIDI System Reset.  It is
        ' separate from an MPU command-port reset and from the physical card reset.
        If messageInBed.Status = &HFF Then
            _systemResetMessagesInBed += 1UL
            Reset()
        End If
    End Sub

    Private Sub ApplyChannelMessage(messageInBed As WavetableMidiMessage)
        Dim channelIndexInBed As Integer = messageInBed.ChannelZeroBased
        If channelIndexInBed < 0 OrElse channelIndexInBed >= ChannelCount Then Return
        Dim channelInBed As ChannelState = _channelsInBed(channelIndexInBed)

        Select Case messageInBed.CommandNibble
            Case &H80
                _noteOffMessagesInBed += 1UL
                ReleaseNote(channelInBed, CInt(messageInBed.Data1 And &H7F))

            Case &H90
                Dim noteInBed As Integer = CInt(messageInBed.Data1 And &H7F)
                Dim velocityInBed As Byte = CByte(messageInBed.Data2 And &H7F)
                If velocityInBed = 0 Then
                    _noteOffMessagesInBed += 1UL
                    ReleaseNote(channelInBed, noteInBed)
                Else
                    _noteOnMessagesInBed += 1UL
                    PressNote(channelInBed, noteInBed, velocityInBed)
                End If

            Case &HA0
                channelInBed.PolyPressure(CInt(messageInBed.Data1 And &H7F)) = CByte(messageInBed.Data2 And &H7F)

            Case &HB0
                _controlChangeMessagesInBed += 1UL
                ApplyControlChange(channelInBed,
                                   CInt(messageInBed.Data1 And &H7F),
                                   CByte(messageInBed.Data2 And &H7F))

            Case &HC0
                _programChangeMessagesInBed += 1UL
                channelInBed.Program = CByte(messageInBed.Data1 And &H7F)

            Case &HD0
                channelInBed.ChannelPressure = CByte(messageInBed.Data1 And &H7F)

            Case &HE0
                _pitchBendMessagesInBed += 1UL
                channelInBed.PitchBend = (CInt(messageInBed.Data2 And &H7F) << 7) Or CInt(messageInBed.Data1 And &H7F)
        End Select
    End Sub

    Private Sub PressNote(channelInBed As ChannelState, noteInBed As Integer, velocityInBed As Byte)
        If noteInBed < 0 OrElse noteInBed >= NoteCount Then Return

        If Not channelInBed.KeyDown(noteInBed) AndAlso Not channelInBed.Sustained(noteInBed) Then
            channelInBed.ActiveNotes += 1
            _activeNotesInBed += 1
        End If

        channelInBed.KeyDown(noteInBed) = True
        channelInBed.Sustained(noteInBed) = False
        channelInBed.Velocity(noteInBed) = velocityInBed
    End Sub

    Private Sub ReleaseNote(channelInBed As ChannelState, noteInBed As Integer)
        If noteInBed < 0 OrElse noteInBed >= NoteCount Then Return
        If Not channelInBed.KeyDown(noteInBed) AndAlso Not channelInBed.Sustained(noteInBed) Then Return

        channelInBed.KeyDown(noteInBed) = False
        If channelInBed.Controllers(64) >= 64 Then
            channelInBed.Sustained(noteInBed) = True
            Return
        End If

        StopNote(channelInBed, noteInBed)
    End Sub

    Private Sub StopNote(channelInBed As ChannelState, noteInBed As Integer)
        Dim wasActiveInBed As Boolean = channelInBed.KeyDown(noteInBed) OrElse channelInBed.Sustained(noteInBed) OrElse channelInBed.Velocity(noteInBed) <> 0
        channelInBed.KeyDown(noteInBed) = False
        channelInBed.Sustained(noteInBed) = False
        channelInBed.Velocity(noteInBed) = 0
        channelInBed.PolyPressure(noteInBed) = 0

        If wasActiveInBed Then
            If channelInBed.ActiveNotes > 0 Then channelInBed.ActiveNotes -= 1
            If _activeNotesInBed > 0 Then _activeNotesInBed -= 1
        End If
    End Sub

    Private Sub ApplyControlChange(channelInBed As ChannelState, controllerInBed As Integer, valueInBed As Byte)
        If controllerInBed < 0 OrElse controllerInBed >= 128 Then Return

        Dim previousSustainInBed As Boolean = channelInBed.Controllers(64) >= 64
        channelInBed.Controllers(controllerInBed) = valueInBed

        Select Case controllerInBed
            Case 64 ' Damper / sustain pedal
                Dim sustainNowInBed As Boolean = valueInBed >= 64
                If previousSustainInBed AndAlso Not sustainNowInBed Then
                    ReleaseSustainedNotes(channelInBed)
                End If

            Case 120 ' All Sound Off -- immediate, independent of sustain
                SilenceChannel(channelInBed)

            Case 121 ' Reset All Controllers
                Dim hadSustainInBed As Boolean = channelInBed.Controllers(64) >= 64
                Dim bankMsbInBed As Byte = channelInBed.Controllers(0)
                Dim bankLsbInBed As Byte = channelInBed.Controllers(32)
                Array.Clear(channelInBed.Controllers, 0, channelInBed.Controllers.Length)
                channelInBed.Controllers(0) = bankMsbInBed
                channelInBed.Controllers(32) = bankLsbInBed
                channelInBed.Controllers(7) = 100
                channelInBed.Controllers(10) = 64
                channelInBed.Controllers(11) = 127
                If hadSustainInBed Then ReleaseSustainedNotes(channelInBed)
                channelInBed.PitchBend = 8192
                channelInBed.ChannelPressure = 0

            Case 123 ' All Notes Off -- release keys; sustain may hold them
                For noteInBed As Integer = 0 To NoteCount - 1
                    If channelInBed.KeyDown(noteInBed) Then ReleaseNote(channelInBed, noteInBed)
                Next
        End Select
    End Sub

    Private Sub ReleaseSustainedNotes(channelInBed As ChannelState)
        For noteInBed As Integer = 0 To NoteCount - 1
            If channelInBed.Sustained(noteInBed) AndAlso Not channelInBed.KeyDown(noteInBed) Then
                StopNote(channelInBed, noteInBed)
            End If
        Next
    End Sub

    Private Sub SilenceChannel(channelInBed As ChannelState)
        For noteInBed As Integer = 0 To NoteCount - 1
            If channelInBed.KeyDown(noteInBed) OrElse channelInBed.Sustained(noteInBed) OrElse channelInBed.Velocity(noteInBed) <> 0 Then
                StopNote(channelInBed, noteInBed)
            End If
        Next
    End Sub

    Public Function DiagnosticText(profileInBed As WavetableProfile) As String
        Dim builderInBed As New StringBuilder()
        builderInBed.Append("    MIDI state           : active notes=").Append(_activeNotesInBed.ToString("N0")).AppendLine()
        builderInBed.Append("    note on / note off   : ").Append(_noteOnMessagesInBed.ToString("N0")).Append(" / ").Append(_noteOffMessagesInBed.ToString("N0")).AppendLine()
        builderInBed.Append("    CC / program / bend  : ").Append(_controlChangeMessagesInBed.ToString("N0")).Append(" / ").Append(_programChangeMessagesInBed.ToString("N0")).Append(" / ").Append(_pitchBendMessagesInBed.ToString("N0")).AppendLine()
        builderInBed.Append("    MIDI system resets   : ").Append(_systemResetMessagesInBed.ToString("N0")).AppendLine()

        For channelIndexInBed As Integer = 0 To ChannelCount - 1
            Dim channelInBed As ChannelState = _channelsInBed(channelIndexInBed)
            Dim isDrumInBed As Boolean = profileInBed IsNot Nothing AndAlso profileInBed.DrumChannelOneBased = channelIndexInBed + 1
            If channelInBed.ActiveNotes = 0 AndAlso channelInBed.Program = 0 AndAlso channelInBed.Controllers(0) = 0 AndAlso channelInBed.Controllers(32) = 0 Then Continue For

            builderInBed.Append("    ch ").Append((channelIndexInBed + 1).ToString("00"))
            If isDrumInBed Then builderInBed.Append(" drums") Else builderInBed.Append("      ")
            builderInBed.Append(" : bank ").Append(channelInBed.Controllers(0).ToString()).Append(":").Append(channelInBed.Controllers(32).ToString())
            builderInBed.Append(" prog ").Append(channelInBed.Program.ToString())
            builderInBed.Append(" notes ").Append(channelInBed.ActiveNotes.ToString())
            builderInBed.Append(" vol/exp/pan ").Append(channelInBed.Controllers(7).ToString()).Append("/").Append(channelInBed.Controllers(11).ToString()).Append("/").Append(channelInBed.Controllers(10).ToString())
            builderInBed.Append(" bend ").Append(channelInBed.PitchBend.ToString()).AppendLine()
        Next

        Return builderInBed.ToString().TrimEnd(ControlChars.Cr, ControlChars.Lf)
    End Function
End Class
