using Permafrost2.DomainControllerAgent.UI.Services;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;

namespace Permafrost2.DomainControllerAgent.UI;

public partial class MainForm : Form
{
    private readonly IAgentServiceManager _serviceManager;
    private readonly IApiClient _apiClient;
    private readonly ILogger<MainForm> _logger;
    private System.Windows.Forms.Timer _refreshTimer = null!;

    public MainForm(IAgentServiceManager serviceManager, IApiClient apiClient, ILogger<MainForm> logger)
    {
        InitializeComponent();
        _serviceManager = serviceManager;
        _apiClient = apiClient;
        _logger = logger;

        InitializeTimer();
        LoadInitialData();
    }

    private void InitializeTimer()
    {
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 5000; // 5 seconds
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

    private async void LoadInitialData()
    {
        await RefreshServiceStatus();
        await RefreshAgentStatus();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshServiceStatus();
    }

    private async Task RefreshServiceStatus()
    {
        try
        {
            var status = await _serviceManager.GetServiceStatusAsync();
            UpdateServiceStatusUI(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh service status");
            lblServiceStatus.Text = "Error getting service status";
            lblServiceStatus.ForeColor = Color.Red;
        }
    }

    private async Task RefreshAgentStatus()
    {
        try
        {
            var connected = await _apiClient.CheckConnectionAsync();
            lblApiConnection.Text = connected ? "Connected" : "Disconnected";
            lblApiConnection.ForeColor = connected ? Color.Green : Color.Red;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check API connection");
            lblApiConnection.Text = "Error";
            lblApiConnection.ForeColor = Color.Red;
        }
    }

    private void UpdateServiceStatusUI(ServiceControllerStatus status)
    {
        lblServiceStatus.Text = status.ToString();
        lblServiceStatus.ForeColor = status switch
        {
            ServiceControllerStatus.Running => Color.Green,
            ServiceControllerStatus.Stopped => Color.Red,
            ServiceControllerStatus.StartPending or ServiceControllerStatus.StopPending => Color.Orange,
            _ => Color.Gray
        };

        btnStartService.Enabled = status == ServiceControllerStatus.Stopped;
        btnStopService.Enabled = status == ServiceControllerStatus.Running;
        btnRestartService.Enabled = status == ServiceControllerStatus.Running;
    }

    private async void btnStartService_Click(object sender, EventArgs e)
    {
        try
        {
            btnStartService.Enabled = false;
            await _serviceManager.StartServiceAsync();
            _logger.LogInformation("Service start requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service");
            MessageBox.Show($"Failed to start service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnStartService.Enabled = true;
        }
    }

    private async void btnStopService_Click(object sender, EventArgs e)
    {
        try
        {
            btnStopService.Enabled = false;
            await _serviceManager.StopServiceAsync();
            _logger.LogInformation("Service stop requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service");
            MessageBox.Show($"Failed to stop service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnStopService.Enabled = true;
        }
    }

    private async void btnRestartService_Click(object sender, EventArgs e)
    {
        try
        {
            btnRestartService.Enabled = false;
            await _serviceManager.RestartServiceAsync();
            _logger.LogInformation("Service restart requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart service");
            MessageBox.Show($"Failed to restart service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnRestartService.Enabled = true;
        }
    }

    private void btnViewLogs_Click(object sender, EventArgs e)
    {
        try
        {
            var logForm = new LogViewerForm(_serviceManager);
            logForm.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open log viewer");
            MessageBox.Show($"Failed to open log viewer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnConfiguration_Click(object sender, EventArgs e)
    {
        try
        {
            var configForm = new ConfigurationForm(_serviceManager);
            configForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open configuration");
            MessageBox.Show($"Failed to open configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


}
