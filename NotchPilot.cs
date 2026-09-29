using System;
using System.Collections.Generic;
using Tekla.Structures.Drawing;
using Tekla.Structures.Solid;
using TSM = Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

namespace RoAxisDimensionRemover
{
    // Wstawianie wymiaru wcięcia (cut fitting) po skasowaniu wymiarów do
    // osi. Reguła dopasowania ściana↔wymiar i asymetria widoku
    // długość/szerokość zweryfikowane na [35021] i [3.5013] - patrz
    // AGENTS.md, sekcja "Wymiar wcięcia". Bez blokady rysunku od
    // 2026-09-25 (decyzja operatora, poinformowanego o ryzyku) - wciąż
    // nierozwiązany, osobny problem: program nie odróżnia złącza, które
    // FAKTYCZNIE potrzebuje wymiaru wcięcia, od takiego, które tylko
    // geometrycznie ma ścianę cięcia (patrz AGENTS.md, "Następne kroki").
    internal static class NotchPilot
    {
        // Tolerancja błędu numerycznego projekcji, nie próg geometrii:
        // zmierzone punkty pilota mają Z=0. Wspólna z DiagRunner.
        internal const double NumericalZero = 0.000001;

        // Ściana o mniejszym kącie cięcia to praktycznie proste zakończenie
        // rury. ZMIERZONE 2026-09-25 na żywym [3.5027]: ta sama bryła może
        // mieć ścianę pod PRAWDZIWYM, widocznym kątem (koniec "ścięty") i
        // drugą, gdzie kąt jest tak mały, że koniec wygląda jak zwykłe
        // płaskie zakończenie (operator: "z jednej strony płaskie") - mimo
        // że OBIE kwalifikują się geometrycznie (>1 pętla, normalna
        // niedokładnie równoległa do osi). Odróżnia je stosunek
        // długość/szerokość cięcia (= 1/cos kąta cięcia): ~5° ([3.5027])
        // pomijamy, ~19,9° ([35021]) i ~45° ([3.5013]/[3.5027]) wstawiamy.
        // 10° to SZACUNEK (w połowie między 5° a 19,9°), nie pomiar - do
        // doprecyzowania, gdy pojawi się złącze bliżej granicy. NIE
        // rozwiązuje problemu z 24.09 ([3.5013] drugi koniec miał TEN SAM
        // ~45° kąt, a operator go odrzucił z innego, nieznanego powodu).
        internal const double MinCutAngleDegrees = 10.0;

        public static bool InsertWidth(Drawing drawing, Action<string> log, TSG.Point referencePoint = null, bool dryRun = false, View referenceView = null)
        {
            return Insert(drawing, log, longest: false, label: "szerokości", referencePoint, dryRun, referenceView);
        }

        public static bool InsertLength(Drawing drawing, Action<string> log, TSG.Point referencePoint = null, bool dryRun = false, View referenceView = null)
        {
            return Insert(drawing, log, longest: true, label: "długości", referencePoint, dryRun, referenceView);
        }

        private static bool Insert(Drawing drawing, Action<string> log, bool longest, string label, TSG.Point referencePoint, bool dryRun, View referenceView = null)
        {
            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                log("WSTRZYMANO: brak połączenia z Teklą (Model).");
                return false;
            }

