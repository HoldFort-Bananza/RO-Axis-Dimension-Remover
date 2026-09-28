using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;

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
        internal const double AxisToleranceMm = 0.5;

        // Próg, powyżej którego współrzędna MIĘDZY końcami wymiaru uznajemy
        // za "różną" (czyli tę oś w ogóle bierzemy pod uwagę przy szukaniu
        // zera). Bez tego wymiar płaski w widoku (dla którego jedna
        // współrzędna, typowo Z, jest 0 dla OBU końców, bo widok jest 2D, nie
        // dlatego że to oś) łapałby się jako "na osi" - zdarzyło się na
        // [35270] w v1, patrz AGENTS.md.
        internal const double CoordDiffersToleranceMm = 0.01;

        // Jednostki modelu (mm), NIE mm na papierze - StartPoint/EndPoint są
        // w jednostkach modelu (patrz ../AGENTS.md, pułapka jednostek).
        // Szacunek, nie pomiar: dwa punkty tego samego złącza RO leżą w
        // odległości rzędu promienia profilu (dziesiątki mm), a osobne
        // złącza na jednym rysunku balustrady dzieli zwykle metr i więcej.
        // 300 mm to margines bezpieczeństwa między tymi skalami - do
        // zweryfikowania na kolejnych rysunkach z wieloma złączami w jednym
        // widoku.
        internal const double SameJointDistanceMm = 300.0;

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

            // ZMIERZONE 2026-09-25: TouchesAxis jest czysto geometryczny
            // (współrzędna blisko zera + głębia Z + krótka długość) i NIC w
            // kodzie wcześniej nie sprawdzało, czy część jest w ogóle
            // profilem RO - mimo że cały opis narzędzia (nazwa, komentarze)
            // zakłada tylko RO. Skan całego modelu (--diag-find-candidates)
            // znalazł setki "kandydatów" na blachach/dźwigarach/kątownikach
            // (Blech/Träger/Winkel), operator na żywym [21050] (blacha,
            // zwykłe rozstawy otworów 20/30/70/120/130 mm) potwierdził: "nic
            // nie powinniśmy kasować z tej blachy" - to był realny, nie
            // teoretyczny, risk fałszywego dopasowania. Guard: jeśli widok
            // nie zawiera ŻADNEJ części o profilu zaczynającym się na "RO",
            // nic nie rusza - bez względu na to, ile TouchesAxis znajdzie.
            if (!ViewHasRoProfile(view, log))
            {
                log("Ten widok nie zawiera części o profilu RO - narzędzie jest ograniczone do profili RO, nic nie kasuję.");
                return result;
            }

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

        // "RO" to konwencja nazewnictwa profili w katalogu Tekli (rura
        // okrągła) - zmierzone na [35021]/[3.5013]: Profile.ProfileString
        // = "RO42.4*3.2". Brak połączenia z Model albo brak części z takim
        // profilem w widoku = bezpieczny domyślny wynik "false" (nic nie
        // kasuj), nigdy "zgaduj, że to RO".
        private static bool ViewHasRoProfile(ViewBase view, Action<string> log)
        {
            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                log("Brak połączenia z Teklą (Model) - nie mogę sprawdzić profilu części, nic nie kasuję.");
                return false;
            }
            var parts = view.GetAllObjects(new[] { typeof(Part) });
            while (parts.MoveNext())
            {
                if (!(parts.Current is Part drawingPart)) continue;
                if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;
                string profile;
                try { profile = modelPart.Profile.ProfileString; }
                catch { continue; }
                if (profile != null && profile.TrimStart().StartsWith("RO", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static double PointDistance(Tekla.Structures.Geometry3d.Point p1, Tekla.Structures.Geometry3d.Point p2)
        {
            double dx = p1.X - p2.X, dy = p1.Y - p2.Y, dz = p1.Z - p2.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // internal: reużywane przez DiagRunner (--diag-notch-match), żeby
        // dopasowanie ściany cięcia do wymiaru bazowało na TEJ SAMEJ regule
        // wykrywania "dotyka osi", zamiast duplikować ją niezależnie.
        //
        // ZMIERZONE na żywym [35021] (operator zgłosił: program niepotrzebnie
        // kasuje "21 mm" - promień rury, potrzebny na budowie). Odczyt
        // Wartość+Start+End dla WSZYSTKICH wymiarów w widoku (--diag-dimension-style)
        // pokazał:
        //   "21 mm": Start=(0;-21,20;0) End=(7,68;0;0)     - Z=0 na OBU końcach (płaski)
        //   "8 mm":  Start=(0;-21,20;0) End=(7,68;0;21,20) - Z zmienia się 0->21,2
        // "21 mm" to zwykły, płaski (2D) wymiar promienia profilu okrągłego -
        // dokładnie przypadek z punktu "Pułapki API": lokalny punkt
        // referencyjny rury leży na osi Z DEFINICJI, więc wymiar do niego
        // "dotyka osi" mimo że jest całkowicie poprawny i nie ma nic
        // wspólnego ze skośnym cięciem. "8 mm" ma realną głębię (Z różni się
        // między końcami) - to znak, że jeden z jego końców leży na SKOŚNYM
        // cięciu (nie leży płasko w widoku), czyli faktycznie jest lokalnym
        // artefaktem złącza. Rozróżnienie: artefakt złącza ma realną
        // rozpiętość w Z, zwykły płaski wymiar (promień, długość, pozycja) -
        // nie, nawet jeśli przypadkiem "dotyka" zera we współrzędnej Y.
        //
        // Nie zmierzone jeszcze: złącze, gdzie skos leży tak, że artefakt
        // wychodzi płaski w Z (nie zaobserwowane na [35021]/[3.5013]) -
        // gdyby się pojawiło, ten warunek błędnie by go NIE złapał.
        internal static bool TouchesAxis(StraightDimension sd)
        {
            bool yDiffers = Math.Abs(sd.StartPoint.Y - sd.EndPoint.Y) > CoordDiffersToleranceMm;
            bool zDiffers = Math.Abs(sd.StartPoint.Z - sd.EndPoint.Z) > CoordDiffersToleranceMm;

            if (!zDiffers)
            {
                // Płaski wymiar (Z stałe na całej długości) - zwykła
                // geometria widoku 2D (promień, pozycja, długość), nie
                // artefakt skośnego cięcia. Patrz komentarz wyżej.
                return false;
            }

            if (yDiffers && (NearZero(sd.StartPoint.Y) || NearZero(sd.EndPoint.Y)))
            {
                return true;
            }
            if (NearZero(sd.StartPoint.Z) || NearZero(sd.EndPoint.Z))
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
        internal static double? GetDisplayedValue(StraightDimension sd)
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
