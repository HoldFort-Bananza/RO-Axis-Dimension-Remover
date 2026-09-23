using System;
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
    // [3.5013] - patrz CLAUDE.md). Ta klasa jest wywoływana bez człowieka
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
        // SetActiveDrawing(d, true) otwiera na ekranie (patrz ../CLAUDE.md,
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
                        var loops = face.GetLoopEnumerator();
                        while (loops.MoveNext())
                        {
                            if (!(loops.Current is Loop loop))
                            {
                                continue;
                            }
                            var vertices = loop.GetVertexEnumerator();
                            while (vertices.MoveNext())
                            {
                                if (!(vertices.Current is Tekla.Structures.Geometry3d.Point v))
                                {
                                    continue;
                                }
                                vertexCount++;
                                minX = Math.Min(minX, v.X);
                                maxX = Math.Max(maxX, v.X);
                            }
                        }
                        Log($"[notch]   ściana {faceIndex}: Normal=({face.Normal.X:F2};{face.Normal.Y:F2};{face.Normal.Z:F2}) wierzchołków={vertexCount} X-zakres=[{minX:F1};{maxX:F1}]");
                    }
                }
            }
        }
    }
}
