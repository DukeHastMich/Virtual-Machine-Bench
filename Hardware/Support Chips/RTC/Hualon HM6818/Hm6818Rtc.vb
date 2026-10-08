Imports System

' Physical Hualon HM6818-family RTC worker used by the Octek FOX II specimen.
' Current behavior inherits the project's MC146818-compatible RTC engine.  This
' wrapper exists so board identity is not lost and future HM6818-specific
' differences have a real silicon boundary instead of entering motherboard code.
Public Class Hm6818Rtc
    Inherits CmosRtc

    Public Sub New(picInBed As Pic8259,
                   Optional persistencePathInBed As String = Nothing)
        MyBase.New(picInBed, persistencePathInBed, 64, Nothing)
    End Sub

    Public ReadOnly Property SiliconIdentity As String
        Get
            Return "Hualon HM6818P / HM6818-family RTC"
        End Get
    End Property
End Class
