using System;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;
using Tekla.Structures.Solid;
using TSM = Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

namespace RoAxisDimensionRemover
{
    // Świadomie trwały element projektu, NIE tymczasowe rusztowanie (w
    // odróżnieniu od usuniętego już Inspector.cs) - automatyzacja (Claude
    // Code) nie umie klikać przycisku w MainForm, więc to jedyny sposób
    // odpalić dry-run na aktywnym rysunku bez człowieka przy GUI. dryRun
    // jest tu na sztywno true - z linii komend nie da się tego przełączyć,
    // więc ten tryb nigdy nie kasuje.
    //
    // WAŻNE: to zostaje tak NA ZAWSZE, nawet po tym, jak MainForm.cs dostał
    // dryRun: false (patrz AGENTS.md). Ta klasa jest wywoływana bez
    // człowieka przy przycisku - z linii komend, przez automatyzację/agenta
    // AI. Taka ścieżka nigdy nie powinna dostać możliwości realnego
    // kasowania, niezależnie od tego, jak dobrze zweryfikowana jest reguła.
    //
    // Geometria (cięciwy, ściany cięcia, przeliczenie do układu widoku)
    // pochodzi z NotchPilot - diagnostyka ma liczyć DOKŁADNIE to samo co
    // produkcyjny przycisk, nie własną kopię, która mogłaby się rozjechać.
    internal static class DiagRunner
    {
        private static void Log(string s) => Console.WriteLine(s);

        public static void RunOnActiveDrawing()
        {
            var drawing = ActiveDrawing();
            if (drawing != null) RunOn(drawing);
        }

        public static void RunOnMark(string mark)
        {
            var drawing = OpenByMark(mark);
            if (drawing != null) RunOn(drawing);
        }

        // Picker (wybór widoku przez kliknięcie w MainForm) wymaga GUI, więc
        // z linii komend nie da się go odtworzyć - diag przechodzi więc po
        // WSZYSTKICH widokach na arkuszu, w przeciwieństwie do przycisku w
        // MainForm, który działa na jednym widoku wskazanym przez operatora.
        private static void RunOn(Drawing drawing)
        {
            Log($"Aktywny rysunek: {drawing.Mark} / {drawing.Name}");
            int viewsChecked = 0, found = 0;
            foreach (var view in SheetViews(drawing).OfType<View>())
            {
                viewsChecked++;
                found += RoAxisDimensionService.RemoveAxisDimensions(drawing, view, Log, dryRun: true);
            }
            Log($"Gotowe. Widoków: {viewsChecked}, znalezionych kandydatów do usunięcia: {found}.");
        }

        /// <summary>
        /// Tylko odczyt: skanuje WSZYSTKIE rysunki w modelu (bez otwierania
        /// żadnego na ekranie - `GetSheet().GetAllObjects()` działa na
        /// uchwycie Drawing bez SetActiveDrawing) i loguje te, które mają
        /// choć jeden wymiar do osi. Cel: dobierać rysunki testowe po
        /// danych. Trwa > 10 min na całym modelu - uruchamiać w tle.
        /// </summary>
        // marksFile: plik z listą Mark (po jednym w linii) - skan tylko tych
        // rysunków, z logiem każdego wymiaru. Pełny skan 2298 rysunków
        // trwał 2026-09-30 ponad godzinę (Tekla urosła do 6 GB), a do
        // porównania reguł przed/po wystarczą rysunki, które mają kandydatów.
        public static void RunFindCandidatesDiag(string marksFile = null)
        {
            var only = marksFile == null ? null : new HashSet<string>(
                System.IO.File.ReadAllLines(marksFile).Select(l => l.Trim()).Where(l => l.Length > 0));
            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return;
            }

