Option Strict On
Option Explicit On

Imports System

' Physical/logical seam between a sound card's guest-visible MIDI transmitter and
' an installed Wave Blaster-compatible daughterboard.
'
' The connector knows nothing about SoundFont files, renderer objects, WinForms,
' or host audio.  Other wavetable-capable cards may reuse this class later by
' feeding it bytes from their own authentic MIDI transmit path.
Public NotInheritable Class WaveBlasterConnector
    Private _daughterboardInBed As WaveBlasterDaughterboard
    Private _bytesForwardedInBed As ULong
    Private _bytesWithNoBoardInBed As ULong

    Public ReadOnly Property IsAttached As Boolean
        Get
            Return _daughterboardInBed IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property Daughterboard As WaveBlasterDaughterboard
        Get
            Return _daughterboardInBed
        End Get
    End Property

    Public Sub Attach(profileInBed As WavetableProfile,
                      Optional renderSinkInBed As IWavetableRenderEventSink = Nothing)
        If profileInBed Is Nothing Then Throw New ArgumentNullException(NameOf(profileInBed))
        _daughterboardInBed = New WaveBlasterDaughterboard(profileInBed, renderSinkInBed)
    End Sub

    Public Sub Detach()
        _daughterboardInBed = Nothing
    End Sub

    ' Called only after a byte has genuinely passed through the owning card's
    ' guest-visible MIDI UART path.
    Public Sub TransmitMidiByte(valueInBed As Byte)
        Dim boardInBed As WaveBlasterDaughterboard = _daughterboardInBed
        If boardInBed Is Nothing Then
            _bytesWithNoBoardInBed += 1UL
            Return
        End If

        _bytesForwardedInBed += 1UL
        boardInBed.ReceiveMidiByte(valueInBed)
    End Sub

    Public Sub HardwareReset()
        If _daughterboardInBed IsNot Nothing Then _daughterboardInBed.HardwareReset()
    End Sub

    Public Function DiagnosticText() As String
        Dim stateInBed As String = If(IsAttached, "attached", "empty connector")
        Dim textInBed As String = "  Wave Blaster connector: " & stateInBed & Environment.NewLine &
                                  "    MIDI bytes forwarded : " & _bytesForwardedInBed.ToString("N0") & Environment.NewLine &
                                  "    bytes with no board  : " & _bytesWithNoBoardInBed.ToString("N0")
        If _daughterboardInBed IsNot Nothing Then
            textInBed &= Environment.NewLine & _daughterboardInBed.DiagnosticText()
        End If
        Return textInBed
    End Function
End Class
