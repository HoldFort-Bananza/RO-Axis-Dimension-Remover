using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;

namespace RoAxisDimensionRemover
{
    /// <summary>
    /// Cała logika, zero wiedzy o UI. Kasuje nadmiarowy wymiar prosty "do
    /// osi" na profilach RO: w widoku przekroju/detalu miejsca łączenia
    /// Tekla czasem auto-generuje wymiar zaczepiony o teoretyczną OŚ profilu
    /// (współrzędna promienia ≈ 0) zamiast o jego widoczną powierzchnię -
    /// typowo przy skosie/ucięciu pod kątem. Jeśli takich wymiarów jest w
    /// jednym złączu więcej niż jeden, zostaje tylko ten o największej
    /// wyświetlanej wartości (patrz `Dimension.Value`, PUŁAPKA niżej) - reszta
    /// to duplikaty.
    ///
    /// Reguła NIE jest zgadywana - każda stała ma pochodzenie w komentarzu
    /// obok niej. Pełna historia (4 wersje, w tym dwie, które realnie
    /// skasowały dobre wymiary na żywym modelu) i odrzucone podejścia są w
    /// `CLAUDE.md` w tym repo - przeczytaj przed zmianą tej klasy.
    ///
    /// PUŁAPKA: `Dimension.Value` to rzut na kierunek wymiaru, nie odległość
    /// euklidesowa między `StartPoint`/`EndPoint` - "krótszy" trzeba oceniać
    /// po wyświetlanej wartości, nie po surowej geometrii.
    /// </summary>
    public class RoAxisDimensionService
    {
        // mm na papierze. Promień profilu RO nigdy nie schodzi blisko zera,
        // więc ten margines bezpiecznie odróżnia "dokładnie na osi" od
        // "na powierzchni" nawet dla najcieńszych rur.
        private const double AxisToleranceMm = 0.5;

        // Próg, powyżej którego współrzędna MIĘDZY końcami wymiaru uznajemy
        // za "różną" (czyli tę oś w ogóle bierzemy pod uwagę przy szukaniu
        // zera). Bez tego wymiar płaski w widoku (dla którego jedna
        // współrzędna, typowo Z, jest 0 dla OBU końców, bo widok jest 2D, nie
        // dlatego że to oś) łapałby się jako "na osi" - zdarzyło się na
        // [35270] w v1, patrz CLAUDE.md.
        private const double CoordDiffersToleranceMm = 0.01;

        // Jednostki modelu (mm), NIE mm na papierze - StartPoint/EndPoint są
        // w jednostkach modelu (patrz ../CLAUDE.md, pułapka jednostek).
        // Szacunek, nie pomiar: dwa punkty tego samego złącza RO leżą w
        // odległości rzędu promienia profilu (dziesiątki mm), a osobne
        // złącza na jednym rysunku balustrady dzieli zwykle metr i więcej.
        // 300 mm to margines bezpieczeństwa między tymi skalami - do
        // zweryfikowania na kolejnych rysunkach z wieloma złączami w jednym
        // widoku.
        private const double SameJointDistanceMm = 300.0;

        public class Result
        {
            public int ViewsChecked;
            public int RemovedCount;
        }

        public Result RemoveRedundantAxisDimensions(Drawing drawing, Action<string> log, bool dryRun = false)
        {
            var result = new Result();
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view))
                {
                    continue;
                }
                result.ViewsChecked++;

                var candidates = new List<(StraightDimension Dim, double Value)>();
                var objs = view.GetAllObjects();
                while (objs.MoveNext())
                {
                    if (!(objs.Current is StraightDimension sd) || !TouchesAxis(sd))
                    {
                        continue;
                    }

                    // Dwuznaczność oś/powierzchnia dotyczy tylko krótkiego,
                    // lokalnego wymiaru przy złączu. Wymiar całkowitej
                    // długości profilu też "dotyka osi" (jego punkt startowy
                    // jest w definicji na osi - lokalny początek układu
                    // współrzędnych rury), ale to nie jest ten sam przypadek -
                    // odsiewamy go po własnej długości, zanim w ogóle trafi do
                    // kandydatów. Zdiagnozowane na [3.5013] w v4, patrz CLAUDE.md.
                    double ownLength = PointDistance(sd.StartPoint, sd.EndPoint);
                    if (ownLength > SameJointDistanceMm)
                    {
                        if (dryRun)
                        {
                            log($"[diag] wymiar dotyka osi, ale ma {ownLength:F0} mm własnej długości (>{SameJointDistanceMm:F0} mm) - to nie lokalny artefakt złącza, pomijam jako kandydata.");
                        }
                        continue;
                    }

                    double? value = GetDisplayedValue(sd);
                    if (value == null)
                    {
                        log("UWAGA: wymiar dotyka osi, ale nie udało się odczytać wyświetlanej wartości - pomijam go, żeby niczego nie zgadywać.");
                        continue;
                    }
                    candidates.Add((sd, value.Value));
                }

                if (candidates.Count < 2)
                {
                    continue; // jedyny wymiar do osi w tym widoku - nie duplikat
                }

