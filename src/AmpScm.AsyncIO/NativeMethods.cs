using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace AmpScm.AsyncIO
{
    static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFileW(
            [MarshalAs(UnmanagedType.LPWStr)] string filename,
            uint access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
            IntPtr templateFile);

        public delegate void IOCompletionCallback(uint errorCode, uint numBytes, IntPtr lpOverlapped);

        [DllImport("kernel32.dll")]
        public static extern bool ReadFileEx(SafeFileHandle hFile, [Out] byte[] lpBuffer,
            int nNumberOfBytesToRead, [In] IntPtr lpOverlapped,
            IOCompletionCallback lpCompletionRoutine);
    }
}
