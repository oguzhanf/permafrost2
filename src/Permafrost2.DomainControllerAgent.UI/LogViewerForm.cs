using Permafrost2.DomainControllerAgent.UI.Services;

namespace Permafrost2.DomainControllerAgent.UI;

public partial class LogViewerForm : Form
{
    private readonly IAgentServiceManager _serviceManager;
    private TextBox txtLogs = null!;
    private Button btnRefresh = null!;
    private Button btnClear = null!;

    public LogViewerForm(IAgentServiceManager serviceManager)
    {
        _serviceManager = serviceManager;
        InitializeComponent();
        LoadLogs();
    }

    private void InitializeComponent()
    {
        this.txtLogs = new TextBox();
        this.btnRefresh = new Button();
        this.btnClear = new Button();
        this.SuspendLayout();

        // txtLogs
        this.txtLogs.Location = new Point(12, 12);
        this.txtLogs.Multiline = true;
        this.txtLogs.Name = "txtLogs";
        this.txtLogs.ReadOnly = true;
        this.txtLogs.ScrollBars = ScrollBars.Both;
        this.txtLogs.Size = new Size(760, 400);
        this.txtLogs.TabIndex = 0;
        this.txtLogs.Font = new Font("Consolas", 9F);

        // btnRefresh
        this.btnRefresh.Location = new Point(12, 425);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new Size(75, 23);
        this.btnRefresh.TabIndex = 1;
        this.btnRefresh.Text = "Refresh";
        this.btnRefresh.UseVisualStyleBackColor = true;
        this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);

        // btnClear
        this.btnClear.Location = new Point(100, 425);
        this.btnClear.Name = "btnClear";
        this.btnClear.Size = new Size(75, 23);
        this.btnClear.TabIndex = 2;
        this.btnClear.Text = "Clear";
        this.btnClear.UseVisualStyleBackColor = true;
        this.btnClear.Click += new EventHandler(this.btnClear_Click);

        // LogViewerForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(784, 461);
        this.Controls.Add(this.txtLogs);
        this.Controls.Add(this.btnRefresh);
        this.Controls.Add(this.btnClear);
        this.Name = "LogViewerForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Agent Logs";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private async void LoadLogs()
    {
        try
        {
            var logs = await _serviceManager.GetServiceLogsAsync();
            txtLogs.Text = logs;
            txtLogs.SelectionStart = txtLogs.Text.Length;
            txtLogs.ScrollToCaret();
        }
        catch (Exception ex)
        {
            txtLogs.Text = $"Error loading logs: {ex.Message}";
        }
    }

    private async void btnRefresh_Click(object sender, EventArgs e)
    {
        btnRefresh.Enabled = false;
        try
        {
            await Task.Run(() => LoadLogs());
        }
        finally
        {
            btnRefresh.Enabled = true;
        }
    }

    private void btnClear_Click(object sender, EventArgs e)
    {
        txtLogs.Clear();
    }
}
