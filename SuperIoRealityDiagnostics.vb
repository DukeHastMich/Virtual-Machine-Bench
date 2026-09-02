Imports System
Imports System.Collections.Generic

' Focused, host-only checks for the C&T 82C206-compatible peripheral blocks.
' These tests instantiate isolated devices and never alter the running guest.
Public NotInheritable Class SuperIoRealityDiagnostics
    Private Sub New()
    End Sub

    Public Shared Function RunAll() As String
        Dim failuresInBed As New List(Of String)()
        TestDmaRequestAndPriorityInBed(failuresInBed)
        TestPicCascadeProgrammingInBed(failuresInBed)
        TestRtcDividerAndWeekdayInBed(failuresInBed)
        If failuresInBed.Count = 0 Then Return "82C206 focused diagnostics: PASS"
        Return "82C206 focused diagnostics: FAIL" & Environment.NewLine &
               String.Join(Environment.NewLine, failuresInBed)
    End Function

    Private Shared Sub TestDmaRequestAndPriorityInBed(failuresInBed As List(Of String))
        Dim memoryInBed(&HFFFFF) As Byte
        Dim dmaInBed As New Dma8237(
            Function(channelInBed, addressInBed) memoryInBed(CInt(addressInBed And &HFFFFFUI)),
            Sub(channelInBed, addressInBed, valueInBed) memoryInBed(CInt(addressInBed And &HFFFFFUI)) = valueInBed,
            Function(channelInBed, addressInBed) As UInt16
                Dim lowInBed As Byte = memoryInBed(CInt(addressInBed And &HFFFFFUI))
                Dim highInBed As Byte = memoryInBed(CInt((addressInBed + 1UI) And &HFFFFFUI))
                Return CUShort(CUInt(lowInBed) Or (CUInt(highInBed) << 8))
            End Function,
            Sub(channelInBed, addressInBed, valueInBed)
                memoryInBed(CInt(addressInBed And &HFFFFFUI)) = CByte(valueInBed And &HFFUS)
                memoryInBed(CInt((addressInBed + 1UI) And &HFFFFFUI)) = CByte(valueInBed >> 8)
            End Sub)

        ' Channels 1 and 2: single mode, peripheral-to-memory, unmasked.
        dmaInBed.WritePort(&HBUS, &H45) : dmaInBed.WritePort(&HAUS, &H1)
        dmaInBed.WritePort(&HBUS, &H46) : dmaInBed.WritePort(&HAUS, &H2)
        dmaInBed.SetDreq(2, True)
        If Not dmaInBed.Dma8HoldRequestAsserted Then failuresInBed.Add("DMA DREQ did not assert HRQ/HOLD.")
        dmaInBed.SetDreq(1, True)
        Dim oneByteInBed() As Byte = {&H5A}
        If dmaInBed.TransferToMemory(2, oneByteInBed, 0, 1) <> 0 Then
            failuresInBed.Add("DMA fixed priority allowed channel 2 ahead of channel 1.")
        End If
        If dmaInBed.TransferToMemory(1, oneByteInBed, 0, 1) <> 1 Then
            failuresInBed.Add("DMA highest-priority channel 1 was not serviced.")
        End If
        dmaInBed.SetDreq(1, False) : dmaInBed.SetDreq(2, False)
        If dmaInBed.Dma8HoldRequestAsserted Then failuresInBed.Add("DMA HRQ remained asserted after requests cleared.")
    End Sub

    Private Shared Sub TestPicCascadeProgrammingInBed(failuresInBed As List(Of String))
        Dim picInBed As New Pic8259()
        picInBed.WritePort(&H20US, &H11)
        picInBed.WritePort(&H21US, &H8)
        picInBed.WritePort(&H21US, &H8) ' slave wired/programmed on IR3
        picInBed.WritePort(&H21US, &H1)
        If picInBed.IsProgrammedCascadeInput(2) OrElse Not picInBed.IsProgrammedCascadeInput(3) Then
            failuresInBed.Add("PIC cascade input did not follow ICW3.")
        End If
    End Sub

    Private Shared Sub TestRtcDividerAndWeekdayInBed(failuresInBed As List(Of String))
        Dim picInBed As New Pic8259(&HA0US, &HA1US, &H70)
        Dim rtcInBed As New CmosRtc(picInBed)

        rtcInBed.WritePort(&H70US, &H6) : rtcInBed.WritePort(&H71US, &H5)
        rtcInBed.WritePort(&H70US, &H6)
        If rtcInBed.ReadPort(&H71US) <> &H5 Then failuresInBed.Add("RTC weekday register was not writable/readable.")

        rtcInBed.WritePort(&H70US, &HB) : rtcInBed.WritePort(&H71US, &H42) ' PIE, 24-hour BCD
        rtcInBed.WritePort(&H70US, &HA) : rtcInBed.WritePort(&H71US, &H66) ' divider reset, RS=6
        rtcInBed.AdvanceTime(2000000000L)
        rtcInBed.WritePort(&H70US, &HC)
        If (rtcInBed.ReadPort(&H71US) And &H40) <> 0 Then failuresInBed.Add("RTC generated PF while divider was reset.")

        rtcInBed.WritePort(&H70US, &HA) : rtcInBed.WritePort(&H71US, &H26)
        rtcInBed.AdvanceTime(1000000000L)
        rtcInBed.WritePort(&H70US, &HC)
        If (rtcInBed.ReadPort(&H71US) And &H40) = 0 Then failuresInBed.Add("RTC failed to generate PF with normal divider running.")
    End Sub
End Class
