using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

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
        private readonly TextBox proxyTextBox;
        private readonly CheckBox restartCheckBox;
        private readonly CheckBox patchConfigCheckBox;
        private readonly TextBox logTextBox;
        private readonly LauncherConfig config;

        public MainForm()
        {
            Text = "Codex Proxy Launcher";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 430);
            MinimumSize = new Size(620, 430);
            Font = new Font("Segoe UI", 9F);

            config = LauncherConfig.Load();

            var title = new Label
            {
                Text = "Codex Proxy Launcher",
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 16)
            };

            var proxyLabel = new Label
            {
                Text = "Proxy URL",
                AutoSize = true,
                Location = new Point(22, 68)
            };

            proxyTextBox = new TextBox
            {
                Text = config.ProxyUrl,
                Location = new Point(24, 92),
                Width = 560,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            restartCheckBox = new CheckBox
            {
                Text = "Close existing Microsoft Store Codex processes before launch",
                Checked = config.RestartCodex,
                AutoSize = true,
                Location = new Point(24, 130)
            };

            patchConfigCheckBox = new CheckBox
            {
                Text = "Patch %USERPROFILE%\\.codex\\config.toml for Codex child processes",
                Checked = config.PatchCodexConfig,
                AutoSize = true,
                Location = new Point(24, 158)
            };

            var launchButton = NewButton("Launch Codex", 24, 198, 130);
            launchButton.Click += delegate { RunAction(LaunchCodex); };

            var shortcutButton = NewButton("Create Shortcut", 164, 198, 130);
            shortcutButton.Click += delegate { RunAction(CreateDesktopShortcut); };

            var saveButton = NewButton("Save Config", 304, 198, 120);
            saveButton.Click += delegate { RunAction(SaveConfig); };

            var unpatchButton = NewButton("Remove Patch", 434, 198, 130);
            unpatchButton.Click += delegate { RunAction(RemovePatch); };

            logTextBox = new TextBox
            {
                Location = new Point(24, 244),
                Size = new Size(560, 155),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White
            };

            Controls.Add(title);
            Controls.Add(proxyLabel);
            Controls.Add(proxyTextBox);
            Controls.Add(restartCheckBox);
            Controls.Add(patchConfigCheckBox);
            Controls.Add(launchButton);
            Controls.Add(shortcutButton);
            Controls.Add(saveButton);
            Controls.Add(unpatchButton);
            Controls.Add(logTextBox);

            Log("Ready. Default proxy: " + config.ProxyUrl);
        }

        private static Button NewButton(string text, int x, int y, int width)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
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
                MessageBox.Show(this, ex.Message, "Codex Proxy Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveConfig()
        {
            var proxy = ProxyTools.NormalizeProxyUrl(proxyTextBox.Text);
            config.ProxyUrl = proxy;
            config.RestartCodex = restartCheckBox.Checked;
            config.PatchCodexConfig = patchConfigCheckBox.Checked;
            config.Save();
            Log("Saved config: " + LauncherConfig.ConfigPath);
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
            Log("Created shortcut: " + shortcutPath);
        }

        private void RemovePatch()
        {
            CodexConfigPatch.Remove();
            Log("Removed Codex config proxy patch.");
        }

        private void Log(string message)
        {
            logTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
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
