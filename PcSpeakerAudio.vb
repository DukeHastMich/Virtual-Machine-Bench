Imports System
Imports System.Collections.Concurrent
Imports System.Runtime.InteropServices
Imports System.Threading

' CROMWELL PC SPEAKER HOST-CLOCK BRICK
'
' Guest-visible side:
'   * PC/AT port 61h and PIT channel 2 remain the only electrical controls.
'   * No host shortcut changes PIT, IRQ0, port I/O, or guest execution timing.
'
' Host-presenter side:
'   * The machine owner now does ONLY cheap 48-kHz phase arithmetic plus one
'     immutable PIT snapshot per audible interval.
'   * All 48-kHz sampling, DC blocking, PCM generation, and waveOut pacing happen
'     on a dedicated host worker.
'   * waveOut may wait for a free Windows buffer on that worker; the 286 never
'     waits for the host sound device.
'
' This also retains the Gauntlet II direct-drive repair: port 61h bit 1 LOW is a
' real waveform level, not "speaker inactive".  Gauntlet II streams ~4.85-kHz
' one-bit samples by toggling that bit with PIT2 gate held low.
'
' IMPORTANT CLOCK ORDER:
'   PcSpeakerDevice still belongs immediately BEFORE Pit8253 in HardwareBus.
'   It snapshots PIT2 at the beginning of each coordinated interval; Pit8253 then
'   advances normally.  The worker reconstructs PIT2.OUT at individual host sample
'   offsets from that immutable snapshot, so it never touches live PIT state.
Public NotInheritable Class PcSpeakerDevice
    Implements IClockedDevice, IClockBatchSafeDevice, IResettableDevice, IDisposable

    Private Const SampleRateInBed As Integer = 48000
    Private Const PicosecondsPerSecondInBed As Long = 1000000000000L
    Private Const StableLowCollapsePicosecondsInBed As Long = 20000000000L ' 20 ms

    Private ReadOnly _pit As Pit8253
    Private ReadOnly _systemControl As AtSystemControlPorts
    Private ReadOnly _hostAudioInBed As New PcSpeakerHostAudioEngine(SampleRateInBed)
    Private ReadOnly _processExitHandler As EventHandler

    ' Cheap rational host-sample clock bookkeeping remains on the coordinated
    ' machine timeline.  The expensive work represented by those samples does not.
    Private _sampleClockNumeratorInBed As Long
    Private _signalActiveInBed As Boolean
    Private _stableLowPicosecondsInBed As Long
    Private _generatedSamplesInBed As ULong
    Private _silentSamplesSkippedInBed As ULong
    Private _disposedInBed As Boolean

    Public Sub New(pit As Pit8253, systemControl As AtSystemControlPorts)
        If pit Is Nothing Then Throw New ArgumentNullException(NameOf(pit))
        If systemControl Is Nothing Then Throw New ArgumentNullException(NameOf(systemControl))
        _pit = pit
        _systemControl = systemControl

        _processExitHandler = AddressOf HandleProcessExitInBed
        AddHandler AppDomain.CurrentDomain.ProcessExit, _processExitHandler
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If _disposedInBed Then Return
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If elapsedPicoseconds = 0 Then Return

        ' Preserve the exact 48-kHz phase across silent intervals without doing
        ' any per-sample work on the machine owner.
        Dim phaseAtStartInBed As Long = _sampleClockNumeratorInBed
        Dim totalInBed As Long =
            phaseAtStartInBed + elapsedPicoseconds * CLng(SampleRateInBed)
        Dim sampleCountInBed As Long =
            totalInBed \ PicosecondsPerSecondInBed
        _sampleClockNumeratorInBed =
            totalInBed Mod PicosecondsPerSecondInBed

        Dim speakerDataInBed As Boolean = _systemControl.SpeakerDataEnabled
        Dim endSignalAfterIntervalInBed As Boolean

        If speakerDataInBed Then
            _signalActiveInBed = True
            _stableLowPicosecondsInBed = 0
        ElseIf Not _signalActiveInBed Then
            If sampleCountInBed > 0 Then
                _silentSamplesSkippedInBed += CULng(sampleCountInBed)
            End If
            Return
        Else
            Dim remainingToCollapseInBed As Long =
                StableLowCollapsePicosecondsInBed - _stableLowPicosecondsInBed
            If elapsedPicoseconds >= remainingToCollapseInBed Then
                _stableLowPicosecondsInBed = StableLowCollapsePicosecondsInBed
                endSignalAfterIntervalInBed = True
            Else
                _stableLowPicosecondsInBed += elapsedPicoseconds
            End If
        End If

        If sampleCountInBed <= 0 Then Return

        Dim pitSnapshotInBed As Pit8253.ChannelOutputSnapshot =
            _pit.CaptureOutputSnapshot(2)

        Dim acceptedInBed As Boolean =
            _hostAudioInBed.PostRenderInterval(elapsedPicoseconds,
                                               phaseAtStartInBed,
                                               sampleCountInBed,
                                               pitSnapshotInBed,
                                               speakerDataInBed,
                                               endSignalAfterIntervalInBed)

        If acceptedInBed Then
            _generatedSamplesInBed += CULng(sampleCountInBed)

            If endSignalAfterIntervalInBed Then
                _signalActiveInBed = False
                _stableLowPicosecondsInBed = 0
            End If
        ElseIf endSignalAfterIntervalInBed Then
            ' Do not declare the signal closed unless the host worker actually
            ' received the final low interval.  A later clock batch can retry it.
            _stableLowPicosecondsInBed = StableLowCollapsePicosecondsInBed
        End If
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        If _disposedInBed Then Return
        _sampleClockNumeratorInBed = 0
        _signalActiveInBed = False
        _stableLowPicosecondsInBed = 0
        _hostAudioInBed.ResetAudioState()
    End Sub

    Public Function DiagnosticText() As String
        Return "Motherboard PC speaker" & Environment.NewLine &
               "  port61 data/gate/node : " &
               If(_systemControl.SpeakerDataEnabled, "1", "0") & "/" &
               If(_systemControl.SpeakerTimerGateEnabled, "1", "0") & "/" &
               If(_systemControl.SpeakerOutput, "1", "0") & Environment.NewLine &
               "  PCM requested/rendered: " &
               _generatedSamplesInBed.ToString("N0") & "/" &
               _hostAudioInBed.RenderedSampleCount.ToString("N0") & Environment.NewLine &
               "  silent samples skipped: " &
               _silentSamplesSkippedInBed.ToString("N0") & Environment.NewLine &
               "  host interval queue drop: " &
               _hostAudioInBed.DroppedIntervalCount.ToString("N0") & Environment.NewLine &
               "  buffers queued/dropped: " &
               _hostAudioInBed.QueuedBufferCount.ToString("N0") & "/" &
               _hostAudioInBed.DroppedBufferCount.ToString("N0") & Environment.NewLine &
               "  waveOut error          : " &
               _hostAudioInBed.LastError.ToString()
    End Function

    Public ReadOnly Property DroppedHostAudioBuffers As ULong
        Get
            Return _hostAudioInBed.DroppedBufferCount
        End Get
    End Property

    Public ReadOnly Property LastHostAudioError As UInteger
        Get
            Return _hostAudioInBed.LastError
        End Get
    End Property

    Private Sub HandleProcessExitInBed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposedInBed Then Return
        _disposedInBed = True
        RemoveHandler AppDomain.CurrentDomain.ProcessExit, _processExitHandler
        _hostAudioInBed.Dispose()
        GC.SuppressFinalize(Me)
    End Sub
