using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;


[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr GetConsoleWindow();

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
const uint STARTF_USESTDHANDLES = 0x00000100;

const int SW_HIDE = 0;
const int SW_SHOW = 5;

public static void ToggleWindowVisibility(IntPtr consoleWindow)
{
    // Check the current visibility and toggle
    // Note: There's no direct way to check if a window is visible or hidden using these APIs,
    // so you might need to track the visibility state externally.
    // For demonstration, we'll just call ShowWindow with SW_SHOW to illustrate.
    // You would toggle between SW_HIDE and SW_SHOW based on the current state.
    ShowWindow(consoleWindow, SW_HIDE); // Change to SW_HIDE to hide
} 

public static void Go()
{
    // Create a security attributes structure that specifies an inheritable handle
    SECURITY_ATTRIBUTES saAttr = new SECURITY_ATTRIBUTES();
    saAttr.length = Marshal.SizeOf(saAttr);
    saAttr.bInheritHandle = true; // Allow the pipe handle to be inherited
    
    // Create a pipe for the child process's STDIN
    if (!CreatePipe(out IntPtr childStdInRead, out IntPtr childStdInWrite, ref saAttr, 0))
    {
        Console.WriteLine("Stdin CreatePipe failed");
        return;
    }
    
    SetHandleInformation(childStdInWrite, HANDLE_FLAG_INHERIT, 0);

    PROCESS_INFORMATION pInfo = new PROCESS_INFORMATION();
    STARTUPINFO sInfo = new STARTUPINFO();
    sInfo.hStdError = IntPtr.Zero; // Use these as necessary
    sInfo.hStdOutput = IntPtr.Zero;
    sInfo.hStdInput = childStdInRead;
    sInfo.dwFlags |= STARTF_USESTDHANDLES;
    sInfo.cb = (uint)Marshal.SizeOf(sInfo);

    // Change lpApplicationName and lpCommandLine as needed
    bool result = CreateProcess(null, "pwsh.exe", ref saAttr, ref saAttr, true, CREATE_NEW_CONSOLE, IntPtr.Zero, null, ref sInfo, out pInfo);
    
    /*System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(Convert.ToInt32(pInfo.dwProcessId));
    IntPtr windowHandle = process.MainWindowHandle;*/

    if (result)
    {
        // Close handles to the child process and its primary thread
        CloseHandle(pInfo.hProcess);
        CloseHandle(pInfo.hThread);

        // Write to the child process's standard input
        using (FileStream fs = new FileStream(new SafeFileHandle(childStdInWrite, true), FileAccess.Write))
        using (StreamWriter sw = new StreamWriter(fs))
        {
            sw.AutoFlush = true;
            sw.WriteLine("Clear-Host");
            sw.WriteLine("echo `e[93mHello from parent process!");
            System.Console.WriteLine("echo Sleeping");
            System.Threading.Thread.Sleep(5000);
            System.Console.WriteLine("echo Toggling");
            ToggleWindowVisibility(windowHandle);
            System.Threading.Thread.Sleep(60000);
            // Add any additional commands you wish to execute
        }

        // Close the pipe handle so the child process stops reading
        CloseHandle(childStdInWrite);
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