using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Luma Player")]
[assembly: AssemblyProduct("Luma Player")]
[assembly: AssemblyDescription("Lightweight hardware-accelerated media player")]
[assembly: AssemblyCompany("Luma Player")]
[assembly: AssemblyVersion("0.6.0.0")]
[assembly: AssemblyFileVersion("0.6.0.0")]

namespace LumaPlayer
{
    internal static class Program
    {
        internal const string DisplayVersion = "0.6";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [STAThread]
        private static void Main(string[] args)
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PlayerForm(args != null && args.Length > 0 ? args[0] : null));
        }
    }
}