End Class

Friend Enum PcSpeakerHostAudioCommandKind As Byte
    RenderInterval = 0
    Reset = 1
End Enum

Friend Structure PcSpeakerHostAudioCommand
    Friend Kind As PcSpeakerHostAudioCommandKind
    Friend Generation As Integer
    Friend ElapsedPicoseconds As Long
    Friend PhaseAtStart As Long
    Friend SampleCount As Long
    Friend PitSnapshot As Pit8253.ChannelOutputSnapshot
    Friend SpeakerDataEnabled As Boolean
    Friend EndSignalAfterInterval As Boolean
End Structure

' The host transducer for the motherboard speaker.  Only this worker performs
' 48-kHz signal generation and only this worker calls WinMM.
Friend NotInheritable Class PcSpeakerHostAudioEngine
    Implements IDisposable

    Private Const PicosecondsPerSecondInBed As Long = 1000000000000L
    Private Const OutputAmplitudeInBed As Double = 6200.0
    Private Const DcBlockerPoleInBed As Double = 0.995
    Private Const CommandQueueCapacityInBed As Integer = 65536

    Private ReadOnly _sampleRateInBed As Integer
    Private ReadOnly _commandsInBed As New BlockingCollection(Of PcSpeakerHostAudioCommand)(CommandQueueCapacityInBed)
    Private ReadOnly _workerInBed As Thread
    Private ReadOnly _waveOutInBed As WinMmWaveOut16

    Private _previousRawInBed As Double
    Private _previousFilteredInBed As Double
    Private _generationInBed As Integer
    Private _disposedInBed As Integer
    Private _renderedSamplesInBed As Long
    Private _droppedIntervalsInBed As Long

    Public Sub New(sampleRateInBed As Integer)
        If sampleRateInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sampleRateInBed))
        _sampleRateInBed = sampleRateInBed
        _waveOutInBed = New WinMmWaveOut16(sampleRateInBed)
        _workerInBed = New Thread(AddressOf WorkerLoopInBed) With {
            .IsBackground = True,
            .Name = "Virtual Machine Bench PC speaker host audio"
        }
        _workerInBed.Start()
    End Sub

    Public Function PostRenderInterval(elapsedPicosecondsInBed As Long,
                                       phaseAtStartInBed As Long,
                                       sampleCountInBed As Long,
                                       pitSnapshotInBed As Pit8253.ChannelOutputSnapshot,
                                       speakerDataEnabledInBed As Boolean,
                                       endSignalAfterIntervalInBed As Boolean) As Boolean
        If sampleCountInBed <= 0 OrElse Volatile.Read(_disposedInBed) <> 0 Then Return False

        Dim commandInBed As New PcSpeakerHostAudioCommand With {
            .Kind = PcSpeakerHostAudioCommandKind.RenderInterval,
            .Generation = Volatile.Read(_generationInBed),
            .ElapsedPicoseconds = elapsedPicosecondsInBed,
            .PhaseAtStart = phaseAtStartInBed,
            .SampleCount = sampleCountInBed,
            .PitSnapshot = pitSnapshotInBed,
            .SpeakerDataEnabled = speakerDataEnabledInBed,
            .EndSignalAfterInterval = endSignalAfterIntervalInBed
        }

        Try
            If _commandsInBed.TryAdd(commandInBed, 0) Then Return True
        Catch ex As InvalidOperationException
        End Try

        Interlocked.Increment(_droppedIntervalsInBed)
        Return False
    End Function

    Public Sub ResetAudioState()
        If Volatile.Read(_disposedInBed) <> 0 Then Return

        Dim generationInBed As Integer = Interlocked.Increment(_generationInBed)
        Dim commandInBed As New PcSpeakerHostAudioCommand With {
            .Kind = PcSpeakerHostAudioCommandKind.Reset,
            .Generation = generationInBed
        }

        Try
            If Not _commandsInBed.TryAdd(commandInBed, 0) Then
                Interlocked.Increment(_droppedIntervalsInBed)
            End If
        Catch ex As InvalidOperationException
        End Try
    End Sub

    Private Sub WorkerLoopInBed()
        Try
            For Each commandInBed As PcSpeakerHostAudioCommand In _commandsInBed.GetConsumingEnumerable()
                If Volatile.Read(_disposedInBed) <> 0 Then Exit For
                If commandInBed.Generation <> Volatile.Read(_generationInBed) Then Continue For

                Select Case commandInBed.Kind
                    Case PcSpeakerHostAudioCommandKind.Reset
                        ResetWorkerStateInBed()

                    Case PcSpeakerHostAudioCommandKind.RenderInterval
                        RenderIntervalInBed(commandInBed)
                End Select
            Next
        Catch ex As InvalidOperationException
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Sub RenderIntervalInBed(commandInBed As PcSpeakerHostAudioCommand)
        Dim numeratorToSampleInBed As Long =
            PicosecondsPerSecondInBed - commandInBed.PhaseAtStart

        For sampleIndexInBed As Long = 0 To commandInBed.SampleCount - 1L
            If Volatile.Read(_disposedInBed) <> 0 Then Return

            Dim sampleOffsetPicosecondsInBed As Long =
                (numeratorToSampleInBed + _sampleRateInBed - 1L) \ _sampleRateInBed
            If sampleOffsetPicosecondsInBed > commandInBed.ElapsedPicoseconds Then
                sampleOffsetPicosecondsInBed = commandInBed.ElapsedPicoseconds
            End If

            Dim rawInBed As Double =
                If(commandInBed.SpeakerDataEnabled AndAlso
                   Pit8253.GetOutputAtOffset(commandInBed.PitSnapshot,
                                             sampleOffsetPicosecondsInBed),
                   1.0,
                   -1.0)

            Dim filteredInBed As Double =
                rawInBed -
                _previousRawInBed +
                DcBlockerPoleInBed * _previousFilteredInBed
            _previousRawInBed = rawInBed
            _previousFilteredInBed = filteredInBed

            Dim pcmInBed As Integer =
                CInt(Math.Round(filteredInBed * OutputAmplitudeInBed))
            If pcmInBed > Short.MaxValue Then
                pcmInBed = Short.MaxValue
            ElseIf pcmInBed < Short.MinValue Then
                pcmInBed = Short.MinValue
            End If

            _waveOutInBed.SubmitSample(CShort(pcmInBed))
            numeratorToSampleInBed += PicosecondsPerSecondInBed
        Next

        Interlocked.Add(_renderedSamplesInBed, commandInBed.SampleCount)

        If commandInBed.EndSignalAfterInterval Then
            _waveOutInBed.EndSignal()
            _previousRawInBed = 0.0
            _previousFilteredInBed = 0.0
        End If
    End Sub

    Private Sub ResetWorkerStateInBed()
        _previousRawInBed = 0.0
        _previousFilteredInBed = 0.0
        _waveOutInBed.Reset()
    End Sub

    Public ReadOnly Property RenderedSampleCount As Long
        Get
            Return Interlocked.Read(_renderedSamplesInBed)
        End Get
    End Property

    Public ReadOnly Property DroppedIntervalCount As Long
        Get
            Return Interlocked.Read(_droppedIntervalsInBed)
        End Get
    End Property

    Public ReadOnly Property QueuedBufferCount As ULong
        Get
            Return _waveOutInBed.QueuedBufferCount
        End Get
    End Property

    Public ReadOnly Property DroppedBufferCount As ULong
        Get
            Return _waveOutInBed.DroppedBufferCount
        End Get
    End Property

    Public ReadOnly Property LastError As UInteger
        Get
            Return _waveOutInBed.LastError
        End Get
    End Property

    Public Sub Dispose() Implements IDisposable.Dispose
        If Interlocked.Exchange(_disposedInBed, 1) <> 0 Then Return

        Try
            _commandsInBed.CompleteAdding()
        Catch ex As InvalidOperationException
        End Try

        If Thread.CurrentThread IsNot _workerInBed Then
            _workerInBed.Join(1000)
        End If

        _waveOutInBed.Dispose()
    End Sub
