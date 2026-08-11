using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LumaPlayer
{
    internal static class MpvNative
    {
        internal const int MPV_FORMAT_FLAG = 3;
        internal const int MPV_FORMAT_INT64 = 4;
        internal const int MPV_FORMAT_DOUBLE = 5;

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr mpv_create();

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_initialize(IntPtr handle);

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_terminate_destroy(IntPtr handle);

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_option_string(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string value);

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_option(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int format,
            ref long value);

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_command(IntPtr handle, IntPtr arguments);

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_get_property")]
        internal static extern int mpv_get_property_double(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int format,
            out double value);

        [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_get_property")]
        internal static extern int mpv_get_property_flag(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int format,
            out int value);

        internal static int Command(IntPtr handle, params string[] arguments)
        {
            if (handle == IntPtr.Zero || arguments == null || arguments.Length == 0)
                return -1;

            List<IntPtr> strings = new List<IntPtr>();
            IntPtr array = IntPtr.Zero;

            try
            {
                for (int i = 0; i < arguments.Length; i++)
                    strings.Add(AllocateUtf8(arguments[i] ?? String.Empty));

                array = Marshal.AllocHGlobal(IntPtr.Size * (arguments.Length + 1));
                for (int i = 0; i < strings.Count; i++)
                    Marshal.WriteIntPtr(array, i * IntPtr.Size, strings[i]);
                Marshal.WriteIntPtr(array, arguments.Length * IntPtr.Size, IntPtr.Zero);

                return mpv_command(handle, array);
            }
            finally
            {
                if (array != IntPtr.Zero)
                    Marshal.FreeHGlobal(array);
                for (int i = 0; i < strings.Count; i++)
                    Marshal.FreeHGlobal(strings[i]);
            }
        }

        private static IntPtr AllocateUtf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }
    }
}
