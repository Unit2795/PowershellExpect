using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    public class PipeServer
    {
        public void InitPipe()
        {
            Task.Run(() => PipeClient());
        }
        
        private async Task PipeClient()
        {
            var pipeClient = new NamedPipeClientStream(".", "observer", PipeDirection.In);
            await pipeClient.ConnectAsync();
            var terminalOutput = Console.OpenStandardOutput();
            
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = pipeClient.Read(buffer, 0, buffer.Length)) > 0)
            {
                await terminalOutput.WriteAsync(buffer, 0, bytesRead);
            }
        }
    }
    
    public class PTY
    {
        public event EventHandler<string> OutputReceived;
        
        private PTYPipe? inputPipe;
        private PTYPipe? outputPipe;
        private PTYHandler? ptyProcess;
        private Process? pwshProcess;
        private System.Diagnostics.Process observerProcess;
        private System.IO.Pipes.NamedPipeServerStream? pipeServer;
        private StreamWriter? pipeWriter;

        /*public PTY()
        {
            EnableVirtualTerminalSequenceProcessing();
        }*/

        public void Run(string workingDirectory = "/")
        {
            inputPipe = new PTYPipe();
            outputPipe = new PTYPipe();
            ptyProcess = PTYHandler.Create(inputPipe.ReadSide, outputPipe.WriteSide, 120, 30);
            pwshProcess = ProcessFactory.Start(PTYHandler.PseudoConsoleThreadAttribute, ptyProcess.Handle, workingDirectory);

            CreateObserver();
            
            Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));
            
            // Free resources if case the console is ungracefully closed (e.g. by the 'x' in the window titlebar or CTRL+C)
            OnClose(() => DisposeResources());

            // Observe process and dispose resources when it exits
            var waitHandle = new TaskCompletionSource<bool>();
            new Thread(() =>
            {
                // Block the thread until the process exits
                WaitForExit(pwshProcess).WaitOne();
                // Ensure all async operations are complete before disposing resources.
                waitHandle.SetResult(true);
                waitHandle.Task.ContinueWith((_) =>
                {
                    DisposeResources();
                });
            }).Start();
        }
        
        public void CopyInputToPipe(string command, bool noNewline = false)
        {
            var writer = new StreamWriter(new FileStream(inputPipe!.WriteSide, FileAccess.Write));
            
            if (noNewline)
            {
                writer.Write(command);
            }
            else
            {
                writer.WriteLine(command);
            }
            writer.Flush();
        }
        
        static string GenerateRandomHash()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] randomBytes = new byte[32]; // 256 bits
                rng.GetBytes(randomBytes);
                return BitConverter.ToString(randomBytes).Replace("-", "").ToLower();
            }
        }

        static int GenerateRandomNumber(int min, int max)
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] randomNumber = new byte[4];
                rng.GetBytes(randomNumber);
                int value = Math.Abs(BitConverter.ToInt32(randomNumber, 0));
                return (value % (max - min + 1)) + min;
            }
        }

        static long GetCurrentUnixTime()
        {
            return ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
        }
        
        static string GenerateUniqueWindowTitle()
        {
            return "PowerShellExpect - " + GenerateRandomHash() + GenerateRandomNumber(10000, 99999) + GetCurrentUnixTime();
        }

        public void CreateObserver()
        {
            observerProcess = new System.Diagnostics.Process();

            // Generate a unique window title to identify the observer terminal
            var windowTitle = GenerateUniqueWindowTitle();

            // Set window size to match PTY, set window title, set output encoding to UTF-8, and initialize the named pipe server
            string scriptContent = $@"
                Import-Module 'C:\Repositories\PowershellExpect\PowershellExpect\PowershellExpectDriver.dll'

                function prompt {{ '' }}
                $Host.UI.RawUI.WindowSize.Height = 30
                $Host.UI.RawUI.WindowSize.Width = 120
                $Host.UI.RawUI.BufferSize.Height = 30
                $Host.UI.RawUI.BufferSize.Width = 120

                $Host.UI.RawUI.WindowTitle = '{windowTitle}'

                [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
                
                $pipeServer = New-Object PowershellExpectDriver.PipeServer
                
                $pipeServer.InitPipe()

                Clear-Host
            ";
            
            observerProcess.StartInfo.FileName = "pwsh.exe";
            observerProcess.StartInfo.Arguments = $"-NoExit -ExecutionPolicy Bypass -Command {scriptContent}";
            observerProcess.StartInfo.UseShellExecute = true;
            observerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            observerProcess.StartInfo.CreateNoWindow = false;
            
            observerProcess.Start();

            // Fetch window handle and set its style (prevent resizing to keep in sync with the PTY window size)
            var timeout = 10;
            var stopwatch = Stopwatch.StartNew();
            IntPtr hWnd = IntPtr.Zero;
            while (stopwatch.Elapsed.TotalSeconds < timeout)
            {
                hWnd = FindWindow(null, windowTitle);
                if (hWnd != IntPtr.Zero)
                {
                    int style = GetWindowLong(hWnd, GWL_STYLE);
                    int newStyle = style & ~WS_THICKFRAME;
                    SetWindowLong(hWnd, GWL_STYLE, newStyle);
                    break;
                }

                Thread.Sleep(500);
            }
            if (hWnd == IntPtr.Zero)
            {
                throw new Exception("Could not find observer terminal to set its style");
            }
            
            // Create a named pipe server to send PTY output to the observer terminal
            string pipeName = "observer";
            pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1 , PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            pipeServer.WaitForConnection();
            pipeWriter = new StreamWriter(pipeServer);
            pipeWriter.AutoFlush = true;
        }
        
        // Read the child process output and write it to an event handler for processing
        private async Task CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            const int bufferLength = 4096;
            var buffer = new byte[bufferLength];
            int bytesRead;

            var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read);
            
            while ((bytesRead = await pseudoConsoleOutput.ReadAsync(buffer, 0, bufferLength)) > 0)
            {
                await pipeServer.WriteAsync(buffer, 0, bytesRead);
                
                string outputChunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                OnOutputReceived(outputChunk);
            }
        }
        
        protected virtual void OnOutputReceived(string outputChunk)
        {
            var handler = OutputReceived;
            OutputReceived?.Invoke(this, outputChunk);
        }
        
        private static void EnableVirtualTerminalSequenceProcessing()
        {
            var hStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(hStdOut, out uint outConsoleMode))
            {
                throw new InvalidOperationException("Could not get console mode");
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
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
        
        public void DisposeResources()
        {
            observerProcess.Close();
            
            Console.ResetColor();
            
            var disposables = new List<IDisposable?> { ptyProcess, pwshProcess, outputPipe, inputPipe };
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }
        
        private static AutoResetEvent WaitForExit(Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };
    }
    
    internal sealed class PTYPipe : IDisposable
    {
        public readonly SafeFileHandle ReadSide;
        public readonly SafeFileHandle WriteSide;

        public PTYPipe()
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
    
    internal sealed class PTYHandler : IDisposable
    {
        public static readonly IntPtr PseudoConsoleThreadAttribute = (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        public IntPtr Handle { get; }

        private PTYHandler(IntPtr handle)
        {
            this.Handle = handle;
        }

        internal static PTYHandler Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, int width, int height)
        {
            var createResult = CreatePseudoConsole(
                new COORD {
                    X = (short)width, 
                    Y = (short)height
                },
                inputReadSide, 
                outputWriteSide,
                0, 
                out IntPtr hPC
            );
            if(createResult != 0)
            {
                throw new InvalidOperationException("Could not create pseudo console. Error Code " + createResult);
            }
            return new PTYHandler(hPC);
        }

        public void Dispose()
        {
            ClosePseudoConsole(Handle);
        }
    }
}