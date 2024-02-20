using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Runtime.InteropServices;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    public class PTY
    {
        public event EventHandler<string> OutputReceived;
        
        private PseudoConsolePipe? inputPipe;
        private PseudoConsolePipe? outputPipe;

        /*public PTY()
        {
            EnableVirtualTerminalSequenceProcessing();
        }*/

        public void Run(string workingDirectory = "/")
        {
            inputPipe = new PseudoConsolePipe();
            outputPipe = new PseudoConsolePipe();
            var pseudoConsole = PseudoConsole.Create(inputPipe.ReadSide, outputPipe.WriteSide,
                (short)Console.WindowWidth, (short)Console.WindowHeight);
            var process = ProcessFactory.Start(PseudoConsole.PseudoConsoleThreadAttribute, pseudoConsole.Handle,
                workingDirectory);
            
            Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));
            
            // free resources in case the console is ungracefully closed (e.g. by the 'x' in the window titlebar)
            OnClose(() => DisposeResources(process, pseudoConsole, outputPipe, inputPipe));

            var waitHandle = new TaskCompletionSource<bool>();
            new Thread(() =>
            {
                WaitForExit(process).WaitOne();
                waitHandle.SetResult(true);
                waitHandle.Task.ContinueWith((_) =>
                {
                    // Ensure all async operations are complete before disposing resources.
                    DisposeResources(process, pseudoConsole, outputPipe, inputPipe);
                });
            }).Start();
        }
        
        public void CopyInputToPipe(string command, bool noNewline = false)
        {
            var writer = new StreamWriter(new FileStream(inputPipe!.WriteSide, FileAccess.Write));
            writer.AutoFlush = true;
            
            if (!noNewline)
            {
                // Append the appropriate newline character(s) based on the operating system.
                command += Environment.NewLine;
            }
            
            writer.Write(command);
        }
        
        // Read the child process output and write it to an event handler for processing
        private async void CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            using (var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read))
            {
                var buffer = new byte[4096]; // 4KB default buffer size
                int bytesRead;
            
                while ((bytesRead = await pseudoConsoleOutput.ReadAsync(buffer, 0, buffer.Length)) > 0) 
                {
                    string outputChunk = Encoding.Default.GetString(buffer, 0, bytesRead);
                    OutputReceived?.Invoke(null,  outputChunk);
                }
            }
        }
        
        private static void EnableVirtualTerminalSequenceProcessing()
        {
            var hStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(hStdOut, out uint outConsoleMode))
            {
                throw new InvalidOperationException("Could not get console mode");
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            if (!SetConsoleMode(hStdOut, outConsoleMode))
            {
                throw new InvalidOperationException("Could not enable virtual terminal processing");
            }
        }
        
        private static void OnClose(Action handler)
        {
            SetConsoleCtrlHandler(eventType =>
            {
                if(eventType == CtrlTypes.CTRL_CLOSE_EVENT)
                {
                    handler();
                }
                return false;
            }, true);
        }
        
        private void DisposeResources(params IDisposable[] disposables)
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }
        
        private static AutoResetEvent WaitForExit(Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };
    }
    
    internal sealed class PseudoConsolePipe : IDisposable
    {
        public readonly SafeFileHandle ReadSide;
        public readonly SafeFileHandle WriteSide;

        public PseudoConsolePipe()
        {
            if (!CreatePipe(out ReadSide, out WriteSide, IntPtr.Zero, 0))
            {
                throw new InvalidOperationException("failed to create pipe");
            }
        }

        #region IDisposable

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReadSide?.Dispose();
                WriteSide?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
    
    internal sealed class PseudoConsole : IDisposable
    {
        public static readonly IntPtr PseudoConsoleThreadAttribute = (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        public IntPtr Handle { get; }

        private PseudoConsole(IntPtr handle)
        {
            this.Handle = handle;
        }

        internal static PseudoConsole Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, int width, int height)
        {
            var createResult = CreatePseudoConsole(
                new COORD { X = (short)width, Y = (short)height },
                inputReadSide, outputWriteSide,
                0, out IntPtr hPC);
            if(createResult != 0)
            {
                throw new InvalidOperationException("Could not create pseudo console. Error Code " + createResult);
            }
            return new PseudoConsole(hPC);
        }

        public void Dispose()
        {
            ClosePseudoConsole(Handle);
        }
    }
    
    static class ProcessFactory
    {
        /// <summary>
        /// Start and configure a process. The return value represents the process and should be disposed.
        /// </summary>
        internal static Process Start(IntPtr attributes, IntPtr hPC, string workingDirectory = "/")
        {
            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, workingDirectory);
            return new Process(startupInfo, processInfo);
        }

        private static STARTUPINFOEX ConfigureProcessThread(IntPtr hPC, IntPtr attributes)
        {
            // this method implements the behavior described in https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session#preparing-for-creation-of-the-child-process

            var lpSize = IntPtr.Zero;
            var success = InitializeProcThreadAttributeList(
                lpAttributeList: IntPtr.Zero,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );
            if (success || lpSize == IntPtr.Zero) // we're not expecting `success` here, we just want to get the calculated lpSize
            {
                throw new InvalidOperationException("Could not calculate the number of bytes for the attribute list. " + Marshal.GetLastWin32Error());
            }

            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

            success = InitializeProcThreadAttributeList(
                lpAttributeList: startupInfo.lpAttributeList,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not set up attribute list. " + Marshal.GetLastWin32Error());
            }

            success = UpdateProcThreadAttribute(
                lpAttributeList: startupInfo.lpAttributeList,
                dwFlags: 0,
                attribute: attributes,
                lpValue: hPC,
                cbSize: (IntPtr)IntPtr.Size,
                lpPreviousValue: IntPtr.Zero,
                lpReturnSize: IntPtr.Zero
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not set pseudoconsole thread attribute. " + Marshal.GetLastWin32Error());
            }

            return startupInfo;
        }

        private static PROCESS_INFORMATION RunProcess(ref STARTUPINFOEX sInfoEx, string workingDirectory = "/")
        {
            int securityAttributeSize = Marshal.SizeOf<SECURITY_ATTRIBUTES>();
            var pSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var tSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var success = CreateProcess(
                lpApplicationName: null,
                lpCommandLine: "pwsh.exe",
                lpProcessAttributes: ref pSec,
                lpThreadAttributes: ref tSec,
                bInheritHandles: false,
                dwCreationFlags: EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment: IntPtr.Zero,
                lpCurrentDirectory: null,
                lpStartupInfo: ref sInfoEx,
                lpProcessInformation: out PROCESS_INFORMATION pInfo
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not create process. " + Marshal.GetLastWin32Error());
            }

            return pInfo;
        }
    }
    
    internal sealed class Process : IDisposable
    {
        public Process(STARTUPINFOEX startupInfo, PROCESS_INFORMATION processInfo)
        {
            StartupInfo = startupInfo;
            ProcessInfo = processInfo;
        }

        public STARTUPINFOEX StartupInfo { get; }
        public PROCESS_INFORMATION ProcessInfo { get; }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                }

                // dispose unmanaged state

                // Free the attribute list
                if (StartupInfo.lpAttributeList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(StartupInfo.lpAttributeList);
                    Marshal.FreeHGlobal(StartupInfo.lpAttributeList);
                }

                // Close process and thread handles
                if (ProcessInfo.hProcess != IntPtr.Zero)
                {
                    CloseHandle(ProcessInfo.hProcess);
                }
                if (ProcessInfo.hThread != IntPtr.Zero)
                {
                    CloseHandle(ProcessInfo.hThread);
                }

                disposedValue = true;
            }
        }

        ~Process()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // use the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}