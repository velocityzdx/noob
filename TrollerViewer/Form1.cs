using Microsoft.AspNetCore.SignalR.Client;

namespace TrollerViewer;

public partial class Form1 : Form
{
    private HubConnection? _connection;
    private PictureBox _pictureBox;
    private Label _statusLabel;
    private Button _btnConnect;

    public Form1()
    {
        InitializeComponent();
        SetupUI();
    }

    private void SetupUI()
    {
        this.Text = "Troller Viewer - Control Panel";
        this.Size = new Size(1024, 768);
        this.BackColor = Color.FromArgb(20, 20, 25);
        this.ForeColor = Color.White;

        _statusLabel = new Label
        {
            Text = "Status: Disconnected",
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Padding = new Padding(10, 0, 0, 0)
        };

        _btnConnect = new Button
        {
            Text = "Connect to Relay",
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        _btnConnect.FlatAppearance.BorderSize = 0;
        _btnConnect.Click += BtnConnect_Click;

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };

        this.Controls.Add(_pictureBox);
        this.Controls.Add(_btnConnect);
        this.Controls.Add(_statusLabel);
    }

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        if (_connection != null && _connection.State == HubConnectionState.Connected)
        {
            await _connection.StopAsync();
            return;
        }

        try
        {
            _statusLabel.Text = "Status: Connecting...";
            _btnConnect.Enabled = false;

            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/screenHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.Closed += async (error) =>
            {
                this.Invoke(() =>
                {
                    _statusLabel.Text = "Status: Disconnected";
                    _btnConnect.Text = "Connect to Relay";
                });
            };

            _connection.On<string, string>("ReceiveFrame", (clientId, base64Image) =>
            {
                this.Invoke(() =>
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(base64Image);
                        using var ms = new MemoryStream(bytes);
                        var image = Image.FromStream(ms);
                        
                        var oldImage = _pictureBox.Image;
                        _pictureBox.Image = image;
                        
                        if (oldImage != null)
                            oldImage.Dispose();
                            
                        _statusLabel.Text = $"Status: Receiving live feed from {clientId}";
                    }
                    catch { }
                });
            });

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinAsViewer");
            
            _statusLabel.Text = "Status: Connected. Waiting for clients...";
            _btnConnect.Text = "Disconnect";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Status: Error - {ex.Message}";
        }
        finally
        {
            _btnConnect.Enabled = true;
        }
    }
}
