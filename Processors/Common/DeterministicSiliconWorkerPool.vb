Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

' Host acceleration for silicon work which is provably side-effect free.
'
' Workers may decode bytes, calculate arithmetic/FPU candidates, compute address
' translations, or inspect immutable snapshots.  They MUST NOT mutate guest registers,
' cache/TLB state, memory, I/O devices, interrupt lines, pins, or motherboard time.
' Results are returned in input order and are committed only by the owning processor on
' the board/runtime thread.  Therefore worker scheduling cannot alter emulated behavior.
Public NotInheritable Class DeterministicSiliconWorkerPool
    Private ReadOnly _maximumWorkersInBed As Integer

    Public Sub New(Optional maximumWorkersInBed As Integer = 0)
        If maximumWorkersInBed < 0 Then Throw New ArgumentOutOfRangeException(NameOf(maximumWorkersInBed))
        If maximumWorkersInBed = 0 Then
            _maximumWorkersInBed = Math.Max(1, Environment.ProcessorCount)
        Else
            _maximumWorkersInBed = Math.Max(1, maximumWorkersInBed)
        End If
    End Sub

    Public ReadOnly Property MaximumWorkers As Integer
        Get
            Return _maximumWorkersInBed
        End Get
    End Property

    Public Function RunPureBatch(Of TInput, TOutput)(inputsInBed As IReadOnlyList(Of TInput),
                                                      workerInBed As Func(Of TInput, TOutput)) As TOutput()
        If inputsInBed Is Nothing Then Throw New ArgumentNullException(NameOf(inputsInBed))
        If workerInBed Is Nothing Then Throw New ArgumentNullException(NameOf(workerInBed))

        If inputsInBed.Count = 0 Then Return Array.Empty(Of TOutput)()
        Dim outputsInBed(inputsInBed.Count - 1) As TOutput

        If _maximumWorkersInBed = 1 OrElse inputsInBed.Count = 1 Then
            For indexInBed As Integer = 0 To inputsInBed.Count - 1
                outputsInBed(indexInBed) = workerInBed(inputsInBed(indexInBed))
            Next
            Return outputsInBed
        End If

        Dim optionsInBed As New ParallelOptions With {
            .MaxDegreeOfParallelism = _maximumWorkersInBed
        }
        Parallel.For(0, inputsInBed.Count, optionsInBed,
                     Sub(indexInBed As Integer)
                         outputsInBed(indexInBed) = workerInBed(inputsInBed(indexInBed))
                     End Sub)
        Return outputsInBed
    End Function

    Public Shared Sub ValidateDeterministicOrderingOrThrow()
        Dim inputsInBed As New List(Of UInteger)()
        For nInBed As UInteger = 0UI To 4095UI
            inputsInBed.Add(nInBed)
        Next

        Dim serialInBed As New DeterministicSiliconWorkerPool(1)
        Dim parallelInBed As New DeterministicSiliconWorkerPool(Math.Max(2, Environment.ProcessorCount))
        Dim operationInBed As Func(Of UInteger, ULong) =
            Function(valueInBed As UInteger) As ULong
                ' Deliberately pure: useful only as a scheduling/order sentinel.
                Dim widenedInBed As ULong = CULng(valueInBed)
                Return ((widenedInBed * &H9E3779B185EBCA87UL) Xor
                        (widenedInBed << CInt(valueInBed And 15UI)))
            End Function

        Dim oneInBed() As ULong = serialInBed.RunPureBatch(inputsInBed, operationInBed)
        Dim manyInBed() As ULong = parallelInBed.RunPureBatch(inputsInBed, operationInBed)
        If oneInBed.Length <> manyInBed.Length Then
            Throw New InvalidOperationException("Silicon worker determinism validator changed result count.")
        End If
        For indexInBed As Integer = 0 To oneInBed.Length - 1
            If oneInBed(indexInBed) <> manyInBed(indexInBed) Then
                Throw New InvalidOperationException("Silicon worker determinism validator changed ordered results at index " & indexInBed.ToString() & ".")
            End If
        Next
    End Sub
End Class
