Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Text

' Bounded diagnostic recorder for decoded daughterboard MIDI messages.
' It stores only immutable message values and counters.  It performs no guest bus
' access and has no influence on MIDI parsing/state or future audio rendering.
Public NotInheritable Class WavetableDiagnostics
    Private Const RecentMessageCapacity As Integer = 24

    Private ReadOnly _recentMessagesInBed As New Queue(Of WavetableMidiMessage)(RecentMessageCapacity)
    Private _channelMessagesInBed As ULong
    Private _systemMessagesInBed As ULong

    Public Sub ResetRecent()
        _recentMessagesInBed.Clear()
    End Sub

    Public Sub RecordMessage(messageInBed As WavetableMidiMessage)
        If Not RuntimeDiagnostics.Enabled Then Return

        If messageInBed.IsChannelMessage Then
            _channelMessagesInBed += 1UL
        Else
            _systemMessagesInBed += 1UL
        End If

        If _recentMessagesInBed.Count >= RecentMessageCapacity Then _recentMessagesInBed.Dequeue()
        _recentMessagesInBed.Enqueue(messageInBed)
    End Sub

    Public Function DiagnosticText() As String
        Dim recentInBed As String = "(none)"
        If _recentMessagesInBed.Count > 0 Then
            Dim builderInBed As New StringBuilder()
            For Each messageInBed As WavetableMidiMessage In _recentMessagesInBed
                If builderInBed.Length > 0 Then builderInBed.Append(" | ")
                builderInBed.Append(messageInBed.Summary())
            Next
            recentInBed = builderInBed.ToString()
        End If

        Return "    channel/system msgs  : " & _channelMessagesInBed.ToString("N0") & " / " & _systemMessagesInBed.ToString("N0") & Environment.NewLine &
               "    recent MIDI messages : " & recentInBed
    End Function
End Class
