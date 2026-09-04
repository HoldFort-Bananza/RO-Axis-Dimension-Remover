using System;
using System.IO;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;

namespace RoAxisDimensionRemover
{
    // Rusztowanie diagnostyczne - USUNĄĆ przed commitem.
    // Szuka innego rysunku pojedynczej części z profilem RO, na którym
    // faktycznie występuje wzorzec nadmiarowego wymiaru do osi - bez
    // otwierania każdego kandydata (GetModelObjectIdentifiers na zamkniętym
    // rysunku, patrz wiki Radius Dimension Mover / 7-Diagnostyka).
    internal static class Inspector
    {
        // Obok .exe, NIE w katalogu tymczasowym sesji Claude Code - patrz
        // ta sama uwaga w MainForm.cs.
        private static readonly string LogPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "logs", "scan_ro.log");

        public static string FindAnotherCandidate(string excludeMark)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, $"{DateTime.Now:HH:mm:ss} start skanu\r\n");
            void Log(string s) => File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {s}\r\n");

            try
            {
                return FindAnotherCandidateCore(excludeMark, Log);
            }
            catch (Exception ex)
            {
                Log($"WYJATEK GLOWNY: {ex}");
                return null;
            }
        }

        private static string FindAnotherCandidateCore(string excludeMark, Action<string> Log)
        {
            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("brak polaczenia z Tekla");
                return null;
            }
            var model = new Model();
            if (!model.GetConnectionStatus())
            {
                Log("brak polaczenia z modelem");
                return null;
            }

            var service = new RoAxisDimensionService();
            int checkedCount = 0, roCount = 0;
            var drawings = dh.GetDrawings();
            while (drawings.MoveNext())
            {
                try
                {
                    var d = drawings.Current;
                    if (!(d is SinglePartDrawing)) continue;
                    if (string.Equals(d.Mark, excludeMark, StringComparison.OrdinalIgnoreCase)) continue;
                    checkedCount++;
                    if (checkedCount % 10 == 0)
                    {
                        Log($"... sprawdzono {checkedCount} rysunkow czesci, RO dotad: {roCount}");
                    }

                    bool isRo = false;
                    foreach (var id in dh.GetModelObjectIdentifiers(d))
                    {
                        if (model.SelectModelObject(id) is Tekla.Structures.Model.Part p &&
                            p.Profile != null &&
                            p.Profile.ProfileString != null &&
                            p.Profile.ProfileString.TrimStart().StartsWith("RO", StringComparison.OrdinalIgnoreCase))
                        {
                            isRo = true;
                            break;
                        }
                    }
                    if (!isRo) continue;
                    roCount++;

                    // Otwieramy po cichu (false), sprawdzamy na sucho, zamykamy bez zapisu.
                    dh.SetActiveDrawing(d, false);
                    int found = 0;
                    try
                    {
                        var result = service.RemoveRedundantAxisDimensions(d, msg => { }, dryRun: true);
                        found = result.RemovedCount;
                    }
                    catch (Exception ex)
                    {
                        Log($"blad przy sprawdzaniu {d.Mark}: {ex.Message}");
                    }
                    Log($"RO: {d.Mark} / {d.Name} - kandydatow do usuniecia: {found}");
                    if (found > 0)
                    {
                        // Trzymamy TĘ SAMĄ referencję Drawing z przelotu - ponowne
                        // szukanie po Mark byłoby wolne (patrz wiki, 14x różnicy).
                        dh.SetActiveDrawing(d, true);
                        Log($"WYBRANY i otwarty: {d.Mark}");
                        Log($"koniec: sprawdzono {checkedCount} rysunkow czesci, RO: {roCount}");
                        return d.Mark;
                    }
                    dh.CloseActiveDrawing(false);

                    if (roCount >= 40)
                    {
                        Log("limit 40 rysunkow RO bez trafienia - przerywam");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"blad na rysunku (pomijam): {ex.GetType().Name}: {ex.Message}");
                }
            }

            Log($"koniec bez trafienia: sprawdzono {checkedCount} rysunkow czesci, RO: {roCount}");
            return null;
        }
    }
}