            // Woła PRAWDZIWĄ RemoveAxisDimensions (dryRun: true), nie
            // duplikuje TouchesAxis samodzielnie - inaczej ten skaner
            // pokazywałby fałszywe trafienia na innych profilach, które
            // guardy RemoveAxisDimensions mają właśnie wykluczyć (ZMIERZONE
            // 2026-09-25 na [21050]).
            int scanned = 0, withCandidates = 0;
            var drawings = dh.GetDrawings();
            while (drawings.MoveNext())
            {
                var drawing = drawings.Current;
                if (only != null && !only.Contains(drawing.Mark)) continue;
                // Pełny skan: per-dimension log nie interesuje, liczy się suma.
                void Silent(string s) { if (only != null && s.StartsWith("[diag] wymiar dotyka osi (") || only != null && s.StartsWith("Znaleziono")) Log($"[find]   {drawing.Mark}: {s}"); }
                scanned++;
                int candidateCount = 0;
                try
                {
                    foreach (var view in SheetViews(drawing))
                    {
                        candidateCount += RoAxisDimensionService.RemoveAxisDimensions(drawing, view, Silent, dryRun: true);
                    }
                }
                catch (Exception ex)
                {
                    Log($"[find] {drawing.Mark}: błąd odczytu ({ex.GetType().Name}: {ex.Message}) - pominięto.");
                    continue;
                }
                if (candidateCount == 0) continue;

                withCandidates++;
                // Ten sam InsertMissing co przycisk "Wstaw", w dry-run - liczy
                // braki wymiaru wcięcia i promienie do rozciągnięcia.
                int missing = 0, stretch = 0;
                try
                {
                    NotchPilot.InsertMissing(drawing, s =>
                    {
                        if (s.Contains("[dry-run] brakująca")) missing++;
                        else if (s.Contains("[dry-run] rozciągnąłbym")) { stretch++; if (only != null) Log($"[find]   {drawing.Mark}: {s}"); }
                    }, dryRun: true);
                }
                catch (Exception ex)
                {
                    Log($"[find] {drawing.Mark}: błąd dry-runu wstawiania ({ex.GetType().Name}: {ex.Message}).");
                }
                string skipped = string.Join(" ", SkippedCutAngles(drawing).Select(a => $"{a:F1}°"));
                Log($"[find] {drawing.Mark} / {drawing.Name}: {candidateCount} do usunięcia, {missing} brakujących wymiarów wcięcia, {stretch} promieni do rozciągnięcia" +
                    (skipped.Length > 0 ? $", ściany odrzucone filtrem kąta: {skipped}." : "."));
            }
            Log($"[find] Przeskanowano {scanned} rysunków, {withCandidates} ma kandydatów.");
        }

        // Kąty ścian cięcia, które filtr MinCutAngleDegrees odrzuca - na tych
        // końcach "Wstaw" nic nie doda, choć "Usuń" mógł coś skasować
        // (ZMIERZONE 2026-09-29 na [35092]: koniec 8,7° z wymiarami 7 i 3 mm).
        // Kąt dokładnie 0 (proste przycięcie) odpada już w CutFaces.
        private static IEnumerable<double> SkippedCutAngles(Drawing drawing)
        {
            var model = new TSM.Model();
            if (!model.GetConnectionStatus()) yield break;
            var seen = new HashSet<string>();
            foreach (var view in SheetViews(drawing))
            {
                var parts = view.GetAllObjects(new[] { typeof(Part) });
                while (parts.MoveNext())
                {
                    if (!(parts.Current is Part drawingPart) || !seen.Add(drawingPart.ModelIdentifier.ToString())) continue;
                    if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;
                    foreach (var (_, outerLoop) in NotchPilot.CutFaces(modelPart.GetSolid(), NotchPilot.BeamAxis(modelPart)))
                    {
                        var centroid = NotchPilot.Centroid(outerLoop);
                        double angle = NotchPilot.CutAngleDegrees(
                            NotchPilot.FindChord(outerLoop, centroid, longest: true),
                            NotchPilot.FindChord(outerLoop, centroid, longest: false));
                        if (angle < NotchPilot.MinCutAngleDegrees) yield return angle;
                    }
                }
            }
        }

