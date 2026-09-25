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
        private const double NumericalZero = 0.000001;

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

            var candidates = flatCandidates;

            Candidate width;
            if (candidates.Count == 1)
            {
                width = candidates[0];
            }
            else if (candidates.Count > 1 && referencePoint != null)
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
                width = candidates[0];
                double bestDistance = Distance(referencePoint, Midpoint(width));
                foreach (var candidate in candidates)
                {
                    double distance = Distance(referencePoint, Midpoint(candidate));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        width = candidate;
                    }
                }
                log($"Znaleziono {candidates.Count} płaskich kandydatów {label} - wybrano najbliższy punktowi referencyjnemu (odległość {bestDistance:F2} mm).");
            }
            else
            {
                log($"WSTRZYMANO: znaleziono {candidates.Count} płaskich kandydatów {label}; wymagany jest dokładnie jeden (albo punkt referencyjny do wyboru najbliższego).");
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
                    $"End=({width.End.X:F2};{width.End.Y:F2};{width.End.Z:F2}) - styl wzięty z istniejącego wymiaru w widoku. Nic nie zmieniono.");
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
            void CountingLog(string s) { log(s); if (s.StartsWith("Wstawiono", StringComparison.Ordinal)) insertedCount++; }

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

                    TSG.Vector axis = null;
                    if (modelPart is TSM.Beam beam)
                    {
                        var delta = beam.EndPoint - beam.StartPoint;
                        axis = new TSG.Vector(delta.X, delta.Y, delta.Z).GetNormal();
                    }

                    foreach (var (majorChord, minorChord) in FindQualifyingChordPairs(modelPart, axis))
                    {
                        InsertResolvedIfMissing(views, drawing, majorChord, longest: true, label: "długości", axis, CountingLog, dryRun);
                        InsertResolvedIfMissing(views, drawing, minorChord, longest: false, label: "szerokości", axis, CountingLog, dryRun);
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
            var faces = part.GetSolid().GetFaceEnumerator();
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

                List<TSG.Point> faceOuterLoop = null;
                foreach (var loop in loops)
                    if (faceOuterLoop == null || LoopSpan(loop) > LoopSpan(faceOuterLoop)) faceOuterLoop = loop;

                // Ten sam próg i ten sam powód co w FindChordCandidates -
                // patrz komentarz tam (PUŁAPKA [3.5027], ~5° pomijamy).
                const double MinCutAngleDegrees = 10.0;
                var centroid = Centroid(faceOuterLoop);
                var majorChord = FindChord(faceOuterLoop, centroid, longest: true);
                var minorChord = FindChord(faceOuterLoop, centroid, longest: false);
                double majorLength = Distance(majorChord.A, majorChord.B);
                double minorLength = Distance(minorChord.A, minorChord.B);
                double cutAngleDegrees = Math.Acos(Math.Min(1.0, minorLength / majorLength)) * 180.0 / Math.PI;
                if (cutAngleDegrees < MinCutAngleDegrees) continue;

                yield return (majorChord, minorChord);
            }
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
                // Zmierzone jako możliwe (patrz komentarz w InsertTest
                // przy "Problem głębi (Z)"), nie traktować jako błąd - po
                // prostu ta konkretna cięciwa nie da się narysować płasko
                // w żadnym z widoków na tym arkuszu.
                return;
            }

            if (HasSameDimension(flatView, start, end))
            {
                return; // już jest - nic do zrobienia, to normalny, częsty przypadek
            }

            if (axisModel == null)
            {
                log($"WSTRZYMANO {label}: nie znam kierunku osi belki (część nie jest TSM.Beam?) - nie da się policzyć wymiaru równoległego/prostopadłego bez zgadywania.");
                return;
            }
            var axisView = ToViewSpaceVector(axisModel, flatView.DisplayCoordinateSystem);

            var reference = FindReferenceDimension(flatView, start, end);
            var referenceSet = reference?.GetDimensionSet() as StraightDimensionSet;
            if (referenceSet?.Attributes == null)
            {
                log($"WSTRZYMANO {label}: nie znaleziono istniejącego wymiaru jako wzorca stylu w tym widoku.");
                return;
            }

            var perpView = new TSG.Vector(-axisView.Y, axisView.X, 0);
            var side = longest ? perpView : axisView;
            var chordVector = new TSG.Vector(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            var measureDir = longest ? axisView : perpView;
            double displayedValue = Math.Abs(chordVector.Dot(measureDir));

            if (dryRun)
            {
                log($"[dry-run] brakująca {label} wcięcia: wstawiłbym {displayedValue:F2} mm, Start=({start.X:F2};{start.Y:F2};{start.Z:F2}) End=({end.X:F2};{end.Y:F2};{end.Z:F2}). Nic nie zmieniono.");
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
            if (!HasSameDimension(flatView, start, end))
            {
                log($"{label}: Insert()/CommitChanges() zwróciły true, ale ponowny odczyt widoku NIE znalazł wstawionego wymiaru - nic się NIE utrwaliło. Nie ufaj temu insertowi.");
                return;
            }
            log($"Wstawiono brakującą {label} wcięcia {displayedValue:F2} mm, potwierdzone ponownym odczytem widoku.");
        }

        private static List<Candidate> FindChordCandidates(View view, TSM.Part part, bool longest)
        {
            var result = new List<Candidate>();
            TSG.Vector axis = null;
            if (part is TSM.Beam beam)
            {
                var delta = beam.EndPoint - beam.StartPoint;
                axis = new TSG.Vector(delta.X, delta.Y, delta.Z).GetNormal();
            }
            TSG.Vector axisView = axis == null ? null : ToViewSpaceVector(axis, view.DisplayCoordinateSystem);

            var faces = part.GetSolid().GetFaceEnumerator();
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

                // Zewnętrzny obrys TEJ JEDNEJ ściany = pętla o większym
                // rozstawie - porównanie zostaje lokalne do ściany, nie do
                // całej bryły (patrz komentarz w InsertTest).
                List<TSG.Point> faceOuterLoop = null;
                foreach (var loop in loops)
                    if (faceOuterLoop == null || LoopSpan(loop) > LoopSpan(faceOuterLoop)) faceOuterLoop = loop;

                // ZMIERZONE 2026-09-25 na żywym [3.5027]: ta sama bryła może
                // mieć ścianę cięcia pod PRAWDZIWYM, widocznym kątem (koniec
                // "ścięty" - operator to potwierdził wizualnie) i drugą,
                // gdzie kąt jest tak mały, że koniec wygląda jak zwykłe
                // płaskie zakończenie rury (operator: "z jednej strony
                // płaskie") - mimo że OBIE geometrycznie kwalifikują się
                // jako "ściana cięcia" (>1 pętla, normalna niedokładnie
                // równoległa do osi). Odróżnia je stosunek długość/szerokość
                // cięcia (= 1/cos kąta cięcia): zmierzone punkty danych -
                // ~5° (koniec płaski, [3.5027]) pomijamy, ~19,9° ([35021]) i
                // ~45° ([3.5013]/[3.5027] drugi koniec) - wstawiamy. Próg
                // 10° to SZACUNEK (mniej więcej w połowie między 5° a 19,9°
                // na tych trzech punktach), nie pomiar - do doprecyzowania,
                // gdy pojawią się kolejne złącza bliżej granicy. NIE
                // rozwiązuje osobnego, wciąż otwartego problemu z 24.09
                // ([3.5013] drugi koniec miał TEN SAM ~45° kąt co pierwszy,
                // a mimo to operator go odrzucił z innego, nieznanego
                // powodu) - to tylko odsiewa przypadki, gdzie kąt sam w
                // sobie jest pomijalny.
                const double MinCutAngleDegrees = 10.0;
                var centroid = Centroid(faceOuterLoop);
                var majorChord = FindChord(faceOuterLoop, centroid, longest: true);
                var minorChord = FindChord(faceOuterLoop, centroid, longest: false);
                double majorLength = Distance(majorChord.A, majorChord.B);
                double minorLength = Distance(minorChord.A, minorChord.B);
                double cutAngleDegrees = Math.Acos(Math.Min(1.0, minorLength / majorLength)) * 180.0 / Math.PI;
                if (cutAngleDegrees < MinCutAngleDegrees) continue;

                var pair = longest ? majorChord : minorChord;
                var cs = view.DisplayCoordinateSystem;
                result.Add(new Candidate { View = view, Start = ToViewSpace(pair.A, cs), End = ToViewSpace(pair.B, cs), AxisView = axisView });
            }
            return result;
        }

        private static bool HasSameDimension(View view, TSG.Point start, TSG.Point end)
        {
            var objects = view.GetAllObjects(new[] { typeof(StraightDimension) });
            while (objects.MoveNext())
            {
                if (!(objects.Current is StraightDimension dimension)) continue;
                bool sameOrder = Distance(dimension.StartPoint, start) <= NumericalZero && Distance(dimension.EndPoint, end) <= NumericalZero;
                bool reverseOrder = Distance(dimension.StartPoint, end) <= NumericalZero && Distance(dimension.EndPoint, start) <= NumericalZero;
                if (sameOrder || reverseOrder) return true;
            }
            return false;
        }

        private static StraightDimension FindReferenceDimension(View view, TSG.Point start, TSG.Point end)
        {
            var objects = view.GetAllObjects(new[] { typeof(StraightDimension) });
            while (objects.MoveNext())
            {
                if (!(objects.Current is StraightDimension dimension)) continue;
                bool sameOrder = Distance(dimension.StartPoint, start) <= NumericalZero && Distance(dimension.EndPoint, end) <= NumericalZero;
                bool reverseOrder = Distance(dimension.StartPoint, end) <= NumericalZero && Distance(dimension.EndPoint, start) <= NumericalZero;
                if (!sameOrder && !reverseOrder) return dimension;
            }
            return null;
        }

        private sealed class Candidate { public View View; public TSG.Point Start; public TSG.Point End; public TSG.Vector AxisView; }

        private static double LoopSpan(List<TSG.Point> loop)
        {
            double max = 0;
            for (int i = 0; i < loop.Count; i++) for (int j = i + 1; j < loop.Count; j++) max = Math.Max(max, Distance(loop[i], loop[j]));
            return max;
        }

        private static TSG.Point Centroid(List<TSG.Point> loop)
        {
            double x = 0, y = 0, z = 0;
            foreach (var point in loop) { x += point.X; y += point.Y; z += point.Z; }
            return new TSG.Point(x / loop.Count, y / loop.Count, z / loop.Count);
        }

        private static (TSG.Point A, TSG.Point B) FindChord(List<TSG.Point> loop, TSG.Point centroid, bool longest)
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

        private static TSG.Point Midpoint(Candidate c) =>
            new TSG.Point((c.Start.X + c.End.X) / 2, (c.Start.Y + c.End.Y) / 2, (c.Start.Z + c.End.Z) / 2);

        private static double Distance(TSG.Point a, TSG.Point b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static TSG.Point ToViewSpace(TSG.Point point, TSG.CoordinateSystem cs)
        {
            var relative = point - cs.Origin;
            var x = new TSG.Vector(cs.AxisX); x.Normalize();
            var y = new TSG.Vector(cs.AxisY); y.Normalize();
            var z = TSG.Vector.Cross(x, y);
            var vector = new TSG.Vector(relative.X, relative.Y, relative.Z);
            return new TSG.Point(vector.Dot(x), vector.Dot(y), vector.Dot(z));
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
