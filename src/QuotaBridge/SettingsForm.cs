namespace CodexQuota;

public sealed class SettingsForm : Form
{
    readonly string path;
    readonly NumericUpDown interval = new() { Minimum = 30, Maximum = 300, Increment = 30, Width = 140 };
    readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly ComboBox decimals = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly NumericUpDown cache = new() { Minimum = 10, Maximum = 60, Increment = 5, Width = 140 };
    readonly Label preview = new() { AutoSize = true, ForeColor = Color.FromArgb(0, 88, 160) };
    readonly Label status = new() { AutoSize = true, MaximumSize = new Size(490, 0) };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };

    public SettingsForm(string settingsPath)
    {
        SuspendLayout();
        path = settingsPath;
        Text = "Codex Quota · 插件选项";
        Font = new Font("Microsoft YaHei UI", 10F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(590, 560);
        ClientSize = new Size(570, 540);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(24), ColumnCount = 2, RowCount = 10 };
        for (int row = 0; row < 10; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
        void Wide(Control c, int row) { c.Margin = new Padding(0, 0, 0, 12); layout.Controls.Add(c, 0, row); layout.SetColumnSpan(c, 2); }
        void Row(string label, Control control, int row) {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 0, 16) }, 0, row);
            control.Margin = new Padding(0, 0, 0, 12); layout.Controls.Add(control, 1, row);
        }
        Wide(new Label { Text = "任务栏额度显示", AutoSize = true, Font = new Font(Font.FontFamily, 15F, FontStyle.Bold) }, 0);
        Wide(new Label { Text = "保存后自动生效，无需重启 TrafficMonitor。", AutoSize = true }, 1);
        mode.Items.AddRange(new object[] { "剩余额度（推荐）", "已用额度" });
        decimals.Items.AddRange(new object[] { "整数，例如 65%", "一位小数，例如 65.5%" });
        Row("刷新间隔（秒）", interval, 2);
        Row("百分比含义", mode, 3);
        Row("小数位", decimals, 4);
        Row("缓存有效期（分钟）", cache, 5);
        Wide(new Label { Text = "刷新可设 30–300 秒，建议 120 秒。失败后自动延长等待；\n受限时遵守服务端等待时间。缓存失败标记 *，过期显示 --。", AutoSize = true }, 6);
        Wide(preview, 7);
        Wide(status, 8);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var defaults = new Button { Text = "恢复默认", AutoSize = true };
        var save = new Button { Text = "保存", AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        defaults.Click += (_, _) => SetValues(new());
        save.Click += (_, _) => {
            try { Values().Save(path); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) {
                MessageBox.Show(this, "无法保存设置，请检查配置文件是否可写。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        buttons.Controls.AddRange(new Control[] { defaults, save, cancel }); Wide(buttons, 9);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        interval.ValueChanged += (_, _) => Preview(); mode.SelectedIndexChanged += (_, _) => Preview();
        decimals.SelectedIndexChanged += (_, _) => Preview(); cache.ValueChanged += (_, _) => Preview();
        SetValues(PluginSettings.Read(path));
        timer.Tick += (_, _) => ReadStatus(); timer.Start(); ReadStatus();
        ResumeLayout(true);
    }
    PluginSettings Values() => new((int)interval.Value, mode.SelectedIndex == 1, Math.Max(0, decimals.SelectedIndex), (int)cache.Value);
    void SetValues(PluginSettings s) { interval.Value = s.RefreshSeconds; mode.SelectedIndex = s.ShowUsed ? 1 : 0; decimals.SelectedIndex = s.DecimalPlaces; cache.Value = s.CacheMinutes; Preview(); }
    void Preview() {
        var s = Values(); var a = s.ShowUsed ? 34.5 : 65.5; var b = s.ShowUsed ? 8.5 : 91.5;
        if (s.DecimalPlaces == 0) { a = s.ShowUsed ? Math.Ceiling(a) : Math.Floor(a); b = s.ShowUsed ? Math.Ceiling(b) : Math.Floor(b); }
        preview.Text = $"示例（{(s.ShowUsed ? "已用" : "剩余")}）： 5h: {a.ToString(s.DecimalPlaces == 0 ? "F0" : "F1")}%    7d: {b.ToString(s.DecimalPlaces == 0 ? "F0" : "F1")}%";
    }
    void ReadStatus() {
        try {
            var snapshot = Path.Combine(Path.GetDirectoryName(path)!, "snapshot.ini");
            var lines = File.ReadAllLines(snapshot);
            var text = lines.FirstOrDefault(l => l.StartsWith("tooltip="))?[8..];
            status.Text = text ?? "等待后台首次刷新。";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "等待后台首次刷新。"; }
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }

    public static void Open(string path) {
        // Worker remains asynchronous; only the separate settings window needs STA.
        var thread = new Thread(() => { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); using var form = new SettingsForm(path); Application.Run(form); });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    }
}
