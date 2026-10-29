using LibVLCSharp.Shared;

namespace RtspQuadViewer;

/// <summary>2×2 grid of camera panels. Double-click a panel to zoom it; F11 for fullscreen.</summary>
sealed class MainForm : Form
{
    const int Gap = 4;

    // RTSP interleaved over TCP: no inbound UDP sockets, so no firewall prompt and fewer dropped frames.
    readonly LibVLC _libVlc = new("--quiet", "--rtsp-tcp");
    readonly AppConfig _config = AppConfig.Load();
    readonly CameraPanel[] _panels = new CameraPanel[AppConfig.Slots];
    CameraPanel? _zoomed;
    FormWindowState _windowStateBeforeFullscreen;

    public MainForm()
    {
        Text = "RTSP Quad Viewer";
        BackColor = Color.FromArgb(11, 13, 16);
        ClientSize = new Size(1280, 740);
        MinimumSize = new Size(480, 320);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings()) { ShortcutKeyDisplayString = "Ctrl+," });
        menu.Items.Add(new ToolStripMenuItem("Fullscreen", null, (_, _) => ToggleFullscreen()) { ShortcutKeyDisplayString = "F11" });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Close());
        ContextMenuStrip = menu;

        for (var i = 0; i < AppConfig.Slots; i++)
        {
            var panel = _panels[i] = new CameraPanel(_libVlc, i, menu);
            panel.ZoomToggled += (_, _) => ToggleZoom(panel);
            Controls.Add(panel);
        }

        Shown += (_, _) =>
        {
            for (var i = 0; i < AppConfig.Slots; i++) _panels[i].SetUri(_config.Cams[i]);
            if (_config.Cams.All(c => c.Length == 0)) OpenSettings();
        };
        LayoutPanels();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutPanels();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F11:
                ToggleFullscreen();
                return true;
            case Keys.Control | Keys.Oemcomma:
                OpenSettings();
                return true;
            case Keys.Escape when _zoomed != null:
                ToggleZoom(_zoomed);
                return true;
            case Keys.Escape when FormBorderStyle == FormBorderStyle.None:
                ToggleFullscreen();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        Hide();
        // Give libvlc a moment to stop cleanly; Program.Main force-exits afterwards regardless.
        Task.WaitAll(_panels.Select(p => p.ShutdownAsync()).ToArray(), TimeSpan.FromSeconds(2));
    }

    void LayoutPanels()
    {
        if (_panels.Any(p => p == null)) return;
        var area = ClientRectangle;
        if (_zoomed != null)
        {
            foreach (var p in _panels) p.Visible = p == _zoomed;
            _zoomed.Bounds = area;
            return;
        }

        var w = (area.Width - Gap * 3) / 2;
        var h = (area.Height - Gap * 3) / 2;
        for (var i = 0; i < _panels.Length; i++)
        {
            var col = i % 2;
            var row = i / 2;
            _panels[i].Bounds = new Rectangle(Gap + col * (w + Gap), Gap + row * (h + Gap), w, h);
            _panels[i].Visible = true;
        }
    }

    void ToggleZoom(CameraPanel panel)
    {
        _zoomed = _zoomed == null ? panel : null;
        LayoutPanels();
    }

    void ToggleFullscreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = _windowStateBeforeFullscreen;
        }
        else
        {
            _windowStateBeforeFullscreen = WindowState;
            // Re-maximizing after the border change makes the window cover the taskbar.
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
    }

    void OpenSettings()
    {
        using var dialog = new SettingsForm(_config.Cams);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _config.Cams = dialog.Cams;
        try
        {
            _config.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not save settings:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        for (var i = 0; i < AppConfig.Slots; i++) _panels[i].SetUri(_config.Cams[i]);
    }
}
