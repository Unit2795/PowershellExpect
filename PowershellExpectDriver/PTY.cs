using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    public class PipeServer
    {
        public void InitPipe()
        {
            Task.Run(() => PipeClient()); // Run the listening loop in a separate task
        }
        
        private async Task PipeClient()
        {
            var pipeClient = new NamedPipeClientStream(".", "observer", PipeDirection.In);
            await pipeClient.ConnectAsync();
            var terminalOutput = Console.OpenStandardOutput();
            
            var buffer = new byte[4096];
            int bytesRead;

            // Read from the pipe and write directly to the terminal output
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
            ptyProcess = PTYHandler.Create(inputPipe.ReadSide, outputPipe.WriteSide, 1200, 300);
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
                Import-Module 'C:\Repositories\PowershellExpect\PowershellExpect\PowershellExpectDriver.dll'

                [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
                
                $pipeServer = New-Object PowershellExpectDriver.PipeServer

                Clear-Host
                
                $pipeServer.InitPipe();
            ";
            
            observerProcess.StartInfo.FileName = "pwsh.exe";
            observerProcess.StartInfo.Arguments = $"-NoExit -ExecutionPolicy Bypass -Command {scriptContent}";
            observerProcess.StartInfo.UseShellExecute = true;
            observerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            observerProcess.StartInfo.CreateNoWindow = false;
            
            observerProcess.Start();
            
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