Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Threading

Friend Enum WavetableHostCommandKind As Byte
    RenderEvent = 0
    RenderFrames = 1
End Enum

' One queue serializes guest-derived MIDI events and machine-time PCM render requests.
' This is important: a note arriving before a RenderFrames command is applied before
' those samples are generated, while a note arriving after it cannot leak backward
' into already-rendered PCM.
Friend Structure WavetableHostCommand
    Public Sub New(eventInBed As WavetableRenderEvent)
        Kind = WavetableHostCommandKind.RenderEvent
        RenderEvent = eventInBed
        FrameCount = 0
    End Sub

    Public Sub New(frameCountInBed As Integer)
        Kind = WavetableHostCommandKind.RenderFrames
        RenderEvent = New WavetableRenderEvent()
        FrameCount = frameCountInBed
    End Sub

    Public ReadOnly Property Kind As WavetableHostCommandKind
    Public ReadOnly Property RenderEvent As WavetableRenderEvent
    Public ReadOnly Property FrameCount As Integer
End Structure

' Host-side lifetime, SoundFont loading, event ordering and PCM generation for a
' wavetable attachment.  All MeltySynth calls live on _workerInBed because the
' renderer is explicitly not thread-safe.
'
' The machine owner never waits for this worker.  It only posts bounded commands and
' consumes already-rendered PCM from a single-producer/single-consumer ring.  If the
' host cannot keep up, wavetable audio underruns/drops while guest MPU/IRQ/DMA timing
' continues normally.
Public NotInheritable Class WavetableHost
    Implements IWavetableRenderEventSink, IDisposable

    Private Const CommandQueueCapacity As Integer = 4096
    Private Const RenderWorkBlockFrames As Integer = 512
    Private Const MaxOutstandingRenderFrames As Long = 16384L
    Private Const PcmRingCapacity As Integer = 32768
    Private Const PcmRingMask As Long = PcmRingCapacity - 1L

    Private ReadOnly _settingsInBed As WavetableAttachmentSettings
    Private ReadOnly _bankDirectoryInBed As String
    Private ReadOnly _commandsInBed As New BlockingCollection(Of WavetableHostCommand)(CommandQueueCapacity)
    Private ReadOnly _statusSyncInBed As New Object()
    Private ReadOnly _workerInBed As Thread

    ' SPSC PCM ring.  The wavetable worker is the only producer; the machine/audio
    ' side is the only consumer.  Positions are monotonically increasing so empty and
    ' full are unambiguous without locks or per-sample allocations.
    Private ReadOnly _pcmLeftInBed(PcmRingCapacity - 1) As Single
    Private ReadOnly _pcmRightInBed(PcmRingCapacity - 1) As Single
    Private _pcmReadPositionInBed As Long
    Private _pcmWritePositionInBed As Long
    Private _pcmDiscardBeforePositionInBed As Long

    Private _rendererInBed As IWavetableRenderer
    Private _resolutionInBed As SoundFontBankResolution
    Private _stateTextInBed As String = "starting"
    Private _lastErrorInBed As String = String.Empty
    Private _disposedInBed As Integer
    Private _rendererReadyInBed As Integer

    ' Worker-owned synth state mirrored through Volatile/Interlocked only.
    ' MeltySynth itself is never queried from another thread.
    Private _activeRendererVoicesInBed As Integer
    Private _pendingRendererEventsInBed As Integer

    Private _eventsPostedInBed As Long
    Private _eventsDroppedInBed As Long
    Private _eventsProcessedInBed As Long
    Private _eventsIgnoredWithoutRendererInBed As Long
    Private _lastSequenceInBed As Long

    Private _renderRequestsPostedInBed As Long
    Private _renderRequestsDroppedInBed As Long
    Private _renderFramesRequestedInBed As Long
    Private _renderFramesProducedInBed As Long
    Private _renderFramesDroppedInBed As Long
    Private _renderFramesOutstandingInBed As Long
    Private _pcmFramesConsumedInBed As Long
    Private _pcmUnderrunsInBed As Long
    Private _pcmPeakQueuedInBed As Long

    Public Sub New(settingsInBed As WavetableAttachmentSettings)
        Me.New(settingsInBed, SoundFontCatalog.ResolveSharedBanksDirectory())
    End Sub

    Friend Sub New(settingsInBed As WavetableAttachmentSettings,
                   bankDirectoryInBed As String)
        _settingsInBed = If(settingsInBed, New WavetableAttachmentSettings()).CloneSettings()
        _bankDirectoryInBed = Path.GetFullPath(bankDirectoryInBed)

        _workerInBed = New Thread(AddressOf WorkerLoopInBed) With {
            .IsBackground = True,
            .Name = "Virtual Machine Bench wavetable host",
            .Priority = ThreadPriority.AboveNormal
        }
        _workerInBed.Start()
    End Sub

    ' Thread-safe conservative indication used only by the host audio presenter.
    ' True means the renderer has voices still evolving OR MIDI/reset work has
    ' crossed the guest/device boundary but has not yet been processed.
    '
    ' This is intentionally NOT based on the daughterboard's NoteOn bookkeeping:
    ' SoundFont voices can outlive NoteOff during release, percussion can be
    ' one-shot, and effects can continue after note-state changes.
    Public ReadOnly Property MayStillProduceAudio As Boolean
        Get
            If Volatile.Read(_disposedInBed) <> 0 Then Return False

            ' Work already accepted at the device/host boundary remains capable
            ' of producing future PCM even while a large SoundFont is still
            ' loading or the renderer worker is temporarily behind.
            If Volatile.Read(_pendingRendererEventsInBed) > 0 OrElse
               Interlocked.Read(_renderFramesOutstandingInBed) > 0 Then
                Return True
            End If

            If Volatile.Read(_rendererReadyInBed) = 0 Then Return False
            Return Volatile.Read(_activeRendererVoicesInBed) > 0
        End Get
    End Property

    ' Guest/MIDI device boundary.  TryAdd with zero wait is intentional: a busy host
    ' synth is allowed to lose renderer events and report it; it is never allowed to
    ' stall the guest's MPU UART transaction.
    Public Function TryPostEvent(eventInBed As WavetableRenderEvent) As Boolean Implements IWavetableRenderEventSink.TryPostEvent
        If Volatile.Read(_disposedInBed) <> 0 Then Return False
        Try
            If _commandsInBed.TryAdd(New WavetableHostCommand(eventInBed), 0) Then
                Interlocked.Increment(_pendingRendererEventsInBed)
                If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_eventsPostedInBed)
                Return True
            End If
        Catch ex As InvalidOperationException
            Return False
        End Try
        If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_eventsDroppedInBed)
        Return False
    End Function

    ' Called from the owning sound card's coordinated AdvanceTime path.  This posts
    ' elapsed guest-time audio work AFTER all MIDI events already sent for that time
    ' interval.  It never waits for MeltySynth.
    Public Function RequestPcmFrames(frameCountInBed As Long) As Boolean
        If frameCountInBed <= 0 Then Return True
        If Volatile.Read(_disposedInBed) <> 0 OrElse Volatile.Read(_rendererReadyInBed) = 0 Then
            If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_renderRequestsDroppedInBed)
            Return False
        End If

        Dim outstandingInBed As Long = Interlocked.Read(_renderFramesOutstandingInBed)
        Dim roomInBed As Long = MaxOutstandingRenderFrames - outstandingInBed
        If roomInBed <= 0 Then
            If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_renderRequestsDroppedInBed)
            If RuntimeDiagnostics.Enabled Then Interlocked.Add(_renderFramesDroppedInBed, frameCountInBed)
            Return False
        End If

        Dim acceptedFramesInBed As Long = Math.Min(frameCountInBed, roomInBed)
        If acceptedFramesInBed > Integer.MaxValue Then acceptedFramesInBed = Integer.MaxValue

        Interlocked.Add(_renderFramesOutstandingInBed, acceptedFramesInBed)
        Try
            If _commandsInBed.TryAdd(New WavetableHostCommand(CInt(acceptedFramesInBed)), 0) Then
                If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_renderRequestsPostedInBed)
                If RuntimeDiagnostics.Enabled Then Interlocked.Add(_renderFramesRequestedInBed, acceptedFramesInBed)
                If acceptedFramesInBed < frameCountInBed Then
                    If RuntimeDiagnostics.Enabled Then Interlocked.Add(_renderFramesDroppedInBed, frameCountInBed - acceptedFramesInBed)
                End If
                Return acceptedFramesInBed = frameCountInBed
            End If
        Catch ex As InvalidOperationException
        End Try

        Interlocked.Add(_renderFramesOutstandingInBed, -acceptedFramesInBed)
        If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_renderRequestsDroppedInBed)
        If RuntimeDiagnostics.Enabled Then Interlocked.Add(_renderFramesDroppedInBed, frameCountInBed)
        Return False
    End Function

    ' Machine/audio-side nonblocking sample pull.  Empty means silence for this host
    ' frame; it does not affect any guest-visible card state.
    Public Function TryReadPcmSample(ByRef leftInBed As Single,
                                     ByRef rightInBed As Single) As Boolean
        leftInBed = 0.0F
        rightInBed = 0.0F
        If Volatile.Read(_disposedInBed) <> 0 OrElse Volatile.Read(_rendererReadyInBed) = 0 Then Return False

        Dim readInBed As Long = Interlocked.Read(_pcmReadPositionInBed)
        Dim discardBeforeInBed As Long = Interlocked.Read(_pcmDiscardBeforePositionInBed)
        If readInBed < discardBeforeInBed Then
            readInBed = discardBeforeInBed
            Interlocked.Exchange(_pcmReadPositionInBed, readInBed)
        End If

        Dim writeInBed As Long = Interlocked.Read(_pcmWritePositionInBed)
        If readInBed >= writeInBed Then
            If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_pcmUnderrunsInBed)
            Return False
        End If

        Dim indexInBed As Integer = CInt(readInBed And PcmRingMask)
        leftInBed = _pcmLeftInBed(indexInBed)
        rightInBed = _pcmRightInBed(indexInBed)
        Interlocked.Exchange(_pcmReadPositionInBed, readInBed + 1L)
        If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_pcmFramesConsumedInBed)
        Return True
    End Function

    ' Host-audio-worker block pull.  This may wait briefly because it is called only
    ' from the dedicated host audio thread, never from the machine owner.  Waiting
    ' here lets MeltySynth finish the machine-time block before the mixer substitutes
    ' silence, removing the producer/consumer race that made Brick 6 jittery.
    Public Function ReadPcmBlockWait(leftInBed As Single(),
                                     rightInBed As Single(),
                                     frameCountInBed As Integer,
                                     waitMillisecondsInBed As Integer) As Integer
        If leftInBed Is Nothing Then Throw New ArgumentNullException(NameOf(leftInBed))
        If rightInBed Is Nothing Then Throw New ArgumentNullException(NameOf(rightInBed))
        If frameCountInBed < 0 OrElse frameCountInBed > leftInBed.Length OrElse frameCountInBed > rightInBed.Length Then
            Throw New ArgumentOutOfRangeException(NameOf(frameCountInBed))
        End If
        If frameCountInBed = 0 Then Return 0

        Dim deadlineInBed As Long = Environment.TickCount64 + Math.Max(0, waitMillisecondsInBed)
        Do
            Dim readInBed As Long = Interlocked.Read(_pcmReadPositionInBed)
            Dim discardBeforeInBed As Long = Interlocked.Read(_pcmDiscardBeforePositionInBed)
            If readInBed < discardBeforeInBed Then
                readInBed = discardBeforeInBed
                Interlocked.Exchange(_pcmReadPositionInBed, readInBed)
            End If
            Dim availableInBed As Long = Interlocked.Read(_pcmWritePositionInBed) - readInBed
            If availableInBed >= frameCountInBed Then Exit Do
            If Volatile.Read(_disposedInBed) <> 0 OrElse Volatile.Read(_rendererReadyInBed) = 0 Then Exit Do
            If Environment.TickCount64 >= deadlineInBed Then Exit Do
            Thread.Yield()
        Loop

        Dim readPositionInBed As Long = Interlocked.Read(_pcmReadPositionInBed)
        Dim discardPositionInBed As Long = Interlocked.Read(_pcmDiscardBeforePositionInBed)
        If readPositionInBed < discardPositionInBed Then
            readPositionInBed = discardPositionInBed
            Interlocked.Exchange(_pcmReadPositionInBed, readPositionInBed)
        End If

        Dim availableFramesInBed As Long = Math.Max(0L, Interlocked.Read(_pcmWritePositionInBed) - readPositionInBed)
        Dim framesToReadInBed As Integer = CInt(Math.Min(CLng(frameCountInBed), availableFramesInBed))
        For indexInBed As Integer = 0 To framesToReadInBed - 1
            Dim ringIndexInBed As Integer = CInt((readPositionInBed + indexInBed) And PcmRingMask)
            leftInBed(indexInBed) = _pcmLeftInBed(ringIndexInBed)
            rightInBed(indexInBed) = _pcmRightInBed(ringIndexInBed)
        Next
        If framesToReadInBed < frameCountInBed Then
            Array.Clear(leftInBed, framesToReadInBed, frameCountInBed - framesToReadInBed)
            Array.Clear(rightInBed, framesToReadInBed, frameCountInBed - framesToReadInBed)
            If RuntimeDiagnostics.Enabled Then Interlocked.Add(_pcmUnderrunsInBed, CLng(frameCountInBed - framesToReadInBed))
        End If
        If framesToReadInBed > 0 Then
            Interlocked.Exchange(_pcmReadPositionInBed, readPositionInBed + framesToReadInBed)
            If RuntimeDiagnostics.Enabled Then Interlocked.Add(_pcmFramesConsumedInBed, framesToReadInBed)
        End If
        Return framesToReadInBed
    End Function

    ' Host presentation flush only.  This is safe on reset/configuration boundaries
    ' and never touches guest-visible MIDI/device state.
    Public Sub DiscardBufferedPcm()
        ' Do not write the consumer read pointer from the worker thread.  Publish a
        ' discard fence instead; TryReadPcmSample is the sole owner that advances
        ' _pcmReadPositionInBed.  This keeps the SPSC ring race-free across reset.
        Dim writeInBed As Long = Interlocked.Read(_pcmWritePositionInBed)
        Interlocked.Exchange(_pcmDiscardBeforePositionInBed, writeInBed)
    End Sub

    Private Sub WorkerLoopInBed()
        Try
            SetStateInBed("resolving bank", String.Empty)
            Dim resolutionInBed As SoundFontBankResolution = SoundFontCatalog.ResolveBank(_bankDirectoryInBed, _settingsInBed.BankFileName)
            SyncLock _statusSyncInBed
                _resolutionInBed = resolutionInBed
            End SyncLock

            If Not resolutionInBed.HasBank Then
                SetStateInBed("silent - no bank", resolutionInBed.Summary())
                DrainWithoutRendererInBed()
                Return
            End If

            If Volatile.Read(_disposedInBed) <> 0 Then Return
            SetStateInBed("loading " & resolutionInBed.ResolvedBank.FileName, String.Empty)

            Dim rendererInBed As IWavetableRenderer = New MeltySynthWavetableRenderer()
            rendererInBed.LoadBank(resolutionInBed.ResolvedBank.FullPath)

            If Volatile.Read(_disposedInBed) <> 0 Then Return
            SyncLock _statusSyncInBed
                _rendererInBed = rendererInBed
                _stateTextInBed = "bank loaded - PCM connected"
                _lastErrorInBed = String.Empty
            End SyncLock
            Volatile.Write(_activeRendererVoicesInBed,
                           rendererInBed.ActiveVoiceCount)
            Volatile.Write(_rendererReadyInBed, 1)

            Dim renderLeftInBed(RenderWorkBlockFrames - 1) As Single
            Dim renderRightInBed(RenderWorkBlockFrames - 1) As Single

            For Each commandInBed As WavetableHostCommand In _commandsInBed.GetConsumingEnumerable()
                If Volatile.Read(_disposedInBed) <> 0 Then Exit For

                Select Case commandInBed.Kind
                    Case WavetableHostCommandKind.RenderEvent
                        Try
                            ProcessEventInBed(rendererInBed, commandInBed.RenderEvent)
                        Finally
                            Volatile.Write(_activeRendererVoicesInBed,
                                           rendererInBed.ActiveVoiceCount)
                            Interlocked.Decrement(_pendingRendererEventsInBed)
                        End Try

                    Case WavetableHostCommandKind.RenderFrames
                        Try
                            RenderFramesInBed(rendererInBed,
                                              commandInBed.FrameCount,
                                              renderLeftInBed,
                                              renderRightInBed)
                        Finally
                            Interlocked.Add(_renderFramesOutstandingInBed, -CLng(commandInBed.FrameCount))
                        End Try
                End Select
            Next
        Catch ex As Exception
            Volatile.Write(_rendererReadyInBed, 0)
            Volatile.Write(_activeRendererVoicesInBed, 0)
            ' File/format/renderer exceptions are host failures.  Keep the VM alive
            ' and surface the error in diagnostics instead of unwinding RunSlice.
            SetStateInBed("silent - renderer fault", ex.GetType().Name & ": " & ex.Message)
            DrainWithoutRendererInBed()
        End Try
    End Sub

    Private Sub ProcessEventInBed(rendererInBed As IWavetableRenderer,
                                  eventInBed As WavetableRenderEvent)
        Try
            Select Case eventInBed.Kind
                Case WavetableRenderEventKind.MidiMessage
                    If eventInBed.MidiMessage.Status = &HFF Then
                        rendererInBed.ResetMidiState()
                        DiscardBufferedPcm()
                    Else
                        rendererInBed.ProcessMidiMessage(eventInBed.MidiMessage)
                    End If
                Case WavetableRenderEventKind.HardwareReset
                    rendererInBed.ResetMidiState()
                    DiscardBufferedPcm()
            End Select
            If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_eventsProcessedInBed)
            Interlocked.Exchange(_lastSequenceInBed, CLng(Math.Min(eventInBed.Sequence, CULng(Long.MaxValue))))
        Catch ex As Exception
            SetStateInBed("silent - renderer event fault", ex.GetType().Name & ": " & ex.Message)
            If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_eventsIgnoredWithoutRendererInBed)
        End Try
    End Sub

    Private Sub RenderFramesInBed(rendererInBed As IWavetableRenderer,
                                  frameCountInBed As Integer,
                                  renderLeftInBed As Single(),
                                  renderRightInBed As Single())
        Dim remainingInBed As Integer = frameCountInBed
        While remainingInBed > 0 AndAlso Volatile.Read(_disposedInBed) = 0
            Dim blockFramesInBed As Integer = Math.Min(remainingInBed, RenderWorkBlockFrames)
            rendererInBed.RenderStereo(renderLeftInBed, renderRightInBed, blockFramesInBed)
            Volatile.Write(_activeRendererVoicesInBed,
                           rendererInBed.ActiveVoiceCount)

            For indexInBed As Integer = 0 To blockFramesInBed - 1
                If Not TryWritePcmSampleInBed(renderLeftInBed(indexInBed), renderRightInBed(indexInBed)) Then
                    If RuntimeDiagnostics.Enabled Then Interlocked.Add(_renderFramesDroppedInBed, CLng(blockFramesInBed - indexInBed))
                    Return
                End If
                If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_renderFramesProducedInBed)
            Next

            remainingInBed -= blockFramesInBed
        End While
    End Sub

    Private Function TryWritePcmSampleInBed(leftInBed As Single,
                                            rightInBed As Single) As Boolean
        Dim writeInBed As Long = Interlocked.Read(_pcmWritePositionInBed)
        Dim readInBed As Long = Interlocked.Read(_pcmReadPositionInBed)
        If writeInBed - readInBed >= PcmRingCapacity Then Return False

        Dim indexInBed As Integer = CInt(writeInBed And PcmRingMask)
        _pcmLeftInBed(indexInBed) = leftInBed
        _pcmRightInBed(indexInBed) = rightInBed
        Interlocked.Exchange(_pcmWritePositionInBed, writeInBed + 1L)

        If RuntimeDiagnostics.Enabled Then
            ' Pure telemetry.  Keep this atomic CAS loop completely out of the
            ' per-PCM-frame hot path during performance-baseline runs.
            Dim queuedInBed As Long = (writeInBed + 1L) - readInBed
            Dim peakInBed As Long = Interlocked.Read(_pcmPeakQueuedInBed)
            While queuedInBed > peakInBed
                Dim previousInBed As Long = Interlocked.CompareExchange(_pcmPeakQueuedInBed, queuedInBed, peakInBed)
                If previousInBed = peakInBed Then Exit While
                peakInBed = previousInBed
            End While
        End If
        Return True
    End Function

    Private Sub DrainWithoutRendererInBed()
        Try
            For Each ignoredInBed As WavetableHostCommand In _commandsInBed.GetConsumingEnumerable()
                If Volatile.Read(_disposedInBed) <> 0 Then Exit For
                If ignoredInBed.Kind = WavetableHostCommandKind.RenderEvent Then
                    Interlocked.Decrement(_pendingRendererEventsInBed)
                    If RuntimeDiagnostics.Enabled Then Interlocked.Increment(_eventsIgnoredWithoutRendererInBed)
                    Dim eventInBed As WavetableRenderEvent = ignoredInBed.RenderEvent
                    Interlocked.Exchange(_lastSequenceInBed, CLng(Math.Min(eventInBed.Sequence, CULng(Long.MaxValue))))
                Else
                    Interlocked.Add(_renderFramesOutstandingInBed, -CLng(ignoredInBed.FrameCount))
                    If RuntimeDiagnostics.Enabled Then Interlocked.Add(_renderFramesDroppedInBed, CLng(ignoredInBed.FrameCount))
                End If
            Next
        Catch ex As InvalidOperationException
        End Try
    End Sub

    Private Sub SetStateInBed(stateInBed As String, errorInBed As String)
        SyncLock _statusSyncInBed
            _stateTextInBed = If(stateInBed, String.Empty)
            _lastErrorInBed = If(errorInBed, String.Empty)
        End SyncLock
    End Sub

    Public Function DiagnosticText() As String
        Dim stateInBed As String
        Dim errorInBed As String
        Dim resolutionInBed As SoundFontBankResolution
        Dim rendererInBed As IWavetableRenderer
        SyncLock _statusSyncInBed
            stateInBed = _stateTextInBed
            errorInBed = _lastErrorInBed
            resolutionInBed = _resolutionInBed
            rendererInBed = _rendererInBed
        End SyncLock

        Dim requestedInBed As String = WavetableAttachmentSettings.NormalizeBankFileName(_settingsInBed.BankFileName)
        Dim resolvedInBed As String = "(none)"
        Dim reasonInBed As String = "pending"
        If resolutionInBed IsNot Nothing Then
            reasonInBed = resolutionInBed.Summary()
            If resolutionInBed.ResolvedBank IsNot Nothing Then resolvedInBed = resolutionInBed.ResolvedBank.FileName
        End If

        Dim rendererTextInBed As String = If(rendererInBed Is Nothing, "(none)", rendererInBed.RendererName)
        Dim sampleRateTextInBed As String = If(rendererInBed Is Nothing, "(n/a)", rendererInBed.SampleRate.ToString() & " Hz")
        Dim queuedPcmInBed As Long = Math.Max(0L, Interlocked.Read(_pcmWritePositionInBed) - Interlocked.Read(_pcmReadPositionInBed))

        Dim textInBed As String = "  Wavetable host        : " & stateInBed & Environment.NewLine &
                                  "    bank requested      : " & requestedInBed & Environment.NewLine &
                                  "    bank resolved       : " & resolvedInBed & Environment.NewLine &
                                  "    selection reason    : " & reasonInBed & Environment.NewLine &
                                  "    renderer            : " & rendererTextInBed & " / " & sampleRateTextInBed & Environment.NewLine &
                                  "    voices / pending evt: " & Volatile.Read(_activeRendererVoicesInBed).ToString() & " / " & Volatile.Read(_pendingRendererEventsInBed).ToString() & Environment.NewLine &
                                  "    command queue       : " & _commandsInBed.Count.ToString() & " / " & CommandQueueCapacity.ToString() & Environment.NewLine &
                                  "    events post/drop    : " & Interlocked.Read(_eventsPostedInBed).ToString("N0") & " / " & Interlocked.Read(_eventsDroppedInBed).ToString("N0") & Environment.NewLine &
                                  "    process/ignored     : " & Interlocked.Read(_eventsProcessedInBed).ToString("N0") & " / " & Interlocked.Read(_eventsIgnoredWithoutRendererInBed).ToString("N0") & Environment.NewLine &
                                  "    render req post/drop: " & Interlocked.Read(_renderRequestsPostedInBed).ToString("N0") & " / " & Interlocked.Read(_renderRequestsDroppedInBed).ToString("N0") & Environment.NewLine &
                                  "    frames request/prod : " & Interlocked.Read(_renderFramesRequestedInBed).ToString("N0") & " / " & Interlocked.Read(_renderFramesProducedInBed).ToString("N0") & Environment.NewLine &
                                  "    frames drop/consume : " & Interlocked.Read(_renderFramesDroppedInBed).ToString("N0") & " / " & Interlocked.Read(_pcmFramesConsumedInBed).ToString("N0") & Environment.NewLine &
                                  "    PCM queued/peak     : " & queuedPcmInBed.ToString("N0") & " / " & Interlocked.Read(_pcmPeakQueuedInBed).ToString("N0") & Environment.NewLine &
                                  "    PCM underruns       : " & Interlocked.Read(_pcmUnderrunsInBed).ToString("N0") & Environment.NewLine &
                                  "    last sequence       : " & Interlocked.Read(_lastSequenceInBed).ToString("N0")
        If errorInBed.Length > 0 Then textInBed &= Environment.NewLine & "    host error          : " & errorInBed
        Return textInBed
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If Interlocked.Exchange(_disposedInBed, 1) <> 0 Then Return
        Volatile.Write(_rendererReadyInBed, 0)
        Volatile.Write(_activeRendererVoicesInBed, 0)
        DiscardBufferedPcm()
        Try
            _commandsInBed.CompleteAdding()
        Catch ex As InvalidOperationException
        End Try
        ' The worker is background-only and SoundFont construction is not safely
        ' cancellable mid-parse.  Do not block chassis shutdown waiting for host I/O.
        ' It checks _disposed immediately after load and exits without publishing it.
    End Sub
End Class
