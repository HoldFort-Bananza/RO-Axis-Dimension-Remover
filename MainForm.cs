using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Tekla.Structures.Drawing;

namespace RoAxisDimensionRemover
{
    /// <summary>
    /// UI: jeden przycisk (usuń wymiary do osi + wstaw wymiar wcięcia), podpis
    /// stanu, log. Logika w RoAxisDimensionService i NotchPilot. Bez nasłuchu
    /// zdarzeń Tekli - stan odświeża się przy fokusie okna.
    /// </summary>
    public class MainForm : Form
    {
        private bool _busy;

        // Operator (2026-09-30): log ma zostać przez wszystkie operacje na
        // jednym rysunku (kilka kliknięć z rzędu), a czyścić się
        // dopiero przy przejściu na inny. Tekla nie daje zdarzenia zamknięcia
        // rysunku, więc porównujemy Mark aktywnego rysunku przy każdym kliku.
        private string _logDrawingMark;

        private Button _cleanupButton;
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
            Height = 455;
            StartPosition = FormStartPosition.CenterScreen;

            _cleanupButton = new Button
            {
                Text = "Posprzątaj wymiary na aktywnym rysunku (profile RO)\nusuwa wymiary do osi, wstawia wymiary wcięcia",
                Left = 15,
                Top = 15,
                Width = 470,
                Height = 75
            };
            _cleanupButton.Click += CleanupButton_Click;

            _statusLabel = new Label
            {
                Left = 15,
                Top = 96,
                Width = 470,
                Height = 20,
                ForeColor = Color.DarkSlateGray
            };

            _logBox = new TextBox
            {
                Left = 15,
                Top = 121,
                Width = 470,
                Height = 295,
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
            Controls.Add(_cleanupButton);
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

            _cleanupButton.Top += UpdateBannerHeight;
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

        /// <summary>
        /// Jeden przycisk (operator, 2026-10-05): „Usuń” na wszystkich widokach
        /// arkusza, potem „Wstaw” na całym rysunku. Dawniej dwa osobne
        /// przyciski i wybór widoku kliknięciem; Shift + klik (wszystkie widoki)
        /// stał się domyślny, bo operator i tak go używał. Reguły bez zmian -
        /// to ten sam cykl, który przechodził bramę na żywo (Usuń z Shift →
        /// Wstaw). Wybór pojedynczego widoku (przypadek [35020]) zniknął razem
        /// z pickerem - na takim rysunku Ctrl+Z cofa widok po widoku.
        /// </summary>
        private void CleanupButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            _busy = true;
            _cleanupButton.Enabled = false;

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
                // dryRun: false - brama bezpieczeństwa reguły v6 przeszła
                // 2026-09-23 (operator na żywym [35021] i [3.5013]: „rysunek
                // opisuje wszystko” i „tak” na realne kasowanie). Historia:
                // AGENTS.md i wiki 10-Dziennik-2026-09.
                const bool dryRun = false;

                BeginLog(drawing, "PORZĄDKOWANIE WYMIARÓW");
                // Każdy widok zatwierdza się osobno, więc Ctrl+Z cofa widok po
                // widoku, a wstawienie to kolejne kroki cofania.
                int removed = 0, viewNumber = 0;
                foreach (var sheetView in DiagRunner.SheetViews(drawing).ToList())
                {
                    viewNumber++;
                    Log($"--- widok {viewNumber} ({sheetView.GetType().Name}) ---");
                    removed += RoAxisDimensionService.RemoveAxisDimensions(drawing, sheetView, Log, dryRun);
                }

                Log("--- wymiary wcięcia ---");
                int inserted = NotchPilot.InsertMissing(drawing, Log);

                _statusLabel.Text = $"Gotowe. Usunięto {removed}, wstawiono {inserted}. Sprawdź w Tekli (Ctrl+Z cofa).";
                if (removed + inserted > 0) TeklaWindowFocus.BringToFront(Log);
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
                _cleanupButton.Enabled = true;
            }
        }

        // Obok .exe, NIE w katalogu tymczasowym sesji Claude Code - ten
        // znika razem z sesją. Jeden plik na uruchomienie.
        private static readonly string DiagLogPath = System.IO.Path.Combine(
            Application.StartupPath, "logs", $"session_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        private void BeginLog(Drawing drawing, string operation)
        {
            if (drawing.Mark != _logDrawingMark)
            {
                _logBox.Clear();
                _logDrawingMark = drawing.Mark;
            }
            else
            {
                Log("");
            }
            Log($"===== {DateTime.Now:HH:mm:ss} {operation} ({drawing.Mark}) =====");
        }

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
