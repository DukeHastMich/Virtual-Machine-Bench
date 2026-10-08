Option Strict On
Option Explicit On

' Immutable handoff object crossing from guest-visible daughterboard state to the
' host renderer queue.  Brick 6 preserves its ordering by serializing these events
' with coordinated machine-time RenderFrames commands on the same host FIFO.  Host
' Stopwatch/DateTime arrival is never used as the guest timeline.
Public Enum WavetableRenderEventKind As Byte
    MidiMessage = 0
    HardwareReset = 1
End Enum

Public Structure WavetableRenderEvent
    Public Sub New(kindInBed As WavetableRenderEventKind,
                   sequenceInBed As ULong,
                   messageInBed As WavetableMidiMessage)
        Kind = kindInBed
        Sequence = sequenceInBed
        MidiMessage = messageInBed
    End Sub

    Public ReadOnly Property Kind As WavetableRenderEventKind
    Public ReadOnly Property Sequence As ULong
    Public ReadOnly Property MidiMessage As WavetableMidiMessage
End Structure

' The device side depends only on this non-blocking sink contract.  It knows
' nothing about SoundFonts, MeltySynth, threads, files, or audio output.
Public Interface IWavetableRenderEventSink
    Function TryPostEvent(eventInBed As WavetableRenderEvent) As Boolean
End Interface
