using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;

namespace RoAxisDimensionRemover
{
    /// <summary>
    /// Cała logika, zero wiedzy o UI. Kasuje wymiar prosty "do osi" na
    /// profilach RO: w widoku przekroju/detalu miejsca łączenia Tekla czasem
    /// auto-generuje wymiar zaczepiony o teoretyczną OŚ profilu (współrzędna
    /// promienia ≈ 0) zamiast o jego widoczną powierzchnię - typowo przy
    /// skosie/ucięciu pod kątem. Takie wymiary są niepotrzebne (informację o
    /// wcięciu ma nosić osobny wymiar wcięcia, nie wymiar do osi), więc
    /// zasada jest teraz prosta: usuń KAŻDY wymiar dotykający osi w
    /// wybranym widoku, bez oceniania, który z nich "jest ważniejszy".
    ///
    /// Wcześniejsza wersja (do PR poprzedzającego ten commit) zamiast tego
    /// grupowała wymiary do osi i kasowała "duplikaty", zostawiając ten o
    /// większej wartości - to miało PUŁAPKĘ 5 (patrz AGENTS.md): para
    /// PROSTOPADŁYCH wymiarów tego samego skosu 45° ma identyczną wyświetlaną
    /// wartość i była błędnie brana za duplikat, więc ginęła jedna z dwóch
    /// niezależnych informacji (poziom albo pion). Skoro teraz kasujemy
    /// wszystkie wymiary do osi bez wyjątku (i doceluje w to miejsce nowy
    /// wymiar wcięcia), problem odróżniania duplikatu od pary prostopadłej
    /// znika - nie ma już decyzji "który zostaje".
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
        // [35270] w v1, patrz AGENTS.md.
        private const double CoordDiffersToleranceMm = 0.01;

        // Jednostki modelu (mm), NIE mm na papierze - StartPoint/EndPoint są
        // w jednostkach modelu (patrz ../AGENTS.md, pułapka jednostek).
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

        /// <summary>
        /// Kasuje wszystkie wymiary "do osi" w JEDNYM widoku (ten, który
        /// operator wybrał Pickerem w MainForm - patrz PUŁAPKA 5 wyżej,
        /// dlaczego to musi być per widok, nie cały arkusz naraz).
        /// </summary>
        public Result RemoveAxisDimensions(Drawing drawing, ViewBase view, Action<string> log, bool dryRun = false)
        {
            var result = new Result { ViewsChecked = 1 };

            var objs = view.GetAllObjects();
            while (objs.MoveNext())
            {
                if (!(objs.Current is StraightDimension sd) || !TouchesAxis(sd))
                {
                    continue;
                }

                // Dwuznaczność oś/powierzchnia dotyczy tylko krótkiego,
                // lokalnego wymiaru przy złączu. Wymiar całkowitej długości
                // profilu też "dotyka osi" (jego punkt startowy jest w
                // definicji na osi - lokalny początek układu współrzędnych
                // rury), ale to nie jest ten sam przypadek - odsiewamy go po
                // własnej długości. Zdiagnozowane na [3.5013] w v4, patrz
                // AGENTS.md.
                double ownLength = PointDistance(sd.StartPoint, sd.EndPoint);
                if (ownLength > SameJointDistanceMm)
                {
                    if (dryRun)
                    {
                        log($"[diag] wymiar dotyka osi, ale ma {ownLength:F0} mm własnej długości (>{SameJointDistanceMm:F0} mm) - to nie lokalny artefakt złącza, pomijam.");
                    }
                    continue;
                }

                double? value = GetDisplayedValue(sd);
                string valueText = value.HasValue ? $"{value.Value:F0} mm" : "? (nie udało się odczytać wartości)";
                log($"{(dryRun ? "Znaleziono" : "Kasuję")} wymiar do osi ({valueText}).  {DescribeDimensionSet(sd)}");
                result.RemovedCount++;
                if (!dryRun)
                {
                    sd.Delete();
                }
            }

            if (result.RemovedCount == 0)
            {
                log("Brak wymiarów do osi w tym widoku - nic do usunięcia.");
            }

            if (result.RemovedCount > 0 && !dryRun)
            {
                drawing.CommitChanges();
            }

            return result;
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

        /// <summary>
        /// Diagnostyka do znalezienia PUŁAPKI 5 (zgłoszone przez operatora:
        /// realne kasowanie usunęło więcej niż jeden zamierzony wymiar - "całą
        /// szerokość albo całą długość"). Tekla grupuje pojedyncze
        /// StraightDimension w StraightDimensionSet ("łańcuch" - wspólna linia
        /// wymiarowa z wieloma odcinkami). GetDimensionSet() mówi, do jakiego
        /// łańcucha należy kandydat - jeśli "24 mm"/"2811 mm" są w TYM SAMYM
        /// zestawie co "12 mm", to .Delete() na jednym elemencie może
        /// kaskadowo ruszyć resztę zestawu w Tekli, mimo że nasz kod prosi o
        /// usunięcie tylko jednego konkretnego obiektu. Czysto do odczytu -
        /// nie wywołuje niczego, co modyfikuje rysunek.
        /// </summary>
        private static string DescribeDimensionSet(StraightDimension sd)
        {
            try
            {
                var set = sd.GetDimensionSet();
                if (set == null)
                {
                    return "DimensionSet=brak (samodzielny wymiar)";
                }

                int count = 0;
                var members = set.GetObjects();
                while (members.MoveNext())
                {
                    count++;
                }
                return $"DimensionSet={set.GetType().Name} elementów={count}";
            }
            catch (Exception ex)
            {
                return $"DimensionSet=błąd odczytu ({ex.GetType().Name}: {ex.Message})";
            }
        }
    }
}
