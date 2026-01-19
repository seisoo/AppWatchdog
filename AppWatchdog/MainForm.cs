using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppWatchdog.Shared;

namespace AppWatchdog.UI
{
    public partial class MainForm : Form
    {
        private const string ServiceNameConst = "AppWatchdog";

        private WatchdogConfig _cfg = new();
        private BindingList<WatchedApp> _apps = new();
        private bool _dirty;

        public MainForm()
        {
            InitializeComponent();

            ApplyAdminButtonBehavior();
            WireUiEvents();

            Shown += MainForm_Shown;
        }

        private void ApplyAdminButtonBehavior()
        {
            SetUacShield(btnInstall, true);
            SetUacShield(btnUninstall, true);
            SetUacShield(btnStart, true);
            SetUacShield(btnStop, true);

            bool isAdmin = IsAdmin();
        }

        private static bool IsAdmin()
        {
            try
            {
                var p = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private const int BCM_FIRST = 0x1600;
        private const int BCM_SETSHIELD = (BCM_FIRST + 0x000C);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static void SetUacShield(Button button, bool show)
        {
            if (button == null) return;
            button.FlatStyle = FlatStyle.System;
            button.UseVisualStyleBackColor = true;
            SendMessage(button.Handle, BCM_SETSHIELD, IntPtr.Zero, show ? new IntPtr(1) : IntPtr.Zero);
        }

        private async void MainForm_Shown(object? sender, EventArgs e)
        {
            RefreshServiceStatus();

            UpdatePipeStatus();

            if (!IsServiceRunning())
            {
                SetConfigUiEnabled(false);
                lblSnapshot.Text = "Dienst ist nicht gestartet.\r\nBitte installieren und starten.";
                return;
            }

            SetConfigUiEnabled(true);

            await SafeLoadConfigAndSnapshotAsync();
            await LoadLogsAsync();
        }

        private bool IsServiceRunning()
        {
            try
            {
                using var sc = new ServiceController(ServiceNameConst);
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshServiceStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceNameConst);
                lblServiceStatus.Text = sc.Status.ToString();
                stService.Text = $"Service: {sc.Status}";
            }
            catch
            {
                lblServiceStatus.Text = "Nicht installiert";
                stService.Text = "Service: nicht installiert";
            }
        }

        private void UpdatePipeStatus()
        {
            try
            {
                PipeClient.Ping();
                stPipe.Text = "Pipe: OK";
            }
            catch
            {
                stPipe.Text = "Pipe: Fehler";
            }
        }

        private void SetConfigUiEnabled(bool enabled)
        {
            tabApps.Enabled = enabled;
            tabMail.Enabled = enabled;
            tabLogs.Enabled = enabled;

            btnSaveApps.Enabled = enabled;
            btnReloadApps.Enabled = enabled;

            btnSaveMail.Enabled = enabled;
            btnReloadMail.Enabled = enabled;

            btnSaveNtfy.Enabled = enabled;
            btnReloadNtfy.Enabled = enabled;

            btnTriggerCheck.Enabled = enabled;
        }

        private async Task SafeLoadConfigAndSnapshotAsync()
        {
            try
            {
                SetBusy(true);

                var cfg = await Task.Run(() => PipeClient.GetConfig());
                ApplyConfigToUi(cfg);

                var snap = await Task.Run(() => PipeClient.GetStatus());
                ApplySnapshotToUi(snap);

                _dirty = false;
                UpdateDirtyIndicators();

                UpdatePipeStatus();
            }
            catch (Exception ex)
            {
                HandlePipeException(ex, context: "Laden");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task SafeSaveConfigAsync()
        {
            try
            {
                SetBusy(true);

                if (!IsServiceRunning())
                    throw new InvalidOperationException("Dienst läuft nicht. Bitte starten.");

                var newCfg = BuildConfigFromUi();

                await Task.Run(() => PipeClient.SaveConfig(newCfg));
                await Task.Run(() => PipeClient.TriggerCheck());

                _cfg = newCfg;
                _dirty = false;
                UpdateDirtyIndicators();

                UpdatePipeStatus();
            }
            catch (Exception ex)
            {
                HandlePipeException(ex, context: "Speichern");
                throw;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void HandlePipeException(Exception ex, string context)
        {

            string msg = ex.Message ?? ex.ToString();

            bool isProtocolMismatch =
                ex is InvalidOperationException &&
                msg.IndexOf("Protocol mismatch", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isProtocolMismatch)
            {
                SetConfigUiEnabled(false);
                RefreshServiceStatus();
                UpdatePipeStatus();

                MessageBox.Show(
                    "Protokoll-Version passt nicht zusammen.\r\n\r\n" +
                    msg + "\r\n\r\n" +
                    "Bitte den Dienst neu starten oder aktualisieren.\r\n" +
                    "Hinweis: Nach einem Dienst-Update muss oft auch die UI-Version aktualisiert werden.",
                    "Protocol mismatch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                lblSnapshot.Text =
                    "Protocol mismatch.\r\n" +
                    "Bitte Dienst neu starten oder aktualisieren.";
                stLastSnapshot.Text = "Snapshot: -";
                return;
            }

            MessageBox.Show(
                $"{context} fehlgeschlagen:\r\n\r\n{msg}",
                "Fehler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            lblSnapshot.Text =
                $"{context} fehlgeschlagen.\r\n" +
                ex.GetType().Name + ": " + msg;

            UpdatePipeStatus();
        }

        private void SetBusy(bool busy)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void ApplyConfigToUi(WatchdogConfig cfg)
        {
            _cfg = cfg;

            _apps = new BindingList<WatchedApp>(cfg.Apps);
            gridApps.DataSource = _apps;

            numCheckInterval.Value = Math.Max(numCheckInterval.Minimum, Math.Min(numCheckInterval.Maximum, cfg.CheckIntervalMinutes));
            numMailInterval.Value = Math.Max(numMailInterval.Minimum, Math.Min(numMailInterval.Maximum, cfg.MailIntervalHours));

            txtSmtpServer.Text = cfg.Smtp.Server ?? "";
            txtSmtpPort.Text = cfg.Smtp.Port.ToString();
            chkSmtpSsl.Checked = cfg.Smtp.EnableSsl;
            txtSmtpUser.Text = cfg.Smtp.User ?? "";
            txtSmtpPass.Text = cfg.Smtp.Password ?? "";
            txtMailFrom.Text = cfg.Smtp.From ?? "";
            txtMailTo.Text = cfg.Smtp.To ?? "";

            txtNtfyBaseUrl.Text = cfg.Ntfy.BaseUrl ?? "https://ntfy.sh";
            txtNtfyTopic.Text = cfg.Ntfy.Topic ?? "";
            txtNtfyToken.Text = cfg.Ntfy.Token ?? "";

            int prio = cfg.Ntfy.Priority;
            prio = Math.Max(1, Math.Min(5, prio));
            cmbNtfyPriority.SelectedIndex = prio - 1;

            UpdateNerdPanels();
        }

        private WatchdogConfig BuildConfigFromUi()
        {
            ValidateSmtpIfSet();

            var cfg = new WatchdogConfig
            {
                Apps = _apps != null ? new System.Collections.Generic.List<WatchedApp>(_apps) : new(),
                CheckIntervalMinutes = (int)numCheckInterval.Value,
                MailIntervalHours = (int)numMailInterval.Value,

                Smtp = new SmtpSettings
                {
                    Server = (txtSmtpServer.Text ?? "").Trim(),
                    Port = int.TryParse(txtSmtpPort.Text, out var p) && p > 0 ? p : 587,
                    EnableSsl = chkSmtpSsl.Checked,
                    User = (txtSmtpUser.Text ?? "").Trim(),
                    Password = txtSmtpPass.Text ?? "",
                    From = (txtMailFrom.Text ?? "").Trim(),
                    To = (txtMailTo.Text ?? "").Trim()
                },

                Ntfy = new NtfySettings
                {
                    Enabled = !string.IsNullOrWhiteSpace(txtNtfyTopic.Text),
                    BaseUrl = string.IsNullOrWhiteSpace(txtNtfyBaseUrl.Text) ? "https://ntfy.sh" : txtNtfyBaseUrl.Text.Trim(),
                    Topic = (txtNtfyTopic.Text ?? "").Trim(),
                    Token = txtNtfyToken.Text ?? "",
                    Priority = (cmbNtfyPriority.SelectedIndex >= 0 ? cmbNtfyPriority.SelectedIndex + 1 : 3)
                }
            };

            return cfg;
        }

        private void UpdateNerdPanels()
        {
            try
            {
                lblAppsNerd.Text =
                    $"Apps: {_cfg.Apps.Count} | Interval: {_cfg.CheckIntervalMinutes}m | MailInterval: {_cfg.MailIntervalHours}h";

                lblMailNerd.Text =
                    $"SMTP: {(string.IsNullOrWhiteSpace(_cfg.Smtp.Server) ? "off" : _cfg.Smtp.Server)}:{_cfg.Smtp.Port} | SSL: {_cfg.Smtp.EnableSsl} | Auth: {(string.IsNullOrWhiteSpace(_cfg.Smtp.User) ? "no" : "yes")}";

                lblNtfyNerd.Text =
                    $"NTFY: {(_cfg.Ntfy.Enabled ? "on" : "off")} | Base: {_cfg.Ntfy.BaseUrl} | Topic: {_cfg.Ntfy.Topic} | Prio: {_cfg.Ntfy.Priority}";
            }
            catch
            {
            }
        }

        private void ApplySnapshotToUi(ServiceSnapshot snap)
        {
            lblSnapshot.Text =
                $"Zeit: {snap.Timestamp:yyyy-MM-dd HH:mm:ss}\r\n" +
                $"Session: {snap.SessionState}\r\n" +
                $"Apps im Snapshot: {snap.Apps.Count}";

            stLastSnapshot.Text = $"Snapshot: {snap.Timestamp:HH:mm:ss}";
        }

        private async Task LoadLogsAsync()
        {
            try
            {
                SetBusy(true);

                var days = await Task.Run(() => PipeClient.ListLogDays());

                cmbLogDate.Items.Clear();
                foreach (var d in days.Days)
                    cmbLogDate.Items.Add(d);

                if (cmbLogDate.Items.Count > 0)
                    cmbLogDate.SelectedIndex = 0;

                lblLogsNerd.Text = $"Tage: {days.Days.Count} | Pipe: OK";
            }
            catch (Exception ex)
            {
                HandlePipeException(ex, context: "Logs laden");
                lblLogsNerd.Text = "Logs: Fehler";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task LoadSelectedLogAsync()
        {
            if (cmbLogDate.SelectedItem is not string day || string.IsNullOrWhiteSpace(day))
                return;

            try
            {
                SetBusy(true);

                var log = await Task.Run(() => PipeClient.GetLogDay(day));
                txtLogs.Text = log.Content ?? "";

                lblLogsNerd.Text = $"Tag: {log.Day} | Zeichen: {txtLogs.TextLength}";
            }
            catch (Exception ex)
            {
                HandlePipeException(ex, context: "Log lesen");
                lblLogsNerd.Text = "Log lesen: Fehler";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "AppWatchdog",
                    "Logs");

                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = path, UseShellExecute = true });
            }
            catch { }
        }

        private void MarkDirty()
        {
            _dirty = true;
            UpdateDirtyIndicators();
        }

        private void UpdateDirtyIndicators()
        {
            string t = _dirty ? "● Ungespeichert" : "✓ Gespeichert";
            lblDirtyApps.Text = t;
            lblDirtyMail.Text = t;
            lblDirtyNtfy.Text = t;
            stDirty.Text = _dirty ? "Config: DIRTY" : "Config: OK";
        }

        private void WireUiEvents()
        {
            menuFileExit.Click += (_, __) => Close();
            menuViewEventLog.Click += (_, __) => OpenEventLog();
            menuViewOpenLogFolder.Click += (_, __) => OpenLogFolder();
            menuHelpAbout.Click += (_, __) =>
            {
                MessageBox.Show("AppWatchdog UI\n(WinForms – klassisch, robust)", "Über", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            numCheckInterval.ValueChanged += (_, __) => MarkDirty();
            numMailInterval.ValueChanged += (_, __) => MarkDirty();

            txtSmtpServer.TextChanged += (_, __) => MarkDirty();
            txtSmtpPort.TextChanged += (_, __) => MarkDirty();
            chkSmtpSsl.CheckedChanged += (_, __) => MarkDirty();
            txtSmtpUser.TextChanged += (_, __) => MarkDirty();
            txtSmtpPass.TextChanged += (_, __) => MarkDirty();
            txtMailFrom.TextChanged += (_, __) => MarkDirty();
            txtMailTo.TextChanged += (_, __) => MarkDirty();

            txtNtfyBaseUrl.TextChanged += (_, __) => MarkDirty();
            txtNtfyTopic.TextChanged += (_, __) => MarkDirty();
            txtNtfyToken.TextChanged += (_, __) => MarkDirty();
            cmbNtfyPriority.SelectedIndexChanged += (_, __) => MarkDirty();

            gridApps.CellValueChanged += (_, __) => MarkDirty();
            gridApps.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (gridApps.IsCurrentCellDirty)
                    gridApps.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            btnAddApp.Click += (_, __) =>
            {
                _apps.Add(new WatchedApp { Enabled = true, Name = "Neue App" });
                MarkDirty();
            };

            btnRemoveApp.Click += (_, __) =>
            {
                if (gridApps.CurrentRow?.DataBoundItem is WatchedApp a)
                {
                    _apps.Remove(a);
                    MarkDirty();
                }
            };

            btnBrowseApp.Click += (_, __) =>
            {
                if (gridApps.CurrentRow?.DataBoundItem is not WatchedApp a)
                    return;

                using OpenFileDialog dlg = new() { Filter = "Programme (*.exe)|*.exe" };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    a.ExePath = dlg.FileName;
                    if (string.IsNullOrWhiteSpace(a.Name))
                        a.Name = Path.GetFileNameWithoutExtension(dlg.FileName);

                    gridApps.Refresh();
                    MarkDirty();
                }
            };

            btnSaveApps.Click += async (_, __) => await SaveConfigClickedAsync();
            btnSaveMail.Click += async (_, __) => await SaveConfigClickedAsync();
            btnSaveNtfy.Click += async (_, __) => await SaveConfigClickedAsync();

            btnReloadApps.Click += async (_, __) => await ReloadClickedAsync();
            btnReloadMail.Click += async (_, __) => await ReloadClickedAsync();
            btnReloadNtfy.Click += async (_, __) => await ReloadClickedAsync();

            btnTestMail.Click += (_, __) => TestMail();
            btnTestNtfy.Click += async (_, __) => await TestNtfyAsync();

            btnTriggerCheck.Click += async (_, __) =>
            {
                if (!IsServiceRunning()) return;

                try
                {
                    SetBusy(true);

                    await Task.Run(() => PipeClient.TriggerCheck());
                    var snap = await Task.Run(() => PipeClient.GetStatus());
                    ApplySnapshotToUi(snap);

                    await LoadLogsAsync();
                }
                catch (Exception ex)
                {
                    HandlePipeException(ex, context: "Check ausführen");
                }
                finally
                {
                    SetBusy(false);
                }
            };

            btnRefreshStatus.Click += async (_, __) =>
            {
                RefreshServiceStatus();
                UpdatePipeStatus();

                if (!IsServiceRunning())
                {
                    SetConfigUiEnabled(false);
                    lblSnapshot.Text = "Dienst ist nicht gestartet.\r\nBitte installieren und starten.";
                    return;
                }

                SetConfigUiEnabled(true);
                await SafeLoadConfigAndSnapshotAsync();
                await LoadLogsAsync();
            };

            btnEventLog.Click += (_, __) => OpenEventLog();

            btnStart.Click += (_, __) => RunElevated("start AppWatchdog");
            btnStop.Click += (_, __) => RunElevated("stop AppWatchdog");

            btnInstall.Click += (_, __) => InstallService();
            btnUninstall.Click += (_, __) => UninstallService();

            btnReloadLogs.Click += async (_, __) => await LoadLogsAsync();
            cmbLogDate.SelectedIndexChanged += async (_, __) => await LoadSelectedLogAsync();

            btnOpenLogFolder.Click += (_, __) => OpenLogFolder();
        }

        private void OpenEventLog()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "eventvwr.msc", UseShellExecute = true });
            }
            catch { }
        }

        private async Task SaveConfigClickedAsync()
        {
            if (!IsServiceRunning())
            {
                MessageBox.Show("Dienst läuft nicht. Bitte starten.", "Hinweis");
                return;
            }

            try
            {
                await SafeSaveConfigAsync();
                MessageBox.Show("Config gespeichert.", "OK");
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException && ex.Message.Contains("Protocol mismatch", StringComparison.OrdinalIgnoreCase))
                    return;

                MessageBox.Show(ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ReloadClickedAsync()
        {
            if (!IsServiceRunning())
            {
                MessageBox.Show("Dienst läuft nicht. Bitte starten.", "Hinweis");
                return;
            }

            if (_dirty &&
                MessageBox.Show("Ungespeicherte Änderungen verwerfen?", "Bestätigung",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            await SafeLoadConfigAndSnapshotAsync();
            await LoadLogsAsync();
        }

        private void ValidateSmtpIfSet()
        {
            var any = !string.IsNullOrWhiteSpace(txtSmtpServer.Text)
                      || !string.IsNullOrWhiteSpace(txtMailFrom.Text)
                      || !string.IsNullOrWhiteSpace(txtMailTo.Text);

            if (!any) return;

            if (string.IsNullOrWhiteSpace(txtSmtpServer.Text))
                throw new Exception("SMTP Server fehlt.");
            if (!int.TryParse(txtSmtpPort.Text, out int p) || p <= 0)
                throw new Exception("SMTP Port ungültig.");
            if (string.IsNullOrWhiteSpace(txtMailFrom.Text))
                throw new Exception("Absender fehlt.");
            if (string.IsNullOrWhiteSpace(txtMailTo.Text))
                throw new Exception("Empfänger fehlt.");
        }

        private void TestMail()
        {
            try
            {
                ValidateSmtpIfSet();

                using SmtpClient smtp = new(txtSmtpServer.Text, int.Parse(txtSmtpPort.Text))
                {
                    EnableSsl = chkSmtpSsl.Checked,
                    Credentials = string.IsNullOrWhiteSpace(txtSmtpUser.Text)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(txtSmtpUser.Text, txtSmtpPass.Text)
                };

                var html = "<h3>AppWatchdog – Test</h3><p>SMTP Konfiguration OK.</p>";
                using var msg = new MailMessage(txtMailFrom.Text, txtMailTo.Text, "AppWatchdog – Test Mail", html)
                {
                    IsBodyHtml = true
                };

                smtp.Send(msg);
                MessageBox.Show("Test-Mail gesendet.", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SMTP Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task TestNtfyAsync()
        {
            try
            {
                var cfg = BuildConfigFromUi();
                if (!cfg.Ntfy.Enabled)
                {
                    MessageBox.Show("NTFY ist nicht aktiviert (Topic fehlt).", "Hinweis");
                    return;
                }

                await SendNtfyAsync(
                    cfg.Ntfy,
                    title: "AppWatchdog – Test",
                    message:
                        "NTFY Konfiguration OK.\n" +
                        $"Zeit: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Host: {Environment.MachineName}\n" +
                        $"Prio: {cfg.Ntfy.Priority}"
                );

                MessageBox.Show("NTFY Test gesendet.", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "NTFY Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static async Task SendNtfyAsync(NtfySettings cfg, string title, string message)
        {
            string baseUrl = (cfg.BaseUrl ?? "https://ntfy.sh").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(cfg.Topic))
                throw new InvalidOperationException("NTFY Topic fehlt.");

            string url = $"{baseUrl}/{cfg.Topic}";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(message ?? "", Encoding.UTF8, "text/plain")
            };

            req.Headers.TryAddWithoutValidation("Title", title ?? "AppWatchdog");
            req.Headers.TryAddWithoutValidation("Priority", cfg.Priority.ToString());

            if (!string.IsNullOrWhiteSpace(cfg.Token))
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + cfg.Token.Trim());

            var resp = await http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"NTFY HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}\n{body}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_dirty)
            {
                var r = MessageBox.Show(
                    "Ungespeicherte Änderungen speichern?",
                    "Beenden",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (r == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (r == DialogResult.Yes)
                {
                    try
                    {
                        SaveConfigToService_BlockingForClose();
                    }
                    catch (Exception ex)
                    {
                        HandlePipeException(ex, context: "Speichern beim Beenden");
                        e.Cancel = true;
                        return;
                    }
                }
            }

            base.OnFormClosing(e);
        }

        private void SaveConfigToService_BlockingForClose()
        {
            if (!IsServiceRunning())
                throw new InvalidOperationException("Dienst läuft nicht, Speichern nicht möglich.");

            var newCfg = BuildConfigFromUi();
            PipeClient.SaveConfig(newCfg);
            PipeClient.TriggerCheck();

            _cfg = newCfg;
            _dirty = false;
            UpdateDirtyIndicators();
        }

        private void InstallService()
        {
            string targetDir = @"C:\AppWatchdog\Service";
            string targetExe = Path.Combine(targetDir, "AppWatchdog.Service.exe");

            string sourceExe = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "AppWatchdog.Service.exe");

            try
            {
                if (!File.Exists(sourceExe))
                    throw new FileNotFoundException("Service-EXE nicht gefunden", sourceExe);

                Directory.CreateDirectory(targetDir);
                File.Copy(sourceExe, targetExe, overwrite: true);

                RunElevated("delete AppWatchdog");
                Thread.Sleep(1000);

                RunElevated($"create AppWatchdog binPath= \"{targetExe}\" start= auto");

                MessageBox.Show("Service installiert.", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Installation fehlgeschlagen:\n\n" + ex.Message,
                    "Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                RefreshServiceStatus();
                UpdatePipeStatus();
            }
        }

        private void UninstallService()
        {
            const string serviceName = "AppWatchdog";
            string serviceDir = @"C:\AppWatchdog\Service";

            try
            {
                try
                {
                    using var sc = new ServiceController(serviceName);
                    if (sc.Status != ServiceControllerStatus.Stopped &&
                        sc.Status != ServiceControllerStatus.StopPending)
                    {
                        RunElevated("stop AppWatchdog");
                        Thread.Sleep(1500);
                    }
                }
                catch { }

                RunElevated("delete AppWatchdog");
                Thread.Sleep(1500);

                if (Directory.Exists(serviceDir))
                    Directory.Delete(serviceDir, recursive: true);

                MessageBox.Show("Service vollständig deinstalliert.", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Deinstallation fehlgeschlagen:\n\n" + ex.Message,
                    "Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                RefreshServiceStatus();
                UpdatePipeStatus();
                SetConfigUiEnabled(false);
            }
        }

        private void RunElevated(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            try
            {
                Process.Start(psi);
            }
            catch
            {
            }
        }
    }
}
