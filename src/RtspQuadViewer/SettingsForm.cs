namespace RtspQuadViewer;

/// <summary>Dialog for editing the four camera URIs.</summary>
sealed class SettingsForm : Form
{
    static readonly string[] Positions = ["top left", "top right", "bottom left", "bottom right"];

    readonly TextBox[] _inputs = new TextBox[AppConfig.Slots];
    readonly Label _error;

    public string[] Cams => _inputs.Select(t => t.Text.Trim()).ToArray();

    public SettingsForm(string[] cams)
    {
        Text = "Camera settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Enter the full RTSP URI for each quadrant. Leave a field empty to keep that quadrant blank.",
            Margin = new Padding(3, 0, 3, 10),
        }, 0, 0);
        layout.SetColumnSpan(layout.Controls[0], 2);

        for (var i = 0; i < AppConfig.Slots; i++)
        {
            var row = 1 + i * 2;
            layout.Controls.Add(new Label { AutoSize = true, Text = $"Camera {i + 1} ({Positions[i]})", Margin = new Padding(3, 6, 3, 2) }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row)!, 2);

            var input = _inputs[i] = new TextBox
            {
                Width = 520,
                Text = cams[i],
                Font = new Font(FontFamily.GenericMonospace, 9.5f),
                PlaceholderText = "rtsp://user:pass@192.168.1.x:554/stream1",
            };
            var clear = new Button { Text = "Clear", AutoSize = true };
            clear.Click += (_, _) => { input.Clear(); input.Focus(); };
            layout.Controls.Add(input, 0, row + 1);
            layout.Controls.Add(clear, 1, row + 1);
        }

        _error = new Label { AutoSize = true, ForeColor = Color.Firebrick, Margin = new Padding(3, 10, 3, 0) };
        layout.Controls.Add(_error, 0, 1 + AppConfig.Slots * 2);
        layout.SetColumnSpan(_error, 2);

        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => TrySave();
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 2 + AppConfig.Slots * 2);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    void TrySave()
    {
        for (var i = 0; i < AppConfig.Slots; i++)
        {
            if (AppConfig.Validate(_inputs[i].Text.Trim()) is { } error)
            {
                _error.Text = $"Camera {i + 1}: {error}";
                _inputs[i].Focus();
                return;
            }
        }
        DialogResult = DialogResult.OK;
    }
}
