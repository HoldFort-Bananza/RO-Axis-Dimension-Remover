using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

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
    public static class RoAxisDimensionService
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

        /// <summary>
        /// Kasuje wszystkie wymiary "do osi" w JEDNYM widoku (ten, który
        /// operator wybrał Pickerem w MainForm - patrz PUŁAPKA 5 wyżej,
        /// dlaczego to musi być per widok, nie cały arkusz naraz). Zwraca
        /// liczbę skasowanych (w dry-run: znalezionych) wymiarów.
        /// </summary>
        public static int RemoveAxisDimensions(Drawing drawing, ViewBase view, Action<string> log, bool dryRun = false)
        {
            int removedCount = 0;

            // ZMIERZONE 2026-09-28 na [225.130] (zespół balustrady ze śrubami
            // M16 i płytkami): 14 kandydatów, wszystkie fałszywe - to
            // geometria detali śrubowych, nie skos rury. Reguła była
            // projektowana i testowana tylko na rysunkach pojedynczej części.
            if (!(drawing is SinglePartDrawing))
            {
                log($"Rysunek typu {drawing.GetType().Name} - narzędzie działa tylko na rysunkach pojedynczej części (SinglePartDrawing), nic nie kasuję.");
                return 0;
            }

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
                return 0;
            }

            // ZMIERZONE 2026-09-29 na [35681] (łuk poręczy "Bogen", RO48.3):
            // 10 z 15 wymiarów widoku - w tym 63 i 29, opisujące gięcie -
            // spełniało TouchesAxis, bo na giętej rurze prawie każdy punkt ma
            // "głębię Z" względem cięciwy łuku. Reguła była projektowana i
            // sprawdzana tylko na prostych rurach.
            if (ViewHasBentPart(view))
            {
                log("Ten widok zawiera giętą rurę - reguła działa tylko na prostych rurach, nic nie kasuję.");
                return 0;
            }

            var cutZones = CutZones(view);
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
                double ownLength = NotchPilot.Distance(sd.StartPoint, sd.EndPoint);
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

                // ZMIERZONE 2026-09-30 na [35260]: rura ma 105 mm, więc jej
                // całkowita długość (0 -> 104,95, koniec w płaszczyźnie widoku,
                // początek w głębi) spełniała TouchesAxis i mieściła się w
                // filtrze 300 mm wyżej - "Usuń" skasowałby długość rury, a
                // "Wstaw" jej nie odtwarza. Artefakt skosu leży z definicji
                // przy ścięciu: oba końce wzdłuż osi w obrębie jednej ściany
                // cięcia. Wszystkie kasowania potwierdzone przez operatora
                // ([35021], [3.5013], [35092]) spełniają ten warunek.
                if (!InCutZone(sd, cutZones))
                {
                    if (dryRun)
                    {
                        log($"[diag] wymiar dotyka osi ({valueText}), ale nie leży w obrębie ścięcia - to nie artefakt skosu, pomijam. Start=({sd.StartPoint.X:F2};{sd.StartPoint.Y:F2};{sd.StartPoint.Z:F2}) End=({sd.EndPoint.X:F2};{sd.EndPoint.Y:F2};{sd.EndPoint.Z:F2})");
                    }
                    continue;
                }

                log($"{(dryRun ? "Znaleziono" : "Kasuję")} wymiar do osi ({valueText}).  {DescribeDimensionSet(sd)}");
                removedCount++;
                if (!dryRun)
                {
                    sd.Delete();
                }
            }

            if (removedCount == 0)
            {
                log("Brak wymiarów do osi w tym widoku - nic do usunięcia.");
            }

            if (removedCount > 0 && !dryRun)
            {
                drawing.CommitChanges();
            }

            return removedCount;
        }

        // "RO" to konwencja nazewnictwa profili w katalogu Tekli (rura
        // okrągła) - zmierzone na [35021]/[3.5013]: Profile.ProfileString
        // = "RO42.4*3.2". Brak połączenia z Model albo brak części z takim
        // profilem w widoku = bezpieczny domyślny wynik "false" (nic nie
        // kasuj), nigdy "zgaduj, że to RO".
        // Brak połączenia z Model = "true" (nic nie kasuj), tak jak w
        // ViewHasRoProfile - nigdy nie zgadujemy, że rura jest prosta.
        private static bool ViewHasBentPart(ViewBase view)
        {
            var model = new TSM.Model();
            if (!model.GetConnectionStatus()) return true;
            var parts = view.GetAllObjects(new[] { typeof(Part) });
            while (parts.MoveNext())
            {
                if (!(parts.Current is Part drawingPart)) continue;
                if (model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart && !NotchPilot.IsStraight(modelPart))
                {
                    return true;
                }
            }
            return false;
        }

        // Odcinek wzdłuż osi rury (w układzie widoku), który zajmuje ściana
        // cięcia. Punkty ściany są w globalnych współrzędnych modelu, wymiary w
        // układzie widoku - przeliczenie tak jak w NotchPilot. Brak View albo
        // połączenia z Model = brak odcinków = nic nie kasujemy.
        private static List<(TSG.Vector Axis, double Min, double Max)> CutZones(ViewBase viewBase)
        {
            var zones = new List<(TSG.Vector Axis, double Min, double Max)>();
            var model = new TSM.Model();
            if (!(viewBase is View view) || !model.GetConnectionStatus()) return zones;
            var cs = view.DisplayCoordinateSystem;
            var parts = view.GetAllObjects(new[] { typeof(Part) });
            while (parts.MoveNext())
            {
                if (!(parts.Current is Part drawingPart)) continue;
                if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;
                var axisModel = NotchPilot.BeamAxis(modelPart);
                if (axisModel == null) continue;
                var axis = NotchPilot.ToViewSpaceVector(axisModel, cs);
                foreach (var (_, outerLoop) in NotchPilot.CutFaces(modelPart.GetSolid(), axisModel))
                {
                    double min = double.MaxValue, max = double.MinValue;
                    foreach (var point in outerLoop)
                    {
                        double t = AlongAxis(NotchPilot.ToViewSpace(point, cs), axis);
                        min = Math.Min(min, t);
                        max = Math.Max(max, t);
                    }
                    zones.Add((axis, min, max));
                }
            }
            return zones;
        }

        private static bool InCutZone(StraightDimension sd, List<(TSG.Vector Axis, double Min, double Max)> zones)
        {
            foreach (var (axis, min, max) in zones)
            {
                double tStart = AlongAxis(sd.StartPoint, axis), tEnd = AlongAxis(sd.EndPoint, axis);
                if (tStart >= min - AxisToleranceMm && tStart <= max + AxisToleranceMm
                    && tEnd >= min - AxisToleranceMm && tEnd <= max + AxisToleranceMm)
                {
                    return true;
                }
            }
            return false;
        }

        private static double AlongAxis(TSG.Point p, TSG.Vector axis) => p.X * axis.X + p.Y * axis.Y + p.Z * axis.Z;

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

        // internal: reużywane przez DiagRunner, żeby diagnostyka bazowała na
        // TEJ SAMEJ regule wykrywania "dotyka osi", zamiast duplikować ją.
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
