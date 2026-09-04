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

            Controls.Add(_runButton);
            Controls.Add(_statusLabel);
            Controls.Add(_logBox);

            Activated += (s, e) => RefreshState();

            Log($"===== Start sesji {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
            RefreshState();
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

                var result = _service.RemoveRedundantAxisDimensions(drawing, Log, dryRun: true);
                _statusLabel.Text = $"Gotowe. Sprawdzono {result.ViewsChecked} widoków, usunięto {result.RemovedCount} wymiarów. Sprawdź wizualnie w Tekli.";
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