        /// <summary>
        /// Czysto do odczytu - zbiera realną geometrię bryły
        /// (Model.Part.GetSolid()) partów widocznych na aktywnym rysunku:
        /// wszystkie ściany z normalnymi, pętle wierzchołków i kandydatów na
        /// ścianę cięcia. Research pod wymiar wcięcia na PRAWDZIWYCH danych.
        /// </summary>
        public static void RunNotchDiag()
        {
            var drawing = ActiveDrawing();
            var model = drawing == null ? null : ConnectedModel();
            if (model == null) return;

            Log($"[notch] Aktywny rysunek: {drawing.Mark} / {drawing.Name}");
            foreach (var view in SheetViews(drawing).OfType<View>())
            {
                var parts = view.GetAllObjects(new[] { typeof(Part) });
                while (parts.MoveNext())
                {
                    if (!(parts.Current is Part drawingPart)) continue;
                    var modelObj = model.SelectModelObject(drawingPart.ModelIdentifier);
                    if (!(modelObj is TSM.Part modelPart))
                    {
                        Log($"[notch] {view.GetType().Name}: ModelIdentifier {drawingPart.ModelIdentifier} nie wskazuje na Part ({modelObj?.GetType().Name ?? "null"}).");
                        continue;
                    }

                    Log($"[notch] {view.GetType().Name}: Part Profile={ProfileOf(modelPart)} Class={modelPart.Class} PartNumber={modelPart.Identifier?.ID}");

                    TSM.Solid solid;
                    try
                    {
                        solid = modelPart.GetSolid();
                    }
                    catch (Exception ex)
                    {
                        Log("[notch]   GetSolid() błąd: " + ex.GetType().Name + ": " + ex.Message);
                        continue;
                    }

                    Log($"[notch]   bryła (jednostki modelu): Min={PointStr(solid.MinimumPoint)} Max={PointStr(solid.MaximumPoint)}");
                    LogAllFaces(solid);

                    // Wszystkie kwalifikujące się ściany osobno (ZMIERZONE na
                    // [3.5013]: bryła może mieć ich kilka - dwa ukośne końce).
                    var cutFaces = NotchPilot.CutFaces(solid, NotchPilot.BeamAxis(modelPart)).ToList();
                    if (cutFaces.Count == 0)
                    {
                        Log("[notch]   brak kandydata na ścianę cięcia w tej bryle (być może prosty koniec bez ukośnego złącza).");
                        continue;
                    }
                    if (cutFaces.Count > 1)
                    {
                        Log($"[notch]   UWAGA: {cutFaces.Count} kwalifikujących się ścian cięcia w tej bryle. Logowane wszystkie:");
                    }
                    foreach (var (face, outerLoop) in cutFaces)
                    {
                        LogCandidateFace(view, face, outerLoop);
                    }
                }
            }
        }

        // Wzorzec z oficjalnej dokumentacji Tekli (przykład przy Solid):
        // Face.Normal + GetLoopEnumerator -> Loop.GetVertexEnumerator. Pętle
        // logowane OSOBNO - ściana cięcia rury to pierścień (obrys + otwór).
        private static void LogAllFaces(TSM.Solid solid)
        {
            int faceIndex = 0;
            var faces = solid.GetFaceEnumerator();
            while (faces.MoveNext())
            {
                faceIndex++;
                if (!(faces.Current is Face face)) continue;

                var all = new List<TSG.Point>();
                int loopIndex = 0;
                var loops = face.GetLoopEnumerator();
                while (loops.MoveNext())
                {
                    loopIndex++;
                    if (!(loops.Current is Loop loop)) continue;
                    var points = new List<TSG.Point>();
                    var vertices = loop.GetVertexEnumerator();
                    while (vertices.MoveNext()) if (vertices.Current is TSG.Point v) points.Add(v);
                    all.AddRange(points);
                    if (points.Count > 4)
                    {
                        Log($"[notch]     pętla {loopIndex} ({points.Count} wierzchołków): {string.Join(" ", points.Select(PointStr))}");
                    }
                }
                string range = all.Count == 0 ? "" :
                    $" X=[{all.Min(p => p.X):F1};{all.Max(p => p.X):F1}] Y=[{all.Min(p => p.Y):F1};{all.Max(p => p.Y):F1}] Z=[{all.Min(p => p.Z):F1};{all.Max(p => p.Z):F1}]";
                Log($"[notch]   ściana {faceIndex}: Normal=({face.Normal.X:F2};{face.Normal.Y:F2};{face.Normal.Z:F2}) wierzchołków={all.Count} pętli={loopIndex}{range}");
            }
        }

