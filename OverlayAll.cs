// OverlayAll - overlay escuro, click-through e always-on-top sobre toda a area de trabalho.
// Compativel com o csc.exe do .NET Framework 4.x (C# 5).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OverlayAll
{
    internal static class Native
    {
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int WM_HOTKEY = 0x0312;
        public const int WM_DPICHANGED = 0x02E0;
        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int MA_NOACTIVATE = 3;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint WDA_NONE = 0x0;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        public static void EnableDpiAwareness()
        {
            // Per-monitor v2 garante que as coordenadas da VirtualScreen sejam pixels reais,
            // mesmo com monitores de escalas diferentes.
            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch (EntryPointNotFoundException) { }
            try { SetProcessDPIAware(); } catch { }
        }
    }

    internal sealed class Settings
    {
        public int OpacityPercent = 20;
        public bool Enabled = true;
        public bool HideFromCapture = true;
        public int? ButtonX;
        public int? ButtonY;

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OverlayAll");
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string[] kv = raw.Split(new[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim();
                    string val = kv[1].Trim();
                    int n;
                    bool b;
                    if (key == "opacity" && int.TryParse(val, out n)) s.OpacityPercent = Clamp(n);
                    else if (key == "enabled" && bool.TryParse(val, out b)) s.Enabled = b;
                    else if (key == "hideFromCapture" && bool.TryParse(val, out b)) s.HideFromCapture = b;
                    else if (key == "buttonX" && int.TryParse(val, out n)) s.ButtonX = n;
                    else if (key == "buttonY" && int.TryParse(val, out n)) s.ButtonY = n;
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                List<string> lines = new List<string>();
                lines.Add("opacity=" + OpacityPercent);
                lines.Add("enabled=" + Enabled);
                lines.Add("hideFromCapture=" + HideFromCapture);
                if (ButtonX.HasValue && ButtonY.HasValue)
                {
                    lines.Add("buttonX=" + ButtonX.Value);
                    lines.Add("buttonY=" + ButtonY.Value);
                }
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }

        public static int Clamp(int v)
        {
            if (v < 5) return 5;
            if (v > 90) return 90;
            return v;
        }
    }

    internal sealed class HelpDialog : Form
    {
        public HelpDialog(int opacityPercent)
        {
            Text = "OverlayAll";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16);
            // Fundo escuro e texto cinza-claro (nunca branco puro): o app serve a quem tem fotofobia.
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.FromArgb(205, 205, 205);

            TableLayoutPanel table = new TableLayoutPanel();
            table.AutoSize = true;
            table.ColumnCount = 2;
            table.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "Overlay ativo (" + opacityPercent + "%)";
            title.Font = new Font(Font.FontFamily, Font.Size + 5, FontStyle.Bold);
            title.AutoSize = true;
            title.Margin = new Padding(0, 0, 0, 10);
            table.Controls.Add(title);
            table.SetColumnSpan(title, 2);

            string[,] rows =
            {
                { "Ctrl+Alt+O", "Liga/desliga o overlay" },
                { "Ctrl+Alt+\u2191", "Aumenta a opacidade (+5%)" },
                { "Ctrl+Alt+\u2193", "Diminui a opacidade (-5%)" },
                { "Ctrl+Alt+Q", "Sai do programa" }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Label key = new Label();
                key.Text = rows[i, 0];
                key.Font = new Font(Font.FontFamily, Font.Size + 3, FontStyle.Bold);
                key.AutoSize = true;
                key.Margin = new Padding(0, 2, 16, 2);
                Label action = new Label();
                action.Text = rows[i, 1];
                action.Font = new Font(Font.FontFamily, Font.Size + 3);
                action.AutoSize = true;
                action.Margin = new Padding(0, 2, 0, 2);
                table.Controls.Add(key);
                table.Controls.Add(action);
            }

            Label tip = new Label();
            tip.Text = "Tambem pelo icone na bandeja do sistema\n(clique direito para o menu, duplo clique liga/desliga).";
            tip.AutoSize = true;
            tip.Margin = new Padding(0, 10, 0, 10);
            table.Controls.Add(tip);
            table.SetColumnSpan(tip, 2);

            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.AutoSize = true;
            ok.MinimumSize = new Size(90, 28);
            ok.Anchor = AnchorStyles.Right;
            ok.Margin = new Padding(0);
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Color.FromArgb(58, 58, 58);
            ok.ForeColor = Color.FromArgb(205, 205, 205);
            ok.FlatAppearance.BorderColor = Color.FromArgb(110, 110, 110);
            ok.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 75, 75);
            ok.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 48, 48);
            table.Controls.Add(ok);
            table.SetColumnSpan(ok, 2);

            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = ok;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Barra de titulo escura (Windows 10 1809+ / 11); em versoes antigas o erro e ignorado.
            int on = 1;
            try { Native.DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)); }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            Activate();
        }
    }

    // Botao pequeno e escuro, grudado na lateral direita por padrao; clique abre o modal, arrastar move.
    internal sealed class FloatingButton : Form
    {
        private readonly Action onClick;
        private readonly Action<Point> onMoved;
        private Point downCursor;
        private Point downLocation;
        private bool pressed;
        private bool dragging;

        public FloatingButton(Action onClick, Point? saved, Action<Point> onMoved)
        {
            this.onClick = onClick;
            this.onMoved = onMoved;

            float scale;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Opacity = 0.9;
            BackColor = Color.FromArgb(45, 45, 45);
            ForeColor = Color.FromArgb(205, 205, 205);
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f, FontStyle.Bold);
            Size = new Size((int)(30 * scale), (int)(48 * scale));
            Location = saved.HasValue ? saved.Value : DefaultLocation();
            EnsureOnScreen();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        // Clicar/arrastrar nao pode roubar o foco do app em uso.
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE)
            {
                m.Result = new IntPtr(Native.MA_NOACTIVATE);
                return;
            }
            base.WndProc(ref m);
        }

        public void ApplyAffinity(bool hideFromCapture)
        {
            if (!IsHandleCreated) return;
            Native.SetWindowDisplayAffinity(Handle, hideFromCapture ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE);
        }

        private Point DefaultLocation()
        {
            Screen primary = Screen.PrimaryScreen;
            return new Point(primary.Bounds.Right - Width,
                primary.WorkingArea.Top + (primary.WorkingArea.Height - Height) / 2);
        }

        // Se a posicao salva ficou fora de qualquer monitor (monitor removido), volta ao padrao.
        public void EnsureOnScreen()
        {
            foreach (Screen s in Screen.AllScreens)
            {
                Rectangle visible = Rectangle.Intersect(s.WorkingArea, Bounds);
                if (visible.Width * visible.Height >= Width * Height / 2) return;
            }
            Location = DefaultLocation();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = true;
            dragging = false;
            downCursor = Cursor.Position;
            downLocation = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pressed) return;
            Point c = Cursor.Position;
            int dx = c.X - downCursor.X;
            int dy = c.Y - downCursor.Y;
            if (!dragging)
            {
                Size drag = SystemInformation.DragSize;
                if (Math.Abs(dx) <= drag.Width / 2 && Math.Abs(dy) <= drag.Height / 2) return;
                dragging = true;
                Cursor = Cursors.SizeAll;
            }
            Rectangle vs = SystemInformation.VirtualScreen;
            int x = Math.Max(vs.Left, Math.Min(vs.Right - Width, downLocation.X + dx));
            int y = Math.Max(vs.Top, Math.Min(vs.Bottom - Height, downLocation.Y + dy));
            Location = new Point(x, y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !pressed) return;
            pressed = false;
            Cursor = Cursors.Hand;
            if (dragging)
            {
                dragging = false;
                onMoved(Location);
            }
            else
            {
                onClick();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen border = new Pen(Color.FromArgb(110, 110, 110)))
            {
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }
            TextRenderer.DrawText(e.Graphics, "?", Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    internal sealed class OverlayForm : Form
    {
        private const int HK_TOGGLE = 1;
        private const int HK_UP = 2;
        private const int HK_DOWN = 3;
        private const int HK_EXIT = 4;

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "OverlayAll";
        public const string StartupArg = "--startup";

        private readonly Settings settings;
        private readonly bool showHelpOnStart;
        private bool helpOpen;
        private FloatingButton fab;
        private readonly NotifyIcon tray;
        private readonly System.Windows.Forms.Timer keepOnTop;
        private ToolStripMenuItem toggleItem;
        private ToolStripMenuItem captureItem;
        private ToolStripMenuItem startupItem;
        private ToolStripMenuItem opacityMenu;

        public OverlayForm(Settings settings, bool showHelpOnStart)
        {
            this.settings = settings;
            this.showHelpOnStart = showHelpOnStart;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            TopMost = true;
            Text = AppName;
            Bounds = SystemInformation.VirtualScreen;
            ApplyOpacity();

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Shield;
            tray.ContextMenuStrip = BuildMenu();
            tray.Visible = true;
            tray.DoubleClick += delegate { ToggleEnabled(); };
            UpdateTrayText();

            // Outras janelas "topmost" podem subir por cima; reafirma posicao e tamanho periodicamente.
            keepOnTop = new System.Windows.Forms.Timer();
            keepOnTop.Interval = 2000;
            keepOnTop.Tick += delegate { ReassertTopmost(); };
            keepOnTop.Start();

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOPMOST
                              | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeys();
            ApplyCaptureAffinity();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Botao "owned" pelo overlay: fica sempre acima dele (sem escurecer) e some/aparece junto com ele.
            Point? saved = null;
            if (settings.ButtonX.HasValue && settings.ButtonY.HasValue)
                saved = new Point(settings.ButtonX.Value, settings.ButtonY.Value);
            fab = new FloatingButton(ShowHelp, saved, delegate(Point p)
            {
                settings.ButtonX = p.X;
                settings.ButtonY = p.Y;
                settings.Save();
            });
            fab.Show(this);
            fab.ApplyAffinity(settings.HideFromCapture);
            if (!settings.Enabled) Hide();
            if (showHelpOnStart) BeginInvoke(new MethodInvoker(ShowHelp));
        }

        // Dialogo "dono" = overlay: janelas owned ficam sempre acima do owner, mesmo com o overlay topmost.
        private void ShowHelp()
        {
            if (helpOpen) return;
            helpOpen = true;
            try
            {
                using (HelpDialog dlg = new HelpDialog(settings.OpacityPercent))
                {
                    dlg.ShowDialog(this);
                }
            }
            finally { helpOpen = false; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case HK_TOGGLE: ToggleEnabled(); break;
                    case HK_UP: SetOpacity(settings.OpacityPercent + 5); break;
                    case HK_DOWN: SetOpacity(settings.OpacityPercent - 5); break;
                    case HK_EXIT: Close(); break;
                }
                return;
            }
            if (m.Msg == Native.WM_MOUSEACTIVATE)
            {
                m.Result = new IntPtr(Native.MA_NOACTIVATE);
                return;
            }
            if (m.Msg == Native.WM_DPICHANGED)
            {
                // Ignora o retangulo sugerido pelo Windows; o overlay sempre cobre a VirtualScreen.
                FitToScreens();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            keepOnTop.Stop();
            Native.UnregisterHotKey(Handle, HK_TOGGLE);
            Native.UnregisterHotKey(Handle, HK_UP);
            Native.UnregisterHotKey(Handle, HK_DOWN);
            Native.UnregisterHotKey(Handle, HK_EXIT);
            tray.Visible = false;
            tray.Dispose();
            settings.Save();
            base.OnFormClosing(e);
        }

        private void RegisterHotkeys()
        {
            uint mods = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT;
            string failed = "";
            if (!Native.RegisterHotKey(Handle, HK_TOGGLE, mods, (uint)Keys.O)) failed += " Ctrl+Alt+O";
            if (!Native.RegisterHotKey(Handle, HK_UP, mods & ~Native.MOD_NOREPEAT, (uint)Keys.Up)) failed += " Ctrl+Alt+Seta Cima";
            if (!Native.RegisterHotKey(Handle, HK_DOWN, mods & ~Native.MOD_NOREPEAT, (uint)Keys.Down)) failed += " Ctrl+Alt+Seta Baixo";
            if (!Native.RegisterHotKey(Handle, HK_EXIT, mods, (uint)Keys.Q)) failed += " Ctrl+Alt+Q";
            if (failed.Length > 0)
            {
                tray.ShowBalloonTip(5000, AppName,
                    "Atalhos ja em uso por outro programa:" + failed + ". Use o icone da bandeja.",
                    ToolTipIcon.Warning);
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            toggleItem = new ToolStripMenuItem("Overlay ativo  (Ctrl+Alt+O)");
            toggleItem.Click += delegate { ToggleEnabled(); };
            menu.Items.Add(toggleItem);

            opacityMenu = new ToolStripMenuItem("Opacidade  (Ctrl+Alt+Setas)");
            int[] presets = { 10, 15, 20, 25, 30, 40, 50, 60, 70 };
            foreach (int p in presets)
            {
                int value = p;
                ToolStripMenuItem item = new ToolStripMenuItem(value + "%");
                item.Tag = value;
                item.Click += delegate { SetOpacity(value); };
                opacityMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(opacityMenu);

            captureItem = new ToolStripMenuItem("Ocultar em prints e compartilhamento de tela");
            captureItem.Click += delegate
            {
                settings.HideFromCapture = !settings.HideFromCapture;
                ApplyCaptureAffinity();
                settings.Save();
                RefreshMenuItems();
            };
            menu.Items.Add(captureItem);

            startupItem = new ToolStripMenuItem("Iniciar com o Windows");
            startupItem.Click += delegate
            {
                SetStartup(!IsStartupEnabled());
                RefreshMenuItems();
            };
            menu.Items.Add(startupItem);

            ToolStripMenuItem helpItem = new ToolStripMenuItem("Mostrar controles");
            helpItem.Click += delegate { ShowHelp(); };
            menu.Items.Add(helpItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Sair  (Ctrl+Alt+Q)");
            exitItem.Click += delegate { Close(); };
            menu.Items.Add(exitItem);

            menu.Opening += delegate { RefreshMenuItems(); };
            RefreshMenuItems();
            return menu;
        }

        private void RefreshMenuItems()
        {
            toggleItem.Checked = settings.Enabled;
            captureItem.Checked = settings.HideFromCapture;
            startupItem.Checked = IsStartupEnabled();
            foreach (ToolStripItem i in opacityMenu.DropDownItems)
            {
                ToolStripMenuItem mi = i as ToolStripMenuItem;
                if (mi != null) mi.Checked = (int)mi.Tag == settings.OpacityPercent;
            }
        }

        private void ToggleEnabled()
        {
            settings.Enabled = !settings.Enabled;
            if (settings.Enabled)
            {
                FitToScreens();
                Show();
                ReassertTopmost();
            }
            else
            {
                Hide();
            }
            settings.Save();
            UpdateTrayText();
            RefreshMenuItems();
        }

        private void SetOpacity(int percent)
        {
            settings.OpacityPercent = Settings.Clamp(percent);
            ApplyOpacity();
            settings.Save();
            UpdateTrayText();
            RefreshMenuItems();
        }

        private void ApplyOpacity()
        {
            Opacity = settings.OpacityPercent / 100.0;
        }

        private void ApplyCaptureAffinity()
        {
            if (!IsHandleCreated) return;
            // WDA_EXCLUDEFROMCAPTURE exige Windows 10 2004+; em versoes antigas simplesmente falha.
            Native.SetWindowDisplayAffinity(Handle,
                settings.HideFromCapture ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE);
            if (fab != null) fab.ApplyAffinity(settings.HideFromCapture);
        }

        private void UpdateTrayText()
        {
            tray.Text = AppName + " - " + (settings.Enabled ? settings.OpacityPercent + "%" : "desligado");
        }

        private void OnDisplayChanged(object sender, EventArgs e)
        {
            if (InvokeRequired) BeginInvoke(new MethodInvoker(FitToScreens));
            else FitToScreens();
        }

        private void FitToScreens()
        {
            Rectangle r = SystemInformation.VirtualScreen;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, r.X, r.Y, r.Width, r.Height, Native.SWP_NOACTIVATE);
            if (fab != null) fab.EnsureOnScreen();
        }

        private void ReassertTopmost()
        {
            if (!settings.Enabled || !Visible) return;
            if (Bounds != SystemInformation.VirtualScreen) FitToScreens();
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return k != null && k.GetValue(AppName) != null;
                }
            }
            catch { return false; }
        }

        private static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enable) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\" " + StartupArg);
                    else k.DeleteValue(AppName, false);
                }
            }
            catch { }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool isNew;
            using (Mutex mutex = new Mutex(true, @"Local\OverlayAll.SingleInstance", out isNew))
            {
                if (!isNew)
                {
                    MessageBox.Show("OverlayAll ja esta em execucao (veja o icone na bandeja).", "OverlayAll",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Native.EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool fromStartup = Array.IndexOf(args, OverlayForm.StartupArg) >= 0;
                Application.Run(new OverlayForm(Settings.Load(), !fromStartup));
            }
        }
    }
}
