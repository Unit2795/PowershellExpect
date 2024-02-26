using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    public class PTY
    {
        public event EventHandler<string> OutputReceived;
        
        private PTYPipe? inputPipe;
        private PTYPipe? outputPipe;
        private PTYHandler? ptyProcess;
        private Process? pwshProcess;
        private System.Diagnostics.Process observerProcess;
        private System.IO.Pipes.NamedPipeClientStream? pipeClient;
        private StreamWriter? pipeWriter;
        private string vtlog = "";

        /*public PTY()
        {
            EnableVirtualTerminalSequenceProcessing();
        }*/

        public void Run(string workingDirectory = "/")
        {
            inputPipe = new PTYPipe();
            outputPipe = new PTYPipe();
            ptyProcess = PTYHandler.Create(inputPipe.ReadSide, outputPipe.WriteSide, 120, 50);
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

        public void CreateObserver()
        {
            observerProcess = new System.Diagnostics.Process();
            
            string scriptContent = @"
                # In the observer PowerShell window
                $pipeName = 'observer'
                $pipe = new-object System.IO.Pipes.NamedPipeServerStream($pipeName, [System.IO.Pipes.PipeDirection]::In)
                $pipe.WaitForConnection()

                $reader = New-Object System.IO.StreamReader($pipe)
                
                while ($true) {
                    $line = $reader.Readline()
                    if ($line -ne $null) {
                        Write-Host $line -NoNewline
                    }
                }
            ";
            
            observerProcess.StartInfo.FileName = "pwsh.exe";
            observerProcess.StartInfo.Arguments = $"-ExecutionPolicy Bypass -Command {scriptContent}";
            observerProcess.StartInfo.UseShellExecute = true;
            observerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            observerProcess.StartInfo.CreateNoWindow = false;
            
            observerProcess.Start();
            
            string pipeName = "observer";
            pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipeClient.Connect(30000);
            pipeWriter = new StreamWriter(pipeClient);
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
                string outputChunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                pipeWriter.WriteAsync(outputChunk);
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

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING ;
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