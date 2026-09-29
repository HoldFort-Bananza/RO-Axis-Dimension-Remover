using System;
using System.Windows.Forms;

namespace RoAxisDimensionRemover
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Tryb konsolowy do diagnostyki bez GUI - patrz DiagRunner.cs.
            // Przełącznik z brakującym "[Mark]" (albo nieznany) uruchamia GUI.
            string mark = args.Length > 1 ? args[1] : null;
            switch (args.Length > 0 ? args[0] : null)
            {
                case "--diag-active": DiagRunner.RunOnActiveDrawing(); return;
                case "--diag-notch": DiagRunner.RunNotchDiag(); return;
                case "--diag-dimension-style": DiagRunner.RunDimensionStyleDiag(); return;
                case "--diag-find-candidates": DiagRunner.RunFindCandidatesDiag(); return;
                case "--diag-mark" when mark != null: DiagRunner.RunOnMark(mark); return;
                case "--diag-notch-match" when mark != null: DiagRunner.RunNotchMatchDiag(mark); return;
                case "--diag-notch-insert-dryrun" when mark != null: DiagRunner.RunNotchInsertDryRun(mark); return;
                case "--diag-notch-fill-dryrun" when mark != null: DiagRunner.RunNotchFillDryRun(mark); return;
                case "--diag-notch-raw" when mark != null: DiagRunner.RunNotchRawDiag(mark); return;
                case "--diag-view-objects" when mark != null: DiagRunner.RunViewObjectsDiag(mark); return;
                case "--diag-view-bounds" when mark != null: DiagRunner.RunViewBoundsDiag(mark); return;
                case "--diag-connection" when mark != null: DiagRunner.RunConnectionDiag(mark); return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
