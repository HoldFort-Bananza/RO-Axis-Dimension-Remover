using System;
using System.Collections.Generic;
using Tekla.Structures.Drawing;
using Tekla.Structures.Solid;
using TSM = Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

namespace RoAxisDimensionRemover
{
    // Jednorazowy, jawnie ograniczony pilot. Nie jest jeszcze regułą
    // produkcyjną: brama bezpieczeństwa dla tworzenia wymiaru trwa.
    internal static class NotchPilot
    {
        private const string PilotDrawingMark = "[35021]";

        // [3.5013] było tymczasowo dopuszczone do REALNEGO insertu
        // 2026-09-24, żeby przetestować regułę dopasowania ściana↔wymiar na
        // złączu z wieloma ścianami cięcia (patrz AGENTS.md, "Następne
        // kroki" pkt 2). Test wykrył NOWY, nierozwiązany problem domenowy
        // ("ma ścianę cięcia" ≠ "potrzebuje wymiaru wcięcia") i operator
        // zdecydował odłożyć temat - [3.5013] WYCOFANE z dopuszczenia,
        // zgodnie z tą decyzją. Przywrócić dopiero po rozwiązaniu tego
        // problemu, nie wcześniej.
        private static readonly string[] AllowedRealInsertMarks = { PilotDrawingMark };

        private const double NumericalZero = 0.000001;

        public static bool InsertWidthTest(Drawing drawing, Action<string> log, TSG.Point referencePoint = null, bool dryRun = false)
        {
            return InsertTest(drawing, log, longest: false, label: "szerokości", referencePoint, dryRun);
        }

        public static bool InsertLengthTest(Drawing drawing, Action<string> log, TSG.Point referencePoint = null, bool dryRun = false)
        {
            return InsertTest(drawing, log, longest: true, label: "długości", referencePoint, dryRun);
        }

        private static bool InsertTest(Drawing drawing, Action<string> log, bool longest, string label, TSG.Point referencePoint, bool dryRun)
        {
            // Blokada marki dotyczy tylko REALNEGO wstawienia - dry-run
            // nic nie modyfikuje, więc to bezpieczne do sprawdzenia reguły
            // dopasowania na innych złączach bez zdejmowania blokady.
            bool markAllowed = dryRun || Array.Exists(AllowedRealInsertMarks,
                m => string.Equals(drawing.Mark, m, StringComparison.OrdinalIgnoreCase));
            if (!markAllowed)
            {
                log("TEST WSTRZYMANY: pilot jest ograniczony do rysunków " + string.Join(", ", AllowedRealInsertMarks) + ".");
                return false;
            }

            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                log("TEST WSTRZYMANY: brak połączenia z Teklą (Model).");
                return false;
            }

            var candidates = new List<Candidate>();
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view)) continue;
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
                            candidates.Add(candidate);
                        }
                    }
                }
            }

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
                log($"TEST WSTRZYMANY: znaleziono {candidates.Count} płaskich kandydatów {label}; wymagany jest dokładnie jeden (albo punkt referencyjny do wyboru najbliższego).");
                return false;
            }

            if (HasSameDimension(width.View, width.Start, width.End))
            {
                log("TEST WSTRZYMANY: taki wymiar już istnieje w widoku.");
                return false;
            }

            var reference = FindReferenceDimension(width.View, width.Start, width.End);
            var referenceSet = reference?.GetDimensionSet() as StraightDimensionSet;
            if (referenceSet?.Attributes == null)
            {
                log("TEST WSTRZYMANY: nie znaleziono istniejącego wymiaru jako wzorca stylu.");
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
                log("TEST WSTRZYMANY: nie znam kierunku osi belki (część nie jest TSM.Beam?) - nie da się policzyć wymiaru równoległego/prostopadłego bez zgadywania.");
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
                log("TEST: StraightDimension.Insert() zwrócił false.");
                return false;
            }
            if (!drawing.CommitChanges("Test wymiaru szerokości wcięcia"))
            {
                log("TEST: Insert() się powiódł, ale CommitChanges() zwrócił false.");
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
                log($"TEST: Insert()/CommitChanges() zwróciły true, ale ponowny odczyt widoku NIE znalazł wstawionego wymiaru {label} - nic się NIE utrwaliło. Nie ufaj temu insertowi.");
                return false;
            }

            log($"TEST: wstawiono {label} wcięcia {displayedValue:F2} mm, potwierdzone ponownym odczytem widoku. Obejrzyj rysunek w Tekli; Ctrl+Z cofa.");
            return true;
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

                var pair = FindChord(faceOuterLoop, Centroid(faceOuterLoop), longest);
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
