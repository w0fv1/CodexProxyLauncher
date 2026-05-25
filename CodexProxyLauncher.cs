using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexProxyLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Any(x => x.Equals("--launch", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    LauncherRuntime.Launch(LauncherConfig.Load(), null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Codex Proxy Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return;
            }

            if (args.Any(x => x.Equals("--create-shortcut", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    LauncherShortcut.CreateDirectLaunchShortcut(Application.ExecutablePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Codex Proxy Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class MainForm : Form
    {
        private readonly Panel contentPanel;
        private readonly Button homeNavButton;
        private readonly Button settingsNavButton;
        private readonly TextBox proxyTextBox;
        private readonly CheckBox restartCheckBox;
        private readonly CheckBox patchConfigCheckBox;
        private TextBox logTextBox;
        private readonly LauncherConfig config;
        private readonly ThemePalette palette;

        public MainForm()
        {
            Text = UiText.AppTitle;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(860, 560);
            MinimumSize = new Size(760, 500);
            Font = new Font("Segoe UI", 9.5F);
            palette = ThemePalette.Current();
            BackColor = palette.Window;
            ForeColor = palette.Text;
            NativeTheme.ApplySystemTitleBarTheme(Handle, palette.Dark);

            config = LauncherConfig.Load();

            var sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 188,
                Padding = new Padding(14, 18, 14, 18),
                BackColor = palette.Sidebar
            };

            var brand = new Label
            {
                Dock = DockStyle.Top,
                Height = 42,
                Text = UiText.AppTitle,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Regular),
                ForeColor = palette.Text,
                TextAlign = ContentAlignment.MiddleLeft
            };

            homeNavButton = NewNavButton(UiText.Home);
            homeNavButton.Top = 58;
            homeNavButton.Click += delegate { ShowHome(); };

            settingsNavButton = NewNavButton(UiText.Settings);
            settingsNavButton.Top = 104;
            settingsNavButton.Click += delegate { ShowSettings(); };

            sidebar.Controls.Add(settingsNavButton);
            sidebar.Controls.Add(homeNavButton);
            sidebar.Controls.Add(brand);

            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.Window,
                Padding = new Padding(36)
            };

            Controls.Add(contentPanel);
            Controls.Add(sidebar);

            proxyTextBox = NewTextBox(config.ProxyUrl);
            restartCheckBox = NewCheckBox(UiText.RestartCodex, config.RestartCodex);
            patchConfigCheckBox = NewCheckBox(UiText.PatchConfig, config.PatchCodexConfig);
            logTextBox = NewLogBox();

            ShowHome();
            Log(UiText.Ready(config.ProxyUrl));
        }

        private Button NewNavButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Left = 0,
                Width = 160,
                Height = 38,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = palette.Sidebar,
                ForeColor = palette.Text,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = palette.NavHover;
            button.FlatAppearance.MouseDownBackColor = palette.NavSelected;
            return button;
        }

        private void SelectNav(Button selected)
        {
            homeNavButton.BackColor = selected == homeNavButton ? palette.NavSelected : palette.Sidebar;
            settingsNavButton.BackColor = selected == settingsNavButton ? palette.NavSelected : palette.Sidebar;
        }

        private void ShowHome()
        {
            SelectNav(homeNavButton);
            contentPanel.Controls.Clear();

            var center = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.Window,
                ColumnCount = 1,
                RowCount = 5
            };
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var logo = new PictureBox
            {
                Size = new Size(112, 112),
                Anchor = AnchorStyles.None,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = palette.Window,
                Image = LoadCodexLogo()
            };

            var name = new Label
            {
                Text = "Codex",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 18F, FontStyle.Regular),
                ForeColor = palette.Text
            };

            var startButton = NewButton(UiText.Start, true, 180);
            startButton.Anchor = AnchorStyles.None;
            startButton.Click += delegate { RunAction(LaunchCodex); };

            center.Controls.Add(new Panel { BackColor = palette.Window }, 0, 0);
            center.Controls.Add(logo, 0, 1);
            center.Controls.Add(name, 0, 2);
            center.Controls.Add(startButton, 0, 3);
            center.Controls.Add(new Panel { BackColor = palette.Window }, 0, 4);
            contentPanel.Controls.Add(center);
        }

        private Image LoadCodexLogo()
        {
            try
            {
                var icon = IconTools.ExtractIcon(CodexApp.Find().ExePath, 128);
                if (icon != null) return icon.ToBitmap();
            }
            catch
            {
            }

            var bitmap = new Bitmap(96, 96);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(palette.Accent))
            using (var font = new Font("Segoe UI Semibold", 42F))
            {
                graphics.Clear(palette.Window);
                graphics.FillEllipse(brush, 8, 8, 80, 80);
                TextRenderer.DrawText(graphics, "C", font, new Rectangle(0, 10, 96, 72), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            return bitmap;
        }

        private void ShowSettings()
        {
            SelectNav(settingsNavButton);
            contentPanel.Controls.Clear();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.Window,
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = UiText.Settings,
                Dock = DockStyle.Top,
                Height = 48,
                Font = new Font("Segoe UI Semibold", 20F, FontStyle.Regular),
                ForeColor = palette.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 14)
            };

            var settingsCard = CreateCard();
            settingsCard.RowCount = 5;
            settingsCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsCard.Margin = new Padding(0, 0, 0, 18);

            var proxyLabel = NewLabel(UiText.ProxyUrl, true);
            settingsCard.Controls.Add(proxyLabel, 0, 0);
            settingsCard.Controls.Add(proxyTextBox, 0, 1);
            settingsCard.Controls.Add(restartCheckBox, 0, 2);
            settingsCard.Controls.Add(patchConfigCheckBox, 0, 3);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = palette.Card,
                Margin = new Padding(0, 16, 0, 0)
            };
            var shortcutButton = NewButton(UiText.CreateShortcut, false, 150);
            shortcutButton.Click += delegate { RunAction(CreateDesktopShortcut); };
            var saveButton = NewButton(UiText.SaveConfig, false, 120);
            saveButton.Click += delegate { RunAction(SaveConfig); };
            var unpatchButton = NewButton(UiText.RemovePatch, false, 130);
            unpatchButton.Click += delegate { RunAction(RemovePatch); };
            buttons.Controls.Add(shortcutButton);
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(unpatchButton);
            settingsCard.Controls.Add(buttons, 0, 4);

            var logCard = CreateCard();
            logCard.RowCount = 2;
            logCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            logCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var logLabel = NewLabel(UiText.Activity, true);
            logCard.Controls.Add(logLabel, 0, 0);
            logCard.Controls.Add(logTextBox, 0, 1);

            var footer = new Label
            {
                Text = UiText.ThemeHint,
                AutoSize = true,
                ForeColor = palette.SubtleText,
                Margin = new Padding(1, 14, 0, 0)
            };

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(settingsCard, 0, 1);
            root.Controls.Add(logCard, 0, 2);
            root.Controls.Add(footer, 0, 3);
            contentPanel.Controls.Add(root);
        }

        private TextBox NewTextBox(string text)
        {
            return new TextBox
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 32,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = palette.Input,
                ForeColor = palette.Text,
                Font = new Font("Segoe UI", 10F),
                Margin = new Padding(0, 0, 0, 14)
            };
        }

        private TextBox NewLogBox()
        {
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = palette.Log,
                ForeColor = palette.Text,
                Font = new Font("Consolas", 9F),
                Margin = new Padding(0)
            };
            return box;
        }

        private Label NewLabel(string text, bool strong)
        {
            return new Label
            {
                Text = text,
                Font = new Font(strong ? "Segoe UI Semibold" : "Segoe UI", 9.5F),
                AutoSize = true,
                ForeColor = palette.Text,
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        private TableLayoutPanel CreateCard()
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.Card,
                Padding = new Padding(18),
                ColumnCount = 1,
                Margin = new Padding(0)
            };
        }

        private CheckBox NewCheckBox(string text, bool isChecked)
        {
            return new CheckBox
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                ForeColor = palette.Text,
                BackColor = palette.Card,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 2, 0, 8)
            };
        }

        private Button NewButton(string text, bool primary, int width)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = false,
                Size = new Size(width, 40),
                Margin = new Padding(0, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                ForeColor = primary ? Color.White : palette.Text,
                BackColor = primary ? palette.Accent : palette.Button,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? palette.Accent : palette.Border;
            button.FlatAppearance.MouseOverBackColor = primary ? palette.AccentHover : palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = primary ? palette.AccentPressed : palette.ButtonPressed;
            return button;
        }

        private void RunAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.Message, UiText.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveConfig()
        {
            var proxy = ProxyTools.NormalizeProxyUrl(proxyTextBox.Text);
            config.ProxyUrl = proxy;
            config.RestartCodex = restartCheckBox.Checked;
            config.PatchCodexConfig = patchConfigCheckBox.Checked;
            config.Save();
            Log(UiText.SavedConfig(LauncherConfig.ConfigPath));
        }

        private void LaunchCodex()
        {
            SaveConfig();
            LauncherRuntime.Launch(config, Log);
        }

        private void CreateDesktopShortcut()
        {
            SaveConfig();
            var exePath = Application.ExecutablePath;
            var shortcutPath = LauncherShortcut.CreateDirectLaunchShortcut(exePath);
            Log(UiText.CreatedShortcut(shortcutPath));
        }

        private void RemovePatch()
        {
            CodexConfigPatch.Remove();
            Log(UiText.RemovedPatch);
        }

        private void Log(string message)
        {
            logTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

    }

    internal static class UiText
    {
        private static readonly bool Chinese = IsChineseSystem();

        public static string AppTitle { get { return Chinese ? "Codex 代理启动器" : "Codex Proxy Launcher"; } }
        public static string Subtitle { get { return Chinese ? "不启用系统代理，仅让 Codex Desktop 走本地代理。" : "Start Codex Desktop through a local proxy without changing the system proxy."; } }
        public static string Home { get { return Chinese ? "主页" : "Home"; } }
        public static string Settings { get { return Chinese ? "设置" : "Settings"; } }
        public static string ProxyUrl { get { return Chinese ? "代理地址" : "Proxy URL"; } }
        public static string RestartCodex { get { return Chinese ? "启动前关闭现有 Microsoft Store 版 Codex 进程" : "Close existing Microsoft Store Codex processes before launch"; } }
        public static string PatchConfig { get { return Chinese ? "为 Codex 子进程写入 .codex\\config.toml 代理配置" : "Patch .codex\\config.toml for Codex child processes"; } }
        public static string Start { get { return Chinese ? "开始" : "Start"; } }
        public static string LaunchCodex { get { return Chinese ? "启动 Codex" : "Launch Codex"; } }
        public static string CreateShortcut { get { return Chinese ? "创建快捷方式" : "Create Shortcut"; } }
        public static string SaveConfig { get { return Chinese ? "保存配置" : "Save Config"; } }
        public static string RemovePatch { get { return Chinese ? "移除补丁" : "Remove Patch"; } }
        public static string Activity { get { return Chinese ? "活动日志" : "Activity"; } }
        public static string RemovedPatch { get { return Chinese ? "已移除 Codex 配置代理补丁。" : "Removed Codex config proxy patch."; } }
        public static string ThemeHint { get { return Chinese ? "外观跟随 Windows 系统设置。" : "Appearance follows Windows system settings."; } }

        public static string Ready(string proxyUrl)
        {
            return Chinese ? "就绪。默认代理：" + proxyUrl : "Ready. Default proxy: " + proxyUrl;
        }

        public static string SavedConfig(string path)
        {
            return Chinese ? "已保存配置：" + path : "Saved config: " + path;
        }

        public static string CreatedShortcut(string path)
        {
            return Chinese ? "已创建快捷方式：" + path : "Created shortcut: " + path;
        }

        private static bool IsChineseSystem()
        {
            var name = System.Globalization.CultureInfo.CurrentUICulture.Name;
            return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class ThemePalette
    {
        public bool Dark;
        public Color Window;
        public Color Sidebar;
        public Color Card;
        public Color Input;
        public Color Log;
        public Color NavSelected;
        public Color NavHover;
        public Color Button;
        public Color ButtonHover;
        public Color ButtonPressed;
        public Color Border;
        public Color Text;
        public Color SubtleText;
        public Color Accent;
        public Color AccentHover;
        public Color AccentPressed;

        public static ThemePalette Current()
        {
            var dark = IsDarkMode();
            if (dark)
            {
                return new ThemePalette
                {
                    Dark = true,
                    Window = Color.FromArgb(32, 32, 32),
                    Sidebar = Color.FromArgb(28, 28, 28),
                    Card = Color.FromArgb(43, 43, 43),
                    Input = Color.FromArgb(36, 36, 36),
                    Log = Color.FromArgb(28, 28, 28),
                    NavSelected = Color.FromArgb(55, 55, 55),
                    NavHover = Color.FromArgb(48, 48, 48),
                    Button = Color.FromArgb(55, 55, 55),
                    ButtonHover = Color.FromArgb(65, 65, 65),
                    ButtonPressed = Color.FromArgb(48, 48, 48),
                    Border = Color.FromArgb(72, 72, 72),
                    Text = Color.FromArgb(245, 245, 245),
                    SubtleText = Color.FromArgb(170, 170, 170),
                    Accent = Color.FromArgb(0, 120, 212),
                    AccentHover = Color.FromArgb(16, 137, 230),
                    AccentPressed = Color.FromArgb(0, 96, 170)
                };
            }

            return new ThemePalette
            {
                Dark = false,
                Window = Color.FromArgb(243, 243, 243),
                Sidebar = Color.FromArgb(250, 250, 250),
                Card = Color.White,
                Input = Color.White,
                Log = Color.FromArgb(250, 250, 250),
                NavSelected = Color.FromArgb(235, 243, 252),
                NavHover = Color.FromArgb(242, 242, 242),
                Button = Color.FromArgb(249, 249, 249),
                ButtonHover = Color.FromArgb(246, 246, 246),
                ButtonPressed = Color.FromArgb(238, 238, 238),
                Border = Color.FromArgb(224, 224, 224),
                Text = Color.FromArgb(31, 31, 31),
                SubtleText = Color.FromArgb(96, 96, 96),
                Accent = Color.FromArgb(0, 120, 212),
                AccentHover = Color.FromArgb(0, 103, 192),
                AccentPressed = Color.FromArgb(0, 90, 158)
            };
        }

        private static bool IsDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class NativeTheme
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void ApplySystemTitleBarTheme(IntPtr handle, bool dark)
        {
            if (handle == IntPtr.Zero) return;

            try
            {
                var value = dark ? 1 : 0;
                DwmSetWindowAttribute(handle, 20, ref value, sizeof(int));
                DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
            }
            catch
            {
                // Older Windows builds simply ignore the title bar theme hint.
            }
        }
    }

    internal static class IconTools
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint PrivateExtractIcons(
            string szFileName,
            int nIconIndex,
            int cxIcon,
            int cyIcon,
            IntPtr[] phicon,
            uint[] piconid,
            uint nIcons,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon ExtractIcon(string path, int size)
        {
            var handles = new IntPtr[1];
            var ids = new uint[1];
            var count = PrivateExtractIcons(path, 0, size, size, handles, ids, 1, 0);
            if (count > 0 && handles[0] != IntPtr.Zero)
            {
                try
                {
                    return (Icon)Icon.FromHandle(handles[0]).Clone();
                }
                finally
                {
                    DestroyIcon(handles[0]);
                }
            }

            var fallback = Icon.ExtractAssociatedIcon(path);
            return fallback == null ? null : (Icon)fallback.Clone();
        }
    }

    internal static class LauncherRuntime
    {
        public static void Launch(LauncherConfig config, Action<string> log)
        {
            config.ProxyUrl = ProxyTools.NormalizeProxyUrl(config.ProxyUrl);

            var codex = CodexApp.Find();
            Log(log, "Codex: " + codex.ExePath);

            if (config.PatchCodexConfig)
            {
                CodexConfigPatch.Apply(config.ProxyUrl);
                Log(log, "Patched Codex child process config.");
            }

            if (config.RestartCodex)
            {
                var stopped = ProcessTools.StopCodexStoreProcesses(codex.InstallLocation);
                Log(log, "Stopped Codex Store processes: " + stopped);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = codex.ExePath,
                WorkingDirectory = codex.AppDir,
                UseShellExecute = false
            };
            ProxyTools.AddProxyEnvironment(startInfo, config.ProxyUrl);
            startInfo.Arguments = "--proxy-server=" + ProxyTools.QuoteArg(config.ProxyUrl) +
                                  " --proxy-bypass-list=" + ProxyTools.QuoteArg("<-loopback>;localhost;127.0.0.1;::1");

            Process.Start(startInfo);
            Log(log, "Started Codex with proxy: " + config.ProxyUrl);
        }

        private static void Log(Action<string> log, string message)
        {
            if (log != null) log(message);
        }
    }

    internal static class ProxyTools
    {
        public static string NormalizeProxyUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Proxy URL cannot be empty.");
            }

            var trimmed = value.Trim();
            if (!trimmed.Contains("://"))
            {
                trimmed = "http://" + trimmed;
            }

            var uri = new Uri(trimmed);
            if (string.IsNullOrEmpty(uri.Host) || uri.Port < 0)
            {
                throw new InvalidOperationException("Proxy URL must include host and port.");
            }

            return uri.AbsoluteUri.TrimEnd('/');
        }

        public static void AddProxyEnvironment(ProcessStartInfo startInfo, string proxyUrl)
        {
            var bypass = "localhost,127.0.0.1,::1";
            SetEnv(startInfo, "HTTP_PROXY", proxyUrl);
            SetEnv(startInfo, "HTTPS_PROXY", proxyUrl);
            SetEnv(startInfo, "ALL_PROXY", proxyUrl);
            SetEnv(startInfo, "http_proxy", proxyUrl);
            SetEnv(startInfo, "https_proxy", proxyUrl);
            SetEnv(startInfo, "all_proxy", proxyUrl);
            SetEnv(startInfo, "NO_PROXY", bypass);
            SetEnv(startInfo, "no_proxy", bypass);
            SetEnv(startInfo, "NODE_USE_ENV_PROXY", "1");
        }

        public static string QuoteArg(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void SetEnv(ProcessStartInfo startInfo, string key, string value)
        {
            if (startInfo.EnvironmentVariables.ContainsKey(key))
            {
                startInfo.EnvironmentVariables[key] = value;
            }
            else
            {
                startInfo.EnvironmentVariables.Add(key, value);
            }
        }
    }

    internal sealed class LauncherConfig
    {
        public static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexProxyLauncher");

        public static readonly string ConfigPath = Path.Combine(ConfigDir, "config.ini");

        public string ProxyUrl = "http://127.0.0.1:7890";
        public bool RestartCodex = true;
        public bool PatchCodexConfig = true;

        public static LauncherConfig Load()
        {
            var config = new LauncherConfig();
            if (!File.Exists(ConfigPath))
            {
                return config;
            }

            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var index = line.IndexOf('=');
                if (index <= 0) continue;

                var key = line.Substring(0, index).Trim();
                var value = line.Substring(index + 1).Trim();
                if (key.Equals("ProxyUrl", StringComparison.OrdinalIgnoreCase)) config.ProxyUrl = value;
                if (key.Equals("RestartCodex", StringComparison.OrdinalIgnoreCase)) config.RestartCodex = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (key.Equals("PatchCodexConfig", StringComparison.OrdinalIgnoreCase)) config.PatchCodexConfig = value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            return config;
        }

        public void Save()
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllLines(ConfigPath, new[]
            {
                "ProxyUrl=" + ProxyUrl,
                "RestartCodex=" + RestartCodex.ToString().ToLowerInvariant(),
                "PatchCodexConfig=" + PatchCodexConfig.ToString().ToLowerInvariant()
            });
        }
    }

    internal sealed class CodexApp
    {
        public string InstallLocation;
        public string AppDir;
        public string ExePath;

        public static CodexApp Find()
        {
            var running = FindFromRunningProcess();
            if (running != null)
            {
                return running;
            }

            var installLocation = FindInstallLocationWithPowerShell();
            if (!string.IsNullOrWhiteSpace(installLocation))
            {
                var appDir = Path.Combine(installLocation.Trim(), "app");
                var exe = Path.Combine(appDir, "Codex.exe");
                if (File.Exists(exe))
                {
                    return new CodexApp
                    {
                        InstallLocation = installLocation.Trim(),
                        AppDir = appDir,
                        ExePath = exe
                    };
                }
            }

            throw new InvalidOperationException("Could not find Microsoft Store Codex at C:\\Program Files\\WindowsApps\\OpenAI.Codex_*\\app\\Codex.exe.");
        }

        private static CodexApp FindFromRunningProcess()
        {
            foreach (var process in Process.GetProcessesByName("Codex"))
            {
                string path;
                try
                {
                    path = process.MainModule == null ? null : process.MainModule.FileName;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(path)) continue;
                var marker = "\\OpenAI.Codex_";
                var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0) continue;
                var appIndex = path.IndexOf("\\app\\", markerIndex, StringComparison.OrdinalIgnoreCase);
                if (appIndex < 0) continue;
                if (!path.EndsWith("\\app\\Codex.exe", StringComparison.OrdinalIgnoreCase)) continue;

                var installLocation = path.Substring(0, appIndex);
                return new CodexApp
                {
                    InstallLocation = installLocation,
                    AppDir = Path.Combine(installLocation, "app"),
                    ExePath = path
                };
            }

            return null;
        }

        private static string FindInstallLocationWithPowerShell()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-AppxPackage -Name OpenAI.Codex | Sort-Object Version -Descending | Select-Object -First 1).InstallLocation\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null) return null;
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return process.ExitCode == 0 ? output : null;
            }
        }
    }

    internal static class ProcessTools
    {
        public static int StopCodexStoreProcesses(string installLocation)
        {
            var count = 0;
            foreach (var process in Process.GetProcesses())
            {
                string path;
                try
                {
                    path = process.MainModule == null ? null : process.MainModule.FileName;
                }
                catch
                {
                    continue;
                }

                if (path == null) continue;
                if (!path.StartsWith(installLocation, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    process.Kill();
                    count++;
                }
                catch
                {
                    // Best effort; Codex may already be exiting.
                }
            }

            return count;
        }
    }

    internal static class CodexConfigPatch
    {
        private const string Begin = "# BEGIN codex-proxy launcher";
        private const string End = "# END codex-proxy launcher";

        public static void Apply(string proxyUrl)
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                throw new InvalidOperationException("Codex config was not found: " + configPath);
            }

            var content = File.ReadAllText(configPath, Encoding.UTF8);
            var clean = RemoveBlock(content);
            var block = BuildBlock(proxyUrl);
            string updated;

            var table = "[mcp_servers.node_repl.env]";
            var index = clean.IndexOf(table, StringComparison.Ordinal);
            if (index >= 0)
            {
                var insertAt = index + table.Length;
                updated = clean.Substring(0, insertAt) + block + clean.Substring(insertAt);
            }
            else
            {
                updated = clean.TrimEnd() + Environment.NewLine + table + block + Environment.NewLine;
            }

            File.Copy(configPath, configPath + ".codex-proxy.bak", true);
            File.WriteAllText(configPath, updated, new UTF8Encoding(false));
        }

        public static void Remove()
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath)) return;
            var content = File.ReadAllText(configPath, Encoding.UTF8);
            var clean = RemoveBlock(content);
            File.Copy(configPath, configPath + ".codex-proxy.bak", true);
            File.WriteAllText(configPath, clean, new UTF8Encoding(false));
        }

        private static string BuildBlock(string proxyUrl)
        {
            var bypass = "localhost,127.0.0.1,::1";
            return Environment.NewLine +
                   Begin + Environment.NewLine +
                   "HTTP_PROXY = \"" + EscapeToml(proxyUrl) + "\"" + Environment.NewLine +
                   "HTTPS_PROXY = \"" + EscapeToml(proxyUrl) + "\"" + Environment.NewLine +
                   "ALL_PROXY = \"" + EscapeToml(proxyUrl) + "\"" + Environment.NewLine +
                   "http_proxy = \"" + EscapeToml(proxyUrl) + "\"" + Environment.NewLine +
                   "https_proxy = \"" + EscapeToml(proxyUrl) + "\"" + Environment.NewLine +
                   "all_proxy = \"" + EscapeToml(proxyUrl) + "\"" + Environment.NewLine +
                   "NO_PROXY = \"" + bypass + "\"" + Environment.NewLine +
                   "no_proxy = \"" + bypass + "\"" + Environment.NewLine +
                   "NODE_USE_ENV_PROXY = \"1\"" + Environment.NewLine +
                   End + Environment.NewLine;
        }

        private static string RemoveBlock(string content)
        {
            var start = content.IndexOf(Begin, StringComparison.Ordinal);
            if (start < 0) return content;

            var lineStart = content.LastIndexOf('\n', start);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var end = content.IndexOf(End, start, StringComparison.Ordinal);
            if (end < 0) return content;

            var lineEnd = content.IndexOf('\n', end);
            lineEnd = lineEnd < 0 ? content.Length : lineEnd + 1;
            return content.Remove(lineStart, lineEnd - lineStart);
        }

        private static string EscapeToml(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string GetConfigPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "config.toml");
        }
    }

    internal static class ShortcutTools
    {
        public static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string iconPath)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("WScript.Shell COM object is unavailable.");
            }

            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments ?? "";
            shortcut.WorkingDirectory = workingDirectory ?? "";
            shortcut.IconLocation = iconPath ?? targetPath;
            shortcut.Save();
        }
    }

    internal static class LauncherShortcut
    {
        public static string CreateDirectLaunchShortcut(string launcherExePath)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktop, "Codex Proxy.lnk");
            string iconPath;
            try
            {
                iconPath = CodexApp.Find().ExePath;
            }
            catch
            {
                iconPath = launcherExePath;
            }

            ShortcutTools.CreateShortcut(
                shortcutPath,
                launcherExePath,
                "--launch",
                Path.GetDirectoryName(launcherExePath),
                iconPath);

            return shortcutPath;
        }
    }
}
