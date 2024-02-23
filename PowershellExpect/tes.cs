using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;


[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

// Constants for SetHandleInformation
const uint HANDLE_FLAG_INHERIT = 0x00000001;

[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool CreateProcess(
    string lpApplicationName,
    string lpCommandLine,
    ref SECURITY_ATTRIBUTES lpProcessAttributes,
    ref SECURITY_ATTRIBUTES lpThreadAttributes,
    bool bInheritHandles,
    uint dwCreationFlags,
    IntPtr lpEnvironment,
    string lpCurrentDirectory,
    [In] ref STARTUPINFO lpStartupInfo,
    out PROCESS_INFORMATION lpProcessInformation);

[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool CloseHandle(IntPtr hObject);

public struct SECURITY_ATTRIBUTES
{
    public int length;
    public IntPtr lpSecurityDescriptor;
    public bool bInheritHandle;
}

public struct PROCESS_INFORMATION
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public uint dwProcessId;
    public uint dwThreadId;
}

public struct STARTUPINFO
{
    public uint cb;
    public string lpReserved;
    public string lpDesktop;
    public string lpTitle;
    public uint dwX;
    public uint dwY;
    public uint dwXSize;
    public uint dwYSize;
    public uint dwXCountChars;
    public uint dwYCountChars;
    public uint dwFillAttribute;
    public uint dwFlags;
    public short wShowWindow;
    public short cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
}

const uint CREATE_NEW_CONSOLE = 0x00000010;

public static void Go()
{
    SECURITY_ATTRIBUTES pSec = new SECURITY_ATTRIBUTES();
    SECURITY_ATTRIBUTES tSec = new SECURITY_ATTRIBUTES();
    pSec.length = Marshal.SizeOf(pSec);
    tSec.length = Marshal.SizeOf(tSec);

    PROCESS_INFORMATION pInfo = new PROCESS_INFORMATION();
    STARTUPINFO sInfo = new STARTUPINFO();
    sInfo.cb = (uint)Marshal.SizeOf(sInfo);

    // Change lpApplicationName and lpCommandLine as needed
    bool result = CreateProcess(null, "pwsh.exe", ref pSec, ref tSec, false, CREATE_NEW_CONSOLE, IntPtr.Zero, null, ref sInfo, out pInfo);

    if (result)
    {
        // Process has started
        Console.WriteLine($"Process started: PID = {pInfo.dwProcessId}");
        // Close handles when done
        CloseHandle(pInfo.hProcess);
        CloseHandle(pInfo.hThread);
    }
    else
    {
        // Handle error
        Console.WriteLine("Process start failed.");
    }
}

Go()