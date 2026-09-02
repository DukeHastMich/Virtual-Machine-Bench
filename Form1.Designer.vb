<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form1))
        PictureBox1 = New PictureBox()
        SystemLoop = New Timer(components)
        Mode2 = New Timer(components)
        Mode4 = New Timer(components)
        Mode3 = New Timer(components)
        GPU = New Timer(components)
        Label4 = New Label()
        Label1 = New Label()
        Label2 = New Label()
        ProgressBar1 = New ProgressBar()
        ProgressBar2 = New ProgressBar()
        Label3 = New Label()
        ProgressBar5 = New ProgressBar()
        ProgressBar4 = New ProgressBar()
        Label6 = New Label()
        Label5 = New Label()
        ProgressBar7 = New ProgressBar()
        ProgressBar6 = New ProgressBar()
        Label7 = New Label()
        Label8 = New Label()
        ProgressBar3 = New ProgressBar()
        Label9 = New Label()
        ProgressBar8 = New ProgressBar()
        Label10 = New Label()
        ProgressBar9 = New ProgressBar()
        Label11 = New Label()
        ProgressBar10 = New ProgressBar()
        PB_CPU_Utilization = New ProgressBar()
        Label13 = New Label()
        Label12 = New Label()
        ProgressBar12 = New ProgressBar()
        ProgressBar11 = New ProgressBar()
        Label15 = New Label()
        Label14 = New Label()
        ProgressBar14 = New ProgressBar()
        ProgressBar13 = New ProgressBar()
        CType(PictureBox1, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' PictureBox1
        ' 
        PictureBox1.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        PictureBox1.BorderStyle = BorderStyle.FixedSingle
        PictureBox1.Image = CType(resources.GetObject("PictureBox1.Image"), Image)
        PictureBox1.Location = New Point(18, 47)
        PictureBox1.Margin = New Padding(4, 3, 4, 3)
        PictureBox1.Name = "PictureBox1"
        PictureBox1.Size = New Size(1840, 1099)
        PictureBox1.SizeMode = PictureBoxSizeMode.StretchImage
        PictureBox1.TabIndex = 0
        PictureBox1.TabStop = False
        ' 
        ' SystemLoop
        ' 
        SystemLoop.Interval = 20
        ' 
        ' Mode2
        ' 
        Mode2.Interval = 13
        ' 
        ' Mode4
        ' 
        Mode4.Interval = 300
        ' 
        ' Mode3
        ' 
        Mode3.Interval = 13
        ' 
        ' GPU
        ' 
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Location = New Point(13, 29)
        Label4.Name = "Label4"
        Label4.Size = New Size(43, 15)
        Label4.TabIndex = 6
        Label4.Text = "|Power"
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Location = New Point(175, 29)
        Label1.Name = "Label1"
        Label1.Size = New Size(43, 15)
        Label1.TabIndex = 1
        Label1.Text = "|FDD A"
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Location = New Point(306, 29)
        Label2.Name = "Label2"
        Label2.Size = New Size(44, 15)
        Label2.TabIndex = 2
        Label2.Text = "|HDD 0"
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.BackColor = Color.DimGray
        ProgressBar1.ForeColor = Color.Green
        ProgressBar1.Location = New Point(221, 29)
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(14, 15)
        ProgressBar1.TabIndex = 3
        ' 
        ' ProgressBar2
        ' 
        ProgressBar2.BackColor = Color.DimGray
        ProgressBar2.ForeColor = Color.Red
        ProgressBar2.Location = New Point(353, 29)
        ProgressBar2.Name = "ProgressBar2"
        ProgressBar2.Size = New Size(14, 15)
        ProgressBar2.TabIndex = 4
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Location = New Point(79, 29)
        Label3.Name = "Label3"
        Label3.Size = New Size(42, 15)
        Label3.TabIndex = 7
        Label3.Text = "|Turbo"
        ' 
        ' ProgressBar5
        ' 
        ProgressBar5.BackColor = Color.DimGray
        ProgressBar5.ForeColor = Color.FromArgb(CByte(0), CByte(192), CByte(0))
        ProgressBar5.Location = New Point(59, 29)
        ProgressBar5.Name = "ProgressBar5"
        ProgressBar5.Size = New Size(14, 15)
        ProgressBar5.TabIndex = 8
        ProgressBar5.Value = 1
        ' 
        ' ProgressBar4
        ' 
        ProgressBar4.BackColor = Color.DimGray
        ProgressBar4.ForeColor = Color.Yellow
        ProgressBar4.Location = New Point(124, 29)
        ProgressBar4.Name = "ProgressBar4"
        ProgressBar4.Size = New Size(14, 15)
        ProgressBar4.TabIndex = 9
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Location = New Point(1212, 29)
        Label6.Name = "Label6"
        Label6.Size = New Size(38, 15)
        Label6.TabIndex = 10
        Label6.Text = "KB TX"
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Location = New Point(1270, 29)
        Label5.Name = "Label5"
        Label5.Size = New Size(38, 15)
        Label5.TabIndex = 11
        Label5.Text = "KB RX"
        ' 
        ' ProgressBar7
        ' 
        ProgressBar7.ForeColor = Color.Gold
        ProgressBar7.Location = New Point(1250, 29)
        ProgressBar7.Name = "ProgressBar7"
        ProgressBar7.Size = New Size(14, 15)
        ProgressBar7.TabIndex = 12
        ' 
        ' ProgressBar6
        ' 
        ProgressBar6.ForeColor = Color.Gold
        ProgressBar6.Location = New Point(1308, 29)
        ProgressBar6.Name = "ProgressBar6"
        ProgressBar6.Size = New Size(14, 15)
        ProgressBar6.TabIndex = 13
        ' 
        ' Label7
        ' 
        Label7.AutoSize = True
        Label7.Location = New Point(574, 29)
        Label7.Name = "Label7"
        Label7.Size = New Size(48, 15)
        Label7.TabIndex = 14
        Label7.Text = "|CPU Ut"
        ' 
        ' Label8
        ' 
        Label8.AutoSize = True
        Label8.Location = New Point(241, 29)
        Label8.Name = "Label8"
        Label8.Size = New Size(42, 15)
        Label8.TabIndex = 15
        Label8.Text = "|FDD B"
        ' 
        ' ProgressBar3
        ' 
        ProgressBar3.BackColor = Color.DimGray
        ProgressBar3.ForeColor = Color.Green
        ProgressBar3.Location = New Point(286, 29)
        ProgressBar3.Name = "ProgressBar3"
        ProgressBar3.Size = New Size(14, 15)
        ProgressBar3.TabIndex = 16
        ' 
        ' Label9
        ' 
        Label9.AutoSize = True
        Label9.Location = New Point(373, 29)
        Label9.Name = "Label9"
        Label9.Size = New Size(44, 15)
        Label9.TabIndex = 17
        Label9.Text = "|HDD 1"
        ' 
        ' ProgressBar8
        ' 
        ProgressBar8.BackColor = Color.DimGray
        ProgressBar8.ForeColor = Color.Red
        ProgressBar8.Location = New Point(420, 29)
        ProgressBar8.Name = "ProgressBar8"
        ProgressBar8.Size = New Size(14, 15)
        ProgressBar8.TabIndex = 18
        ' 
        ' Label10
        ' 
        Label10.AutoSize = True
        Label10.Location = New Point(440, 29)
        Label10.Name = "Label10"
        Label10.Size = New Size(44, 15)
        Label10.TabIndex = 19
        Label10.Text = "|HDD 2"
        ' 
        ' ProgressBar9
        ' 
        ProgressBar9.BackColor = Color.DimGray
        ProgressBar9.ForeColor = Color.Red
        ProgressBar9.Location = New Point(487, 29)
        ProgressBar9.Name = "ProgressBar9"
        ProgressBar9.Size = New Size(14, 15)
        ProgressBar9.TabIndex = 20
        ' 
        ' Label11
        ' 
        Label11.AutoSize = True
        Label11.Location = New Point(507, 29)
        Label11.Name = "Label11"
        Label11.Size = New Size(44, 15)
        Label11.TabIndex = 21
        Label11.Text = "|HDD 3"
        ' 
        ' ProgressBar10
        ' 
        ProgressBar10.BackColor = Color.DimGray
        ProgressBar10.ForeColor = Color.Red
        ProgressBar10.Location = New Point(554, 29)
        ProgressBar10.Name = "ProgressBar10"
        ProgressBar10.Size = New Size(14, 15)
        ProgressBar10.TabIndex = 22
        ' 
        ' PB_CPU_Utilization
        ' 
        PB_CPU_Utilization.BackColor = Color.DimGray
        PB_CPU_Utilization.ForeColor = Color.FromArgb(CByte(0), CByte(192), CByte(0))
        PB_CPU_Utilization.Location = New Point(628, 29)
        PB_CPU_Utilization.Name = "PB_CPU_Utilization"
        PB_CPU_Utilization.Size = New Size(55, 15)
        PB_CPU_Utilization.Step = 1
        PB_CPU_Utilization.Style = ProgressBarStyle.Continuous
        PB_CPU_Utilization.TabIndex = 23
        PB_CPU_Utilization.Value = 1
        ' 
        ' Label13
        ' 
        Label13.AutoSize = True
        Label13.Location = New Point(1074, 29)
        Label13.Name = "Label13"
        Label13.Size = New Size(41, 15)
        Label13.TabIndex = 24
        Label13.Text = "Eth TX"
        ' 
        ' Label12
        ' 
        Label12.AutoSize = True
        Label12.Location = New Point(1141, 29)
        Label12.Name = "Label12"
        Label12.Size = New Size(41, 15)
        Label12.TabIndex = 25
        Label12.Text = "Eth RX"
        ' 
        ' ProgressBar12
        ' 
        ProgressBar12.ForeColor = Color.Gold
        ProgressBar12.Location = New Point(1121, 29)
        ProgressBar12.Name = "ProgressBar12"
        ProgressBar12.Size = New Size(14, 15)
        ProgressBar12.TabIndex = 26
        ' 
        ' ProgressBar11
        ' 
        ProgressBar11.ForeColor = Color.Gold
        ProgressBar11.Location = New Point(1188, 29)
        ProgressBar11.Name = "ProgressBar11"
        ProgressBar11.Size = New Size(14, 15)
        ProgressBar11.TabIndex = 27
        ' 
        ' Label15
        ' 
        Label15.AutoSize = True
        Label15.Location = New Point(939, 29)
        Label15.Name = "Label15"
        Label15.Size = New Size(40, 15)
        Label15.TabIndex = 28
        Label15.Text = "Ser TX"
        ' 
        ' Label14
        ' 
        Label14.AutoSize = True
        Label14.Location = New Point(1006, 29)
        Label14.Name = "Label14"
        Label14.Size = New Size(40, 15)
        Label14.TabIndex = 29
        Label14.Text = "Ser RX"
        ' 
        ' ProgressBar14
        ' 
        ProgressBar14.ForeColor = Color.Gold
        ProgressBar14.Location = New Point(986, 29)
        ProgressBar14.Name = "ProgressBar14"
        ProgressBar14.Size = New Size(14, 15)
        ProgressBar14.TabIndex = 30
        ' 
        ' ProgressBar13
        ' 
        ProgressBar13.ForeColor = Color.Gold
        ProgressBar13.Location = New Point(1053, 29)
        ProgressBar13.Name = "ProgressBar13"
        ProgressBar13.Size = New Size(14, 15)
        ProgressBar13.TabIndex = 31
        ' 
        ' Form1
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1879, 1158)
        Controls.Add(ProgressBar13)
        Controls.Add(ProgressBar14)
        Controls.Add(Label14)
        Controls.Add(Label15)
        Controls.Add(ProgressBar11)
        Controls.Add(ProgressBar12)
        Controls.Add(Label12)
        Controls.Add(Label13)
        Controls.Add(PB_CPU_Utilization)
        Controls.Add(ProgressBar10)
        Controls.Add(Label11)
        Controls.Add(ProgressBar9)
        Controls.Add(Label10)
        Controls.Add(ProgressBar8)
        Controls.Add(Label9)
        Controls.Add(ProgressBar3)
        Controls.Add(Label8)
        Controls.Add(Label7)
        Controls.Add(ProgressBar6)
        Controls.Add(ProgressBar7)
        Controls.Add(Label5)
        Controls.Add(Label6)
        Controls.Add(ProgressBar4)
        Controls.Add(ProgressBar5)
        Controls.Add(Label3)
        Controls.Add(Label4)
        Controls.Add(ProgressBar2)
        Controls.Add(ProgressBar1)
        Controls.Add(Label2)
        Controls.Add(Label1)
        Controls.Add(PictureBox1)
        KeyPreview = True
        Margin = New Padding(4, 3, 4, 3)
        Name = "Form1"
        Text = "Virtual Computer"
        CType(PictureBox1, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()

    End Sub
    Friend WithEvents PictureBox1 As System.Windows.Forms.PictureBox
    Friend WithEvents SystemLoop As System.Windows.Forms.Timer
    Friend WithEvents Mode2 As System.Windows.Forms.Timer
    Friend WithEvents Mode4 As System.Windows.Forms.Timer
    Friend WithEvents Mode3 As System.Windows.Forms.Timer
    Friend WithEvents GPU As System.Windows.Forms.Timer
    Friend WithEvents Label4 As Label
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents ProgressBar1 As ProgressBar
    Friend WithEvents ProgressBar2 As ProgressBar
    Friend WithEvents Label3 As Label
    Friend WithEvents ProgressBar5 As ProgressBar
    Friend WithEvents ProgressBar4 As ProgressBar
    Friend WithEvents Label6 As Label
    Friend WithEvents Label5 As Label
    Friend WithEvents ProgressBar7 As ProgressBar
    Friend WithEvents ProgressBar6 As ProgressBar
    Friend WithEvents Label7 As Label
    Friend WithEvents Label8 As Label
    Friend WithEvents ProgressBar3 As ProgressBar
    Friend WithEvents Label9 As Label
    Friend WithEvents ProgressBar8 As ProgressBar
    Friend WithEvents Label10 As Label
    Friend WithEvents ProgressBar9 As ProgressBar
    Friend WithEvents Label11 As Label
    Friend WithEvents ProgressBar10 As ProgressBar
    Friend WithEvents PB_CPU_Utilization As ProgressBar
    Friend WithEvents Label13 As Label
    Friend WithEvents Label12 As Label
    Friend WithEvents ProgressBar12 As ProgressBar
    Friend WithEvents ProgressBar11 As ProgressBar
    Friend WithEvents Label15 As Label
    Friend WithEvents Label14 As Label
    Friend WithEvents ProgressBar14 As ProgressBar
    Friend WithEvents ProgressBar13 As ProgressBar

End Class