            // ZMIERZONE 2026-09-25 na żywym [3.5013] (surowy zrzut
            // --diag-notch-raw): złącze z DWIEMA ścianami cięcia ma, w
            // KAŻDYM z dwóch widoków (ramek na arkuszu), dokładnie jedną
            // ścianę z płaską (Z≈0) cięciwą DŁUGOŚCI i drugą (przeciwną) ze
            // płaską cięciwą SZEROKOŚCI - nigdy obie naraz dla tej samej
            // ściany w tym samym widoku. Fizycznie: krótki kierunek owalnego
            // przecięcia rury "ucieka w głąb kartki" akurat w tej ramce,
            // która pokazuje długi kierunek płasko - i odwrotnie w drugiej
            // ramce. Operator zaakceptował (2026-09-25): długość i szerokość
            // TEGO SAMEGO końca mogą wylądować w RÓŻNYCH ramkach, każda w
            // całości w jednej - byle żaden POJEDYNCZY wymiar nie był
            // rozdzielony między dwie ramki (co i tak nie zdarza się w tym
            // API - Insert() zawsze celuje w jeden View). Stąd asymetria
            // niżej: DŁUGOŚĆ ogranicza się do widoku źródłowego wymiaru do
            // osi (tam jej płaski kandydat zawsze jest właściwą ścianą -
            // zmierzone), a SZEROKOŚĆ szuka po CAŁYM rysunku wśród płaskich
            // kandydatów (tam, gdzie faktycznie wychodzi płasko, może być w
            // INNYM widoku niż długość tego samego końca - to jest zamierzone,
            // nie błąd).
            var flatCandidates = new List<Candidate>();
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view)) continue;
                // Ograniczenie do widoku źródłowego dotyczy TYLKO długości -
                // dla szerokości szukamy po całym rysunku (patrz komentarz
                // wyżej). Po Origin, nie ReferenceEquals/Name: View.Name bywa
                // puste (zmierzone 2026-09-25), a uchwyty widoków mogą się
                // różnić instancją między wywołaniami.
                if (longest && referenceView != null && Distance(view.Origin, referenceView.Origin) > NumericalZero) continue;
                var parts = view.GetAllObjects(new[] { typeof(Part) });
                while (parts.MoveNext())
                {
                    if (!(parts.Current is Part drawingPart)) continue;
                    if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;
                    // ZMIERZONE na [3.5013] (drugie złącze, poza blokadą
                    // pilota): jedna bryła może mieć WIĘCEJ NIŻ JEDNĄ
                    // kwalifikującą się ścianę cięcia (dwa różne końce tego
                    // samego kawałka). Wcześniej ta funkcja cicho brała tylko
                    // ścianę o największym LoopSpan w całej bryle - to była
                    // niezmierzona, zgadywana reguła. Teraz zbieramy
                    // wszystkie kandydatów z tej części i liczymy każdą - a
                    // istniejący wymóg "dokładnie 1 płaski kandydat w CAŁYM
                    // rysunku" niżej sam odrzuci sytuację, w której jest ich
                    // więcej, zamiast po cichu wybrać jedną.
                    foreach (var candidate in FindChordCandidates(view, modelPart, longest))
                    {
                        // To jedynie tolerancja błędu numerycznego projekcji,
                        // nie próg geometrii: zmierzone punkty pilota mają Z=0.
                        if (Math.Abs(candidate.Start.Z) <= NumericalZero
                            && Math.Abs(candidate.End.Z) <= NumericalZero)
                        {
                            flatCandidates.Add(candidate);
                        }
                    }
                }
            }

            Candidate width;
            if (flatCandidates.Count == 1)
            {
                width = flatCandidates[0];
            }
            else if (flatCandidates.Count > 1 && referencePoint != null)
            {
                // Reguła dopasowania ściana↔wymiar, zaprojektowana i
                // zweryfikowana ODCZYTOWO na [35021] i [3.5013] przez
                // --diag-notch-match 2026-09-23 (patrz AGENTS.md, sekcja
                // "Reguła dopasowania ściana↔wymiar"): StraightDimension i
                // punkty ściany po ToViewSpace żyją w tym samym lokalnym
                // układzie widoku, więc "najbliższy środek cięciwy do
                // środka usuwanego wymiaru" jednoznacznie rozdzielił oba
                // końce [3.5013] (~5775 mm od siebie vs 15-18 mm do
                // właściwej ściany). Loguje wybór jawnie - nie cicho, jak
                // ostrzega komentarz przy zbieraniu kandydatów wyżej.
                width = flatCandidates[0];
                double bestDistance = Distance(referencePoint, Midpoint(width));
                foreach (var candidate in flatCandidates)
                {
                    double distance = Distance(referencePoint, Midpoint(candidate));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        width = candidate;
                    }
                }
                log($"Znaleziono {flatCandidates.Count} płaskich kandydatów {label} - wybrano najbliższy punktowi referencyjnemu (odległość {bestDistance:F2} mm).");
            }
            else
            {
                log($"WSTRZYMANO: znaleziono {flatCandidates.Count} płaskich kandydatów {label}; wymagany jest dokładnie jeden (albo punkt referencyjny do wyboru najbliższego).");
                return false;
            }

            if (HasSameDimension(width.View, width.Start, width.End))
            {
                log("WSTRZYMANO: taki wymiar już istnieje w widoku.");
                return false;
            }

            var reference = FindReferenceDimension(width.View, width.Start, width.End);
            var referenceSet = reference?.GetDimensionSet() as StraightDimensionSet;
            if (referenceSet?.Attributes == null)
            {
                log("WSTRZYMANO: nie znaleziono istniejącego wymiaru jako wzorca stylu.");
                return false;
            }

            // ZMIERZONE 2026-09-24 na [3.5013], DWA błędne podejścia po kolei:
            // 1) kopiowanie UpDirection z przypadkowego istniejącego wymiaru
            //    dało wymiarowi długości (cięciwa UKOŚNA) rzut na zły
            //    kierunek - PUŁAPKA 2 (AGENTS.md): StraightDimension pokazuje
            //    RZUT na kierunek prostopadły do Up, nie surowy dystans.
            // 2) Poprawka "licz Up z kierunku samej cięciwy" dawała wymiar
            //    PO SKOSIE (rzut = pełna, prawdziwa długość cięciwy) - operator
            //    (2026-09-24, na żywo) to odrzucił: "wymiaru nie daje się po
            //    skosie, zawsze prostopadle lub równolegle do parta".
            // Poprawka: kierunek liczony z OSI BELKI (już mamy z
            // TSM.Beam.StartPoint/EndPoint, przeliczonej do układu widoku),
            // nie z cięciwy ani z cudzego wymiaru. Długość wcięcia = rzut na
            // kierunek RÓWNOLEGŁY do osi (linia wymiarowa biegnie wzdłuż
            // profilu); szerokość = rzut na kierunek PROSTOPADŁY (w poprzek).
            // Dla złącza pod DOKŁADNIE 45° oba rzuty tej samej przekątnej
            // cięciwy wychodzą sobie równe (operator to potwierdził na żywo:
            // "no jest 42 tak jak powinno być") - to nie błąd, taka jest
            // geometria tego konkretnego kąta, nie każdego złącza.
            if (width.AxisView == null)
            {
                log("WSTRZYMANO: nie znam kierunku osi belki (część nie jest TSM.Beam?) - nie da się policzyć wymiaru równoległego/prostopadłego bez zgadywania.");
                return false;
            }
            var axisView = width.AxisView;
            var perpView = new TSG.Vector(-axisView.Y, axisView.X, 0);
            var side = longest ? perpView : axisView;
            // Wartość, którą Tekla NAPRAWDĘ wyświetli - rzut (Start->End) na
            // kierunek POMIARU (prostopadły do side), NIE surowy dystans
            // (patrz PUŁAPKA 2 wyżej - log ma pokazywać to, co faktycznie
            // wyjdzie na rysunku, nie geometrię).
            var chord = new TSG.Vector(width.End.X - width.Start.X, width.End.Y - width.Start.Y, width.End.Z - width.Start.Z);
            var measureDir = longest ? axisView : perpView;
            double displayedValue = Math.Abs(chord.Dot(measureDir));

            if (dryRun)
            {
                log($"[dry-run] {label} wcięcia: wstawiłbym {displayedValue:F2} mm (rzut, nie surowy dystans cięciwy {Distance(width.Start, width.End):F2} mm), " +
                    $"Start=({width.Start.X:F2};{width.Start.Y:F2};{width.Start.Z:F2}) " +
                    $"End=({width.End.X:F2};{width.End.Y:F2};{width.End.Z:F2}) {ViewTag(width.View)} - styl wzięty z istniejącego wymiaru w widoku. Nic nie zmieniono.");
                return true;
            }

            var dimension = new StraightDimension(
                width.View, width.Start, width.End, side, reference.Distance, referenceSet.Attributes);
            if (!dimension.Insert())
            {
                log("StraightDimension.Insert() zwrócił false.");
                return false;
            }
            if (!drawing.CommitChanges("Wymiar wcięcia"))
            {
                log("Insert() się powiódł, ale CommitChanges() zwrócił false.");
                return false;
            }

            // ZMIERZONE 2026-09-24 na [3.5013]: Insert()+CommitChanges() oba
            // zwróciły true, ale niezależny odczyt (nowy proces,
            // --diag-dimension-style) NIE znalazł wstawionego wymiaru -
            // fałszywy "sukces" w logu. Nie ufać samym wartościom zwrotnym -
            // odczytać widok jeszcze raz i sprawdzić, czy wymiar NAPRAWDĘ
            // tam jest, zanim log powie "wstawiono".
            if (!HasSameDimension(width.View, width.Start, width.End))
            {
                log($"Insert()/CommitChanges() zwróciły true, ale ponowny odczyt widoku NIE znalazł wstawionego wymiaru {label} - nic się NIE utrwaliło. Nie ufaj temu insertowi.");
                return false;
            }

            log($"Wstawiono {label} wcięcia {displayedValue:F2} mm, potwierdzone ponownym odczytem widoku.");
            return true;
        }

        // ZMIERZONE 2026-09-25 na żywym [3.5027]: skasowanie starego wymiaru
        // do osi w jednym widoku pociągnęło za sobą (kaskadowo, przez
        // wspólny StraightDimensionSet - patrz RoAxisDimensionService,
        // DescribeDimensionSet) już wstawiony wymiar wcięcia w DRUGIM
        // widoku, mimo że kod nigdy nie prosił o usunięcie tego drugiego
        // obiektu. Insert/InsertWidth/InsertLength (wyżej) wymagają
        // referencePoint pochodzącego z ISTNIEJĄCEGO wymiaru do osi - jeśli
        // ten zniknie (kaskadowo albo przez normalne "Usuń wymiary do osi"),
        // nie ma już jak wywołać insertu dla tego złącza. Ta metoda omija
        // ten problem: szuka kwalifikujących się ścian cięcia BEZPOŚREDNIO
        // w geometrii bryły (nie przez wymiar do osi) i wstawia brakujące
        // wymiary wcięcia tam, gdzie ich jeszcze nie ma - niezależnie od
        // tego, czy oryginalny wymiar do osi wciąż istnieje.
        public static int InsertMissing(Drawing drawing, Action<string> log, bool dryRun = false)
        {
            int insertedCount = 0;
            void CountingLog(string s) { log(s); if (s.StartsWith("Wstawiono", StringComparison.Ordinal) || s.StartsWith("Rozciągnięto", StringComparison.Ordinal)) insertedCount++; }

            // Ten sam zakres co RemoveAxisDimensions: reguła sprawdzona tylko na
            // rysunkach pojedynczej części. Na zespole InsertMissing przeszedłby
            // po WSZYSTKICH częściach i wstawił wymiar wcięcia każdej skośnej
            // rury - czy biuro tego chce na zespołach, nikt nie potwierdził.
            if (!(drawing is SinglePartDrawing))
            {
                log($"Rysunek typu {drawing.GetType().Name} - wymiar wcięcia wstawiam tylko na rysunkach pojedynczej części (SinglePartDrawing), nic nie wstawiam.");
                return 0;
            }

            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                log("WSTRZYMANO: brak połączenia z Teklą (Model).");
                return 0;
            }

            var views = new List<View>();
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (top.Current is View v) views.Add(v);
            }

            // Ta sama część bywa wypisana w KAŻDYM widoku, w którym jest
            // narysowana - przetwarzamy jej ściany tylko raz (po
            // ModelIdentifier), inaczej wstawialibyśmy/sprawdzali to samo
            // wielokrotnie.
            var processedParts = new HashSet<string>();
            foreach (var view in views)
            {
                var parts = view.GetAllObjects(new[] { typeof(Part) });
                while (parts.MoveNext())
                {
                    if (!(parts.Current is Part drawingPart)) continue;
                    string key = drawingPart.ModelIdentifier.ToString();
                    if (!processedParts.Add(key)) continue;
                    if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;

                    var axis = BeamAxis(modelPart);
                    foreach (var (majorChord, minorChord) in FindQualifyingChordPairs(modelPart, axis))
                    {
                        InsertResolvedIfMissing(views, drawing, majorChord, longest: true, label: "długości", axis, CountingLog, dryRun);
                        InsertResolvedIfMissing(views, drawing, minorChord, longest: false, label: "szerokości", axis, CountingLog, dryRun);
                        StretchRadiusToDiameter(views, drawing, majorChord, axis, CountingLog, dryRun);
                    }
                }
            }
            return insertedCount;
        }

        // Ściany cięcia + ich obie cięciwy (długość, szerokość) w
        // WSPÓŁRZĘDNYCH MODELU (nie widoku) - ten sam filtr kąta co
        // FindChordCandidates, ale bez zależności od konkretnego widoku,
        // żeby dało się to policzyć raz na część, nie raz na widok.
        private static IEnumerable<((TSG.Point A, TSG.Point B) Major, (TSG.Point A, TSG.Point B) Minor)> FindQualifyingChordPairs(TSM.Part part, TSG.Vector axis)
        {
            foreach (var (_, outerLoop) in CutFaces(part.GetSolid(), axis))
            {
                var centroid = Centroid(outerLoop);
                var majorChord = FindChord(outerLoop, centroid, longest: true);
                var minorChord = FindChord(outerLoop, centroid, longest: false);
                if (CutAngleDegrees(majorChord, minorChord) < MinCutAngleDegrees) continue;

                yield return (majorChord, minorChord);
            }
        }

        // Ściany cięcia bryły razem z ich zewnętrzną pętlą, BEZ filtra kąta
        // (diagnostyka chce widzieć też te odrzucone). Profil RO jest pusty w
        // środku, więc ściana cięcia to PIERŚCIEŃ: >1 pętla, a zewnętrzny
        // obrys to pętla o większym rozstawie - porównanie lokalne do jednej
        // ściany, nie do całej bryły. Wierzchołków różnych pętli nie wolno
        // zlewać w jedną listę (dawało bezsensowne cięciwy - patrz AGENTS.md).
        // Normalna ~równoległa do osi belki = proste przycięcie, nie skos.
        internal static IEnumerable<(Face Face, List<TSG.Point> OuterLoop)> CutFaces(TSM.Solid solid, TSG.Vector axis)
        {
            var faces = solid.GetFaceEnumerator();
            while (faces.MoveNext())
            {
                if (!(faces.Current is Face face)) continue;
                var loops = new List<List<TSG.Point>>();
                var enumerator = face.GetLoopEnumerator();
                while (enumerator.MoveNext())
                {
                    if (!(enumerator.Current is Loop loop)) continue;
                    var points = new List<TSG.Point>();
                    var vertices = loop.GetVertexEnumerator();
                    while (vertices.MoveNext()) if (vertices.Current is TSG.Point p) points.Add(p);
                    if (points.Count > 4) loops.Add(points);
                }
                if (loops.Count < 2) continue;
                if (axis != null && Math.Abs(new TSG.Vector(face.Normal).GetNormal().Dot(axis)) > 0.999) continue;

                List<TSG.Point> outerLoop = null;
                foreach (var loop in loops)
                    if (outerLoop == null || LoopSpan(loop) > LoopSpan(outerLoop)) outerLoop = loop;
                yield return (face, outerLoop);
            }
        }

        // Kąt cięcia z proporcji cięciw: długość = szerokość / cos(kąt).
        internal static double CutAngleDegrees((TSG.Point A, TSG.Point B) major, (TSG.Point A, TSG.Point B) minor)
        {
            double ratio = Distance(minor.A, minor.B) / Distance(major.A, major.B);
            return Math.Acos(Math.Min(1.0, ratio)) * 180.0 / Math.PI;
        }

        internal static TSG.Vector BeamAxis(TSM.Part part)
        {
            if (!(part is TSM.Beam beam)) return null;
            var delta = beam.EndPoint - beam.StartPoint;
            return new TSG.Vector(delta.X, delta.Y, delta.Z).GetNormal();
        }

        // Dla cięciwy w WSPÓŁRZĘDNYCH MODELU: szuka WŚRÓD WSZYSTKICH widoków
        // rysunku tego jednego, w którym wychodzi płasko (Z≈0 po
        // ToViewSpace) - zmierzone 2026-09-25: dana cięciwa danej ściany
        // wychodzi płasko dokładnie w jednym widoku, nie w obu i nie w
        // żadnym akurat na tym złączu, ale nie zakładamy tego na sztywno -
        // więcej niż jeden płaski widok = niejednoznaczne, WSTRZYMAJ się
        // zamiast zgadywać który wybrać.
        private static void InsertResolvedIfMissing(List<View> views, Drawing drawing, (TSG.Point A, TSG.Point B) chordModel, bool longest, string label, TSG.Vector axisModel, Action<string> log, bool dryRun)
        {
            View flatView = null;
            TSG.Point start = null, end = null;
            foreach (var view in views)
            {
                var cs = view.DisplayCoordinateSystem;
                var s = ToViewSpace(chordModel.A, cs);
                var e = ToViewSpace(chordModel.B, cs);
                if (Math.Abs(s.Z) > NumericalZero || Math.Abs(e.Z) > NumericalZero) continue;
                if (flatView != null)
                {
                    log($"WSTRZYMANO {label}: ta cięciwa wychodzi płasko w więcej niż jednym widoku - niejednoznaczne, nie zgaduję którego użyć.");
                    return;
                }
                flatView = view;
                start = s;
                end = e;
            }
            if (flatView == null)
            {
                // Zmierzone jako możliwe (patrz AGENTS.md, "Problem głębi
                // (Z)"), nie błąd - ta cięciwa po prostu nie wychodzi płasko
                // w żadnym z widoków na tym arkuszu.
                return;
            }

            if (axisModel == null)
            {
                log($"WSTRZYMANO {label}: nie znam kierunku osi belki (część nie jest TSM.Beam?) - nie da się policzyć wymiaru równoległego/prostopadłego bez zgadywania.");
                return;
            }
            var axisView = ToViewSpaceVector(axisModel, flatView.DisplayCoordinateSystem);
            var perpView = new TSG.Vector(-axisView.Y, axisView.X, 0);
            var side = longest ? perpView : axisView;

            if (HasSameDimension(flatView, start, end, side))
            {
                return; // już jest - nic do zrobienia, to normalny, częsty przypadek
            }

            var reference = FindReferenceDimension(flatView, start, end);
            // Zmierzone 2026-09-29 na [35020]: "Usuń" skasował oba wymiary w
            // widoku (8 i 21 do osi) i "Wstaw" nie miał tam wzorca stylu -
            // koniec 19,9° został bez opisu. Operator wybrał: styl z innego
            // widoku tego samego rysunku, żeby wymiar wcięcia zastąpił
            // skasowane (dokładnie to, co człowiek zrobiłby rozciągając je).
            foreach (var other in views)
            {
                if (reference != null) break;
                if (!ReferenceEquals(other, flatView)) reference = FindReferenceDimension(other, start, end);
            }
            var referenceSet = reference?.GetDimensionSet() as StraightDimensionSet;
            if (referenceSet?.Attributes == null)
            {
                log($"WSTRZYMANO {label}: nie znaleziono istniejącego wymiaru jako wzorca stylu na tym rysunku.");
                return;
            }

            var chordVector = new TSG.Vector(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            var measureDir = longest ? axisView : perpView;
            double displayedValue = Math.Abs(chordVector.Dot(measureDir));

            if (dryRun)
            {
                log($"[dry-run] brakująca {label} wcięcia: wstawiłbym {displayedValue:F2} mm, Start=({start.X:F2};{start.Y:F2};{start.Z:F2}) End=({end.X:F2};{end.Y:F2};{end.Z:F2}) {ViewTag(flatView)}. Nic nie zmieniono.");
                return;
            }

            var dimension = new StraightDimension(flatView, start, end, side, reference.Distance, referenceSet.Attributes);
            if (!dimension.Insert())
            {
                log($"{label}: StraightDimension.Insert() zwrócił false.");
                return;
            }
            if (!drawing.CommitChanges("Wymiar wcięcia"))
            {
                log($"{label}: Insert() się powiódł, ale CommitChanges() zwrócił false.");
                return;
            }
            if (!HasSameDimension(flatView, start, end, side))
            {
                log($"{label}: Insert()/CommitChanges() zwróciły true, ale ponowny odczyt widoku NIE znalazł wstawionego wymiaru - nic się NIE utrwaliło. Nie ufaj temu insertowi.");
                return;
            }
            log($"Wstawiono brakującą {label} wcięcia {displayedValue:F2} mm, potwierdzone ponownym odczytem widoku.");
        }

        // Tolerancja "ten sam punkt" dla końca istniejącego wymiaru vs końca
        // cięciwy z bryły, mm modelu w układzie widoku. Zmierzone 2026-09-29
        // na [35095]: koniec wymiaru promienia i koniec cięciwy długości
        // zgadzają się co do setnej (89,65;21,20;0,00) - 0,01 mm to margines
        // na zaokrąglenia, nie na "mniej więcej ten sam punkt".
        private const double SamePointToleranceMm = 0.01;

        // Operator (2026-09-29, [35095]): płaski wymiar promienia rury przy
        // skośnym końcu (21 mm, od czubka cięcia do osi) ma sięgać od czubka
        // do czubka cięcia - pełna średnica, 42 mm. Zmierzone: jeden koniec
        // tego wymiaru leży DOKŁADNIE na końcu płaskiej cięciwy długości
        // cięcia, drugi na osi rury. Rozciągamy tylko taki wymiar: przesuwamy
        // koniec z osi na DRUGI koniec tej samej cięciwy. Wymiar, który tylko
        // przypadkiem ma ~promień, ale nie startuje z czubka cięcia, zostaje.
        private static void StretchRadiusToDiameter(List<View> views, Drawing drawing, (TSG.Point A, TSG.Point B) chordModel, TSG.Vector axisModel, Action<string> log, bool dryRun)
        {
            if (axisModel == null) return;
            foreach (var view in views)
            {
                var cs = view.DisplayCoordinateSystem;
                var a = ToViewSpace(chordModel.A, cs);
                var b = ToViewSpace(chordModel.B, cs);
                if (Math.Abs(a.Z) > NumericalZero || Math.Abs(b.Z) > NumericalZero) continue;

                var axisView = ToViewSpaceVector(axisModel, cs);
                var perpView = new TSG.Vector(-axisView.Y, axisView.X, 0);
                double diameter = Math.Abs(new TSG.Vector(b.X - a.X, b.Y - a.Y, b.Z - a.Z).Dot(perpView));

                // Najpierw lista, potem Insert()/Delete() - bez zmieniania
                // rysunku w trakcie przechodzenia jego enumeratorem.
                var dimensions = new List<StraightDimension>();
                var objects = view.GetAllObjects(new[] { typeof(StraightDimension) });
                while (objects.MoveNext()) if (objects.Current is StraightDimension sd) dimensions.Add(sd);

                foreach (var dimension in dimensions)
                {
                    var start = dimension.StartPoint;
                    var end = dimension.EndPoint;
                    if (Math.Abs(start.Z) > NumericalZero || Math.Abs(end.Z) > NumericalZero) continue;

                    // Który koniec wymiaru to czubek cięcia i który czubek (A czy B).
                    bool startOnTip = Distance(start, a) <= SamePointToleranceMm || Distance(start, b) <= SamePointToleranceMm;
                    bool endOnTip = Distance(end, a) <= SamePointToleranceMm || Distance(end, b) <= SamePointToleranceMm;
                    if (startOnTip == endOnTip) continue; // żaden koniec albo oba (to już jest średnica)
                    var tip = startOnTip ? start : end;
                    var other = startOnTip ? end : start;
                    var oppositeTip = Distance(tip, a) <= SamePointToleranceMm ? b : a;

                    // Drugi koniec musi leżeć na osi rury: w połowie średnicy od
                    // czubka, mierząc prostopadle do osi.
                    double offset = Math.Abs(new TSG.Vector(other.X - tip.X, other.Y - tip.Y, 0).Dot(perpView));
                    if (Math.Abs(offset - diameter / 2) > RoAxisDimensionService.AxisToleranceMm) continue;

                    // Sam warunek "czubek + oś" łapał też wymiar CAŁKOWITEJ
                    // długości rury - zmierzone 2026-09-29 na [35010]: 2677 mm
                    // od (0;0) (punkt referencyjny, na osi) do czubka cięcia
                    // (2677,2;-21,2). v0.3.2 zastąpiłaby go wymiarem 42 mm.
                    // Promień: mierzy W POPRZEK rury (Up wzdłuż osi - wszystkie
                    // zmierzone promienie na [35095]/[35021]/[35010]), a jego
                    // koniec na osi leży wzdłuż osi w obrębie cięcia. 2677 nie
                    // spełnia żadnego z tych dwóch warunków.
                    var up = new TSG.Vector(dimension.UpDirection).GetNormal();
                    if (Math.Abs(up.Dot(axisView.GetNormal())) < 0.99) continue;
                    double tA = new TSG.Vector(a.X, a.Y, 0).Dot(axisView), tB = new TSG.Vector(b.X, b.Y, 0).Dot(axisView);
                    double tOther = new TSG.Vector(other.X, other.Y, 0).Dot(axisView);
                    if (tOther < Math.Min(tA, tB) - SamePointToleranceMm || tOther > Math.Max(tA, tB) + SamePointToleranceMm) continue;

                    if (dryRun)
                    {
                        log($"[dry-run] rozciągnąłbym wymiar promienia {offset:F2} mm do średnicy {diameter:F2} mm: koniec ({other.X:F2};{other.Y:F2}) -> ({oppositeTip.X:F2};{oppositeTip.Y:F2}) {ViewTag(view)}. Nic nie zmieniono.");
                        continue;
                    }

                    if (HasSameDimension(view, tip, oppositeTip, axisView))
                    {
                        continue; // średnica już jest - nie dublujemy
                    }

                    // Nie Modify() z nowym punktem: zmierzone 2026-09-29 na
                    // [35095] - Modify()+CommitChanges() zwróciły true, odczyt w
                    // tym samym procesie pokazał nowy punkt, a niezależny odczyt
                    // (--diag-dimension-style) stary. Tekla zmiany punktu nie
                    // zastosowała. Zamiast tego: nowy wymiar w stylu, kierunku i
                    // odsunięciu starego, a stary kasujemy dopiero po udanym
                    // wstawieniu - przy porażce nic nie ginie.
                    var set = dimension.GetDimensionSet() as StraightDimensionSet;
                    if (set?.Attributes == null)
                    {
                        log("Rozciąganie promienia: WSTRZYMANO - nie da się odczytać stylu istniejącego wymiaru.");
                        continue;
                    }
                    var diameterDimension = new StraightDimension(view, tip, oppositeTip, dimension.UpDirection, dimension.Distance, set.Attributes);
                    if (!diameterDimension.Insert())
                    {
                        log("Rozciąganie promienia: StraightDimension.Insert() zwrócił false, promień zostaje bez zmian.");
                        continue;
                    }
                    if (!dimension.Delete())
                    {
                        log("Rozciąganie promienia: wstawiono średnicę, ale Delete() starego promienia zwrócił false - na rysunku są oba, usuń promień ręcznie.");
                    }
                    if (!drawing.CommitChanges("Rozciągnięcie promienia do średnicy"))
                    {
                        log("Rozciąganie promienia: CommitChanges() zwrócił false.");
                        continue;
                    }
                    if (!HasSameDimension(view, tip, oppositeTip, axisView))
                    {
                        log("Rozciąganie promienia: ponowny odczyt widoku NIE znalazł średnicy - nic się NIE utrwaliło.");
                        continue;
                    }
                    log($"Rozciągnięto wymiar promienia {offset:F2} mm do średnicy {diameter:F2} mm (nowy wymiar w miejsce starego), potwierdzone ponownym odczytem widoku.");
                }
            }
        }

        private static List<Candidate> FindChordCandidates(View view, TSM.Part part, bool longest)
        {
            var result = new List<Candidate>();
            var cs = view.DisplayCoordinateSystem;
            var axis = BeamAxis(part);
            var axisView = axis == null ? null : ToViewSpaceVector(axis, cs);
            foreach (var (major, minor) in FindQualifyingChordPairs(part, axis))
            {
                var pair = longest ? major : minor;
                result.Add(new Candidate { View = view, Start = ToViewSpace(pair.A, cs), End = ToViewSpace(pair.B, cs), AxisView = axisView });
            }
            return result;
        }

        // up != null: liczy się też kierunek pomiaru. Rozciągnięty promień
        // (średnica, Up wzdłuż osi) i długość wcięcia (Up prostopadle) mają
        // na [35095] DOKŁADNIE te same końce - bez porównania kierunku jeden
        // udawałby drugi i brakujący wymiar nigdy by się nie wstawił.
        private static bool HasSameDimension(View view, TSG.Point start, TSG.Point end, TSG.Vector up = null)
        {
            var objects = view.GetAllObjects(new[] { typeof(StraightDimension) });
            while (objects.MoveNext())
            {
                if (!(objects.Current is StraightDimension dimension) || !SameEnds(dimension, start, end)) continue;
                if (up == null) return true;
                var dimUp = new TSG.Vector(dimension.UpDirection).GetNormal();
                if (Math.Abs(dimUp.Dot(new TSG.Vector(up).GetNormal())) > 0.99) return true;
            }
            return false;
        }

        private static StraightDimension FindReferenceDimension(View view, TSG.Point start, TSG.Point end)
        {
            var objects = view.GetAllObjects(new[] { typeof(StraightDimension) });
            while (objects.MoveNext())
            {
                if (objects.Current is StraightDimension dimension && !SameEnds(dimension, start, end)) return dimension;
            }
            return null;
        }

        private static bool SameEnds(StraightDimension dimension, TSG.Point start, TSG.Point end)
        {
            bool sameOrder = Distance(dimension.StartPoint, start) <= NumericalZero && Distance(dimension.EndPoint, end) <= NumericalZero;
            bool reverseOrder = Distance(dimension.StartPoint, end) <= NumericalZero && Distance(dimension.EndPoint, start) <= NumericalZero;
            return sameOrder || reverseOrder;
        }

        private sealed class Candidate { public View View; public TSG.Point Start; public TSG.Point End; public TSG.Vector AxisView; }

        internal static double LoopSpan(List<TSG.Point> loop)
        {
            double max = 0;
            for (int i = 0; i < loop.Count; i++) for (int j = i + 1; j < loop.Count; j++) max = Math.Max(max, Distance(loop[i], loop[j]));
            return max;
        }

        internal static TSG.Point Centroid(List<TSG.Point> loop)
        {
            double x = 0, y = 0, z = 0;
            foreach (var point in loop) { x += point.X; y += point.Y; z += point.Z; }
            return new TSG.Point(x / loop.Count, y / loop.Count, z / loop.Count);
        }

        // Cięciwa = para wierzchołków, której środek leży najbliżej centroidu
        // pętli (odporne na kolejność wierzchołków); wśród nich najdłuższa
        // (długość cięcia) albo najkrótsza (szerokość). Tolerancja 5%
        // rozpiętości, bo przy dyskretnej elipsie offset rzadko jest zerem.
        internal static (TSG.Point A, TSG.Point B) FindChord(List<TSG.Point> loop, TSG.Point centroid, bool longest)
        {
            double minimumOffset = double.MaxValue;
            var pairs = new List<(TSG.Point A, TSG.Point B, double Offset)>();
            for (int i = 0; i < loop.Count; i++) for (int j = i + 1; j < loop.Count; j++)
            {
                var middle = new TSG.Point((loop[i].X + loop[j].X) / 2, (loop[i].Y + loop[j].Y) / 2, (loop[i].Z + loop[j].Z) / 2);
                double offset = Distance(middle, centroid);
                pairs.Add((loop[i], loop[j], offset));
                minimumOffset = Math.Min(minimumOffset, offset);
            }
            double tolerance = Math.Max(0.5, LoopSpan(loop) * 0.05);
            var throughCenter = pairs.FindAll(pair => pair.Offset <= minimumOffset + tolerance);
            throughCenter.Sort((a, b) => Distance(a.A, a.B).CompareTo(Distance(b.A, b.B)));
            var pick = longest ? throughCenter[throughCenter.Count - 1] : throughCenter[0];
            return (pick.A, pick.B);
        }

        // ZMIERZONE 2026-09-25: bez widoku w logu dry-run nie da się
        // zauważyć, że dwa wymiary trafiłyby do różnych (albo tego samego)
        // widoku - View.Name bywa puste, Origin odróżnia widoki arkusza.
        private static string ViewTag(View view) => $"widok Origin=({view.Origin.X:F2};{view.Origin.Y:F2})";

        private static TSG.Point Midpoint(Candidate c) =>
            new TSG.Point((c.Start.X + c.End.X) / 2, (c.Start.Y + c.End.Y) / 2, (c.Start.Z + c.End.Z) / 2);

        internal static double Distance(TSG.Point a, TSG.Point b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        // Ręczny rzut na osie układu widoku - CoordinateSystem w Open API nie
        // ma metody Transform, a dokumentacja DisplayCoordinateSystem mówi
        // wprost, że służy do przeliczania punktów modelu do widoku.
        internal static TSG.Point ToViewSpace(TSG.Point point, TSG.CoordinateSystem cs)
        {
            var relative = point - cs.Origin;
            var v = ToViewSpaceVector(new TSG.Vector(relative.X, relative.Y, relative.Z), cs);
            return new TSG.Point(v.X, v.Y, v.Z);
        }

        // Jak ToViewSpace, ale dla KIERUNKU (wektora), nie punktu - bez
        // odejmowania Origin (kierunek nie ma pozycji). Używane do
        // przeliczenia osi belki (z modelu) na układ widoku, żeby wymiar
        // wcięcia dało się narysować równolegle/prostopadle do PRAWDZIWEJ
        // osi profilu, nie do przypadkowego kierunku.
        private static TSG.Vector ToViewSpaceVector(TSG.Vector v, TSG.CoordinateSystem cs)
        {
            var x = new TSG.Vector(cs.AxisX); x.Normalize();
            var y = new TSG.Vector(cs.AxisY); y.Normalize();
            var z = TSG.Vector.Cross(x, y);
            return new TSG.Vector(v.Dot(x), v.Dot(y), v.Dot(z));
        }
    }
}
