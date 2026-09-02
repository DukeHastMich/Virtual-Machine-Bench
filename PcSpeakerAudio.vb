Imports System
Imports System.Runtime.InteropServices

' CROMWELL PC SPEAKER BRICK 1
'
' Guest-visible side:
'   PC/AT System Control Port B (61h) bit 0 drives PIT channel-2 GATE.
'   Port 61h bit 1 enables the speaker data path.
'   The audible one-bit node is therefore SpeakerDataEnable AND PIT2.OUT.
'
' Host-presenter side:
'   The signal is sampled against emulated physical time at 48 kHz and sent to
'   WinMM waveOut asynchronously.  Nothing in this class changes guest timing,
'   invents a timer frequency, or calls Console.Beep/SystemSounds shortcuts.
'
' IMPORTANT CLOCK ORDER:
'   This device must be registered immediately BEFORE Pit8253 on HardwareBus.
'   AdvanceTime then observes the PIT state at the beginning of the interval and
'   asks Pit8253.GetOutputAtOffset() for the exact channel-2 logic state at each
'   PCM sample point inside that interval.  Pit8253 advances afterward.
Public NotInheritable Class PcSpeakerDevice
    Implements IClockedDevice, IClockBatchSafeDevice, IResettableDevice, IDisposable

    Private Const SampleRateInBed As Integer = 48000
    Private Const PicosecondsPerSecondInBed As Long = 1000000000000L
    Private Const OutputAmplitudeInBed As Double = 6200.0
    Private Const DcBlockerPoleInBed As Double = 0.995

    Private ReadOnly _pit As Pit8253
    Private ReadOnly _systemControl As AtSystemControlPorts
    Private ReadOnly _waveOut As New WinMmWaveOut16(SampleRateInBed)
    Private ReadOnly _processExitHandler As EventHandler

    ' Fractional PCM clock, in (picoseconds * samples/second) modulo 10^12.
    Private _sampleClockNumeratorInBed As Long

    ' Host acoustic coupling only.  A physical dynamic speaker does not radiate
    ' steady DC; this one-pole blocker removes the DAC's artificial DC component
    ' without altering the guest-visible one-bit node.
    Private _previousRawInBed As Double
    Private _previousFilteredInBed As Double
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

        Dim phaseAtStartInBed As Long = _sampleClockNumeratorInBed
        Dim totalInBed As Long = phaseAtStartInBed + elapsedPicoseconds * CLng(SampleRateInBed)
        Dim sampleCountInBed As Long = totalInBed \ PicosecondsPerSecondInBed
        _sampleClockNumeratorInBed = totalInBed Mod PicosecondsPerSecondInBed

        If sampleCountInBed <= 0 Then Return

        Dim numeratorToSampleInBed As Long = PicosecondsPerSecondInBed - phaseAtStartInBed

        For sampleIndexInBed As Long = 0 To sampleCountInBed - 1
            ' Ceiling division places the sample at the first picosecond whose
            ' rational 48-kHz clock reaches the requested boundary.
            Dim sampleOffsetPicosecondsInBed As Long =
                (numeratorToSampleInBed + SampleRateInBed - 1L) \ SampleRateInBed
            If sampleOffsetPicosecondsInBed > elapsedPicoseconds Then
                sampleOffsetPicosecondsInBed = elapsedPicoseconds
            End If

            Dim rawInBed As Double = 0.0
            If _systemControl.SpeakerDataEnabled Then
                rawInBed = If(_pit.GetOutputAtOffset(2, sampleOffsetPicosecondsInBed), 1.0, -1.0)
            End If

            Dim filteredInBed As Double =
                rawInBed - _previousRawInBed + DcBlockerPoleInBed * _previousFilteredInBed
            _previousRawInBed = rawInBed
            _previousFilteredInBed = filteredInBed

            Dim pcmInBed As Integer = CInt(Math.Round(filteredInBed * OutputAmplitudeInBed))
            If pcmInBed > Short.MaxValue Then
                pcmInBed = Short.MaxValue
            ElseIf pcmInBed < Short.MinValue Then
                pcmInBed = Short.MinValue
            End If

            _waveOut.SubmitSample(CShort(pcmInBed))
            numeratorToSampleInBed += PicosecondsPerSecondInBed
        Next
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        If _disposedInBed Then Return
        _sampleClockNumeratorInBed = 0
        _previousRawInBed = 0.0
        _previousFilteredInBed = 0.0
        _waveOut.Reset()
    End Sub

    Public ReadOnly Property DroppedHostAudioBuffers As ULong
        Get
            Return _waveOut.DroppedBufferCount
        End Get
    End Property

    Public ReadOnly Property LastHostAudioError As UInteger
        Get
            Return _waveOut.LastError
        End Get
    End Property

    Private Sub HandleProcessExitInBed(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposedInBed Then Return
        _disposedInBed = True
        RemoveHandler AppDomain.CurrentDomain.ProcessExit, _processExitHandler
        _waveOut.Dispose()
        GC.SuppressFinalize(Me)
    End Sub
End Class

' Host-only PCM presenter.  Four important rules:
'  1. Never blocks the emulator waiting for the host sound card.
'  2. Never feeds time back into the guest.
'  3. Reuses pinned buffers; there is no allocation in the per-sample path.
'  4. If the host cannot open waveOut, the guest speaker hardware keeps running.
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

        Dim bufferIndexInBed As Integer = FindAvailableBufferInBed()
        If bufferIndexInBed < 0 Then
            _droppedBufferCountInBed += 1UL
            Return
        End If

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
        _nextBufferInBed = (bufferIndexInBed + 1) Mod BufferCountInBed
    End Sub

    Private Function FindAvailableBufferInBed() As Integer
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

        Return -1
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