        private static void LogCandidateFace(View view, Face cutFace, List<TSG.Point> outerLoop)
        {
            var centroid = NotchPilot.Centroid(outerLoop);
            var major = NotchPilot.FindChord(outerLoop, centroid, longest: true);
            var minor = NotchPilot.FindChord(outerLoop, centroid, longest: false);
            Log($"[notch]   KANDYDAT ściana cięcia (Normal=({cutFace.Normal.X:F2};{cutFace.Normal.Y:F2};{cutFace.Normal.Z:F2})): długość={NotchPilot.Distance(major.A, major.B):F2} mm (model) szerokość={NotchPilot.Distance(minor.A, minor.B):F2} mm (model)");
            Log($"[notch]     długość: {PointStr(major.A)} -> {PointStr(major.B)}");
            Log($"[notch]     szerokość: {PointStr(minor.A)} -> {PointStr(minor.B)}");
            var cs = view.DisplayCoordinateSystem;
            Log($"[notch]     długość w widoku: {PointStr(NotchPilot.ToViewSpace(major.A, cs))} -> {PointStr(NotchPilot.ToViewSpace(major.B, cs))}");
            Log($"[notch]     szerokość w widoku: {PointStr(NotchPilot.ToViewSpace(minor.A, cs))} -> {PointStr(NotchPilot.ToViewSpace(minor.B, cs))}");
        }

        /// <summary>
        /// Tylko odczyt: wartość, geometria i styl istniejących wymiarów
        /// aktywnego rysunku. Główne narzędzie weryfikacji: po każdej
        /// operacji zapisu odczytywać OSOBNYM procesem (odczyt w tym samym
        /// procesie potrafi kłamać - patrz AGENTS.md).
        /// </summary>
        public static void RunDimensionStyleDiag()
        {
            var drawing = ActiveDrawing();
            if (drawing == null) return;

            Log($"[dim-style] Aktywny rysunek: {drawing.Mark} / {drawing.Name}");
            int viewIndex = 0;
            foreach (var view in SheetViews(drawing).OfType<View>())
            {
                viewIndex++;
                int dimensionIndex = 0;
                var dimensions = view.GetAllObjects(new[] { typeof(StraightDimension) });
                while (dimensions.MoveNext())
                {
                    if (!(dimensions.Current is StraightDimension dimension)) continue;
                    dimensionIndex++;
                    var attributes = (dimension.GetDimensionSet() as StraightDimensionSet)?.Attributes;
                    double? value = RoAxisDimensionService.GetDisplayedValue(dimension);
                    Log($"[dim-style] widok {viewIndex}, wymiar {dimensionIndex}: " +
                        $"Wartość={(value.HasValue ? value.Value.ToString("F2") : "?")} " +
                        $"Start={PointStr(dimension.StartPoint)} End={PointStr(dimension.EndPoint)} " +
                        $"Up=({dimension.UpDirection.X:F2};{dimension.UpDirection.Y:F2};{dimension.UpDirection.Z:F2}) " +
                        $"Distance={dimension.Distance:F2} Attributes={attributes}");
                }
            }
        }

        /// <summary>
        /// Tylko odczyt: dopasowuje każdy wymiar "do osi" do najbliższej
        /// ściany cięcia w TYM SAMYM widoku (środek wymiaru vs centroid
        /// ściany po ToViewSpace - oba w lokalnym układzie widoku). Research
        /// z 2026-09-23 pod regułę dopasowania ściana↔wymiar.
        /// </summary>
        public static void RunNotchMatchDiag(string mark)
        {
            var drawing = OpenByMark(mark);
            var model = drawing == null ? null : ConnectedModel();
            if (model == null) return;

            Log($"[notch-match] Rysunek: {drawing.Mark} / {drawing.Name}");
            foreach (var view in SheetViews(drawing).OfType<View>())
            {
                var mids = AxisDimensionMids(view).ToList();
                foreach (var (mid, ownLength) in mids)
                {
                    Log($"[notch-match]   wymiar do osi: mid(widok)={PointStr(mid)} własna_długość={ownLength:F1} mm");
                }
                if (mids.Count == 0) continue;

                var cs = view.DisplayCoordinateSystem;
                var faceCandidates = new List<(TSG.Point ViewMid, double MajorLen, double MinorLen, TSG.Vector Normal)>();
                foreach (var modelPart in ModelParts(view, model, "[notch-match]"))
                {
                    TSM.Solid solid;
                    try { solid = modelPart.GetSolid(); }
                    catch { continue; }

                    foreach (var (face, outerLoop) in NotchPilot.CutFaces(solid, NotchPilot.BeamAxis(modelPart)))
                    {
                        var centroid = NotchPilot.Centroid(outerLoop);
                        var major = NotchPilot.FindChord(outerLoop, centroid, longest: true);
                        var minor = NotchPilot.FindChord(outerLoop, centroid, longest: false);
                        faceCandidates.Add((
                            NotchPilot.ToViewSpace(centroid, cs),
                            NotchPilot.Distance(major.A, major.B),
                            NotchPilot.Distance(minor.A, minor.B),
                            new TSG.Vector(face.Normal)));
                    }
                }

                if (faceCandidates.Count == 0)
                {
                    Log("[notch-match]   brak kandydatów na ścianę cięcia w tym widoku - nie ma czego dopasować.");
                    continue;
                }

                foreach (var (mid, _) in mids)
                {
                    var best = faceCandidates.OrderBy(c => NotchPilot.Distance(mid, c.ViewMid)).First();
                    Log($"[notch-match]   DOPASOWANIE: wymiar mid={PointStr(mid)} -> ściana Normal=({best.Normal.X:F2};{best.Normal.Y:F2};{best.Normal.Z:F2}) długość={best.MajorLen:F2} mm szerokość={best.MinorLen:F2} mm odległość_do_środka={NotchPilot.Distance(mid, best.ViewMid):F2} mm (widok)");
                }
            }
        }

