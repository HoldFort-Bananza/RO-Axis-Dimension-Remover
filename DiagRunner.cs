using System;
using System.Collections.Generic;
using Tekla.Structures.Drawing;
using Tekla.Structures.Solid;
using TSM = Tekla.Structures.Model;

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
    // dryRun: false (2026-09-04, po potwierdzeniu operatora na [35270] i
    // [3.5013] - patrz AGENTS.md). Ta klasa jest wywoływana bez człowieka
    // przy przycisku - z linii komend, przez automatyzację/agenta AI. Taka
    // ścieżka nigdy nie powinna dostać możliwości realnego kasowania,
    // niezależnie od tego, jak dobrze zweryfikowana jest reguła.
    internal static class DiagRunner
    {
        public static void RunOnActiveDrawing()
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą.");
                return;
            }

            var drawing = dh.GetActiveDrawing();
            if (drawing == null)
            {
                Log("Brak otwartego rysunku.");
                return;
            }
            RunOn(drawing, Log);
        }

        // Otwiera rysunek po Mark i uruchamia na nim tę samą diagnostykę -
        // do porównania [35270] vs [3.5013] bez ręcznego klikania w Tekli.
        // SetActiveDrawing(d, true) otwiera na ekranie (patrz ../AGENTS.md,
        // pułapka SetActiveDrawing) - celowo, żeby operator mógł od razu
        // spojrzeć na wynik.
        public static void RunOnMark(string mark)
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą.");
                return;
            }

            var drawings = dh.GetDrawings();
            while (drawings.MoveNext())
            {
                var d = drawings.Current;
                if (!string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                dh.SetActiveDrawing(d, true);
                RunOn(d, Log);
                return;
            }
            Log($"Nie znaleziono rysunku o Mark={mark}.");
        }

        // Picker (wybór widoku przez kliknięcie w MainForm) wymaga GUI, więc
        // z linii komend nie da się go odtworzyć - diag przechodzi więc po
        // WSZYSTKICH widokach na arkuszu, w przeciwieństwie do przycisku w
        // MainForm, który działa na jednym widoku wskazanym przez operatora.
        private static void RunOn(Drawing drawing, Action<string> log)
        {
            log($"Aktywny rysunek: {drawing.Mark} / {drawing.Name}");
            var service = new RoAxisDimensionService();
            int viewsChecked = 0, found = 0;
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view))
                {
                    continue;
                }
                viewsChecked++;
                var result = service.RemoveAxisDimensions(drawing, view, log, dryRun: true);
                found += result.RemovedCount;
            }
            log($"Gotowe. Widoków: {viewsChecked}, znalezionych kandydatów do usunięcia: {found}.");
        }

        /// <summary>
        /// Czysto do odczytu (bez modyfikacji) - zbiera realną geometrię
        /// bryły (Model.Part.GetSolid()) partów widocznych na aktywnym
        /// rysunku, żeby zaprojektować regułę tworzenia wymiaru wcięcia
        /// (cut fitting) na PRAWDZIWYCH danych, a nie na zgadywanym
        /// kształcie - zgodnie z AGENTS.md, sekcja "Najważniejsze zanim
        /// cokolwiek zrobisz", pkt 4.
        /// </summary>
        public static void RunNotchDiag()
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return;
            }
            var drawing = dh.GetActiveDrawing();
            if (drawing == null)
            {
                Log("Brak otwartego rysunku.");
                return;
            }

            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Model).");
                return;
            }

            Log($"[notch] Aktywny rysunek: {drawing.Mark} / {drawing.Name}");
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view))
                {
                    continue;
                }

                var partsEnum = view.GetAllObjects(new[] { typeof(Part) });
                while (partsEnum.MoveNext())
                {
                    if (!(partsEnum.Current is Part drawingPart))
                    {
                        continue;
                    }

                    var modelObj = model.SelectModelObject(drawingPart.ModelIdentifier);
                    if (!(modelObj is TSM.Part modelPart))
                    {
                        Log($"[notch] {view.GetType().Name}: ModelIdentifier {drawingPart.ModelIdentifier} nie wskazuje na Part ({modelObj?.GetType().Name ?? "null"}).");
                        continue;
                    }

                    Log($"[notch] {view.GetType().Name}: Part Profile={modelPart.Profile?.ProfileString} Class={modelPart.Class} PartNumber={modelPart.Identifier?.ID}");

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

                    Log($"[notch]   bryła (jednostki modelu): Min=({solid.MinimumPoint.X:F1};{solid.MinimumPoint.Y:F1};{solid.MinimumPoint.Z:F1}) Max=({solid.MaximumPoint.X:F1};{solid.MaximumPoint.Y:F1};{solid.MaximumPoint.Z:F1})");

                    // Wzorzec z oficjalnej dokumentacji Tekli (przykład przy
                    // Solid): Face.Normal + GetLoopEnumerator ->
                    // Loop.GetVertexEnumerator -> punkty. Cel: znaleźć wśród
                    // ścian bryły tę, która jest powierzchnią cięcia (nie
                    // zgadywać - patrzeć na realne normalne i liczby
                    // wierzchołków, zanim cokolwiek się założy o regule).
                    int faceIndex = 0;
                    var faces = solid.GetFaceEnumerator();
                    while (faces.MoveNext())
                    {
                        faceIndex++;
                        if (!(faces.Current is Face face))
                        {
                            continue;
                        }

                        int vertexCount = 0;
                        double minX = double.MaxValue, maxX = double.MinValue;
                        double minY = double.MaxValue, maxY = double.MinValue;
                        double minZ = double.MaxValue, maxZ = double.MinValue;
                        int loopIndex = 0;
                        var loops = face.GetLoopEnumerator();
                        while (loops.MoveNext())
                        {
                            loopIndex++;
                            if (!(loops.Current is Loop loop))
                            {
                                continue;
                            }
                            // RO to profil PUSTY W ŚRODKU (rura), więc ściana
                            // cięcia to PIERŚCIEŃ - jedna pętla to obrys
                            // zewnętrzny, druga to otwór wewnętrzny. Wcześniej
                            // (błąd, poprawione) zlewałem wierzchołki obu
                            // pętli w jedną listę, co dawało bezsensowne
                            // dystanse (punkty z RÓŻNYCH pętli mylone z
                            // przeciwległymi punktami tej samej elipsy).
                            // Loguję pętle OSOBNO.
                            var loopVertexDump = new List<string>();
                            int loopVertexCount = 0;
                            var vertices = loop.GetVertexEnumerator();
                            while (vertices.MoveNext())
                            {
                                if (!(vertices.Current is Tekla.Structures.Geometry3d.Point v))
                                {
                                    continue;
                                }
                                vertexCount++;
                                loopVertexCount++;
                                minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X);
                                minY = Math.Min(minY, v.Y); maxY = Math.Max(maxY, v.Y);
                                minZ = Math.Min(minZ, v.Z); maxZ = Math.Max(maxZ, v.Z);
                                loopVertexDump.Add($"({v.X:F2};{v.Y:F2};{v.Z:F2})");
                            }
                            if (loopVertexCount > 4)
                            {
                                Log($"[notch]     pętla {loopIndex} ({loopVertexCount} wierzchołków): {string.Join(" ", loopVertexDump)}");
                            }
                        }
                        Log($"[notch]   ściana {faceIndex}: Normal=({face.Normal.X:F2};{face.Normal.Y:F2};{face.Normal.Z:F2}) wierzchołków={vertexCount} pętli={loopIndex} X=[{minX:F1};{maxX:F1}] Y=[{minY:F1};{maxY:F1}] Z=[{minZ:F1};{maxZ:F1}]");
                    }

                    TryLogNotchCandidate(view, modelPart, solid, Log);
                }
            }
        }

        /// <summary>
        /// Tylko odczyt: zapisuje geometrię i wspólne atrybuty istniejących
        /// wymiarów, aby nowy wymiar przejął styl z rysunku zamiast domyślnego.
        /// </summary>
        public static void RunDimensionStyleDiag()
        {
            void Log(string s) => Console.WriteLine(s);
            var handler = new DrawingHandler();
            if (!handler.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return;
            }
            var drawing = handler.GetActiveDrawing();
            if (drawing == null)
            {
                Log("Brak otwartego rysunku.");
                return;
            }

            Log($"[dim-style] Aktywny rysunek: {drawing.Mark} / {drawing.Name}");
            int viewIndex = 0;
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view)) continue;
                viewIndex++;
                int dimensionIndex = 0;
                var dimensions = view.GetAllObjects(new[] { typeof(StraightDimension) });
                while (dimensions.MoveNext())
                {
                    if (!(dimensions.Current is StraightDimension dimension)) continue;
                    dimensionIndex++;
                    var set = dimension.GetDimensionSet() as StraightDimensionSet;
                    var attributes = set?.Attributes;
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
        /// Szuka ściany cięcia (ta z >1 pętlą - profil RO jest pusty w
        /// środku, więc cięta powierzchnia to pierścień: obrys zewnętrzny +
        /// otwór) i liczy z NIEJ (nie z zgadywania) parę punktów "długość
        /// cięcia" (najdłuższa cięciwa obrysu zewnętrznego) i "szerokość
        /// cięcia" (cięciwa przechodząca przez środek, najkrótsza spośród
        /// tych bliskich środkowi - odporne na kolejność wierzchołków w
        /// pętli, w przeciwieństwie do wcześniejszego zgadywania "co N-ty
        /// wierzchołek"). Loguje punkty w układzie modelu I po przeliczeniu
        /// na układ widoku (View.DisplayCoordinateSystem - z dokumentacji:
        /// "can be used to transform global points from the model that is
        /// to be inserted in the view"). Czysto do odczytu - nic nie
        /// wstawia do rysunku.
        /// </summary>
        private static void TryLogNotchCandidate(View view, TSM.Part modelPart, TSM.Solid solid, Action<string> log)
        {
            Tekla.Structures.Geometry3d.Vector axisDir = null;
            if (modelPart is TSM.Beam beam)
            {
                var delta = beam.EndPoint - beam.StartPoint;
                axisDir = new Tekla.Structures.Geometry3d.Vector(delta.X, delta.Y, delta.Z).GetNormal();
            }

            // ZMIERZONE na [3.5013]: część może mieć WIĘCEJ NIŻ JEDNĄ
            // kwalifikującą się ścianę cięcia (dwa różne ukośne końce tego
            // samego krótkiego kawałka). Wcześniejsza wersja tej funkcji
            // milcząco zostawiała tylko ścianę o największym LoopSpan w
            // CAŁEJ bryle - na [35021] nie było to widoczne (tylko jedna
            // pasowała), ale to była niesprawdzona zgadywana reguła, nie
            // zmierzona. Teraz zbieramy WSZYSTKIE kwalifikujące się ściany
            // osobno i logujemy każdą - decyzja "która odpowiada temu
            // złączu" zostaje jawnie nierozwiązana, zamiast być ukrytym
            // efektem ubocznym sortowania.
            var candidateFaces = CollectCandidateFaces(solid, axisDir);

            if (candidateFaces.Count == 0)
            {
                log("[notch]   brak kandydata na ścianę cięcia w tej bryle (być może prosty koniec bez ukośnego złącza).");
                return;
            }

            if (candidateFaces.Count > 1)
            {
                log($"[notch]   UWAGA: {candidateFaces.Count} kwalifikujących się ścian cięcia w tej bryle - który odpowiada temu złączu, NIE jest jeszcze rozstrzygnięte. Logowane wszystkie:");
            }

            foreach (var (cutFace, outerLoop) in candidateFaces)
            {
                LogCandidateFace(view, cutFace, outerLoop, log);
            }
        }

        // Wspólne dla TryLogNotchCandidate i RunNotchMatchDiag: znajduje
        // wszystkie ściany cięcia bryły (>1 pętla = pierścień, normalna nie
        // równoległa do osi belki jeśli ją znamy). Nie wybiera "tej
        // właściwej" - to jest właśnie nierozwiązana reguła dopasowania,
        // patrz AGENTS.md "Następne kroki" pkt 1.
        private static List<(Face Face, List<Tekla.Structures.Geometry3d.Point> OuterLoop)> CollectCandidateFaces(
            TSM.Solid solid, Tekla.Structures.Geometry3d.Vector axisDir)
        {
            var candidateFaces = new List<(Face Face, List<Tekla.Structures.Geometry3d.Point> OuterLoop)>();
            var faces = solid.GetFaceEnumerator();
            while (faces.MoveNext())
            {
                if (!(faces.Current is Face face))
                {
                    continue;
                }

                var loopsInFace = new List<List<Tekla.Structures.Geometry3d.Point>>();
                var loops = face.GetLoopEnumerator();
                while (loops.MoveNext())
                {
                    if (!(loops.Current is Loop loop))
                    {
                        continue;
                    }
                    var pts = new List<Tekla.Structures.Geometry3d.Point>();
                    var vertices = loop.GetVertexEnumerator();
                    while (vertices.MoveNext())
                    {
                        if (vertices.Current is Tekla.Structures.Geometry3d.Point v)
                        {
                            pts.Add(v);
                        }
                    }
                    if (pts.Count > 4)
                    {
                        loopsInFace.Add(pts);
                    }
                }

                if (loopsInFace.Count < 2)
                {
                    continue; // nie pierścień - albo ścianka N-kąta, albo płaski koniec bez otworu
                }

                if (axisDir != null)
                {
                    double alignment = Math.Abs(new Tekla.Structures.Geometry3d.Vector(face.Normal).GetNormal().Dot(axisDir));
                    if (alignment > 0.999) // normalna ~równoległa do osi = proste przycięcie, nie ten joint
                    {
                        continue;
                    }
                }

                List<Tekla.Structures.Geometry3d.Point> faceOuterLoop = null;
                foreach (var loop in loopsInFace)
                {
                    if (faceOuterLoop == null || LoopSpan(loop) > LoopSpan(faceOuterLoop))
                    {
                        faceOuterLoop = loop;
                    }
                }
                candidateFaces.Add((face, faceOuterLoop));
            }
            return candidateFaces;
        }

        /// <summary>
        /// Tylko odczyt: dopasowuje każdy wymiar "do osi" (ten sam warunek co
        /// RoAxisDimensionService.RemoveAxisDimensions - reużyty wprost, nie
        /// duplikowany) do najbliższej ściany cięcia w TYM SAMYM widoku.
        /// Reguła: StraightDimension.StartPoint/EndPoint i punkty ściany po
        /// ToViewSpace żyją w TYM SAMYM lokalnym układzie widoku (patrz
        /// AGENTS.md, sekcja "Wymiar wcięcia") - więc "najbliższa ściana do
        /// środka wymiaru" jest prostym, sprawdzalnym kandydatem na regułę z
        /// "Następne kroki" pkt 1. Nic nie usuwa ani nie wstawia - loguje
        /// tylko dopasowanie do oceny na [35021] (1 ściana) i [3.5013]
        /// (2 ściany).
        /// </summary>
        public static void RunNotchMatchDiag(string mark)
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return;
            }

            Drawing drawing = null;
            var drawings = dh.GetDrawings();
            while (drawings.MoveNext())
            {
                if (string.Equals(drawings.Current.Mark, mark, StringComparison.OrdinalIgnoreCase))
                {
                    drawing = drawings.Current;
                    break;
                }
            }
            if (drawing == null)
            {
                Log($"Nie znaleziono rysunku o Mark={mark}.");
                return;
            }
            dh.SetActiveDrawing(drawing, true);

            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Model).");
                return;
            }

            Log($"[notch-match] Rysunek: {drawing.Mark} / {drawing.Name}");
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view))
                {
                    continue;
                }

                var axisDimensionMids = new List<Tekla.Structures.Geometry3d.Point>();
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
                    axisDimensionMids.Add(mid);
                    Log($"[notch-match]   wymiar do osi: mid(widok)={PointStr(mid)} własna_długość={ownLength:F1} mm");
                }
                if (axisDimensionMids.Count == 0)
                {
                    continue;
                }

                var faceCandidates = new List<(Tekla.Structures.Geometry3d.Point ViewMid, double MajorLen, double MinorLen, Tekla.Structures.Geometry3d.Vector Normal)>();
                var partsEnum = view.GetAllObjects(new[] { typeof(Part) });
                while (partsEnum.MoveNext())
                {
                    if (!(partsEnum.Current is Part drawingPart)) continue;
                    if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;

                    Tekla.Structures.Geometry3d.Vector axisDir = null;
                    if (modelPart is TSM.Beam beam)
                    {
                        var delta = beam.EndPoint - beam.StartPoint;
                        axisDir = new Tekla.Structures.Geometry3d.Vector(delta.X, delta.Y, delta.Z).GetNormal();
                    }

                    TSM.Solid solid;
                    try { solid = modelPart.GetSolid(); }
                    catch { continue; }

                    var cs = view.DisplayCoordinateSystem;
                    foreach (var (face, outerLoop) in CollectCandidateFaces(solid, axisDir))
                    {
                        var centroid = Centroid(outerLoop);
                        var major = FindChord(outerLoop, centroid, longest: true);
                        var minor = FindChord(outerLoop, centroid, longest: false);
                        faceCandidates.Add((
                            ToViewSpace(centroid, cs),
                            Distance(major.Item1, major.Item2),
                            Distance(minor.Item1, minor.Item2),
                            new Tekla.Structures.Geometry3d.Vector(face.Normal)));
                    }
                }

                if (faceCandidates.Count == 0)
                {
                    Log("[notch-match]   brak kandydatów na ścianę cięcia w tym widoku - nie ma czego dopasować.");
                    continue;
                }

                foreach (var mid in axisDimensionMids)
                {
                    var best = faceCandidates[0];
                    double bestDist = Distance(mid, best.ViewMid);
                    foreach (var candidate in faceCandidates)
                    {
                        double d = Distance(mid, candidate.ViewMid);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = candidate;
                        }
                    }
                    Log($"[notch-match]   DOPASOWANIE: wymiar mid={PointStr(mid)} -> ściana Normal=({best.Normal.X:F2};{best.Normal.Y:F2};{best.Normal.Z:F2}) długość={best.MajorLen:F2} mm szerokość={best.MinorLen:F2} mm odległość_do_środka={bestDist:F2} mm (widok)");
                }
            }
        }

        /// <summary>
        /// Tylko odczyt: dla każdego wymiaru do osi (ten sam, który
        /// RemoveAxisDimensions by skasował), woła NotchPilot w trybie
        /// dry-run z punktem środka tego wymiaru jako referencją - dokładnie
        /// scenariusz, do którego reguła dopasowania ściana↔wymiar została
        /// zaprojektowana (patrz AGENTS.md), tylko bez blokady marki i bez
        /// realnego Insert()/CommitChanges(). Loguje, co NotchPilot
        /// wstawiłby dla każdego wymiaru osobno - pozwala sprawdzić złącza
        /// z wieloma ścianami cięcia (np. [3.5013]) bez ryzyka.
        /// </summary>
        public static void RunNotchInsertDryRun(string mark)
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return;
            }

            Drawing drawing = null;
            var drawings = dh.GetDrawings();
            while (drawings.MoveNext())
            {
                if (string.Equals(drawings.Current.Mark, mark, StringComparison.OrdinalIgnoreCase))
                {
                    drawing = drawings.Current;
                    break;
                }
            }
            if (drawing == null)
            {
                Log($"Nie znaleziono rysunku o Mark={mark}.");
                return;
            }
            dh.SetActiveDrawing(drawing, true);

            Log($"[notch-insert] Rysunek: {drawing.Mark} / {drawing.Name}");
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view))
                {
                    continue;
                }

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
                    Log($"[notch-insert] wymiar do osi mid={PointStr(mid)} własna_długość={ownLength:F1} mm ->");
                    NotchPilot.InsertWidthTest(drawing, s => Log("[notch-insert]   " + s), mid, dryRun: true);
                    NotchPilot.InsertLengthTest(drawing, s => Log("[notch-insert]   " + s), mid, dryRun: true);
                }
            }
        }

        /// <summary>
        /// Tylko odczyt: zrzuca WSZYSTKIE obiekty w każdym widoku (nie tylko
        /// wymiary) - typ i podstawowe dane. Cel: sprawdzić, czym w Open API
        /// jest adnotacja kąta ("45°"/"19,90°") widoczna na rysunku przy
        /// skośnym cięciu - operator (2026-09-24) zauważył, że bryła może
        /// mieć geometryczną ścianę cięcia bez potrzeby wymiaru wcięcia w
        /// rysunku, a obecność takiej adnotacji może być sygnałem "to
        /// złącze faktycznie trzeba opisać". Patrz AGENTS.md, "Następne
        /// kroki" pkt 2 - to research pod tę regułę, nie gotowa reguła.
        /// </summary>
        public static void RunViewObjectsDiag(string mark)
        {
            void Log(string s) => Console.WriteLine(s);

            var dh = new DrawingHandler();
            if (!dh.GetConnectionStatus())
            {
                Log("Brak połączenia z Teklą (Drawing).");
                return;
            }

            Drawing drawing = null;
            var drawings = dh.GetDrawings();
            while (drawings.MoveNext())
            {
                if (string.Equals(drawings.Current.Mark, mark, StringComparison.OrdinalIgnoreCase))
                {
                    drawing = drawings.Current;
                    break;
                }
            }
            if (drawing == null)
            {
                Log($"Nie znaleziono rysunku o Mark={mark}.");
                return;
            }
            dh.SetActiveDrawing(drawing, true);

            Log($"[view-objects] Rysunek: {drawing.Mark} / {drawing.Name}");
            int viewIndex = 0;
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is ViewBase view)) continue;
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

                    // Loguj szczegóły dla typów, które mogą być adnotacją
                    // kąta - nazwa klasy nie jest znana z góry, więc łapiemy
                    // szeroko (Note/Text/Symbol/Mark w nazwie typu) i
                    // zrzucamy refleksją co ma.
                    if (typeName.IndexOf("Note", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("Symbol", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("Angular", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("Weld", StringComparison.OrdinalIgnoreCase) >= 0)
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

        /// <summary>
        /// Refleksja: szuka pierwszej właściwości typu string (np. Text,
        /// Content, Value) na obiekcie i zwraca "Nazwa=Wartość" - szybki
        /// podgląd zawartości nieznanego typu bez ręcznego wypisywania
        /// wszystkich właściwości API.
        /// </summary>
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

        private static void LogCandidateFace(View view, Face cutFace, List<Tekla.Structures.Geometry3d.Point> outerLoop, Action<string> log)
        {
            var centroid = Centroid(outerLoop);
            // Najdłuższa cięciwa = długość cięcia.
            (Tekla.Structures.Geometry3d.Point A, Tekla.Structures.Geometry3d.Point B) major = FindChord(outerLoop, centroid, longest: true);
            // Najkrótsza cięciwa PRZECHODZĄCA BLISKO ŚRODKA = szerokość cięcia.
            (Tekla.Structures.Geometry3d.Point A, Tekla.Structures.Geometry3d.Point B) minor = FindChord(outerLoop, centroid, longest: false);

            double majorLen = Distance(major.A, major.B);
            double minorLen = Distance(minor.A, minor.B);
            log($"[notch]   KANDYDAT ściana cięcia (Normal=({cutFace.Normal.X:F2};{cutFace.Normal.Y:F2};{cutFace.Normal.Z:F2})): długość={majorLen:F2} mm (model) szerokość={minorLen:F2} mm (model)");
            log($"[notch]     długość: {PointStr(major.A)} -> {PointStr(major.B)}");
            log($"[notch]     szerokość: {PointStr(minor.A)} -> {PointStr(minor.B)}");

            try
            {
                var cs = view.DisplayCoordinateSystem;
                log($"[notch]     długość w widoku: {PointStr(ToViewSpace(major.A, cs))} -> {PointStr(ToViewSpace(major.B, cs))}");
                log($"[notch]     szerokość w widoku: {PointStr(ToViewSpace(minor.A, cs))} -> {PointStr(ToViewSpace(minor.B, cs))}");
            }
            catch (Exception ex)
            {
                log("[notch]     przeliczenie na układ widoku nie powiodło się: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static double LoopSpan(List<Tekla.Structures.Geometry3d.Point> loop)
        {
            double max = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                for (int j = i + 1; j < loop.Count; j++)
                {
                    max = Math.Max(max, Distance(loop[i], loop[j]));
                }
            }
            return max;
        }

        private static Tekla.Structures.Geometry3d.Point Centroid(List<Tekla.Structures.Geometry3d.Point> loop)
        {
            double x = 0, y = 0, z = 0;
            foreach (var p in loop) { x += p.X; y += p.Y; z += p.Z; }
            return new Tekla.Structures.Geometry3d.Point(x / loop.Count, y / loop.Count, z / loop.Count);
        }

        // Cięciwa = para wierzchołków, których środek leży najbliżej centroidu
        // pętli (czyli faktycznie "przechodzi przez środek", niezależnie od
        // kolejności wierzchołków w pętli). Wśród takich par: najdłuższa albo
        // najkrótsza, zależnie od `longest`.
        private static (Tekla.Structures.Geometry3d.Point, Tekla.Structures.Geometry3d.Point) FindChord(
            List<Tekla.Structures.Geometry3d.Point> loop, Tekla.Structures.Geometry3d.Point centroid, bool longest)
        {
            double bestCenterOffset = double.MaxValue;
            var candidates = new List<(Tekla.Structures.Geometry3d.Point, Tekla.Structures.Geometry3d.Point, double)>();
            for (int i = 0; i < loop.Count; i++)
            {
                for (int j = i + 1; j < loop.Count; j++)
                {
                    var mid = new Tekla.Structures.Geometry3d.Point(
                        (loop[i].X + loop[j].X) / 2, (loop[i].Y + loop[j].Y) / 2, (loop[i].Z + loop[j].Z) / 2);
                    double offset = Distance(mid, centroid);
                    candidates.Add((loop[i], loop[j], offset));
                    bestCenterOffset = Math.Min(bestCenterOffset, offset);
                }
            }
            // Tolerancja: pary "przez środek" mają offset bliski najmniejszemu
            // znalezionemu (rzadko dokładnie zero przy dyskretnych wierzchołkach
            // elipsy) - 5% rozpiętości pętli jako margines.
            double tolerance = Math.Max(0.5, LoopSpan(loop) * 0.05);
            var throughCenter = candidates.FindAll(c => c.Item3 <= bestCenterOffset + tolerance);
            throughCenter.Sort((a, b) => Distance(a.Item1, a.Item2).CompareTo(Distance(b.Item1, b.Item2)));
            var pick = longest ? throughCenter[throughCenter.Count - 1] : throughCenter[0];
            return (pick.Item1, pick.Item2);
        }

        private static double Distance(Tekla.Structures.Geometry3d.Point a, Tekla.Structures.Geometry3d.Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static string PointStr(Tekla.Structures.Geometry3d.Point p) => $"({p.X:F2};{p.Y:F2};{p.Z:F2})";

        // Ręczny przelicznik (rzut na osie układu widoku) - CoordinateSystem
        // w Tekla Open API nie ma gotowej metody Transform, ale dokumentacja
        // DisplayCoordinateSystem wprost mówi, że służy do tego celu, więc
        // liczymy to standardowym wzorem projekcji na bazę ortonormalną
        // (Origin + znormalizowane AxisX/AxisY, AxisZ = iloczyn wektorowy).
        private static Tekla.Structures.Geometry3d.Point ToViewSpace(
            Tekla.Structures.Geometry3d.Point p, Tekla.Structures.Geometry3d.CoordinateSystem cs)
        {
            var rel = p - cs.Origin;
            var axisX = new Tekla.Structures.Geometry3d.Vector(cs.AxisX); axisX.Normalize();
            var axisY = new Tekla.Structures.Geometry3d.Vector(cs.AxisY); axisY.Normalize();
            var axisZ = Tekla.Structures.Geometry3d.Vector.Cross(axisX, axisY);
            var relVec = new Tekla.Structures.Geometry3d.Vector(rel.X, rel.Y, rel.Z);
            return new Tekla.Structures.Geometry3d.Point(relVec.Dot(axisX), relVec.Dot(axisY), relVec.Dot(axisZ));
        }
    }
}
