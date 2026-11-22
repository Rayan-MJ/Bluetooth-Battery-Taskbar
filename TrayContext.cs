namespace BtBattery;

/// <summary>
/// Owns the tray icon and its popup menu, listens to <see cref="BtMonitor"/>, and
/// rebuilds the menu and tooltip whenever the set of connected devices changes.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly BtMonitor _monitor = new();

    // The monitor fires on a background thread, but WinForms controls are UI-thread
    // only. This hidden control is our marshaling point: BeginInvoke on it hops work
    // back onto the UI thread. Its handle is forced early so it's ready immediately.
    private readonly Control _marshal = new();

    public TrayContext()
    {
        _ = _marshal.Handle;

        _menu = new ContextMenuStrip();

        _tray = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Bluetooth Battery",
            Visible = true,
            ContextMenuStrip = _menu,
        };

        // Open the menu on left-click too. Calling _menu.Show() directly leaves the
        // menu without a foreground owner, so it won't dismiss on an outside click;
        // NotifyIcon's internal ShowContextMenu does the correct foreground handling.
        _tray.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                typeof(NotifyIcon)
                    .GetMethod("ShowContextMenu",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(_tray, null);
            }
        };

        RebuildMenu(_monitor.Snapshot());

        _monitor.DevicesChanged += devices =>
            _marshal.BeginInvoke(new Action(() => RebuildMenu(devices)));
        _monitor.Start();
    }

    private void RebuildMenu(IReadOnlyList<BtDevice> devices)
    {
        UpdateTooltip(devices);
        _menu.Items.Clear();

        if (devices.Count == 0)
        {
            _menu.Items.Add(new ToolStripMenuItem("No Bluetooth devices connected") { Enabled = false });
        }
        else
        {
            foreach (var device in devices)
            {
                var battery = device.BatteryPercent != null ? device.BatteryPercent + "%" : "-";
                _menu.Items.Add(new ToolStripMenuItem($"{device.Name}  {battery}"));
            }
        }

        _menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (s, e) => ExitApp();
        _menu.Items.Add(exit);
    }

    // Hover tooltip: one line per connected device. Windows caps the text at 63 chars.
    private void UpdateTooltip(IReadOnlyList<BtDevice> devices)
    {
        string text = devices.Count == 0
            ? "No Bluetooth devices connected"
            : string.Join(Environment.NewLine, devices.Select(d =>
                $"{d.Name} {(d.BatteryPercent != null ? d.BatteryPercent + "%" : "-")}"));

        if (text.Length > 63) text = text[..62] + "…";
        _tray.Text = text;
    }

    // Load tray_icon.png, crop its transparent margins, and fit the artwork into a
    // 32x32 square (preserving aspect ratio). Falls back to a system icon if missing.
    private static Icon LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tray_icon.png");
        if (!File.Exists(path)) return SystemIcons.Application;

        const int size = 32;
        using var src = new Bitmap(path);
        var crop = OpaqueBounds(src);

        using var square = new Bitmap(size, size);
        using (var g = Graphics.FromImage(square))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            float scale = Math.Min((float)size / crop.Width, (float)size / crop.Height);
            int w = (int)(crop.Width * scale);
            int h = (int)(crop.Height * scale);
            var dest = new Rectangle((size - w) / 2, (size - h) / 2, w, h);
            g.DrawImage(src, dest, crop, GraphicsUnit.Pixel);
        }
        return Icon.FromHandle(square.GetHicon());
    }

    // Smallest rectangle containing all non-transparent pixels.
    private static Rectangle OpaqueBounds(Bitmap bmp)
    {
        var full = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(full, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        var buffer = new byte[Math.Abs(stride) * bmp.Height];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
        bmp.UnlockBits(data);

        int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < bmp.Width; x++)
            {
                if (buffer[row + x * 4 + 3] > 10) // alpha (pixels are B,G,R,A)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        return maxX < 0 ? full : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    private void ExitApp()
    {
        _monitor.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _marshal.Dispose();
        ExitThread();
    }
}