        /// <summary>
        /// Tylko odczyt: `NotchPilot.InsertMissing` w dry-run - dokładnie
        /// to, co zrobiłby przycisk "Wstaw" (braki wymiaru wcięcia i
        /// promienie do rozciągnięcia).
        /// </summary>
        public static void RunNotchFillDryRun(string mark)
        {
            var drawing = OpenByMark(mark);
            if (drawing == null) return;

            Log($"[notch-fill] Rysunek: {drawing.Mark} / {drawing.Name}");
            NotchPilot.InsertMissing(drawing, s => Log("[notch-fill]   " + s), dryRun: true);
        }

        /// <summary>
        /// Tylko odczyt: dla każdego wymiaru do osi woła starszy mechanizm
        /// NotchPilot.InsertWidth/InsertLength w dry-run, z punktem środka
        /// tego wymiaru jako referencją. Druga, niezależna metoda
        /// weryfikacji tych samych liczb co --diag-notch-fill-dryrun.
        /// </summary>
        public static void RunNotchInsertDryRun(string mark)
        {
            var drawing = OpenByMark(mark);
            if (drawing == null) return;

            Log($"[notch-insert] Rysunek: {drawing.Mark} / {drawing.Name}");
            foreach (var view in SheetViews(drawing).OfType<View>())
            {
                foreach (var (mid, ownLength) in AxisDimensionMids(view))
                {
                    Log($"[notch-insert] wymiar do osi mid={PointStr(mid)} własna_długość={ownLength:F1} mm ->");
                    NotchPilot.InsertWidth(drawing, s => Log("[notch-insert]   " + s), mid, dryRun: true, referenceView: view);
                    NotchPilot.InsertLength(drawing, s => Log("[notch-insert]   " + s), mid, dryRun: true, referenceView: view);
                }
            }
        }

        /// <summary>
        /// Tylko odczyt: zrzuca WSZYSTKICH kandydatów na ścianę cięcia (bez
        /// filtra płaskości Z≈0 i bez filtra kąta, w przeciwieństwie do
        /// NotchPilot) dla KAŻDEGO widoku osobno - obie cięciwy po
        /// ToViewSpace, z pełnym X;Y;Z i flagą płaskości. Źródło danych dla
        /// poprawki asymetrii widoku z 2026-09-25 (patrz AGENTS.md).
        /// </summary>
        public static void RunNotchRawDiag(string mark)
        {
            var drawing = OpenByMark(mark);
            var model = drawing == null ? null : ConnectedModel();
            if (model == null) return;

            Log($"[notch-raw] Rysunek: {drawing.Mark} / {drawing.Name}");
            foreach (var view in SheetViews(drawing).OfType<View>())
            {
                Log($"[notch-raw] widok Origin={PointStr(view.Origin)}");
                foreach (var (mid, _) in AxisDimensionMids(view))
                {
                    Log($"[notch-raw]   wymiar do osi (do usunięcia w tym widoku): mid={PointStr(mid)}");
                }

                var cs = view.DisplayCoordinateSystem;
                foreach (var modelPart in ModelParts(view, model, "[notch-raw]"))
                {
                    TSM.Solid solid;
                    try { solid = modelPart.GetSolid(); }
                    catch { continue; }

                    var centerLine = modelPart.GetCenterLine(false);
                    var centerPoints = new List<TSG.Point>();
                    if (centerLine != null) foreach (var p in centerLine) if (p is TSG.Point cp) centerPoints.Add(cp);
                    Log($"[notch-raw]   oś części (GetCenterLine): {centerPoints.Count} punktów: {string.Join(" ", centerPoints.Select(PointStr))}");

                    int faceIndex = 0;
                    foreach (var (face, outerLoop) in NotchPilot.CutFaces(solid, NotchPilot.BeamAxis(modelPart)))
                    {
                        faceIndex++;
                        var centroid = NotchPilot.Centroid(outerLoop);
                        var major = NotchPilot.FindChord(outerLoop, centroid, longest: true);
                        var minor = NotchPilot.FindChord(outerLoop, centroid, longest: false);
                        double angle = NotchPilot.CutAngleDegrees(major, minor);
                        string verdict = angle < NotchPilot.MinCutAngleDegrees ? "pomijana przez filtr kąta" : "kwalifikuje się";
                        Log($"[notch-raw]   ściana#{faceIndex} Normal=({face.Normal.X:F2};{face.Normal.Y:F2};{face.Normal.Z:F2}) kąt cięcia={angle:F1}° ({verdict})");
                        LogRawChord("długość", NotchPilot.ToViewSpace(major.A, cs), NotchPilot.ToViewSpace(major.B, cs));
                        LogRawChord("szerokość", NotchPilot.ToViewSpace(minor.A, cs), NotchPilot.ToViewSpace(minor.B, cs));
                    }
                }
            }
        }

