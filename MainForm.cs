using System;
using System.Drawing;
using System.Windows.Forms;
using Tekla.Structures.Drawing;

namespace RoAxisDimensionRemover
{
    /// <summary>
    /// UI: jeden przycisk, podpis stanu, log. Logika w RoAxisDimensionService.
    /// v0.1 - bez nasłuchu zdarzeń Tekli, stan odświeża się przy fokusie okna.
    /// </summary>
    public class MainForm : Form
    {
        private readonly RoAxisDimensionService _service = new RoAxisDimensionService();
        private bool _busy;

        private Button _runButton;
        private TextBox _logBox;
        private Label _statusLabel;

        // Pasek z informacją o nowszej wersji. Tworzony zawsze, ale UKRYTY -
        // pokazuje się tylko wtedy, gdy sprawdzenie na GitHubie (UpdateCheck)
        // znajdzie nowszą wersję. Gdy wszystko aktualne albo nie ma
        // internetu, użytkownik nie widzi nic - wzorzec z Radius Dimention
        // Mover.
        private LinkLabel _updateBanner;
        private const int UpdateBannerHeight = 28;

        public MainForm()
        {
            Text = "RO Axis Dimension Remover – Tekla 2025";
            Width = 520;
            Height = 420;
            StartPosition = FormStartPosition.CenterScreen;

            _runButton = new Button
            {
                Text = "Usuń nadmiarowe wymiary do osi (profile RO)",
                Left = 15,
                Top = 15,
                Width = 470,
                Height = 40
            };
            _runButton.Click += RunButton_Click;

            _statusLabel = new Label
            {
                Left = 15,
                Top = 64,
                Width = 470,
                Height = 20,
                ForeColor = Color.DarkSlateGray
            };

            _logBox = new TextBox
            {
                Left = 15,
                Top = 89,
                Width = 470,
                Height = 260,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };

            // Pasek aktualizacji siedzi NAD przyciskiem, ale dopóki jest
            // ukryty, nie zajmuje miejsca - pozostałe kontrolki są przesuwane
            // w dół dopiero w ShowUpdateBanner().
            _updateBanner = new LinkLabel
            {
                Left = 15,
                Top = 12,
                Width = 470,
                Height = 20,
                Visible = false,
                LinkColor = Color.FromArgb(0, 102, 204),
                Font = new Font(Font, FontStyle.Bold)
            };
            _updateBanner.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(UpdateCheck.ReleasesPage);
                }
                catch (Exception ex)
                {
                    Log("Nie udało się otworzyć strony z wydaniami: " + ex.Message);
                }
            };

            Controls.Add(_updateBanner);
            Controls.Add(_runButton);
            Controls.Add(_statusLabel);
            Controls.Add(_logBox);

            Activated += (s, e) => RefreshState();

            Log($"===== Start sesji {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
            RefreshState();

            // Sprawdzenie aktualizacji - w tle, nie blokuje startu, i milczy
            // gdy wszystko jest aktualne.
            UpdateCheck.StartInBackground(
                version => UiInvoke(() => ShowUpdateBanner(version)),
                message => UiInvoke(() => Log(message)));
        }

        /// <summary>
        /// Pokazuje pasek z informacją o nowszej wersji i robi na niego miejsce,
        /// przesuwając pozostałe kontrolki w dół. Wołane TYLKO gdy nowsza wersja
        /// faktycznie istnieje, więc w normalnej sytuacji okno wygląda jak dotąd.
        /// </summary>
        private void ShowUpdateBanner(string version)
        {
            if (_updateBanner.Visible)
            {
                return; // już pokazany
            }

            _updateBanner.Text = "Dostępna nowsza wersja " + version + " - kliknij, aby pobrać";
            _updateBanner.Visible = true;

            _runButton.Top += UpdateBannerHeight;
            _statusLabel.Top += UpdateBannerHeight;
            _logBox.Top += UpdateBannerHeight;
            Height += UpdateBannerHeight;
        }

        /// <summary>
        /// Przenosi wykonanie na wątek UI. UpdateCheck wywołuje swoje callbacki
        /// z wątku roboczego (Task.Run), więc ruszanie kontrolkami wprost z
        /// niego rzuciłoby wyjątkiem. Wyjątki są tu tłumione świadomie - okno
        /// mogło już zniknąć, a to nie powód, żeby przerywać działanie
        /// programu.
        /// </summary>
        private void UiInvoke(Action action)
        {
            try
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch
            {
            }
        }

        private void RefreshState()
        {
            if (_busy) return;
            try
            {
                var dh = new DrawingHandler();
                if (!dh.GetConnectionStatus())
                {
                    _statusLabel.Text = "Brak połączenia z Teklą.";
                    return;
                }
                var drawing = dh.GetActiveDrawing();
                _statusLabel.Text = drawing != null
                    ? $"Aktywny rysunek: {drawing.Mark} / {drawing.Name}"
                    : "Brak otwartego rysunku.";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Brak kontaktu z Teklą (" + ex.GetType().Name + ").";
            }
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;

            _logBox.Clear();
            Log($"===== {DateTime.Now:HH:mm:ss} USUŃ NADMIAROWE WYMIARY =====");
            _busy = true;
            _runButton.Enabled = false;

            try
            {
                var dh = new DrawingHandler();
                if (!dh.GetConnectionStatus())
                {
                    _statusLabel.Text = "Brak połączenia z Teklą.";
                    return;
                }
                var drawing = dh.GetActiveDrawing();
                if (drawing == null)
                {
                    _statusLabel.Text = "Brak otwartego rysunku.";
                    return;
                }

                // dryRun: false od 2026-09-04 - operator potwierdził wizualnie w
                // Tekli, na [35270] i [3.5013], że reguła v4 usuwa dokładnie te
                // wymiary, które przewiduje dry-run (patrz CLAUDE.md, brama
                // bezpieczeństwa). DiagRunner.cs (tryb headless, patrz Program.cs
                // --diag-*) ZOSTAJE na sztywno dryRun: true - to ścieżka
                // wywoływana bez człowieka przy przycisku, nigdy nie powinna
                // dostać możliwości realnego kasowania.
                var result = _service.RemoveRedundantAxisDimensions(drawing, Log, dryRun: false);
                _statusLabel.Text = $"Gotowe. Sprawdzono {result.ViewsChecked} widoków, usunięto {result.RemovedCount} wymiarów. Sprawdź wizualnie w Tekli (Ctrl+Z cofa, jeśli coś jest nie tak).";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Błąd – zobacz log.";
                Log("BŁĄD: " + ex.Message);
                Log(ex.StackTrace);
            }
            finally
            {
                _busy = false;
                _runButton.Enabled = true;
            }
        }

        // Obok .exe, NIE w katalogu tymczasowym sesji Claude Code - ten
        // znika razem z sesją. Jeden plik na uruchomienie.
        private static readonly string DiagLogPath = System.IO.Path.Combine(
            Application.StartupPath, "logs", $"session_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        private void Log(string message)
        {
            _logBox.AppendText(message + Environment.NewLine);
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DiagLogPath));
                System.IO.File.AppendAllText(DiagLogPath, message + Environment.NewLine);
            }
            catch { }
        }
    }
}
