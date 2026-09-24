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
        private Button _notchTestButton;
        private Button _notchLengthTestButton;
        private Button _notchMultiFaceTestButton;
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

            _runButton = new Button
            {
                Text = "Usuń wymiary do osi na wybranym widoku (profile RO)",
                Left = 15,
                Top = 15,
                Width = 470,
                Height = 40
            };
            _runButton.Click += RunButton_Click;

            _notchTestButton = new Button
            {
                Text = "TEST: wstaw szerokość wcięcia 42,4 mm ([35021])",
                Left = 15,
                Top = 60,
                Width = 470,
                Height = 30
            };
            _notchTestButton.Click += NotchTestButton_Click;

            _notchLengthTestButton = new Button
            {
                Text = "TEST: wstaw długość wcięcia 45,09 mm ([35021])",
                Left = 15,
                Top = 95,
                Width = 470,
                Height = 30
            };
            _notchLengthTestButton.Click += NotchLengthTestButton_Click;

            _notchMultiFaceTestButton = new Button
            {
                Text = "TEST: wstaw wcięcie dla pierwszego wymiaru do osi (multi-face, [35021]/[3.5013])",
                Left = 15,
                Top = 130,
                Width = 470,
                Height = 30
            };
            _notchMultiFaceTestButton.Click += NotchMultiFaceTestButton_Click;

            _statusLabel = new Label
            {
                Left = 15,
                Top = 166,
                Width = 470,
                Height = 20,
                ForeColor = Color.DarkSlateGray
            };

            _logBox = new TextBox
            {
                Left = 15,
                Top = 191,
                Width = 470,
                Height = 225,
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
            Controls.Add(_notchTestButton);
            Controls.Add(_notchLengthTestButton);
            Controls.Add(_notchMultiFaceTestButton);
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
            Log($"===== {DateTime.Now:HH:mm:ss} USUŃ WYMIARY DO OSI =====");
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

                // Podejrzenie: zawieszenie zaczęło się po dodaniu
                // TeklaWindowFocus.BringToFront() TUŻ PRZED startem pickera -
                // wcześniejsze testy (bez tego wywołania) kończyły się
                // poprawnie. Wymuszanie fokusu w trakcie uzbrajania pickera
                // mogło zakłócić stan Tekli. Cofamy to - Hide() zostaje (żeby
                // to okno nie zasłaniało kliku), ale bez SetForegroundWindow
                // przed PickPoint.
                Hide();
                ViewBase view;
                try
                {
                    var picker = dh.GetPicker();
                    picker.PickPoint("Kliknij widok (Esc = wybierz z listy)", out _, out view);
                }
                catch (PickerInterruptedException)
                {
                    view = null;
                }
                finally
                {
                    Show();
                    BringToFront();
                    Activate();
                }

                if (view == null)
                {
                    view = PickViewFromList(drawing);
                }

                if (view == null)
                {
                    _statusLabel.Text = "Nie wybrano widoku.";
                    return;
                }
                string viewLabel = view.GetType().Name;

                // dryRun: false - brama bezpieczeństwa dla POPRAWIONEJ
                // reguły TouchesAxis PRZESZŁA 2026-09-23 (ten sam dzień co
                // gate v5, druga runda tego dnia). Poprzednia reguła
                // niepotrzebnie kasowała "21 mm" (płaski wymiar promienia
                // rury, nie artefakt złącza) - operator zgłosił to na
                // żywym [35021]. Naprawiona TouchesAxis (patrz komentarz
                // przy niej w RoAxisDimensionService.cs: wymaga realnej
                // głębi Z, nie tylko Y=0) zweryfikowana dry-runem na żywo na
                // [35021] i NA OBU końcach [3.5013] (każdy koniec dał
                // dokładnie 1 kandydata, "21 mm" zachowany). Operator
                // obejrzał wynik w Tekli i wprost potwierdził na pytanie
                // "czy rysunek nadal opisuje wszystko co musi" (tak) oraz na
                // pytanie o realne kasowanie ("tak").
                const bool dryRun = false;
                var result = _service.RemoveAxisDimensions(drawing, view, Log, dryRun);
                _statusLabel.Text = dryRun
                    ? $"Gotowe (dry-run). Widok: {viewLabel}. Znaleziono {result.RemovedCount} wymiarów do usunięcia - nic nie skasowano. Sprawdź log, czy wygląda poprawnie."
                    : $"Gotowe. Widok: {viewLabel}. Usunięto {result.RemovedCount} wymiarów. Sprawdź wizualnie w Tekli (Ctrl+Z cofa, jeśli coś jest nie tak).";

                // Fokus na Teklę (do Ctrl+Z) ma sens TYLKO gdy coś realnie
                // skasowano - w dry-run (obecny stan na sztywno) nie ma czego
                // cofać, a przenoszenie fokusu na Teklę zabierało operatorowi
                // z oczu wynik, który właśnie się pojawił w tym oknie.
                if (!dryRun)
                {
                    TeklaWindowFocus.BringToFront(Log);
                }
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

        private void NotchTestButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            if (MessageBox.Show(this,
                "Wstawić jeden testowy wymiar szerokości wcięcia na [35021]?\n\nCtrl+Z w Tekli go cofa.",
                "Test wymiaru wcięcia", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            _logBox.Clear();
            Log($"===== {DateTime.Now:HH:mm:ss} TEST SZEROKOŚCI WCIĘCIA =====");
            _busy = true;
            try
            {
                var handler = new DrawingHandler();
                if (!handler.GetConnectionStatus())
                {
                    _statusLabel.Text = "Brak połączenia z Teklą.";
                    return;
                }
                var drawing = handler.GetActiveDrawing();
                if (drawing == null)
                {
                    _statusLabel.Text = "Brak otwartego rysunku.";
                    return;
                }

                bool inserted = NotchPilot.InsertWidthTest(drawing, Log);
                _statusLabel.Text = inserted
                    ? "Wstawiono test szerokości. Sprawdź go w Tekli (Ctrl+Z cofa)."
                    : "Test nie wstawił wymiaru — zobacz log.";
                if (inserted) TeklaWindowFocus.BringToFront(Log);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Błąd testu — zobacz log.";
                Log("BŁĄD: " + ex.Message);
                Log(ex.StackTrace);
            }
            finally
            {
                _busy = false;
            }
        }

        private void NotchLengthTestButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            if (MessageBox.Show(this,
                "Wstawić jeden testowy wymiar długości wcięcia na [35021]?\n\nCtrl+Z w Tekli go cofa.",
                "Test wymiaru wcięcia", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            _logBox.Clear();
            Log($"===== {DateTime.Now:HH:mm:ss} TEST DŁUGOŚCI WCIĘCIA =====");
            _busy = true;
            try
            {
                var handler = new DrawingHandler();
                if (!handler.GetConnectionStatus())
                {
                    _statusLabel.Text = "Brak połączenia z Teklą.";
                    return;
                }
                var drawing = handler.GetActiveDrawing();
                if (drawing == null)
                {
                    _statusLabel.Text = "Brak otwartego rysunku.";
                    return;
                }

                bool inserted = NotchPilot.InsertLengthTest(drawing, Log);
                _statusLabel.Text = inserted
                    ? "Wstawiono test długości. Sprawdź go w Tekli (Ctrl+Z cofa)."
                    : "Test nie wstawił wymiaru — zobacz log.";
                if (inserted) TeklaWindowFocus.BringToFront(Log);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Błąd testu — zobacz log.";
                Log("BŁĄD: " + ex.Message);
                Log(ex.StackTrace);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Test bramy bezpieczeństwa dla reguły dopasowania ściana↔wymiar na
        /// złączu z WIELOMA ścianami cięcia (np. [3.5013]) - dry-run już
        /// potwierdził poprawne liczby (--diag-notch-insert-dryrun), tu
        /// robimy REALNY insert, żeby operator mógł ocenić wizualnie w
        /// Tekli. Znajduje PIERWSZY wymiar do osi (ten sam warunek co
        /// RoAxisDimensionService.RemoveAxisDimensions) w CAŁYM rysunku,
        /// bierze jego środek jako punkt referencyjny dla NotchPilot -
        /// dokładnie scenariusz produkcyjny (skasowany wymiar -> wstawiony
        /// wymiar wcięcia w jego miejsce), tylko że nic tu nie kasuje.
        /// </summary>
        private void NotchMultiFaceTestButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            if (MessageBox.Show(this,
                "Wstawić testowo wymiary wcięcia (szerokość + długość) dla PIERWSZEGO znalezionego wymiaru do osi na aktywnym rysunku?\n\nCtrl+Z w Tekli cofa.",
                "Test wymiaru wcięcia (multi-face)", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            _logBox.Clear();
            Log($"===== {DateTime.Now:HH:mm:ss} TEST WCIĘCIA (MULTI-FACE) =====");
            _busy = true;
            try
            {
                var handler = new DrawingHandler();
                if (!handler.GetConnectionStatus())
                {
                    _statusLabel.Text = "Brak połączenia z Teklą.";
                    return;
                }
                var drawing = handler.GetActiveDrawing();
                if (drawing == null)
                {
                    _statusLabel.Text = "Brak otwartego rysunku.";
                    return;
                }

                // ZMIERZONE 2026-09-24: wcześniejsza wersja brała TYLKO
                // pierwszy znaleziony wymiar do osi (break po trafieniu) -
                // na [3.5013] (dwa złącza) to zawsze łapało to samo pierwsze
                // złącze, a drugi klik tylko mówił "już istnieje". Teraz
                // zbieramy WSZYSTKIE i próbujemy wstawić dla każdego -
                // NotchPilot i tak bezpiecznie pomija te, które już są.
                var referencePoints = new System.Collections.Generic.List<Tekla.Structures.Geometry3d.Point>();
                var top = drawing.GetSheet().GetAllObjects();
                while (top.MoveNext())
                {
                    if (!(top.Current is ViewBase view)) continue;
                    var dims = view.GetAllObjects(new[] { typeof(StraightDimension) });
                    while (dims.MoveNext())
                    {
                        if (!(dims.Current is StraightDimension sd) || !RoAxisDimensionService.TouchesAxis(sd))
                        {
                            continue;
                        }
                        double ownLength = Distance(sd.StartPoint, sd.EndPoint);
                        if (ownLength > RoAxisDimensionService.SameJointDistanceMm)
                        {
                            continue;
                        }
                        var mid = new Tekla.Structures.Geometry3d.Point(
                            (sd.StartPoint.X + sd.EndPoint.X) / 2, (sd.StartPoint.Y + sd.EndPoint.Y) / 2, (sd.StartPoint.Z + sd.EndPoint.Z) / 2);
                        referencePoints.Add(mid);
                    }
                }

                if (referencePoints.Count == 0)
                {
                    _statusLabel.Text = "Nie znaleziono wymiaru do osi na tym rysunku — zobacz log.";
                    Log("Brak kandydata.");
                    return;
                }

                bool anyInserted = false;
                foreach (var referencePoint in referencePoints)
                {
                    Log($"Wymiar do osi, środek=({referencePoint.X:F2};{referencePoint.Y:F2};{referencePoint.Z:F2}):");
                    bool widthInserted = NotchPilot.InsertWidthTest(drawing, Log, referencePoint);
                    // ZMIERZONE 2026-09-24: po CommitChanges() ponowne użycie
                    // TEGO SAMEGO uchwytu Drawing na kolejny insert dawało
                    // błędną blokadę marki (drawing.Mark przestawał się
                    // zgadzać) - odświeżamy uchwyt na wszelki wypadek.
                    drawing = handler.GetActiveDrawing() ?? drawing;
                    bool lengthInserted = NotchPilot.InsertLengthTest(drawing, Log, referencePoint);
                    drawing = handler.GetActiveDrawing() ?? drawing;
                    anyInserted = anyInserted || widthInserted || lengthInserted;
                }
                _statusLabel.Text = anyInserted
                    ? "Wstawiono test wcięcia. Sprawdź go w Tekli (Ctrl+Z cofa)."
                    : "Test nie wstawił nowego wymiaru — zobacz log.";
                if (anyInserted) TeklaWindowFocus.BringToFront(Log);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Błąd testu — zobacz log.";
                Log("BŁĄD: " + ex.Message);
                Log(ex.StackTrace);
            }
            finally
            {
                _busy = false;
            }
        }

        private static double Distance(Tekla.Structures.Geometry3d.Point a, Tekla.Structures.Geometry3d.Point b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// Zapasowa ścieżka wyboru widoku, gdy operator naciśnie Esc w
        /// Tekli zamiast klikać (PickPoint potrafi zawiesić się bez końca
        /// na kliku w pustkę - patrz komentarz w RunButton_Click). Etykieta
        /// to typ widoku + rozmiar w mm na papierze
        /// (GetAxisAlignedBoundingBox - PUŁAPKA jednostek z AGENTS.md: to
        /// mm na papierze, nie jednostki modelu).
        /// </summary>
        private ViewBase PickViewFromList(Drawing drawing)
        {
            var views = new System.Collections.Generic.List<ViewBase>();
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (top.Current is ViewBase vb)
                {
                    views.Add(vb);
                }
            }

            if (views.Count == 0)
            {
                MessageBox.Show(this, "Na arkuszu nie znaleziono żadnego widoku.", "Brak widoków");
                return null;
            }

            using (var dialog = new Form
            {
                Text = "Wybierz widok",
                Width = 420,
                Height = 320,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var list = new ListBox { Left = 10, Top = 10, Width = 384, Height = 220 };
                for (int i = 0; i < views.Count; i++)
                {
                    string size;
                    try
                    {
                        var box = views[i].GetAxisAlignedBoundingBox();
                        size = $"{box.MaxPoint.X - box.MinPoint.X:F0}×{box.MaxPoint.Y - box.MinPoint.Y:F0} mm";
                    }
                    catch (Exception ex)
                    {
                        size = "rozmiar nieznany (" + ex.GetType().Name + ")";
                    }
                    list.Items.Add($"{i + 1}. {views[i].GetType().Name} - {size}");
                }
                list.SelectedIndex = 0;

                var okButton = new Button { Text = "OK", Left = 220, Top = 240, Width = 80, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Anuluj", Left = 310, Top = 240, Width = 80, DialogResult = DialogResult.Cancel };
                dialog.Controls.Add(list);
                dialog.Controls.Add(okButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                return dialog.ShowDialog(this) == DialogResult.OK && list.SelectedIndex >= 0
                    ? views[list.SelectedIndex]
                    : null;
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
