using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace PowershellExpectDriver
{
    static class PInvoke
    {
        // Console P/Invoke native functions
        internal const int STD_OUTPUT_HANDLE = -11;
        internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        internal const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
        internal delegate bool ConsoleEventDelegate(CtrlTypes ctrlType);
        internal enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
        [StructLayout(LayoutKind.Explicit)]
        public struct CHAR_INFO
        {
            [FieldOffset(0)] public char UnicodeChar;
            [FieldOffset(2)] public short Attributes;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }
        
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeFileHandle GetStdHandle(int nStdHandle);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleMode(SafeFileHandle hConsoleHandle, uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetConsoleMode(SafeFileHandle handle, out uint mode);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteConsole(
            IntPtr hConsoleOutput, 
            string lpBuffer, 
            uint nNumberOfCharsToWrite, 
            out uint lpNumberOfCharsWritten, 
            IntPtr lpReserved
        );
        
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool FreeConsole();
        
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        internal const int GWL_STYLE = -16;
        internal const int WS_THICKFRAME = 0x00040000;
        
        
        // PTY P/Invoke native constants
        internal const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        [StructLayout(LayoutKind.Sequential)]
        internal struct COORD
        {
            public short X;
            public short Y;
        }
        
        
        // PTY P/Invoke native functions
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int CreatePseudoConsole(
            COORD size, 
            SafeFileHandle hInput, 
            SafeFileHandle hOutput, 
            uint dwFlags, 
            out IntPtr phPC
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int ClosePseudoConsole(IntPtr hPC);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool CreatePipe(
            out SafeFileHandle hReadPipe, 
            out SafeFileHandle hWritePipe, 
            IntPtr lpPipeAttributes, 
            int nSize
        );
        
        
        // Process P/Invoke native functions
        internal const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        internal const uint CREATE_NEW_CONSOLE = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList, 
            int dwAttributeCount, 
            int dwFlags, 
            ref IntPtr lpSize
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList, 
            uint dwFlags, 
            IntPtr attribute, 
            IntPtr lpValue,
            IntPtr cbSize, 
            IntPtr lpPreviousValue, 
            IntPtr lpReturnSize
        );

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess(
            string? lpApplicationName, 
            string lpCommandLine, 
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes, 
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, 
            string? lpCurrentDirectory, 
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);
    }
}