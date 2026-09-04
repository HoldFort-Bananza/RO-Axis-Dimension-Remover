using System;
using Tekla.Structures.Drawing;

namespace RoAxisDimensionRemover
{
    // Rusztowanie diagnostyczne (jak Inspector.cs) - USUNĄĆ przed wydaniem.
    // Automatyzacja (Claude Code) nie umie klikać przycisku w MainForm, więc
    // to jedyny sposób odpalić dry-run na aktywnym rysunku bez człowieka przy
    // GUI. dryRun jest tu na sztywno true - z linii komend nie da się tego
    // przełączyć, więc ten tryb nigdy nie kasuje.
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

        private static void RunOn(Drawing drawing, Action<string> log)
        {
            log($"Aktywny rysunek: {drawing.Mark} / {drawing.Name}");
            var service = new RoAxisDimensionService();
            var result = service.RemoveRedundantAxisDimensions(drawing, log, dryRun: true);
            log($"Gotowe. Widoków: {result.ViewsChecked}, znalezionych kandydatów do usunięcia: {result.RemovedCount}.");
        }
    }
}