        private static void LogRawChord(string label, TSG.Point start, TSG.Point end)
        {
            bool flat = Math.Abs(start.Z) <= NotchPilot.NumericalZero && Math.Abs(end.Z) <= NotchPilot.NumericalZero;
            Log($"[notch-raw]     {label}: Start={PointStr(start)} End={PointStr(end)} wartość={NotchPilot.Distance(start, end):F2} mm płaska(Z≈0)={flat}");
        }

        /// <summary>
        /// Tylko odczyt: research "czy wymiar wcięcia wychodzi poza arkusz"
        /// (2026-09-28, PORZUCONE - patrz AGENTS.md). Loguje rozmiar arkusza
        /// i widoków, bounding box zawartości i długość wektorów układów
        /// widoku (sprawdzone: oba jednostkowe, nie niosą skali).
        /// </summary>
        public static void RunViewBoundsDiag(string mark)
        {
            var drawing = OpenByMark(mark);
            if (drawing == null) return;

            Log($"[view-bounds] Rysunek: {drawing.Mark} / {drawing.Name}");
            try
            {
                var sheet = drawing.GetSheet();
                Log($"[view-bounds] arkusz: Width={sheet.Width:F2} Height={sheet.Height:F2}");
            }
            catch (Exception ex)
            {
                Log($"[view-bounds] arkusz: błąd odczytu ({ex.GetType().Name}: {ex.Message})");
            }

            foreach (var view in SheetViews(drawing).OfType<View>())
            {
                Log($"[view-bounds] widok Origin={PointStr(view.Origin)} Width={view.Width:F2} Height={view.Height:F2}");
                try
                {
                    var box = view.GetAxisAlignedBoundingBox();
                    Log($"[view-bounds]   bounding box zawartości: Min={PointStr(box.MinPoint)} Max={PointStr(box.MaxPoint)}");
                }
                catch (Exception ex)
                {
                    Log($"[view-bounds]   bounding box: błąd odczytu ({ex.GetType().Name}: {ex.Message})");
                }
                var vcs = view.ViewCoordinateSystem;
                var dcs = view.DisplayCoordinateSystem;
                Log($"[view-bounds]   ViewCoordinateSystem: Origin={PointStr(vcs.Origin)} |AxisX|={new TSG.Vector(vcs.AxisX).GetLength():F6} |AxisY|={new TSG.Vector(vcs.AxisY).GetLength():F6}");
                Log($"[view-bounds]   DisplayCoordinateSystem: Origin={PointStr(dcs.Origin)} |AxisX|={new TSG.Vector(dcs.AxisX).GetLength():F6} |AxisY|={new TSG.Vector(dcs.AxisY).GetLength():F6}");
            }
        }

