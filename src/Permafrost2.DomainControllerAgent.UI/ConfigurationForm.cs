using Permafrost2.DomainControllerAgent.UI.Services;
using System.Text.Json;

namespace Permafrost2.DomainControllerAgent.UI;

public partial class ConfigurationForm : Form
{
    private readonly IAgentServiceManager _serviceManager;
    private TextBox txtApiUrl = null!;
    private TextBox txtHeartbeatInterval = null!;
    private TextBox txtDataCollectionInterval = null!;
    private CheckBox chkDetailedLogging = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    public ConfigurationForm(IAgentServiceManager serviceManager)
    {
        _serviceManager = serviceManager;
        InitializeComponent();
        LoadConfiguration();
    }

    private void InitializeComponent()
    {
        this.txtApiUrl = new TextBox();
        this.txtHeartbeatInterval = new TextBox();
        this.txtDataCollectionInterval = new TextBox();
        this.chkDetailedLogging = new CheckBox();
        this.btnSave = new Button();
        this.btnCancel = new Button();
        this.SuspendLayout();

        // Labels and controls layout
        var lblApiUrl = new Label { Text = "API URL:", Location = new Point(12, 15), Size = new Size(120, 23) };
        this.txtApiUrl.Location = new Point(140, 12);
        this.txtApiUrl.Size = new Size(300, 23);

        var lblHeartbeat = new Label { Text = "Heartbeat Interval (sec):", Location = new Point(12, 45), Size = new Size(120, 23) };
        this.txtHeartbeatInterval.Location = new Point(140, 42);
        this.txtHeartbeatInterval.Size = new Size(100, 23);

        var lblDataCollection = new Label { Text = "Data Collection (min):", Location = new Point(12, 75), Size = new Size(120, 23) };
        this.txtDataCollectionInterval.Location = new Point(140, 72);
        this.txtDataCollectionInterval.Size = new Size(100, 23);

        this.chkDetailedLogging.Text = "Enable Detailed Logging";
        this.chkDetailedLogging.Location = new Point(12, 105);
        this.chkDetailedLogging.Size = new Size(200, 23);

        this.btnSave.Location = new Point(285, 140);
        this.btnSave.Size = new Size(75, 23);
        this.btnSave.Text = "Save";
        this.btnSave.UseVisualStyleBackColor = true;
        this.btnSave.Click += new EventHandler(this.btnSave_Click);

        this.btnCancel.Location = new Point(365, 140);
        this.btnCancel.Size = new Size(75, 23);
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

        // ConfigurationForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(460, 180);
        this.Controls.Add(lblApiUrl);
        this.Controls.Add(this.txtApiUrl);
        this.Controls.Add(lblHeartbeat);
        this.Controls.Add(this.txtHeartbeatInterval);
        this.Controls.Add(lblDataCollection);
        this.Controls.Add(this.txtDataCollectionInterval);
        this.Controls.Add(this.chkDetailedLogging);
        this.Controls.Add(this.btnSave);
        this.Controls.Add(this.btnCancel);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "ConfigurationForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Agent Configuration";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private async void LoadConfiguration()
    {
        try
        {
            var config = await _serviceManager.GetServiceConfigurationAsync();
            
            if (config.ContainsKey("Permafrost2") && config["Permafrost2"] is JsonElement permafrost2)
            {
                if (permafrost2.TryGetProperty("ApiBaseUrl", out var apiUrl))
                    txtApiUrl.Text = apiUrl.GetString() ?? "";
                
                if (permafrost2.TryGetProperty("HeartbeatIntervalSeconds", out var heartbeat))
                    txtHeartbeatInterval.Text = heartbeat.GetInt32().ToString();
                
                if (permafrost2.TryGetProperty("DataCollectionIntervalMinutes", out var dataCollection))
                    txtDataCollectionInterval.Text = dataCollection.GetInt32().ToString();
                
                if (permafrost2.TryGetProperty("EnableDetailedLogging", out var detailedLogging))
                    chkDetailedLogging.Checked = detailedLogging.GetBoolean();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        try
        {
            var config = await _serviceManager.GetServiceConfigurationAsync();
            
            // Update Permafrost2 section
            var permafrost2Config = new Dictionary<string, object>
            {
                ["ApiBaseUrl"] = txtApiUrl.Text,
                ["HeartbeatIntervalSeconds"] = int.Parse(txtHeartbeatInterval.Text),
                ["DataCollectionIntervalMinutes"] = int.Parse(txtDataCollectionInterval.Text),
                ["EnableDetailedLogging"] = chkDetailedLogging.Checked
            };

            config["Permafrost2"] = permafrost2Config;
            
            await _serviceManager.UpdateServiceConfigurationAsync(config);
            
            MessageBox.Show("Configuration saved successfully. Restart the service for changes to take effect.", 
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }
}
