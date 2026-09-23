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
                    Log($"[dim-style] widok {viewIndex}, wymiar {dimensionIndex}: " +
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

            Face cutFace = null;
            List<Tekla.Structures.Geometry3d.Point> outerLoop = null;
            var faces = solid.GetFaceEnumerator();
            while (faces.MoveNext())
            {
                if (!(faces.Current is Face face))
                {
                    continue;
                }

                // Ściana cięcia ma >1 pętlę (profil pusty w środku) i - jeśli
                // znamy oś belki - normalna NIE jest równoległa do osi (to
                // odróżnia ukośne cięcie w złączu od prostego przycięcia na
                // końcu, patrz [35021]: ściana 25 kontra 26).
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

                // Zewnętrzny obrys = ten o większym "rozstawie" (odporne na
                // to, która pętla jest zwrócona jako pierwsza).
                foreach (var loop in loopsInFace)
                {
                    if (outerLoop == null || LoopSpan(loop) > LoopSpan(outerLoop))
                    {
                        outerLoop = loop;
                        cutFace = face;
                    }
                }
            }

            if (cutFace == null || outerLoop == null)
            {
                log("[notch]   brak kandydata na ścianę cięcia w tej bryle (być może prosty koniec bez ukośnego złącza).");
                return;
            }

            var centroid = Centroid(outerLoop);
            // Najdłuższa cięciwa = długość cięcia.
            (Tekla.Structures.Geometry3d.Point A, Tekla.Structures.Geometry3d.Point B) major = FindChord(outerLoop, centroid, longest: true);
            // Najkrótsza cięciwa PRZECHODZĄCA BLISKO ŚRODKA = szerokość cięcia.
            (Tekla.Structures.Geometry3d.Point A, Tekla.Structures.Geometry3d.Point B) minor = FindChord(outerLoop, centroid, longest: false);

            double majorLen = Distance(major.A, major.B);
            double minorLen = Distance(minor.A, minor.B);
            log($"[notch]   KANDYDAT ściana cięcia: długość={majorLen:F2} mm (model) szerokość={minorLen:F2} mm (model)");
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
