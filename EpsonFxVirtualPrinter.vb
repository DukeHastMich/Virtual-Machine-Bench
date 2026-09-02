Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Drawing.Text
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Threading

' CROMWELL TECHNOLOGIES VIRTUAL PAPER PRINTER
' ===========================================
'
' This file deliberately separates three different layers which are easy to
' accidentally blur together in an emulator:
'
'   1. ParallelPortSpp (LegacyIoPorts.vb) is the PC-side interface card.  It owns
'      378h/278h, STROBE, BUSY, ACK, SELECT, PAPER END, /ERROR, IRQ7/IRQ5, etc.
'
'   2. EpsonFxVirtualPrinter is the peripheral at the far end of the Centronics
'      cable.  It sees only bytes and connector control lines.  It has no access
'      to CPU registers, INT 17h, DOS, Windows, or the host UI.
'
'   3. Virtual paper is a host presentation backend.  The printer marks a page;
'      that page is serialized as PNG and/or a raster-backed PDF.  The guest does
'      not receive a magic "print to PDF" device and cannot bypass the emulated
'      parallel hardware.
'
' The command language is an intentionally useful Epson FX/ESC-P subset aimed at
' 1980s DOS software.  It supports ordinary text formatting and the classic
' eight-dot bit-image modes used by applications that drew boxes, charts, logos,
' banners and screen dumps.  Unknown ESC/P commands are ignored rather than
' treated as printable data, matching the forgiving behavior expected by old
' printer drivers.

Public Enum VirtualPrinterOutputMode
    PdfAndPng = 0
    PdfOnly = 1
    PngOnly = 2
End Enum

