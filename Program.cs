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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
