Imports System

' Physical CPU packages which fit the current 80286 motherboard socket.
' None is intentionally a legal population state: the motherboard may be
' powered and observed with an empty processor socket, but no CPU is scheduled.
Public NotInheritable Class ProcessorCatalog286
    Private Sub New()
    End Sub

    Public Shared ReadOnly Property SupportedIds As String()
        Get
            Return New String() {
                MachineHardwarePopulationConfiguration.NoneId,
                MachineHardwarePopulationConfiguration.HarrisCs80C28625Id
            }
        End Get
    End Property

    Public Shared Function DisplayName(idInBed As String) As String
        Select Case MachineHardwarePopulationConfiguration.NormalizeCpuId(idInBed)
            Case MachineHardwarePopulationConfiguration.NoneId
                Return "None — empty CPU socket"
            Case Else
                Return MachineProfile286.ProcessorVendor & " " & MachineProfile286.ProcessorModel
        End Select
    End Function

    Public Shared Function CreateProcessor(idInBed As String) As Processor286
        Select Case MachineHardwarePopulationConfiguration.NormalizeCpuId(idInBed)
            Case MachineHardwarePopulationConfiguration.NoneId
                Return Nothing
            Case MachineHardwarePopulationConfiguration.HarrisCs80C28625Id
                Return New Processor286()
            Case Else
                Throw New InvalidOperationException("Unsupported processor package '" & If(idInBed, String.Empty) & "'.")
        End Select
    End Function
End Class