        /// <summary>
        /// Tylko odczyt: typy WSZYSTKICH obiektów w każdym widoku, plus
        /// podgląd treści typów, które mogą być adnotacją kąta. Research z
        /// 2026-09-24 ("które złącze potrzebuje wymiaru wcięcia") - dwie
        /// hipotezy obalone, patrz AGENTS.md.
        /// </summary>
        public static void RunViewObjectsDiag(string mark)
        {
            var drawing = OpenByMark(mark);
            if (drawing == null) return;

            Log($"[view-objects] Rysunek: {drawing.Mark} / {drawing.Name}");
            int viewIndex = 0;
            foreach (var view in SheetViews(drawing))
            {
                viewIndex++;
                Log($"[view-objects] widok {viewIndex}: typ widoku={view.GetType().Name}");
                var objects = view.GetAllObjects();
                var counts = new Dictionary<string, int>();
                while (objects.MoveNext())
                {
                    var obj = objects.Current;
                    if (obj == null) continue;
                    string typeName = obj.GetType().Name;
                    counts[typeName] = counts.TryGetValue(typeName, out int c) ? c + 1 : 1;

                    // Nazwa klasy adnotacji kąta nie jest znana z góry, więc
                    // łapiemy szeroko po fragmencie nazwy typu.
                    if (new[] { "Note", "Text", "Symbol", "Angular", "Weld" }.Any(k => typeName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        Log($"[view-objects]   widok {viewIndex}, typ={typeName}: {DumpFirstStringProperty(obj)}");
                    }
                }
                foreach (var kv in counts)
                {
                    Log($"[view-objects] widok {viewIndex}: {kv.Key} x{kv.Value}");
                }
            }
        }

        // Refleksja: pierwsza niepusta właściwość typu string - szybki
        // podgląd zawartości nieznanego typu.
        private static string DumpFirstStringProperty(object obj)
        {
            try
            {
                foreach (var prop in obj.GetType().GetProperties())
                {
                    if (prop.PropertyType != typeof(string) || !prop.CanRead) continue;
                    string value;
                    try { value = prop.GetValue(obj) as string; }
                    catch { continue; }
                    if (!string.IsNullOrEmpty(value)) return $"{prop.Name}={value}";
                }
                return "(brak właściwości string z treścią)";
            }
            catch (Exception ex)
            {
                return $"(błąd odczytu: {ex.GetType().Name}: {ex.Message})";
            }
        }

        /// <summary>
        /// Tylko odczyt: dla każdego rysunkowego `Connection` loguje modelowy
        /// `TSM.Connection` (Number/Name/Class) oraz część podstawową i
        /// połączone. Trop z 2026-09-24 pod regułę "które złącze potrzebuje
        /// wymiaru wcięcia", nierozstrzygnięty.
        /// </summary>
        public static void RunConnectionDiag(string mark)
        {
            var drawing = OpenByMark(mark);
            var model = drawing == null ? null : ConnectedModel();
            if (model == null) return;

            Log($"[connection] Rysunek: {drawing.Mark} / {drawing.Name}");
            int viewIndex = 0;
            foreach (var view in SheetViews(drawing))
            {
                viewIndex++;
                var objects = view.GetAllObjects(new[] { typeof(Connection) });
                while (objects.MoveNext())
                {
                    if (!(objects.Current is Connection drawingConnection)) continue;
                    if (!(model.SelectModelObject(drawingConnection.ModelIdentifier) is TSM.Connection modelConnection))
                    {
                        Log($"[connection] widok {viewIndex}: nie udało się rozwiązać ModelIdentifier na TSM.Connection.");
                        continue;
                    }
                    Log($"[connection] widok {viewIndex}: Number={modelConnection.Number} Name={modelConnection.Name} Class={modelConnection.GetType().Name}");
                    LogConnectedPart("  primary", modelConnection.GetPrimaryObject());
                    var secondaries = modelConnection.GetSecondaryObjects();
                    if (secondaries == null) continue;
                    foreach (TSM.ModelObject secondary in secondaries)
                    {
                        LogConnectedPart("  secondary", secondary);
                    }
                }
            }
        }

        private static void LogConnectedPart(string label, TSM.ModelObject obj)
        {
            if (obj == null)
                Log($"[connection] {label}: (brak)");
            else if (obj is TSM.Part part)
                Log($"[connection] {label}: {part.GetType().Name} Name={part.Name} Profile={ProfileOf(part)}");
            else
                Log($"[connection] {label}: {obj.GetType().Name}");
        }

        // --- wspólne dla trybów diagnostycznych ---

        private static Drawing ActiveDrawing()
        {
            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return null;
            }
            var drawing = dh.GetActiveDrawing();
            if (drawing == null) Log("Brak otwartego rysunku.");
            return drawing;
        }