End Class

Friend NotInheritable Class WinMmWaveOut16
    Implements IDisposable

    Private Const WaveMapperInBed As UInteger = UInteger.MaxValue
    Private Const WaveFormatPcmInBed As UShort = 1US
    Private Const WhdrDoneInBed As UInteger = &H1UI
    Private Const CallbackNullInBed As UInteger = 0UI
    Private Const BufferCountInBed As Integer = 8
    Private Const SamplesPerBufferInBed As Integer = 480   ' 10 ms at 48 kHz

    <StructLayout(LayoutKind.Sequential)>
    Private Structure WaveFormatExInBed
        Public FormatTag As UShort
        Public Channels As UShort
        Public SamplesPerSec As UInteger
        Public AvgBytesPerSec As UInteger
        Public BlockAlign As UShort
        Public BitsPerSample As UShort
        Public ExtraSize As UShort
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure WaveHeaderInBed
        Public Data As IntPtr
        Public BufferLength As UInteger
        Public BytesRecorded As UInteger
        Public User As UIntPtr
        Public Flags As UInteger
        Public Loops As UInteger
        Public NextHeader As IntPtr
        Public Reserved As UIntPtr
    End Structure

    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutOpen(ByRef waveOutHandle As IntPtr,
                                        deviceId As UInteger,
                                        ByRef format As WaveFormatExInBed,
                                        callback As IntPtr,
                                        instance As IntPtr,
                                        flags As UInteger) As UInteger
    End Function

    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutPrepareHeader(waveOutHandle As IntPtr,
                                                 header As IntPtr,
                                                 headerSize As UInteger) As UInteger
    End Function

    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutUnprepareHeader(waveOutHandle As IntPtr,
                                                   header As IntPtr,
                                                   headerSize As UInteger) As UInteger
    End Function

    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutWrite(waveOutHandle As IntPtr,
                                         header As IntPtr,
                                         headerSize As UInteger) As UInteger
    End Function

    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutReset(waveOutHandle As IntPtr) As UInteger
    End Function

    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutClose(waveOutHandle As IntPtr) As UInteger
    End Function

    Private ReadOnly _sampleRateInBed As Integer
    Private ReadOnly _stagingInBed(SamplesPerBufferInBed - 1) As Short
    Private _stagingCountInBed As Integer

    Private ReadOnly _buffersInBed(BufferCountInBed - 1)() As Short
    Private ReadOnly _dataPinsInBed(BufferCountInBed - 1) As GCHandle
    Private ReadOnly _headerPointersInBed(BufferCountInBed - 1) As IntPtr
    Private ReadOnly _preparedInBed(BufferCountInBed - 1) As Boolean
    Private ReadOnly _inFlightInBed(BufferCountInBed - 1) As Boolean

    Private _waveOutHandleInBed As IntPtr
    Private _nextBufferInBed As Integer
    Private _openedInBed As Boolean
    Private _disabledInBed As Boolean
    Private _disposedInBed As Boolean
    Private _lastErrorInBed As UInteger
    Private _droppedBufferCountInBed As ULong
    Private _queuedBufferCountInBed As ULong

    Public Sub New(sampleRate As Integer)
        If sampleRate <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sampleRate))
        _sampleRateInBed = sampleRate
    End Sub

    Public ReadOnly Property LastError As UInteger
        Get
            Return _lastErrorInBed
        End Get
    End Property

    Public ReadOnly Property DroppedBufferCount As ULong
        Get
            Return _droppedBufferCountInBed
        End Get
    End Property

    Public ReadOnly Property QueuedBufferCount As ULong
        Get
            Return _queuedBufferCountInBed
        End Get
    End Property

    Public Sub SubmitSample(value As Short)
        If _disposedInBed OrElse _disabledInBed Then Return

        _stagingInBed(_stagingCountInBed) = value
        _stagingCountInBed += 1
        If _stagingCountInBed < SamplesPerBufferInBed Then Return

        QueueStagingBufferInBed()
        _stagingCountInBed = 0
    End Sub

    Private Sub QueueStagingBufferInBed()
        If Not _openedInBed Then
            ' Stay completely dormant while the emulated speaker is silent.
            Dim nonSilentInBed As Boolean
            For iInBed As Integer = 0 To SamplesPerBufferInBed - 1
                If _stagingInBed(iInBed) <> 0S Then
                    nonSilentInBed = True
                    Exit For
                End If
            Next
            If Not nonSilentInBed Then Return
            If Not OpenInBed() Then Return
        End If

        Dim bufferIndexInBed As Integer = WaitForAvailableBufferInBed()
        If bufferIndexInBed < 0 Then Return

        Array.Copy(_stagingInBed, _buffersInBed(bufferIndexInBed), SamplesPerBufferInBed)

        Dim resultInBed As UInteger =
            waveOutWrite(_waveOutHandleInBed,
                         _headerPointersInBed(bufferIndexInBed),
                         CUInt(Marshal.SizeOf(GetType(WaveHeaderInBed))))
        If resultInBed <> 0UI Then
            _lastErrorInBed = resultInBed
            _inFlightInBed(bufferIndexInBed) = False
            Return
        End If

        _inFlightInBed(bufferIndexInBed) = True
        _queuedBufferCountInBed += 1UL
        _nextBufferInBed = (bufferIndexInBed + 1) Mod BufferCountInBed
    End Sub

    ' Finish the final audible fragment with silence exactly once when port 61h
    ' disconnects the speaker. Subsequent silent motherboard time is free.
    Public Sub EndSignal()
        If _disposedInBed OrElse _disabledInBed OrElse _stagingCountInBed <= 0 Then Return
        Array.Clear(_stagingInBed, _stagingCountInBed, SamplesPerBufferInBed - _stagingCountInBed)
        QueueStagingBufferInBed()
        _stagingCountInBed = 0
    End Sub

    Private Function WaitForAvailableBufferInBed() As Integer
        ' Host-side pacing only.  This loop can never stall the 286 because the
        ' PC-speaker renderer owns its own worker thread.
        Do
            For offsetInBed As Integer = 0 To BufferCountInBed - 1
                Dim indexInBed As Integer = (_nextBufferInBed + offsetInBed) Mod BufferCountInBed

                If Not _inFlightInBed(indexInBed) Then Return indexInBed

                Dim headerInBed As WaveHeaderInBed =
                    Marshal.PtrToStructure(Of WaveHeaderInBed)(_headerPointersInBed(indexInBed))
                If (headerInBed.Flags And WhdrDoneInBed) <> 0UI Then
                    _inFlightInBed(indexInBed) = False
                    Return indexInBed
                End If
            Next

            If _disposedInBed OrElse _disabledInBed OrElse _waveOutHandleInBed = IntPtr.Zero Then
                Return -1
            End If
            Thread.Sleep(1)
        Loop
    End Function

    Private Function OpenInBed() As Boolean
        If _openedInBed Then Return True
        If _disabledInBed OrElse _disposedInBed Then Return False

        Dim formatInBed As New WaveFormatExInBed With {
            .FormatTag = WaveFormatPcmInBed,
            .Channels = 1US,
            .SamplesPerSec = CUInt(_sampleRateInBed),
            .AvgBytesPerSec = CUInt(_sampleRateInBed * 2),
            .BlockAlign = 2US,
            .BitsPerSample = 16US,
            .ExtraSize = 0US
        }

        Dim resultInBed As UInteger =
            waveOutOpen(_waveOutHandleInBed,
                        WaveMapperInBed,
                        formatInBed,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CallbackNullInBed)
        If resultInBed <> 0UI Then
            _lastErrorInBed = resultInBed
            _waveOutHandleInBed = IntPtr.Zero
            _disabledInBed = True
            Return False
        End If

        Dim headerSizeInBed As UInteger = CUInt(Marshal.SizeOf(GetType(WaveHeaderInBed)))

        Try
            For iInBed As Integer = 0 To BufferCountInBed - 1
                _buffersInBed(iInBed) = New Short(SamplesPerBufferInBed - 1) {}
                _dataPinsInBed(iInBed) = GCHandle.Alloc(_buffersInBed(iInBed), GCHandleType.Pinned)
                _headerPointersInBed(iInBed) = Marshal.AllocHGlobal(CInt(headerSizeInBed))

                Dim headerInBed As New WaveHeaderInBed With {
                    .Data = _dataPinsInBed(iInBed).AddrOfPinnedObject(),
                    .BufferLength = CUInt(SamplesPerBufferInBed * 2),
                    .BytesRecorded = 0UI,
                    .User = UIntPtr.Zero,
                    .Flags = 0UI,
                    .Loops = 0UI,
                    .NextHeader = IntPtr.Zero,
                    .Reserved = UIntPtr.Zero
                }
                Marshal.StructureToPtr(headerInBed, _headerPointersInBed(iInBed), False)

                resultInBed = waveOutPrepareHeader(_waveOutHandleInBed,
                                                   _headerPointersInBed(iInBed),
                                                   headerSizeInBed)
                If resultInBed <> 0UI Then
                    _lastErrorInBed = resultInBed
                    Throw New InvalidOperationException("waveOutPrepareHeader failed")
                End If

                _preparedInBed(iInBed) = True
            Next
        Catch
            CloseOpenResourcesInBed()
            _disabledInBed = True
            Return False
        End Try

        _openedInBed = True
        Return True
    End Function

    Public Sub Reset()
        If _disposedInBed Then Return

        _stagingCountInBed = 0
        Array.Clear(_stagingInBed, 0, _stagingInBed.Length)

        If _waveOutHandleInBed <> IntPtr.Zero Then
            Dim resultInBed As UInteger = waveOutReset(_waveOutHandleInBed)
            If resultInBed <> 0UI Then _lastErrorInBed = resultInBed
        End If

        For iInBed As Integer = 0 To BufferCountInBed - 1
            _inFlightInBed(iInBed) = False
        Next
        _nextBufferInBed = 0
    End Sub

    Private Sub CloseOpenResourcesInBed()
        Dim headerSizeInBed As UInteger = CUInt(Marshal.SizeOf(GetType(WaveHeaderInBed)))

        If _waveOutHandleInBed <> IntPtr.Zero Then
            waveOutReset(_waveOutHandleInBed)
        End If

        For iInBed As Integer = 0 To BufferCountInBed - 1
            If _preparedInBed(iInBed) AndAlso
               _waveOutHandleInBed <> IntPtr.Zero AndAlso
               _headerPointersInBed(iInBed) <> IntPtr.Zero Then
                waveOutUnprepareHeader(_waveOutHandleInBed,
                                       _headerPointersInBed(iInBed),
                                       headerSizeInBed)
                _preparedInBed(iInBed) = False
            End If

            If _headerPointersInBed(iInBed) <> IntPtr.Zero Then
                Marshal.FreeHGlobal(_headerPointersInBed(iInBed))
                _headerPointersInBed(iInBed) = IntPtr.Zero
            End If

            If _dataPinsInBed(iInBed).IsAllocated Then _dataPinsInBed(iInBed).Free()
            _inFlightInBed(iInBed) = False
        Next

        If _waveOutHandleInBed <> IntPtr.Zero Then
            waveOutClose(_waveOutHandleInBed)
            _waveOutHandleInBed = IntPtr.Zero
        End If

        _openedInBed = False
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposedInBed Then Return
        _disposedInBed = True
        CloseOpenResourcesInBed()
        GC.SuppressFinalize(Me)
    End Sub
End Class