                if (dryRun)
                {
                    log($"[diag] widok, kandydatów do osi: {candidates.Count}");
                    foreach (var c in candidates)
                    {
                        log($"[diag]   wartość={c.Value:F1}  Start=({c.Dim.StartPoint.X:F1};{c.Dim.StartPoint.Y:F1};{c.Dim.StartPoint.Z:F1})  End=({c.Dim.EndPoint.X:F1};{c.Dim.EndPoint.Y:F1};{c.Dim.EndPoint.Z:F1})");
                    }
                }

                var clusters = GroupByProximity(candidates);
                if (dryRun)
                {
                    log($"[diag] klastrów po grupowaniu: {clusters.Count} (progi {SameJointDistanceMm} mm)");
                }

                foreach (var cluster in clusters)
                {
                    if (dryRun)
                    {
                        log($"[diag]   klaster rozmiar={cluster.Count}  wartości=[{string.Join(", ", cluster.Select(c => c.Value.ToString("F1")))}]");
                    }
                    if (cluster.Count < 2)
                    {
                        continue; // pojedyncze złącze - nie duplikat
                    }

                    var keep = cluster.OrderByDescending(c => c.Value).First();
                    foreach (var c in cluster)
                    {
                        if (ReferenceEquals(c.Dim, keep.Dim))
                        {
                            continue;
                        }
                        log($"Złącze: {(dryRun ? "znaleziono" : "kasuję")} nadmiarowy wymiar do osi ({c.Value:F0} mm), zostaje ({keep.Value:F0} mm).");
                        result.RemovedCount++;
                        if (!dryRun)
                        {
                            c.Dim.Delete();
                        }
                    }
                }
            }

            if (result.RemovedCount > 0 && !dryRun)
            {
                drawing.CommitChanges();
            }

            return result;
        }

        /// <summary>
        /// Grupuje wymiary "do osi" po bliskości geometrycznej (to samo
        /// złącze), a nie po przynależności do widoku - jeden widok może
        /// zawierać kilka niepowiązanych złączy RO (zdarzyło się na
        /// [3.5013] w v2, patrz CLAUDE.md). Single-linkage: dwa wymiary
        /// trafiają do tej samej grupy, jeśli KTÓRYKOLWIEK z ich punktów
        /// końcowych leży bliżej niż SameJointDistanceMm od punktu drugiego
        /// wymiaru.
        /// </summary>
        private static List<List<(StraightDimension Dim, double Value)>> GroupByProximity(
            List<(StraightDimension Dim, double Value)> candidates)
        {
            int n = candidates.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int i) => parent[i] == i ? i : (parent[i] = Find(parent[i]));
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (MinPointDistance(candidates[i].Dim, candidates[j].Dim) < SameJointDistanceMm)
                    {
                        Union(i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<(StraightDimension, double)>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<(StraightDimension, double)>();
                    groups[root] = list;
                }
                list.Add(candidates[i]);
            }
            return groups.Values.ToList();
        }

        private static double MinPointDistance(StraightDimension a, StraightDimension b)
        {
            double d1 = PointDistance(a.StartPoint, b.StartPoint);
            double d2 = PointDistance(a.StartPoint, b.EndPoint);
            double d3 = PointDistance(a.EndPoint, b.StartPoint);
            double d4 = PointDistance(a.EndPoint, b.EndPoint);
            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }

        private static double PointDistance(Tekla.Structures.Geometry3d.Point p1, Tekla.Structures.Geometry3d.Point p2)
        {
            double dx = p1.X - p2.X, dy = p1.Y - p2.Y, dz = p1.Z - p2.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static bool TouchesAxis(StraightDimension sd)
        {
            bool yDiffers = Math.Abs(sd.StartPoint.Y - sd.EndPoint.Y) > CoordDiffersToleranceMm;
            bool zDiffers = Math.Abs(sd.StartPoint.Z - sd.EndPoint.Z) > CoordDiffersToleranceMm;

            if (yDiffers && (NearZero(sd.StartPoint.Y) || NearZero(sd.EndPoint.Y)))
            {
                return true;
            }
            if (zDiffers && (NearZero(sd.StartPoint.Z) || NearZero(sd.EndPoint.Z)))
            {
                return true;
            }
            return false;
        }

        private static bool NearZero(double v) => Math.Abs(v) < AxisToleranceMm;

        /// <summary>
        /// Dimension.Value to kolekcja elementów tekstu wymiaru (obsługa
        /// mieszanego formatowania), nie sama liczba. Szukamy w niej
        /// pierwszego elementu z właściwością "Value" dającą się sparsować
        /// jako liczba - zmierzone na [35270] przez zrzut refleksją.
        /// </summary>
        private static double? GetDisplayedValue(StraightDimension sd)
        {
            if (!(sd.Value is IEnumerable en))
            {
                return null;
            }
            foreach (var item in en)
            {
                if (item == null) continue;
                var prop = item.GetType().GetProperty("Value");
                if (prop == null) continue;
                var raw = prop.GetValue(item);
                if (raw != null && double.TryParse(raw.ToString(), out double d))
                {
                    return d;
                }
            }
            return null;
        }
    }
}