        // Otwiera rysunek po Mark na ekranie (SetActiveDrawing(d, true) -
        // celowo, żeby operator mógł od razu spojrzeć na wynik).
        private static Drawing OpenByMark(string mark)
        {
            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return null;
            }
            var drawings = dh.GetDrawings();
            while (drawings.MoveNext())
            {
                if (string.Equals(drawings.Current.Mark, mark, StringComparison.OrdinalIgnoreCase))
                {
                    return OpenUnlessActive(dh, drawings.Current);
                }
            }
            Log($"Nie znaleziono rysunku o Mark={mark}.");
            return null;
        }

        // SetActiveDrawing na rysunku, który JUŻ jest otwarty, przeładowuje go
        // i gubi niezapisane zmiany - zmierzone 2026-09-29 na [35095]: diag
        // po Mark cofnął 7 wymiarów skasowanych chwilę wcześniej przyciskiem
        // (model testowy nie zapisuje się, limit licencji). Otwarty rysunek
        // o tym samym Mark bierzemy więc tak, jak jest.
        private static Drawing OpenUnlessActive(DrawingHandler dh, Drawing drawing)
        {
            var active = dh.GetActiveDrawing();
            if (active != null && string.Equals(active.Mark, drawing.Mark, StringComparison.OrdinalIgnoreCase))
            {
                return active;
            }
            // ZMIERZONE 2026-09-29 na [3.5027]: rysunek nieaktualny względem
            // modelu nie da się otworzyć bez aktualizacji. Aktualizacja to
            // zmiana rysunku, więc diagnostyka jej nie robi - tylko mówi.
            try
            {
                dh.SetActiveDrawing(drawing, true);
            }
            catch (CannotPerformOperationDrawingNotUpToDateException)
            {
                Log($"Rysunek {drawing.Mark} jest nieaktualny względem modelu - zaktualizuj go w Tekli, potem uruchom diagnostykę ponownie.");
                return null;
            }
            return drawing;
        }

        private static TSM.Model ConnectedModel()
        {
            var model = new TSM.Model();
            if (model.GetConnectionStatus()) return model;
            Log("Brak połączenia z Teklą (Model).");
            return null;
        }

        internal static IEnumerable<ViewBase> SheetViews(Drawing drawing)
        {
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (top.Current is ViewBase view) yield return view;
            }
        }

        // Części modelu narysowane w widoku, z logiem profilu (przydatne przy
        // każdej wątpliwości "czy to na pewno rura RO").
        private static IEnumerable<TSM.Part> ModelParts(View view, TSM.Model model, string prefix)
        {
            var parts = view.GetAllObjects(new[] { typeof(Part) });
            while (parts.MoveNext())
            {
                if (!(parts.Current is Part drawingPart)) continue;
                if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;
                Log($"{prefix}   część: {modelPart.GetType().Name} Name={modelPart.Name} Profile={ProfileOf(modelPart)}");
                yield return modelPart;
            }
        }

        // Te same wymiary, które RemoveAxisDimensions by skasował (bez
        // guardów rysunku/profilu - diag ma je pokazać zawsze).
        private static IEnumerable<(TSG.Point Mid, double OwnLength)> AxisDimensionMids(View view)
        {
            var dims = view.GetAllObjects(new[] { typeof(StraightDimension) });
            while (dims.MoveNext())
            {
                if (!(dims.Current is StraightDimension sd) || !RoAxisDimensionService.TouchesAxis(sd)) continue;
                double ownLength = NotchPilot.Distance(sd.StartPoint, sd.EndPoint);
                if (ownLength > RoAxisDimensionService.SameJointDistanceMm) continue;
                var mid = new TSG.Point(
                    (sd.StartPoint.X + sd.EndPoint.X) / 2, (sd.StartPoint.Y + sd.EndPoint.Y) / 2, (sd.StartPoint.Z + sd.EndPoint.Z) / 2);
                yield return (mid, ownLength);
            }
        }

        private static string ProfileOf(TSM.Part part)
        {
            try { return part.Profile.ProfileString; }
            catch { return "?"; }
        }

        private static string PointStr(TSG.Point p) => $"({p.X:F2};{p.Y:F2};{p.Z:F2})";
    }
}
