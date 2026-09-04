using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace RoAxisDimensionRemover
{
    /// <summary>
    /// Po kliknięciu "Usuń" użytkownik chce od razu nacisnąć Ctrl+Z w Tekli,
    /// jeśli wynik wygląda źle - ale fokus systemu Windows zostaje na tym
    /// programie, więc Ctrl+Z poszłoby w pustkę (albo w log/przycisk).
    /// Tekla Open API nie ma metody "aktywuj główne okno" - to zwykłe okno
    /// Win32 osobnego procesu, więc przełączamy fokus przez user32, nie
    /// przez API modelu/rysunku.
    /// </summary>
    internal static class TeklaWindowFocus
    {
        private const string TeklaProcessName = "TeklaStructures";
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        /// <summary>
        /// Świadomie cicho przy niepowodzeniu (log opcjonalny) - to funkcja
        /// wygody, nie krytyczna operacja. Brak znalezionego okna Tekli albo
        /// odmowa Windows (SetForegroundWindow ma własne restrykcje) nie
        /// powinny przerywać działania programu.
        /// </summary>
        public static void BringToFront(Action<string> log = null)
        {
            try
            {
                var process = Process.GetProcessesByName(TeklaProcessName)
                    .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
                if (process == null)
                {
                    log?.Invoke("Nie znaleziono okna Tekla Structures do sfokusowania.");
                    return;
                }

                IntPtr handle = process.MainWindowHandle;
                if (IsIconic(handle))
                {
                    ShowWindow(handle, SW_RESTORE);
                }
                SetForegroundWindow(handle);
            }
            catch (Exception ex)
            {
                log?.Invoke("Nie udało się sfokusować okna Tekli: " + ex.Message);
            }
        }
    }
}
