using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FaceitACToggle
{
    public partial class MainWindow : Window
    {
        private const string DriverName = "faceit";

        public MainWindow()
        {
            // Регистрация кодировок для .NET 10 (OEM 866)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            InitializeComponent();
        }

        private async void ToggleAC_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ToggleAC?.IsChecked == true)
                    await Task.Run(() => ToggleDriver("start"));
                else
                    await Task.Run(() => ToggleDriver("stop"));
            }
            catch (Exception ex)
            {
                LogBox?.AppendText($"[ERROR] {ex.Message}\n");
            }
        }

        private void ToggleDriver(string action)
        {
            string scAction = action.Equals("start", StringComparison.OrdinalIgnoreCase) ? "start" : "stop";
            string expectedStatus = action.Equals("start", StringComparison.OrdinalIgnoreCase) ? "RUNNING" : "STOPPED";

            Dispatcher.Invoke(() =>
            {
                LogBox?.AppendText($"[{DateTime.Now:HH:mm:ss}] {action}ing driver...\n");
            });

            try
            {
                var (exitCode, output, error) = RunProcess("sc", $"{scAction} \"{DriverName}\"", 15000);

                Dispatcher.Invoke(() =>
                {
                    LogBox?.AppendText($"[{DateTime.Now:HH:mm:ss}] sc {scAction} output (exit {exitCode}):\n{output}\n");
                    if (!string.IsNullOrWhiteSpace(error))
                        LogBox?.AppendText($"[{DateTime.Now:HH:mm:ss}] sc {scAction} error:\n{error}\n");
                });

                // Ждём, пока драйвер достигнет нужного состояния
                bool reached = WaitForStatus(expectedStatus, TimeSpan.FromSeconds(10));

                string finalStatus = GetDriverStatus() ?? "UNKNOWN";

                Dispatcher.Invoke(() =>
                {
                    LogBox?.AppendText($"[{DateTime.Now:HH:mm:ss}] Driver status: {finalStatus}\n");
                    LogBox?.AppendText($"[{DateTime.Now:HH:mm:ss}] Command executed: sc {scAction} \"{DriverName}\"; reached expected: {reached}\n");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogBox?.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR executing driver operation: {ex.Message}\n");
                });
            }
        }

        private (int ExitCode, string StdOut, string StdErr) RunProcess(string fileName, string arguments, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.GetEncoding(866), // OEM 866 для читаемого лога
                    StandardErrorEncoding = Encoding.GetEncoding(866)
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                    return (-1, string.Empty, "Failed to start process");

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();

                bool exited = proc.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { proc.Kill(); } catch { }
                    return (-1, stdout, stderr + "\nProcess timed out and was killed.");
                }

                return (proc.ExitCode, stdout, stderr);
            }
            catch (Exception ex)
            {
                return (-1, string.Empty, ex.Message);
            }
        }

        private string? GetDriverStatus()
        {
            var (_, output, _) = RunProcess("sc", $"query \"{DriverName}\"", 5000);
            if (string.IsNullOrWhiteSpace(output))
                return null;

            // STATE : 1 STOPPED или STATE : 4 RUNNING
            var match = Regex.Match(output, @"STATE\s*:\s*\d+\s+([A-Z_]+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value.ToUpperInvariant();

            return null;
        }

        private bool WaitForStatus(string expectedStatus, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                string? status = GetDriverStatus();
                if (!string.IsNullOrEmpty(status) && status.Equals(expectedStatus, StringComparison.OrdinalIgnoreCase))
                    return true;

                Thread.Sleep(500);
            }
            return false;
        }

        private void ToggleAC_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}