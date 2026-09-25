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
            if (args.Length > 0 && args[0] == "--diag-active")
            {
                DiagRunner.RunOnActiveDrawing();
                return;
            }
            if (args.Length > 1 && args[0] == "--diag-mark")
            {
                DiagRunner.RunOnMark(args[1]);
                return;
            }
            if (args.Length > 0 && args[0] == "--diag-notch")
            {
                DiagRunner.RunNotchDiag();
                return;
            }
            if (args.Length > 0 && args[0] == "--diag-dimension-style")
            {
                DiagRunner.RunDimensionStyleDiag();
                return;
            }
            if (args.Length > 1 && args[0] == "--diag-notch-match")
            {
                DiagRunner.RunNotchMatchDiag(args[1]);
                return;
            }
            if (args.Length > 1 && args[0] == "--diag-notch-insert-dryrun")
            {
                DiagRunner.RunNotchInsertDryRun(args[1]);
                return;
            }
            if (args.Length > 1 && args[0] == "--diag-view-objects")
            {
                DiagRunner.RunViewObjectsDiag(args[1]);
                return;
            }
            if (args.Length > 1 && args[0] == "--diag-notch-raw")
            {
                DiagRunner.RunNotchRawDiag(args[1]);
                return;
            }
            if (args.Length > 1 && args[0] == "--diag-connection")
            {
                DiagRunner.RunConnectionDiag(args[1]);
                return;
            }
            if (args.Length > 0 && args[0] == "--diag-find-candidates")
            {
                DiagRunner.RunFindCandidatesDiag();
                return;
            }
            if (args.Length > 1 && args[0] == "--diag-notch-fill-dryrun")
            {
                DiagRunner.RunNotchFillDryRun(args[1]);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
