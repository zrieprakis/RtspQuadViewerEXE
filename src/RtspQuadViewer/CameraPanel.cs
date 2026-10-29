using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using Timer = System.Windows.Forms.Timer;

namespace RtspQuadViewer;

/// <summary>One quadrant: a libvlc player for an RTSP URI, with a label overlay and automatic reconnect.</summary>
sealed class CameraPanel : Panel
{
    // Restart a stream that hasn't advanced or finished connecting within these windows.
    static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(10);
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);
    static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    readonly int _index;
    readonly LibVLC _libVlc;
    readonly MediaPlayer _player;
    readonly VideoView _view;
    readonly Label _label;
    readonly Label _placeholder;
    readonly Timer _watchdog;

    // All libvlc calls that can block (play/stop) run sequentially off the UI thread.
    Task _ops = Task.CompletedTask;
    string _uri = "";
    bool _shuttingDown;
    DateTime _lastStart;
    long _lastTime = -1;
    DateTime _lastProgress;

    public event EventHandler? ZoomToggled;

    public CameraPanel(LibVLC libVlc, int index, ContextMenuStrip menu)
    {
        _index = index;
        _libVlc = libVlc;
        BackColor = Color.Black;
        ContextMenuStrip = menu;

        _player = new MediaPlayer(libVlc)
        {
            EnableHardwareDecoding = true,
            // Let mouse/keyboard input reach WinForms instead of libvlc's video window.
            EnableMouseInput = false,
            EnableKeyInput = false,
        };

        _view = new VideoView { Dock = DockStyle.Fill, BackColor = Color.Black, MediaPlayer = _player, ContextMenuStrip = menu };

        _placeholder = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(138, 148, 163),
            BackColor = Color.Black,
            Text = "Not configured\r\nRight-click → Settings",
            ContextMenuStrip = menu,
        };

        _label = new Label
        {
            AutoSize = true,
            Location = new Point(8, 8),
            Padding = new Padding(6, 2, 6, 2),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(20, 24, 29),
            ContextMenuStrip = menu,
        };

        Controls.Add(_view);
        Controls.Add(_placeholder);
        Controls.Add(_label);
        _label.BringToFront();

        foreach (Control c in new Control[] { this, _view, _placeholder, _label })
            c.DoubleClick += (_, _) => ZoomToggled?.Invoke(this, EventArgs.Empty);

        _player.Playing += (_, _) => OnUi(() => SetStatus(null));

        _watchdog = new Timer { Interval = 2000 };
        _watchdog.Tick += (_, _) => CheckHealth();
        _watchdog.Start();

        ShowState();
    }

    public string Uri => _uri;

    /// <summary>Starts, switches or stops the stream. No-op if the URI is unchanged.</summary>
    public void SetUri(string uri)
    {
        if (uri == _uri) return;
        _uri = uri;
        ShowState();
        if (_uri.Length == 0) Enqueue(() => _player.Stop());
        else Start();
    }

    /// <summary>Stops playback and releases the player. Returns a task that completes when libvlc is done.</summary>
    public Task ShutdownAsync()
    {
        _shuttingDown = true;
        _watchdog.Stop();
        return Enqueue(() =>
        {
            _player.Stop();
            _player.Dispose();
        });
    }

    void Start()
    {
        var uri = _uri;
        _lastStart = DateTime.UtcNow;
        _lastTime = -1;
        _lastProgress = _lastStart;
        SetStatus("connecting…");
        Enqueue(() =>
        {
            _player.Stop();
            using var media = new Media(_libVlc, uri, FromType.FromLocation);
            media.AddOption(":network-caching=300");
            media.AddOption(":rtsp-tcp");
            media.AddOption(":no-audio");
            _player.Play(media);
        });
    }

    void CheckHealth()
    {
        if (_shuttingDown || _uri.Length == 0 || !_ops.IsCompleted) return;
        var now = DateTime.UtcNow;
        var state = _player.State;

        switch (state)
        {
            case VLCState.Playing:
                var time = _player.Time;
                if (time != _lastTime) { _lastTime = time; _lastProgress = now; }
                else if (now - _lastProgress > StallTimeout) Restart("stalled");
                break;
            case VLCState.Opening or VLCState.Buffering:
                if (now - _lastStart > ConnectTimeout) Restart("timed out");
                break;
            case VLCState.Paused:
                break;
            default: // Stopped, Ended, Error, NothingSpecial
                if (now - _lastStart > RetryDelay) Restart(state == VLCState.Error ? "error" : "disconnected");
                break;
        }
    }

    void Restart(string reason)
    {
        Start();
        SetStatus($"{reason}, reconnecting…");
    }

    void ShowState()
    {
        var configured = _uri.Length > 0;
        _view.Visible = configured;
        _placeholder.Visible = !configured;
        SetStatus(null);
        _label.BringToFront();
    }

    void SetStatus(string? status)
    {
        _label.Text = status == null || _uri.Length == 0 ? $"Camera {_index + 1}" : $"Camera {_index + 1} · {status}";
    }

    Task Enqueue(Action action)
    {
        _ops = _ops.ContinueWith(_ =>
        {
            try { action(); }
            catch (Exception e) { System.Diagnostics.Debug.WriteLine($"[cam{_index + 1}] {e}"); }
        }, TaskScheduler.Default);
        return _ops;
    }

    // libvlc raises events on its own threads; never call back into libvlc from them.
    void OnUi(Action action)
    {
        if (_shuttingDown || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }
}