Public NotInheritable Class EpsonFxVirtualPrinter
    Implements IParallelPeripheral, IParallelStatusSource, IDisposable

    Private Enum ParserState
        Normal
        EscapeCommand
        FixedParameters
        HorizontalTabs
        VerticalTabs
        GraphicsData
    End Enum

    Private Const PaperWidthInches As Double = 8.5R
    Private Const PaperHeightInches As Double = 11.0R
    Private Const RenderDpi As Integer = 180
    Private Const DefaultIdleFlushMilliseconds As Integer = 8000
    Private Const MaximumCapturedBytes As Integer = 1024 * 1024

    Private Shared _jobSerial As Long

    Private ReadOnly _sync As New Object()
    Private ReadOnly _logicalName As String
    Private ReadOnly _outputRoot As String
    Private ReadOnly _received As New Queue(Of Byte)()
    Private ReadOnly _completedPageFiles As New List(Of String)()
    Private ReadOnly _parameters As New List(Of Byte)()
    Private ReadOnly _horizontalTabColumns As New List(Of Integer)()
    Private ReadOnly _idleTimer As System.Threading.Timer

    Private _droppedBytes As Long
    Private _rawBytesAccepted As Long
    Private _charactersRendered As Long
    Private _graphicsBytesRendered As Long
    Private _pagesRendered As Long
    Private _jobsCompleted As Long

    Private _selectInAsserted As Boolean
    Private _initializeAsserted As Boolean
    Private _autoFeedAsserted As Boolean
    Private _online As Boolean = True
    Private _paperLoaded As Boolean = True
    Private _backendError As String = String.Empty

    Private _parserState As ParserState
    Private _pendingCommand As Integer = -1
    Private _parameterBytesNeeded As Integer
    Private _graphicsBytesRemaining As Integer
    Private _graphicsHorizontalDpi As Double = 60.0R

    ' Mechanical / formatting state.  Positions are kept in inches so ESC/P
    ' spacing remains independent of the host rendering DPI.
    Private _xInches As Double
    Private _yInches As Double
    Private _leftMarginInches As Double
    Private _rightMarginInches As Double
    Private _pageLengthInches As Double
    Private _lineSpacingInches As Double
    Private _cpi As Double
    Private _condensed As Boolean
    Private _permanentDoubleWidth As Boolean
    Private _oneLineDoubleWidth As Boolean
    Private _emphasized As Boolean
    Private _doubleStrike As Boolean
    Private _italic As Boolean
    Private _underline As Boolean
    Private _superscript As Boolean
    Private _subscript As Boolean
    Private _skipPerforationLines As Integer

    Private _pageBitmap As Bitmap
    Private _pageGraphics As Graphics
    Private _pageHasInk As Boolean
    Private _jobDirectory As String
    Private _jobPageNumber As Integer
    Private _lastOutputPath As String = String.Empty
    Private _outputMode As VirtualPrinterOutputMode = VirtualPrinterOutputMode.PdfAndPng
    Private _disposed As Boolean

    Public Sub New(logicalName As String, outputRoot As String)
        If String.IsNullOrWhiteSpace(logicalName) Then Throw New ArgumentException("A printer name is required.", NameOf(logicalName))
        If String.IsNullOrWhiteSpace(outputRoot) Then Throw New ArgumentException("An output directory is required.", NameOf(outputRoot))

        _logicalName = logicalName.Trim()
        _outputRoot = Path.GetFullPath(outputRoot)
        _idleTimer = New System.Threading.Timer(AddressOf IdleFlushCallback, Nothing, Timeout.Infinite, Timeout.Infinite)
        ResetFormattingState(resetVerticalPosition:=True)
    End Sub

    Public ReadOnly Property LogicalName As String
        Get
            Return _logicalName
        End Get
    End Property

    Public ReadOnly Property OutputDirectory As String
        Get
            Return Path.Combine(_outputRoot, _logicalName)
        End Get
    End Property

    Public Property OutputMode As VirtualPrinterOutputMode
        Get
            SyncLock _sync
                Return _outputMode
            End SyncLock
        End Get
        Set(value As VirtualPrinterOutputMode)
            SyncLock _sync
                _outputMode = value
            End SyncLock
        End Set
    End Property

    ' ONLINE and PAPER LOADED are physical printer controls/state.  The parallel
    ' port polls them through IParallelStatusSource, so changing either property
    ' changes the guest-visible status register without any BIOS shortcut.
    Public Property Online As Boolean
        Get
            SyncLock _sync
                Return _online
            End SyncLock
        End Get
        Set(value As Boolean)
            SyncLock _sync
                _online = value
            End SyncLock
        End Set
    End Property

    Public Property PaperLoaded As Boolean
        Get
            SyncLock _sync
                Return _paperLoaded
            End SyncLock
        End Get
        Set(value As Boolean)
            SyncLock _sync
                _paperLoaded = value
            End SyncLock
        End Set
    End Property

    Public ReadOnly Property Busy As Boolean Implements IParallelStatusSource.Busy
        Get
            SyncLock _sync
                Return _initializeAsserted OrElse Not _selectInAsserted OrElse Not _online OrElse Not _paperLoaded OrElse Not String.IsNullOrEmpty(_backendError)
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property PaperEnd As Boolean Implements IParallelStatusSource.PaperEnd
        Get
            SyncLock _sync
                Return Not _paperLoaded
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property Selected As Boolean Implements IParallelStatusSource.Selected
        Get
            SyncLock _sync
                Return _online AndAlso _selectInAsserted
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property ErrorOk As Boolean Implements IParallelStatusSource.ErrorOk
        Get
            SyncLock _sync
                Return _online AndAlso _paperLoaded AndAlso String.IsNullOrEmpty(_backendError)
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property ReceivedBytes As Byte()
        Get
            SyncLock _sync
                Return _received.ToArray()
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property DroppedBytes As Long
        Get
            SyncLock _sync
                Return _droppedBytes
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property SelectInAsserted As Boolean
        Get
            SyncLock _sync
                Return _selectInAsserted
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property InitializeAsserted As Boolean
        Get
            SyncLock _sync
                Return _initializeAsserted
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property AutoFeedAsserted As Boolean
        Get
            SyncLock _sync
                Return _autoFeedAsserted
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property LastOutputPath As String
        Get
            SyncLock _sync
                Return _lastOutputPath
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property BackendError As String
        Get
            SyncLock _sync
                Return _backendError
            End SyncLock
        End Get
    End Property

    Public Sub ClearDiagnosticCapture()
        SyncLock _sync
            _received.Clear()
            _droppedBytes = 0
        End SyncLock
    End Sub

    Public Function AcceptByte(value As Byte) As Boolean Implements IParallelPeripheral.AcceptByte
        SyncLock _sync
            ThrowIfDisposed()

            ' A Centronics printer cannot accept data while deselected, reset is
            ' being held, paper is out, or its backend has entered an error state.
            If Not _selectInAsserted OrElse _initializeAsserted OrElse Not _online OrElse Not _paperLoaded OrElse Not String.IsNullOrEmpty(_backendError) Then
                Return False
            End If

            If _received.Count >= MaximumCapturedBytes Then
                _received.Dequeue()
                _droppedBytes += 1
            End If
            _received.Enqueue(value)
            _rawBytesAccepted += 1

            Try
                ConsumeByte(value)
                ArmIdleFlush()
                Return True
            Catch ex As Exception
                ' A host filesystem/GDI failure is represented as a printer error,
                ' not as an exception escaping into the emulated motherboard.
                _backendError = ex.GetType().Name & ": " & ex.Message
                Return False
            End Try
        End SyncLock
    End Function

    Public Sub ControlLinesChanged(selectIn As Boolean, initialize As Boolean, autoFeed As Boolean) Implements IParallelPeripheral.ControlLinesChanged
        SyncLock _sync
            ThrowIfDisposed()
            Dim initializeRisingEdge As Boolean = initialize AndAlso Not _initializeAsserted
            _selectInAsserted = selectIn
            _initializeAsserted = initialize
            _autoFeedAsserted = autoFeed

            If initializeRisingEdge Then
                ' /INIT resets printer electronics and formatting, but it cannot
                ' erase ink already deposited on the current sheet.  Keep the
                ' physical vertical paper position while returning modes/carriage
                ' to power-on defaults.
                ResetFormattingState(resetVerticalPosition:=False)
            End If
        End SyncLock
    End Sub

    Public Function FlushJob() As String
        SyncLock _sync
            ThrowIfDisposed()
            _idleTimer.Change(Timeout.Infinite, Timeout.Infinite)
            Return FinalizeJobLocked(ejectCurrentPage:=True)
        End SyncLock
    End Function

    Public Sub EjectPage()
        SyncLock _sync
            ThrowIfDisposed()
            CommitCurrentPageLocked(forceBlankPage:=True)
            ResetPaperPositionForNewPage()
            ArmIdleFlush()
        End SyncLock
    End Sub

    Public Sub CancelCurrentJob()
        SyncLock _sync
            ThrowIfDisposed()
            _idleTimer.Change(Timeout.Infinite, Timeout.Infinite)
            DisposeCurrentPage()
            If Not String.IsNullOrEmpty(_jobDirectory) AndAlso Directory.Exists(_jobDirectory) Then
                Try
                    Directory.Delete(_jobDirectory, recursive:=True)
                Catch
                    ' Cancellation is best-effort.  A locked page file should not
                    ' crash or electrically fault the emulated printer.
                End Try
            End If
            _completedPageFiles.Clear()
            _jobDirectory = Nothing
            _jobPageNumber = 0
            ResetPaperPositionForNewPage()
        End SyncLock
    End Sub

    Public Sub ClearBackendError()
        SyncLock _sync
            _backendError = String.Empty
        End SyncLock
    End Sub

    Public Function DiagnosticText() As String
        SyncLock _sync
            Dim modeText As String = _outputMode.ToString()
            Dim errorText As String = If(String.IsNullOrEmpty(_backendError), "none", _backendError)
            Return $"{_logicalName} Epson FX virtual printer: online={_online} paper={If(_paperLoaded, "loaded", "OUT")} " &
                   $"select-in={_selectInAsserted} init={_initializeAsserted} autofeed={_autoFeedAsserted} " &
                   $"x={_xInches:0.000}in y={_yInches:0.000}in cpi={EffectiveCpi():0.##} line={_lineSpacingInches:0.####}in " &
                   $"bytes={_rawBytesAccepted} text={_charactersRendered} gfx={_graphicsBytesRendered} pages={_pagesRendered} jobs={_jobsCompleted} " &
                   $"output={modeText} backend-error={errorText}"
        End SyncLock
    End Function

    Private Sub ConsumeByte(value As Byte)
        Select Case _parserState
            Case ParserState.GraphicsData
                RenderGraphicsByte(value)
                _graphicsBytesRemaining -= 1
                If _graphicsBytesRemaining <= 0 Then _parserState = ParserState.Normal
                Return

            Case ParserState.HorizontalTabs
                If value = 0 Then
                    _horizontalTabColumns.Sort()
                    _parserState = ParserState.Normal
                ElseIf value <= 137 Then
                    If Not _horizontalTabColumns.Contains(CInt(value)) Then _horizontalTabColumns.Add(CInt(value))
                End If
                Return

            Case ParserState.VerticalTabs
                ' Vertical tab channels are uncommon in PC software.  Consume the
                ' terminated definition correctly so its bytes never print as text;
                ' ordinary VT still advances one configured line below.
                If value = 0 Then _parserState = ParserState.Normal
                Return

            Case ParserState.FixedParameters
                _parameters.Add(value)
                If _parameters.Count >= _parameterBytesNeeded Then ExecutePendingCommand()
                Return

            Case ParserState.EscapeCommand
                _parserState = ParserState.Normal
                ExecuteEscapeCommand(value)
                Return
        End Select

        Select Case value
            Case &H0
                Return
            Case &H7
                Return ' BEL: no host beep; a printer bell is not PC speaker audio.
            Case &H8
                Backspace()
            Case &H9
                HorizontalTab()
            Case &HA
                LineFeed()
            Case &HB
                LineFeed()
            Case &HC
                FormFeed()
            Case &HD
                CarriageReturn()
                If _autoFeedAsserted Then LineFeed()
            Case &HE
                _oneLineDoubleWidth = True
            Case &HF
                _condensed = True
            Case &H12
                _condensed = False
            Case &H14
                _oneLineDoubleWidth = False
            Case &H1B
                _parserState = ParserState.EscapeCommand
            Case &H20 To &HFF
                If value <> &H7F Then PrintCharacter(value)
        End Select
    End Sub

    Private Sub ExecuteEscapeCommand(command As Byte)
        Select Case command
            Case AscW("@"c)
                ResetFormattingState(resetVerticalPosition:=False)
            Case AscW("E"c)
                _emphasized = True
            Case AscW("F"c)
                _emphasized = False
            Case AscW("G"c)
                _doubleStrike = True
            Case AscW("H"c)
                _doubleStrike = False
            Case AscW("4"c)
                _italic = True
            Case AscW("5"c)
                _italic = False
            Case AscW("P"c)
                _cpi = 10.0R
            Case AscW("M"c)
                _cpi = 12.0R
            Case AscW("g"c)
                _cpi = 15.0R
            Case AscW("0"c)
                _lineSpacingInches = 1.0R / 8.0R
            Case AscW("1"c)
                _lineSpacingInches = 7.0R / 72.0R
            Case AscW("2"c)
                _lineSpacingInches = 1.0R / 6.0R
            Case AscW("O"c)
                _skipPerforationLines = 0
            Case AscW("T"c)
                _superscript = False
                _subscript = False
            Case AscW("D"c)
                _horizontalTabColumns.Clear()
                _parserState = ParserState.HorizontalTabs
            Case AscW("B"c), AscW("b"c)
                _parserState = ParserState.VerticalTabs
            Case AscW("!"c), AscW("-"c), AscW("W"c), AscW("S"c), AscW("U"c), AscW("x"c), AscW("R"c),
                 AscW("l"c), AscW("Q"c), AscW("3"c), AscW("A"c), AscW("J"c), AscW("N"c), AscW("C"c),
                 AscW("+"c), AscW("p"c), AscW("t"c), AscW("k"c), AscW("q"c), AscW("r"c), AscW("a"c), AscW("%"c)
                BeginFixedParameters(command, 1)
            Case AscW("$"c), AscW("\"c), AscW("?"c)
                BeginFixedParameters(command, 2)
            Case AscW("*"c)
                BeginFixedParameters(command, 3) ' mode, nL, nH
            Case AscW("K"c), AscW("L"c), AscW("Y"c), AscW("Z"c)
                BeginFixedParameters(command, 2) ' legacy bit-image nL/nH
            Case Else
                ' Unknown zero-parameter command: deliberately ignored.
        End Select
    End Sub

    Private Sub BeginFixedParameters(command As Integer, count As Integer)
        _pendingCommand = command
        _parameterBytesNeeded = count
        _parameters.Clear()
        _parserState = ParserState.FixedParameters
    End Sub

    Private Sub ExecutePendingCommand()
        Dim command As Integer = _pendingCommand
        Dim p() As Byte = _parameters.ToArray()
        _pendingCommand = -1
        _parameters.Clear()
        _parserState = ParserState.Normal

        Select Case command
            Case AscW("!"c)
                ' ESC ! is the workhorse "master select" used by many DOS and
                ' Windows 3.x Epson drivers.  Bit 1 (proportional) is consumed
                ' but intentionally rendered at fixed pitch by this FX profile.
                _cpi = If((p(0) And &H1) <> 0, 12.0R, 10.0R)
                _condensed = (p(0) And &H4) <> 0
                _emphasized = (p(0) And &H8) <> 0
                _doubleStrike = (p(0) And &H10) <> 0
                _permanentDoubleWidth = (p(0) And &H20) <> 0
                _italic = (p(0) And &H40) <> 0
                _underline = (p(0) And &H80) <> 0
            Case AscW("-"c)
                _underline = (p(0) And 1) <> 0
            Case AscW("W"c)
                _permanentDoubleWidth = (p(0) And 1) <> 0
            Case AscW("S"c)
                _superscript = (p(0) And 1) = 0
                _subscript = Not _superscript
            Case AscW("U"c), AscW("x"c), AscW("R"c)
                ' Unidirectional/NLQ/international-set selection affect print
                ' quality or glyph variants, not the electrical protocol.  Their
                ' parameter is consumed so following data stays synchronized.
            Case AscW("l"c)
                _leftMarginInches = Clamp(CDbl(p(0)) / Math.Max(1.0R, EffectiveCpi()), 0.0R, PaperWidthInches - 0.25R)
                _xInches = Math.Max(_xInches, _leftMarginInches)
            Case AscW("Q"c)
                Dim proposed As Double = CDbl(p(0)) / Math.Max(1.0R, EffectiveCpi())
                _rightMarginInches = Clamp(proposed, _leftMarginInches + 0.25R, PaperWidthInches)
            Case AscW("+"c)
                _lineSpacingInches = Math.Max(1.0R / 360.0R, CDbl(p(0)) / 360.0R)
            Case AscW("p"c), AscW("t"c), AscW("k"c), AscW("q"c), AscW("r"c), AscW("a"c), AscW("%"c), AscW("?"c)
                ' Consume supported-driver setup parameters that do not alter
                ' this fixed-black FX paper model (proportional/typeface/table,
                ' color/justification/user-RAM selection and graphics remap).
            Case AscW("3"c)
                _lineSpacingInches = Math.Max(1.0R / 216.0R, CDbl(p(0)) / 216.0R)
            Case AscW("A"c)
                _lineSpacingInches = Math.Max(1.0R / 72.0R, CDbl(p(0)) / 72.0R)
            Case AscW("J"c)
                AdvancePaper(CDbl(p(0)) / 216.0R, carriageReturnAfter:=False)
            Case AscW("N"c)
                _skipPerforationLines = CInt(p(0))
            Case AscW("C"c)
                If p(0) = 0 Then
                    BeginFixedParameters(AscW("c"c), 1) ' internal second half of ESC C 0 n
                Else
                    _pageLengthInches = Clamp(CDbl(p(0)) * _lineSpacingInches, 1.0R, PaperHeightInches)
                End If
            Case AscW("c"c)
                _pageLengthInches = Clamp(CDbl(p(0)), 1.0R, PaperHeightInches)
            Case AscW("$"c)
                Dim units As Integer = CInt(p(0)) Or (CInt(p(1)) << 8)
                _xInches = Clamp(_leftMarginInches + CDbl(units) / 60.0R, _leftMarginInches, _rightMarginInches)
            Case AscW("\"c)
                Dim unsignedValue As Integer = CInt(p(0)) Or (CInt(p(1)) << 8)
                Dim signedValue As Integer = If(unsignedValue >= &H8000, unsignedValue - &H10000, unsignedValue)
                _xInches = Clamp(_xInches + CDbl(signedValue) / 120.0R, _leftMarginInches, _rightMarginInches)
            Case AscW("*"c)
                ConfigureGraphicsMode(CInt(p(0)))
                BeginGraphics(CInt(p(1)) Or (CInt(p(2)) << 8))
            Case AscW("K"c), AscW("L"c), AscW("Y"c), AscW("Z"c)
                Select Case command
                    Case AscW("K"c) : ConfigureGraphicsMode(0)
                    Case AscW("L"c) : ConfigureGraphicsMode(1)
                    Case AscW("Y"c) : ConfigureGraphicsMode(2)
                    Case AscW("Z"c) : ConfigureGraphicsMode(3)
                End Select
                BeginGraphics(CInt(p(0)) Or (CInt(p(1)) << 8))
        End Select
    End Sub

    Private Sub ConfigureGraphicsMode(mode As Integer)
        ' Epson 9-pin bit-image horizontal densities.  Modes 2 and 3 are the
        ' high-speed/quad variants; the host raster is permitted to place every
        ' requested column even though a real head may suppress adjacent dots.
        Select Case mode And &HFF
            Case 0 : _graphicsHorizontalDpi = 60.0R
            Case 1 : _graphicsHorizontalDpi = 120.0R
            Case 2 : _graphicsHorizontalDpi = 120.0R
            Case 3 : _graphicsHorizontalDpi = 240.0R
            Case 4 : _graphicsHorizontalDpi = 80.0R
            Case 5 : _graphicsHorizontalDpi = 72.0R
            Case 6 : _graphicsHorizontalDpi = 90.0R
            Case Else : _graphicsHorizontalDpi = 60.0R
        End Select
    End Sub

    Private Sub BeginGraphics(byteCount As Integer)
        _graphicsBytesRemaining = Math.Max(0, byteCount)
        If _graphicsBytesRemaining > 0 Then _parserState = ParserState.GraphicsData
    End Sub

    Private Sub RenderGraphicsByte(value As Byte)
        EnsurePage()
        Dim dotDiameterInches As Double = 1.0R / 90.0R
        Dim dotSizePixels As Single = CSng(Math.Max(1.0R, dotDiameterInches * RenderDpi))
        Dim xPixels As Single = InchesToPixels(_xInches)

        For bit As Integer = 0 To 7
            If (value And CByte(&H80 >> bit)) <> 0 Then
                Dim y As Double = _yInches + CDbl(bit) / 72.0R
                If y < EffectivePageBottomInches() Then
                    Dim yPixels As Single = InchesToPixels(y)
                    _pageGraphics.FillEllipse(Brushes.Black, xPixels, yPixels, dotSizePixels, dotSizePixels)
                    _pageHasInk = True
                End If
            End If
        Next

        _xInches += 1.0R / _graphicsHorizontalDpi
        _graphicsBytesRendered += 1
        If _xInches > _rightMarginInches Then _xInches = _rightMarginInches
    End Sub

    Private Sub PrintCharacter(value As Byte)
        Dim charWidth As Double = CharacterWidthInches()
        If _xInches + charWidth > _rightMarginInches + 0.0001R Then
            CarriageReturn()
            LineFeed()
        End If

        EnsurePage()
        Dim character As Char = DecodePcCharacter(value)
        Dim effectiveCpiValue As Double = EffectiveCpi()
        Dim fontPoints As Single = CSng(120.0R / Math.Max(5.0R, effectiveCpiValue))
        If _superscript OrElse _subscript Then fontPoints *= 0.67F

        Dim style As FontStyle = FontStyle.Regular
        If _emphasized Then style = style Or FontStyle.Bold
        If _italic Then style = style Or FontStyle.Italic
        If _underline Then style = style Or FontStyle.Underline

        Using fontInBed As New Font(FontFamily.GenericMonospace, fontPoints, style, GraphicsUnit.Point)
            Dim xPixels As Single = InchesToPixels(_xInches)
            Dim yOffset As Double = 0.0R
            If _superscript Then yOffset = -0.03R
            If _subscript Then yOffset = 0.05R
            Dim yPixels As Single = InchesToPixels(_yInches + yOffset)
            Dim doubleWidth As Boolean = _permanentDoubleWidth OrElse _oneLineDoubleWidth

            Dim savedState As GraphicsState = _pageGraphics.Save()
            If doubleWidth Then
                _pageGraphics.TranslateTransform(xPixels, 0.0F)
                _pageGraphics.ScaleTransform(2.0F, 1.0F)
                xPixels = 0.0F
            End If

            _pageGraphics.DrawString(character.ToString(), fontInBed, Brushes.Black, xPixels, yPixels, StringFormat.GenericTypographic)
            If _doubleStrike Then
                _pageGraphics.DrawString(character.ToString(), fontInBed, Brushes.Black, xPixels, yPixels + 1.0F, StringFormat.GenericTypographic)
            End If
            _pageGraphics.Restore(savedState)
        End Using

        _xInches += charWidth
        _pageHasInk = True
        _charactersRendered += 1
    End Sub

    Private Sub CarriageReturn()
        _xInches = _leftMarginInches
    End Sub

    Private Sub Backspace()
        _xInches = Math.Max(_leftMarginInches, _xInches - CharacterWidthInches())
    End Sub

    Private Sub HorizontalTab()
        Dim currentColumn As Double = (_xInches - _leftMarginInches) * EffectiveCpi()
        For Each tabColumn As Integer In _horizontalTabColumns
            If tabColumn > currentColumn + 0.01R Then
                _xInches = Math.Min(_rightMarginInches, _leftMarginInches + CDbl(tabColumn) / EffectiveCpi())
                Return
            End If
        Next
        ' If all programmed tabs are behind the carriage, remain at the margin.
    End Sub

    Private Sub LineFeed()
        AdvancePaper(_lineSpacingInches, carriageReturnAfter:=False)
        _oneLineDoubleWidth = False
    End Sub

    Private Sub AdvancePaper(distanceInches As Double, carriageReturnAfter As Boolean)
        _yInches += Math.Max(0.0R, distanceInches)
        If carriageReturnAfter Then CarriageReturn()
        If _yInches >= EffectivePageBottomInches() Then
            CommitCurrentPageLocked(forceBlankPage:=False)
            ResetPaperPositionForNewPage()
        End If
    End Sub

    Private Sub FormFeed()
        CommitCurrentPageLocked(forceBlankPage:=True)
        ResetPaperPositionForNewPage()
        _oneLineDoubleWidth = False
    End Sub

    Private Function EffectivePageBottomInches() As Double
        Dim bottom As Double = Math.Min(PaperHeightInches, _pageLengthInches)
        If _skipPerforationLines > 0 Then bottom -= CDbl(_skipPerforationLines) * _lineSpacingInches
        Return Math.Max(0.5R, bottom)
    End Function

    Private Function EffectiveCpi() As Double
        If Not _condensed Then Return _cpi
        If _cpi >= 11.5R Then Return 20.0R
        Return 17.14R
    End Function

    Private Function CharacterWidthInches() As Double
        Dim width As Double = 1.0R / Math.Max(1.0R, EffectiveCpi())
        If _permanentDoubleWidth OrElse _oneLineDoubleWidth Then width *= 2.0R
        Return width
    End Function

    Private Sub ResetFormattingState(resetVerticalPosition As Boolean)
        _parserState = ParserState.Normal
        _pendingCommand = -1
        _parameterBytesNeeded = 0
        _parameters.Clear()
        _graphicsBytesRemaining = 0
        _graphicsHorizontalDpi = 60.0R

        _leftMarginInches = 0.25R
        _rightMarginInches = 8.25R
        _pageLengthInches = PaperHeightInches
        _lineSpacingInches = 1.0R / 6.0R
        _cpi = 10.0R
        _condensed = False
        _permanentDoubleWidth = False
        _oneLineDoubleWidth = False
        _emphasized = False
        _doubleStrike = False
        _italic = False
        _underline = False
        _superscript = False
        _subscript = False
        _skipPerforationLines = 0

        _horizontalTabColumns.Clear()
        For column As Integer = 8 To 80 Step 8
            _horizontalTabColumns.Add(column)
        Next

        _xInches = _leftMarginInches
        If resetVerticalPosition Then _yInches = 0.0R
    End Sub

    Private Sub ResetPaperPositionForNewPage()
        DisposeCurrentPage()
        _xInches = _leftMarginInches
        _yInches = 0.0R
        _pageHasInk = False
    End Sub

    Private Sub EnsurePage()
        If _pageBitmap IsNot Nothing Then Return

        Dim width As Integer = CInt(Math.Round(PaperWidthInches * RenderDpi))
        Dim height As Integer = CInt(Math.Round(PaperHeightInches * RenderDpi))
        _pageBitmap = New Bitmap(width, height, PixelFormat.Format24bppRgb)
        _pageBitmap.SetResolution(RenderDpi, RenderDpi)
        _pageGraphics = Graphics.FromImage(_pageBitmap)
        _pageGraphics.Clear(Color.White)
        _pageGraphics.SmoothingMode = SmoothingMode.None
        _pageGraphics.InterpolationMode = InterpolationMode.NearestNeighbor
        _pageGraphics.PixelOffsetMode = PixelOffsetMode.Half
        _pageGraphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit
    End Sub

    Private Sub DisposeCurrentPage()
        If _pageGraphics IsNot Nothing Then
            _pageGraphics.Dispose()
            _pageGraphics = Nothing
        End If
        If _pageBitmap IsNot Nothing Then
            _pageBitmap.Dispose()
            _pageBitmap = Nothing
        End If
        _pageHasInk = False
    End Sub

    Private Sub EnsureJobDirectoryLocked()
        If Not String.IsNullOrEmpty(_jobDirectory) Then Return
        Dim serial As Long = Interlocked.Increment(_jobSerial)
        Dim stamp As String = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")
        _jobDirectory = Path.Combine(OutputDirectory, $"{stamp}_job-{serial:0000}")
        Directory.CreateDirectory(_jobDirectory)
        _jobPageNumber = 0
    End Sub

    Private Sub CommitCurrentPageLocked(forceBlankPage As Boolean)
        If Not _pageHasInk AndAlso Not forceBlankPage Then Return
        EnsurePage()
        EnsureJobDirectoryLocked()

        _jobPageNumber += 1
        Dim pagePath As String = Path.Combine(_jobDirectory, $"page-{_jobPageNumber:000}.png")
        _pageGraphics.Flush()
        _pageBitmap.Save(pagePath, ImageFormat.Png)
        _completedPageFiles.Add(pagePath)
        _pagesRendered += 1
        DisposeCurrentPage()
    End Sub

    Private Sub ArmIdleFlush()
        ' A physical Centronics link has no formal "end job" signal.  Form-feed
        ' ends a sheet, not necessarily the document.  The host spool backend uses
        ' a conservative silence window to close COPY-to-PRN jobs that omit FF.
        ' The UI also exposes an immediate Flush command for deterministic tests.
        _idleTimer.Change(DefaultIdleFlushMilliseconds, Timeout.Infinite)
    End Sub

    Private Sub IdleFlushCallback(state As Object)
        SyncLock _sync
            If _disposed Then Return
            Try
                FinalizeJobLocked(ejectCurrentPage:=True)
            Catch ex As Exception
                _backendError = ex.GetType().Name & ": " & ex.Message
            End Try
        End SyncLock
    End Sub

    Private Function FinalizeJobLocked(ejectCurrentPage As Boolean) As String
        If ejectCurrentPage AndAlso _pageHasInk Then
            CommitCurrentPageLocked(forceBlankPage:=False)
            ResetPaperPositionForNewPage()
        End If

        If _completedPageFiles.Count = 0 Then Return String.Empty
        EnsureJobDirectoryLocked()

        Dim pdfPath As String = Path.Combine(_jobDirectory, "print-job.pdf")
        If _outputMode = VirtualPrinterOutputMode.PdfAndPng OrElse _outputMode = VirtualPrinterOutputMode.PdfOnly Then
            RasterPdfWriter.WritePdf(_completedPageFiles, pdfPath, PaperWidthInches, PaperHeightInches)
        End If

        If _outputMode = VirtualPrinterOutputMode.PdfOnly Then
            For Each pagePath As String In _completedPageFiles
                Try
                    File.Delete(pagePath)
                Catch
                End Try
            Next
        End If

        If _outputMode = VirtualPrinterOutputMode.PngOnly Then
            _lastOutputPath = _jobDirectory
        Else
            _lastOutputPath = pdfPath
        End If

        _jobsCompleted += 1
        _completedPageFiles.Clear()
        _jobDirectory = Nothing
        _jobPageNumber = 0
        Return _lastOutputPath
    End Function

    Private Shared Function InchesToPixels(value As Double) As Single
        Return CSng(value * RenderDpi)
    End Function

    Private Shared Function Clamp(value As Double, minimum As Double, maximum As Double) As Double
        If value < minimum Then Return minimum
        If value > maximum Then Return maximum
        Return value
    End Function

    ' CP437 is the natural byte-to-glyph bridge for an IBM PC printer path.  The
    ' 128-entry high-half table avoids a runtime dependency on code-page provider
    ' packages while preserving DOS box-drawing and accented characters.
    Private Shared Function DecodePcCharacter(value As Byte) As Char
        If value < &H80 Then Return ChrW(value)
        Return Cp437High(CInt(value) - &H80)
    End Function

    Private Shared ReadOnly Cp437High As Char() = {
        ChrW(&HC7), ChrW(&HFC), ChrW(&HE9), ChrW(&HE2), ChrW(&HE4), ChrW(&HE0), ChrW(&HE5), ChrW(&HE7),
        ChrW(&HEA), ChrW(&HEB), ChrW(&HE8), ChrW(&HEF), ChrW(&HEE), ChrW(&HEC), ChrW(&HC4), ChrW(&HC5),
        ChrW(&HC9), ChrW(&HE6), ChrW(&HC6), ChrW(&HF4), ChrW(&HF6), ChrW(&HF2), ChrW(&HFB), ChrW(&HF9),
        ChrW(&HFF), ChrW(&HD6), ChrW(&HDC), ChrW(&HA2), ChrW(&HA3), ChrW(&HA5), ChrW(&H20A7), ChrW(&H192),
        ChrW(&HE1), ChrW(&HED), ChrW(&HF3), ChrW(&HFA), ChrW(&HF1), ChrW(&HD1), ChrW(&HAA), ChrW(&HBA),
        ChrW(&HBF), ChrW(&H2310), ChrW(&HAC), ChrW(&HBD), ChrW(&HBC), ChrW(&HA1), ChrW(&HAB), ChrW(&HBB),
        ChrW(&H2591), ChrW(&H2592), ChrW(&H2593), ChrW(&H2502), ChrW(&H2524), ChrW(&H2561), ChrW(&H2562), ChrW(&H2556),
        ChrW(&H2555), ChrW(&H2563), ChrW(&H2551), ChrW(&H2557), ChrW(&H255D), ChrW(&H255C), ChrW(&H255B), ChrW(&H2510),
        ChrW(&H2514), ChrW(&H2534), ChrW(&H252C), ChrW(&H251C), ChrW(&H2500), ChrW(&H253C), ChrW(&H255E), ChrW(&H255F),
        ChrW(&H255A), ChrW(&H2554), ChrW(&H2569), ChrW(&H2566), ChrW(&H2560), ChrW(&H2550), ChrW(&H256C), ChrW(&H2567),
        ChrW(&H2568), ChrW(&H2564), ChrW(&H2565), ChrW(&H2559), ChrW(&H2558), ChrW(&H2552), ChrW(&H2553), ChrW(&H256B),
        ChrW(&H256A), ChrW(&H2518), ChrW(&H250C), ChrW(&H2588), ChrW(&H2584), ChrW(&H258C), ChrW(&H2590), ChrW(&H2580),
        ChrW(&H3B1), ChrW(&HDF), ChrW(&H393), ChrW(&H3C0), ChrW(&H3A3), ChrW(&H3C3), ChrW(&HB5), ChrW(&H3C4),
        ChrW(&H3A6), ChrW(&H398), ChrW(&H3A9), ChrW(&H3B4), ChrW(&H221E), ChrW(&H3C6), ChrW(&H3B5), ChrW(&H2229),
        ChrW(&H2261), ChrW(&HB1), ChrW(&H2265), ChrW(&H2264), ChrW(&H2320), ChrW(&H2321), ChrW(&HF7), ChrW(&H2248),
        ChrW(&HB0), ChrW(&H2219), ChrW(&HB7), ChrW(&H221A), ChrW(&H207F), ChrW(&HB2), ChrW(&H25A0), ChrW(&HA0)
    }

    Private Sub ThrowIfDisposed()
        If _disposed Then Throw New ObjectDisposedException(NameOf(EpsonFxVirtualPrinter))
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        SyncLock _sync
            If _disposed Then Return
            _idleTimer.Change(Timeout.Infinite, Timeout.Infinite)
            Try
                FinalizeJobLocked(ejectCurrentPage:=True)
            Catch
                ' Shutdown must not be prevented by a failed print backend.
            End Try
            DisposeCurrentPage()
            _disposed = True
        End SyncLock
        _idleTimer.Dispose()
    End Sub
End Class

' Minimal self-contained PDF 1.4 writer
' ------------------------------------
' No PDF library is required by the emulator.  Each virtual sheet is embedded as
' an 8-bit grayscale Flate-compressed image and painted exactly once on a PDF
' page.  This keeps the renderer deterministic: the PNG and PDF versions derive
' from the same marked-paper raster and cannot disagree about ESC/P layout.
Friend NotInheritable Class RasterPdfWriter
    Private Sub New()
    End Sub

    Public Shared Sub WritePdf(pageFiles As IList(Of String), outputPath As String, paperWidthInches As Double, paperHeightInches As Double)
        If pageFiles Is Nothing OrElse pageFiles.Count = 0 Then Throw New ArgumentException("At least one page is required.", NameOf(pageFiles))
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)))

        Dim pageCount As Integer = pageFiles.Count
        Dim objectCount As Integer = 2 + pageCount * 3
        Dim offsets(objectCount) As Long

        Using streamInBed As New FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read)
            WriteAscii(streamInBed, "%PDF-1.4" & vbLf)
            Dim binaryCommentInBed As Byte() = {&H25, &HE2, &HE3, &HCF, &HD3, &HA}
            streamInBed.Write(binaryCommentInBed, 0, binaryCommentInBed.Length)

            offsets(1) = streamInBed.Position
            WriteAscii(streamInBed, "1 0 obj" & vbLf & "<< /Type /Catalog /Pages 2 0 R >>" & vbLf & "endobj" & vbLf)

            Dim kids As New StringBuilder()
            For pageIndex As Integer = 0 To pageCount - 1
                Dim pageObject As Integer = 3 + pageIndex * 3
                If kids.Length > 0 Then kids.Append(" ")
                kids.Append(pageObject).Append(" 0 R")
            Next

            offsets(2) = streamInBed.Position
            WriteAscii(streamInBed, $"2 0 obj{vbLf}<< /Type /Pages /Count {pageCount} /Kids [{kids}] >>{vbLf}endobj{vbLf}")

            Dim widthPoints As Double = paperWidthInches * 72.0R
            Dim heightPoints As Double = paperHeightInches * 72.0R

            For pageIndex As Integer = 0 To pageCount - 1
                Dim pageObject As Integer = 3 + pageIndex * 3
                Dim imageObject As Integer = pageObject + 1
                Dim contentObject As Integer = pageObject + 2
                Dim imageName As String = "Im" & pageIndex.ToString()

                Dim pixelWidth As Integer
                Dim pixelHeight As Integer
                Dim compressedPixels As Byte() = LoadAndCompressGrayPage(pageFiles(pageIndex), pixelWidth, pixelHeight)

                offsets(pageObject) = streamInBed.Position
                WriteAscii(streamInBed,
                           $"{pageObject} 0 obj{vbLf}" &
                           $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {widthPoints:0.###} {heightPoints:0.###}] " &
                           $"/Resources << /XObject << /{imageName} {imageObject} 0 R >> >> /Contents {contentObject} 0 R >>{vbLf}" &
                           $"endobj{vbLf}")

                offsets(imageObject) = streamInBed.Position
                WriteAscii(streamInBed,
                           $"{imageObject} 0 obj{vbLf}" &
                           $"<< /Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} " &
                           $"/ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode /Length {compressedPixels.Length} >>{vbLf}" &
                           $"stream{vbLf}")
                streamInBed.Write(compressedPixels, 0, compressedPixels.Length)
                WriteAscii(streamInBed, vbLf & "endstream" & vbLf & "endobj" & vbLf)

                Dim content As String = $"q {widthPoints:0.###} 0 0 {heightPoints:0.###} 0 0 cm /{imageName} Do Q{vbLf}"
                Dim contentBytes As Byte() = Encoding.ASCII.GetBytes(content)
                offsets(contentObject) = streamInBed.Position
                WriteAscii(streamInBed, $"{contentObject} 0 obj{vbLf}<< /Length {contentBytes.Length} >>{vbLf}stream{vbLf}")
                streamInBed.Write(contentBytes, 0, contentBytes.Length)
                WriteAscii(streamInBed, "endstream" & vbLf & "endobj" & vbLf)
            Next

            Dim xrefOffset As Long = streamInBed.Position
            WriteAscii(streamInBed, $"xref{vbLf}0 {objectCount + 1}{vbLf}")
            WriteAscii(streamInBed, "0000000000 65535 f " & vbLf)
            For objectNumber As Integer = 1 To objectCount
                WriteAscii(streamInBed, offsets(objectNumber).ToString("0000000000") & " 00000 n " & vbLf)
            Next
            WriteAscii(streamInBed,
                       $"trailer{vbLf}<< /Size {objectCount + 1} /Root 1 0 R >>{vbLf}" &
                       $"startxref{vbLf}{xrefOffset}{vbLf}%%EOF{vbLf}")
        End Using
    End Sub

    Private Shared Function LoadAndCompressGrayPage(path As String, ByRef width As Integer, ByRef height As Integer) As Byte()
        Using source As New Bitmap(path)
            width = source.Width
            height = source.Height
            Using rgb As New Bitmap(width, height, PixelFormat.Format24bppRgb)
                Using g As Graphics = Graphics.FromImage(rgb)
                    g.Clear(Color.White)
                    g.DrawImageUnscaled(source, 0, 0)
                End Using

                Dim rect As New Rectangle(0, 0, width, height)
                Dim data As BitmapData = rgb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
                Try
                    Dim gray(width * height - 1) As Byte
                    Dim stride As Integer = Math.Abs(data.Stride)
                    Dim row(stride - 1) As Byte
                    For y As Integer = 0 To height - 1
                        Dim rowPtr As IntPtr = IntPtr.Add(data.Scan0, y * data.Stride)
                        System.Runtime.InteropServices.Marshal.Copy(rowPtr, row, 0, stride)
                        Dim grayOffset As Integer = y * width
                        For x As Integer = 0 To width - 1
                            Dim i As Integer = x * 3
                            Dim b As Integer = row(i)
                            Dim gValue As Integer = row(i + 1)
                            Dim r As Integer = row(i + 2)
                            gray(grayOffset + x) = CByte((r * 299 + gValue * 587 + b * 114 + 500) \ 1000)
                        Next
                    Next

                    Using compressed As New MemoryStream()
                        Using z As New ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen:=True)
                            z.Write(gray, 0, gray.Length)
                        End Using
                        Return compressed.ToArray()
                    End Using
                Finally
                    rgb.UnlockBits(data)
                End Try
            End Using
        End Using
    End Function

    Private Shared Sub WriteAscii(streamInBed As Stream, text As String)
        Dim bytes As Byte() = Encoding.ASCII.GetBytes(text)
        streamInBed.Write(bytes, 0, bytes.Length)
    End Sub
End Class
