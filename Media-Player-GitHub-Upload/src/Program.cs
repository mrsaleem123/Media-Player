using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
        private const string InstanceMutexName = "Local\\LumaPlayer.SingleInstance.B06AF750";
        private const string PipeName = "LumaPlayer.B06AF750.MediaOpen";
        private static Mutex instanceMutex;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [STAThread]
        private static void Main(string[] args)
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool explicitNewWindow = false;
            string initialFile = null;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (String.Equals(args[i], "--new-window", StringComparison.OrdinalIgnoreCase))
                        explicitNewWindow = true;
                    else if (initialFile == null)
                        initialFile = args[i];
                }
            }

            bool forceNewWindow = explicitNewWindow || (GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
            if (!forceNewWindow)
            {
                bool isPrimary;
                instanceMutex = new Mutex(true, InstanceMutexName, out isPrimary);
                if (!isPrimary)
                {
                    SendToPrimaryInstance(initialFile);
                    instanceMutex.Dispose();
                    instanceMutex = null;
                    return;
                }
            }

            PlayerForm player = new PlayerForm(initialFile);
            if (!forceNewWindow)
            {
                IntPtr ignoredHandle = player.Handle;
                StartPipeServer(player);
            }
            Application.Run(player);

            if (instanceMutex != null)
            {
                try { instanceMutex.ReleaseMutex(); }
                catch { }
                instanceMutex.Dispose();
                instanceMutex = null;
            }
        }

        private static void SendToPrimaryInstance(string path)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        client.Connect(800);
                        using (StreamWriter writer = new StreamWriter(client, new UTF8Encoding(false)))
                        {
                            writer.AutoFlush = true;
                            writer.WriteLine(path ?? String.Empty);
                        }
                    }
                    return;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        private static void StartPipeServer(PlayerForm player)
        {
            Thread serverThread = new Thread(delegate()
            {
                while (!player.IsDisposed)
                {
                    try
                    {
                        string requestedFile;
                        using (NamedPipeServerStream server = new NamedPipeServerStream(
                            PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            server.WaitForConnection();
                            using (StreamReader reader = new StreamReader(server, Encoding.UTF8))
                                requestedFile = reader.ReadLine();
                        }

                        if (player.IsDisposed) return;
                        string capturedFile = requestedFile;
                        player.BeginInvoke(new Action(delegate { player.OpenExternalFile(capturedFile); }));
                    }
                    catch
                    {
                        if (player.IsDisposed) return;
                    }
                }
            });
            serverThread.IsBackground = true;
            serverThread.Name = "Luma Player media-open listener";
            serverThread.Start();
        }
    }
}
