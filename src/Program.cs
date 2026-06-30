using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Pasha
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [STAThread]
        private static void Main()
        {
            // 高DPI / マルチモニタで座標がずれないように
            try { if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) SetProcessDPIAware(); }
            catch { try { SetProcessDPIAware(); } catch { } }

            bool createdNew;
            using (var mutex = new Mutex(true, "Pasha_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Pasha は既に起動しています。", "Pasha",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppController());
            }
        }
    }
}
