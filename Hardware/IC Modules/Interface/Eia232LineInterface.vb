Imports System

' CP63 manual-backed digital projection of MC1488/MC1489 EIA-232 interface silicon.
' Voltage thresholds, loading, slew rate and supply behavior belong to the electrical model.
Public Enum Eia232Polarity As Integer
    Negative = -1
    Positive = 1
End Enum

Public NotInheritable Class VmbMc1488
    Public Const ModuleId As String = "VMB.IFACE.MC1488"
    Public Shared Function Drive(ttlLevel As Boolean) As Eia232Polarity
        ' MC1488 performs one stage of inversion: TTL 0 -> positive EIA-232; TTL 1 -> negative.
        Return If(ttlLevel, Eia232Polarity.Negative, Eia232Polarity.Positive)
    End Function
End Class

Public NotInheritable Class VmbMc1489
    Public Const ModuleId As String = "VMB.IFACE.MC1489"
    Public Shared Function Receive(linePolarity As Eia232Polarity) As Boolean
        ' Receiver restores EIA-232 polarity to TTL logic with inversion.
        Return linePolarity = Eia232Polarity.Negative
    End Function
End Class
