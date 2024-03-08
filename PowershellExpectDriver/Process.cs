using System.Runtime.InteropServices;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    static class ProcessFactory
    {
        // Start and configure a process. The return value represents the process and should be disposed.
        internal static Process Start(IntPtr attributes, IntPtr hPC, string command, string workingDirectory)
        {
            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, command, workingDirectory);
            return new Process(startupInfo, processInfo);
        }

        private static STARTUPINFOEX ConfigureProcessThread(IntPtr hPC, IntPtr attributes)
        {
            // this method implements the behavior described in https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session#preparing-for-creation-of-the-child-process

            var lpSize = IntPtr.Zero;
            var success = InitializeProcThreadAttributeList(
                IntPtr.Zero,
                1,
                0,
                ref lpSize
            );
            if (success || lpSize == IntPtr.Zero) // we're not expecting `success` here, we just want to get the calculated lpSize
                throw new InvalidOperationException("Could not calculate the number of bytes for the attribute list. " + Marshal.GetLastWin32Error());

            var startupInfo = new STARTUPINFOEX
            {
                StartupInfo = { cb = Marshal.SizeOf<STARTUPINFOEX>() },
                lpAttributeList = Marshal.AllocHGlobal(lpSize)
            };

            success = InitializeProcThreadAttributeList(
                startupInfo.lpAttributeList,
                1,
                0,
                ref lpSize
            );
            if (!success)
                throw new InvalidOperationException("Could not set up attribute list. " + Marshal.GetLastWin32Error());

            success = UpdateProcThreadAttribute(
                startupInfo.lpAttributeList,
                0,
                attributes,
                hPC,
                IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero
            );
            if (!success)
                throw new InvalidOperationException("Could not set pseudoconsole thread attribute. " + Marshal.GetLastWin32Error());

            return startupInfo;
        }

        private static PROCESS_INFORMATION RunProcess(ref STARTUPINFOEX sInfoEx, string command, string workingDirectory)
        {
            int securityAttributeSize = Marshal.SizeOf<SECURITY_ATTRIBUTES>();
            var pSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var tSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var success = CreateProcess(
                null,
                command,
                ref pSec,
                ref tSec,
                false,
                EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero,
                workingDirectory,
                ref sInfoEx,
                out PROCESS_INFORMATION pInfo
            );
            if (!success)
                throw new InvalidOperationException("Could not create process. " + Marshal.GetLastWin32Error());

            return pInfo;
        }
    }
    
    internal sealed class Process : IDisposable
    {
        private STARTUPINFOEX StartupInfo { get; }
        public PROCESS_INFORMATION ProcessInfo { get; }

        public Process(STARTUPINFOEX startupInfo, PROCESS_INFORMATION processInfo)
        {
            StartupInfo = startupInfo;
            ProcessInfo = processInfo;
        }

        public void Dispose()
        {
            if (StartupInfo.lpAttributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(StartupInfo.lpAttributeList);
                Marshal.FreeHGlobal(StartupInfo.lpAttributeList);
            }

            if (ProcessInfo.hProcess != IntPtr.Zero)
                CloseHandle(ProcessInfo.hProcess);
            
            if (ProcessInfo.hThread != IntPtr.Zero)
                CloseHandle(ProcessInfo.hThread);
        }
    }
}