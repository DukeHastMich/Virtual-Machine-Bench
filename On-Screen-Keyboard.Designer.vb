<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class On_Screen_Keyboard
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(On_Screen_Keyboard))
        ProgressBar6 = New ProgressBar()
        ProgressBar7 = New ProgressBar()
        Label5 = New Label()
        Label6 = New Label()
        PBX_Logo = New PictureBox()
        ProgressBar1 = New ProgressBar()
        Label1 = New Label()
        CType(PBX_Logo, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' ProgressBar6
        ' 
        ProgressBar6.ForeColor = Color.Gold
        ProgressBar6.Location = New Point(1077, 12)
        ProgressBar6.Name = "ProgressBar6"
        ProgressBar6.Size = New Size(14, 15)
        ProgressBar6.TabIndex = 17
        ' 
        ' ProgressBar7
        ' 
        ProgressBar7.ForeColor = Color.Gold
        ProgressBar7.Location = New Point(1019, 12)
        ProgressBar7.Name = "ProgressBar7"
        ProgressBar7.Size = New Size(14, 15)
        ProgressBar7.TabIndex = 16
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.ForeColor = Color.White
        Label5.Location = New Point(1039, 12)
        Label5.Name = "Label5"
        Label5.Size = New Size(36, 15)
        Label5.TabIndex = 15
        Label5.Text = "Scroll"
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.ForeColor = Color.White
        Label6.Location = New Point(981, 12)
        Label6.Name = "Label6"
        Label6.Size = New Size(33, 15)
        Label6.TabIndex = 14
        Label6.Text = "Caps"
        ' 
        ' PBX_Logo
        ' 
        PBX_Logo.Image = CType(resources.GetObject("PBX_Logo.Image"), Image)
        PBX_Logo.Location = New Point(1097, 1)
        PBX_Logo.Name = "PBX_Logo"
        PBX_Logo.Size = New Size(198, 50)
        PBX_Logo.SizeMode = PictureBoxSizeMode.StretchImage
        PBX_Logo.TabIndex = 22
        PBX_Logo.TabStop = False
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.ForeColor = Color.Gold
        ProgressBar1.Location = New Point(965, 12)
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(14, 15)
        ProgressBar1.TabIndex = 24
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.ForeColor = Color.White
        Label1.Location = New Point(927, 12)
        Label1.Name = "Label1"
        Label1.Size = New Size(34, 15)
        Label1.TabIndex = 23
        Label1.Text = "Num"
        ' 
        ' On_Screen_Keyboard
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.Black
        ClientSize = New Size(1308, 475)
        Controls.Add(ProgressBar1)
        Controls.Add(Label1)
        Controls.Add(PBX_Logo)
        Controls.Add(ProgressBar6)
        Controls.Add(ProgressBar7)
        Controls.Add(Label5)
        Controls.Add(Label6)
        Name = "On_Screen_Keyboard"
        Text = "Cromwell Technologies Keymaster (Clorto edition)"
        CType(PBX_Logo, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents ProgressBar6 As ProgressBar
    Friend WithEvents ProgressBar7 As ProgressBar
    Friend WithEvents Label5 As Label
    Friend WithEvents Label6 As Label
    Friend WithEvents PBX_Logo As PictureBox
    Friend WithEvents ProgressBar1 As ProgressBar
    Friend WithEvents Label1 As Label
End Class
