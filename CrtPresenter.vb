Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging

' Electrical scanout description captured from the emulated card registers.
' The bitmap is a sampled active raster; these fields preserve the timing that
' gives that raster its physical meaning at the card/monitor boundary.
Public Structure VideoScanoutTiming
    Public PixelClockHz As Long
    Public HorizontalTotalDots As Integer
    Public HorizontalActiveDots As Integer
    Public HorizontalSyncStartDots As Integer
    Public HorizontalSyncEndDots As Integer
    Public VerticalTotalLines As Integer
    Public VerticalActiveLines As Integer
    Public VerticalSyncStartLine As Integer
    Public VerticalSyncEndLine As Integer
    Public DoubleScan As Boolean
    Public PixelRepeat As Integer
    Public HorizontalSyncPositive As Boolean
    Public VerticalSyncPositive As Boolean
End Structure

' Host-side CRT presentation only.  This never changes guest VRAM, VGA timing,
' register state, or the mode exposed to software running inside the machine.
'
' Performance rule: the bezel/overscan surface and Graphics object are rebuilt
' only when the guest's scanout geometry changes. Ordinary frames perform one
' nearest-neighbor raster mapping into the persistent CRT surface; the host
' PictureBox preserves that monitor surface's aspect ratio.
Public NotInheritable Class CrtPresenter
    Implements IDisposable

    Private _surface As Bitmap
    Private _graphics As Graphics
    Private _sourceWidth As Integer = -1
    Private _sourceHeight As Integer = -1
    Private _activeX As Integer
    Private _activeY As Integer
    Private _activeWidth As Integer
    Private _activeHeight As Integer
    Private _timingSignature As String = String.Empty

    Public Function Present(activeFrame As Bitmap,
                            timingInBed As VideoScanoutTiming) As Bitmap
        If activeFrame Is Nothing Then Return Nothing

        Dim timingSignatureInBed As String =
            timingInBed.HorizontalTotalDots.ToString() & ":" &
            timingInBed.HorizontalActiveDots.ToString() & ":" &
            timingInBed.VerticalTotalLines.ToString() & ":" &
            timingInBed.VerticalActiveLines.ToString() & ":" &
            timingInBed.PixelRepeat.ToString()

        If _surface Is Nothing OrElse
           activeFrame.Width <> _sourceWidth OrElse
           activeFrame.Height <> _sourceHeight OrElse
           timingSignatureInBed <> _timingSignature Then
            RebuildSurface(activeFrame.Width, activeFrame.Height, timingInBed)
            _timingSignature = timingSignatureInBed
        End If

        ' Map the sampled active raster onto the 4:3 phosphor area. The card has
        ' already generated repeated pixels/scanlines according to its CRTC; the
        ' monitor does not recognize BIOS modes or reinterpret VRAM.
        _graphics.DrawImage(activeFrame,
                            New Rectangle(_activeX, _activeY, _activeWidth, _activeHeight),
                            0,
                            0,
                            activeFrame.Width,
                            activeFrame.Height,
                            GraphicsUnit.Pixel)
        Return _surface
    End Function

    Private Sub RebuildSurface(sourceWidthInBed As Integer,
                               sourceHeightInBed As Integer,
                               timingInBed As VideoScanoutTiming)
        DisposeSurface()

        sourceWidthInBed = Math.Max(1, sourceWidthInBed)
        sourceHeightInBed = Math.Max(1, sourceHeightInBed)
        _sourceWidth = sourceWidthInBed
        _sourceHeight = sourceHeightInBed

        ' A VGA-era multisync CRT has a 4:3 phosphor face. Expand only one host
        ' dimension so scanout samples are never discarded merely to establish
        ' the monitor's physical geometry.
        _activeWidth = sourceWidthInBed
        _activeHeight = sourceHeightInBed
        If CLng(_activeWidth) * 3L < CLng(_activeHeight) * 4L Then
            _activeWidth = CInt(Math.Ceiling(CDbl(_activeHeight) * 4.0 / 3.0))
        ElseIf CLng(_activeWidth) * 3L > CLng(_activeHeight) * 4L Then
            _activeHeight = CInt(Math.Ceiling(CDbl(_activeWidth) * 3.0 / 4.0))
        End If

        Dim shortest As Integer = Math.Min(_activeWidth, _activeHeight)
        Dim bezel As Integer = Math.Max(8, shortest \ 48)
        Dim overscanX As Integer = Math.Max(16, _activeWidth \ 32)
        Dim overscanY As Integer = Math.Max(16, _activeHeight \ 24)

        ' Keep the guest raster completely unscaled here.  The extra pixels are
        ' only overscan + cabinet edge; this preserves the PictureBox's existing
        ' mode-fitting behavior instead of imposing a second aspect conversion.
        Dim canvasWidth As Integer = _activeWidth + 2 * (bezel + overscanX)
        Dim canvasHeight As Integer = _activeHeight + 2 * (bezel + overscanY)

        If (canvasWidth And 1) <> 0 Then canvasWidth += 1
        If (canvasHeight And 1) <> 0 Then canvasHeight += 1

        _activeX = (canvasWidth - _activeWidth) \ 2
        _activeY = (canvasHeight - _activeHeight) \ 2

        _surface = New Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppPArgb)
        _graphics = Graphics.FromImage(_surface)
        _graphics.CompositingMode = CompositingMode.SourceCopy
        _graphics.CompositingQuality = CompositingQuality.HighSpeed
        _graphics.InterpolationMode = InterpolationMode.NearestNeighbor
        _graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed
        _graphics.SmoothingMode = SmoothingMode.None

        ' Static monitor surround.  It is painted once per mode change, not once
        ' per frame.  The black field inside it is the CRT overscan area.
        _graphics.Clear(Color.FromArgb(24, 24, 24))
        Using blackBrush As New SolidBrush(Color.Black)
            _graphics.FillRectangle(blackBrush,
                                    bezel,
                                    bezel,
                                    canvasWidth - bezel * 2,
                                    canvasHeight - bezel * 2)
        End Using

        Using outerHighlight As New Pen(Color.FromArgb(70, 70, 70)),
              innerShadow As New Pen(Color.FromArgb(8, 8, 8))
            _graphics.DrawRectangle(outerHighlight, 0, 0, canvasWidth - 1, canvasHeight - 1)
            _graphics.DrawRectangle(innerShadow,
                                    bezel - 1,
                                    bezel - 1,
                                    canvasWidth - bezel * 2 + 1,
                                    canvasHeight - bezel * 2 + 1)
        End Using
    End Sub

    Private Sub DisposeSurface()
        If _graphics IsNot Nothing Then
            _graphics.Dispose()
            _graphics = Nothing
        End If
        If _surface IsNot Nothing Then
            _surface.Dispose()
            _surface = Nothing
        End If
        _timingSignature = String.Empty
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeSurface()
        GC.SuppressFinalize(Me)
    End Sub
End Class
