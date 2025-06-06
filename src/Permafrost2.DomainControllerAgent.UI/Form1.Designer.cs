namespace Permafrost2.DomainControllerAgent.UI;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.groupBoxService = new GroupBox();
        this.lblServiceStatus = new Label();
        this.btnStartService = new Button();
        this.btnStopService = new Button();
        this.btnRestartService = new Button();
        this.groupBoxConnection = new GroupBox();
        this.lblApiConnection = new Label();
        this.groupBoxActions = new GroupBox();
        this.btnViewLogs = new Button();
        this.btnConfiguration = new Button();
        this.SuspendLayout();

        // groupBoxService
        this.groupBoxService.Controls.Add(this.lblServiceStatus);
        this.groupBoxService.Controls.Add(this.btnStartService);
        this.groupBoxService.Controls.Add(this.btnStopService);
        this.groupBoxService.Controls.Add(this.btnRestartService);
        this.groupBoxService.Location = new Point(12, 12);
        this.groupBoxService.Name = "groupBoxService";
        this.groupBoxService.Size = new Size(360, 120);
        this.groupBoxService.TabIndex = 0;
        this.groupBoxService.TabStop = false;
        this.groupBoxService.Text = "Service Control";

        // lblServiceStatus
        this.lblServiceStatus.AutoSize = true;
        this.lblServiceStatus.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        this.lblServiceStatus.Location = new Point(15, 25);
        this.lblServiceStatus.Name = "lblServiceStatus";
        this.lblServiceStatus.Size = new Size(50, 15);
        this.lblServiceStatus.TabIndex = 0;
        this.lblServiceStatus.Text = "Status: Unknown";

        // btnStartService
        this.btnStartService.Location = new Point(15, 50);
        this.btnStartService.Name = "btnStartService";
        this.btnStartService.Size = new Size(75, 23);
        this.btnStartService.TabIndex = 1;
        this.btnStartService.Text = "Start";
        this.btnStartService.UseVisualStyleBackColor = true;
        this.btnStartService.Click += new EventHandler(this.btnStartService_Click);

        // btnStopService
        this.btnStopService.Location = new Point(100, 50);
        this.btnStopService.Name = "btnStopService";
        this.btnStopService.Size = new Size(75, 23);
        this.btnStopService.TabIndex = 2;
        this.btnStopService.Text = "Stop";
        this.btnStopService.UseVisualStyleBackColor = true;
        this.btnStopService.Click += new EventHandler(this.btnStopService_Click);

        // btnRestartService
        this.btnRestartService.Location = new Point(185, 50);
        this.btnRestartService.Name = "btnRestartService";
        this.btnRestartService.Size = new Size(75, 23);
        this.btnRestartService.TabIndex = 3;
        this.btnRestartService.Text = "Restart";
        this.btnRestartService.UseVisualStyleBackColor = true;
        this.btnRestartService.Click += new EventHandler(this.btnRestartService_Click);

        // groupBoxConnection
        this.groupBoxConnection.Controls.Add(this.lblApiConnection);
        this.groupBoxConnection.Location = new Point(12, 150);
        this.groupBoxConnection.Name = "groupBoxConnection";
        this.groupBoxConnection.Size = new Size(360, 60);
        this.groupBoxConnection.TabIndex = 1;
        this.groupBoxConnection.TabStop = false;
        this.groupBoxConnection.Text = "API Connection";

        // lblApiConnection
        this.lblApiConnection.AutoSize = true;
        this.lblApiConnection.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        this.lblApiConnection.Location = new Point(15, 25);
        this.lblApiConnection.Name = "lblApiConnection";
        this.lblApiConnection.Size = new Size(70, 15);
        this.lblApiConnection.TabIndex = 0;
        this.lblApiConnection.Text = "Checking...";

        // groupBoxActions
        this.groupBoxActions.Controls.Add(this.btnViewLogs);
        this.groupBoxActions.Controls.Add(this.btnConfiguration);
        this.groupBoxActions.Location = new Point(12, 230);
        this.groupBoxActions.Name = "groupBoxActions";
        this.groupBoxActions.Size = new Size(360, 80);
        this.groupBoxActions.TabIndex = 2;
        this.groupBoxActions.TabStop = false;
        this.groupBoxActions.Text = "Actions";

        // btnViewLogs
        this.btnViewLogs.Location = new Point(15, 30);
        this.btnViewLogs.Name = "btnViewLogs";
        this.btnViewLogs.Size = new Size(100, 30);
        this.btnViewLogs.TabIndex = 0;
        this.btnViewLogs.Text = "View Logs";
        this.btnViewLogs.UseVisualStyleBackColor = true;
        this.btnViewLogs.Click += new EventHandler(this.btnViewLogs_Click);

        // btnConfiguration
        this.btnConfiguration.Location = new Point(130, 30);
        this.btnConfiguration.Name = "btnConfiguration";
        this.btnConfiguration.Size = new Size(100, 30);
        this.btnConfiguration.TabIndex = 1;
        this.btnConfiguration.Text = "Configuration";
        this.btnConfiguration.UseVisualStyleBackColor = true;
        this.btnConfiguration.Click += new EventHandler(this.btnConfiguration_Click);

        // MainForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(400, 350);
        this.Controls.Add(this.groupBoxService);
        this.Controls.Add(this.groupBoxConnection);
        this.Controls.Add(this.groupBoxActions);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Permafrost2 Domain Controller Agent Manager";
        this.ResumeLayout(false);
    }

    #endregion

    private GroupBox groupBoxService;
    private Label lblServiceStatus;
    private Button btnStartService;
    private Button btnStopService;
    private Button btnRestartService;
    private GroupBox groupBoxConnection;
    private Label lblApiConnection;
    private GroupBox groupBoxActions;
    private Button btnViewLogs;
    private Button btnConfiguration;
}
