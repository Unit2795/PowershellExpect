using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    public class PTY
    {
        // Event handler for output received from the PTY/child process
        public event EventHandler<string>? HandleOutput;
        
        // Pipes for input and output to the PTY
        private PTYPipe? inputPipe;
        private PTYPipe? outputPipe;
        // PTY process and child process
        private PTYHandler? ptyProcess;
        private Process? pwshProcess;
        // Buffer for storing PTY output
        private TerminalBuffer terminalBuffer = new();
        // Mutex for halting output reading while observer terminal is booting
        private bool readPaused = false;
        private string sessionId = GenerateSessionId();
        private string? dllDirectory;
        private string dllPath = Assembly.GetExecutingAssembly().Location;
        
        private System.Diagnostics.Process? observerProcess;
        private IntPtr observerHandle = IntPtr.Zero;
        private System.IO.Pipes.NamedPipeServerStream? observerOutput;
        private System.IO.Pipes.NamedPipeServerStream? observerInput;
        private string observerOutputPipeName;
        private string observerInputPipeName;
        
        public PTY()
        {
            observerOutputPipeName = $"{sessionId}ObserverOutput";
            observerInputPipeName = $"{sessionId}ObserverInput";

            dllDirectory = Path.GetDirectoryName(dllPath);
            // TODO: Use this to load observer script or remove it
            // childScriptPath = Path.Join(dllDirectory, "ChildScript.ps1");
        }
        
        // Spawn a new PTY and child process
        public void Spawn()
        {
            inputPipe = new PTYPipe();
            outputPipe = new PTYPipe();
            ptyProcess = PTYHandler.Create(inputPipe.ReadSide, outputPipe.WriteSide, 120, 30);
            pwshProcess = ProcessFactory.Start(PTYHandler.PseudoConsoleThreadAttribute, ptyProcess.Handle);
            
            Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));
            
            // Free resources if case the console is ungracefully closed (e.g. by the 'x' in the window titlebar or CTRL+C)
            OnClose(DisposeResources);

            // Observe process and dispose resources when it exits
            var waitHandle = new TaskCompletionSource<bool>();
            new Thread(() =>
            {
                // Block the thread until the process exits
                WaitForExit(pwshProcess).WaitOne();
                // Ensure all async operations are complete before disposing resources.
                waitHandle.SetResult(true);
                waitHandle.Task.ContinueWith((_) => DisposeResources());
            }).Start(); 
        }
        
        // Sends input to the PTY input pipe
        public void CopyInputToPipe(string command, bool noNewline = false)
        {
            var writer = new StreamWriter(new FileStream(inputPipe!.WriteSide, FileAccess.Write));
            
            if (noNewline)
                writer.Write(command);
            else
                writer.WriteLine(command);

            writer.Flush();
        }
        
        // Read the PTY output pipe, writes it to event handler for processing, and forwards it
        private async Task CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            const int bufferLength = 4096;
            var byteBuffer = new byte[bufferLength];
            int bytesRead;

            var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read);
            
            while ((bytesRead = await pseudoConsoleOutput.ReadAsync(byteBuffer.AsMemory(0, bufferLength))) > 0)
            {
                while (readPaused)
                    await Task.Delay(500);
                
                terminalBuffer.Append(byteBuffer.AsMemory(0, bytesRead));
                
                if (observerOutput != null)
                    await observerOutput.WriteAsync(byteBuffer.AsMemory(0, bytesRead));
                
                string outputChunk = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
                OnOutputReceived(outputChunk);
            }
        }
        
        protected virtual void OnOutputReceived(string outputChunk)
        {
            var handler = HandleOutput;
            handler?.Invoke(this, outputChunk);
        }
        
        private static void OnClose(Action handler)
        {
            SetConsoleCtrlHandler(eventType =>
            {
                if(eventType == CtrlTypes.CTRL_CLOSE_EVENT)
                    handler();
                
                return false;
            }, true);
        }
        
        public void DisposeResources()
        {
            Console.ResetColor();
            
            observerOutput?.Dispose();
            observerInput?.Dispose();
            observerProcess?.Kill();
            ptyProcess?.Dispose();
            pwshProcess?.Dispose();
            outputPipe?.Dispose();
            inputPipe?.Dispose();
        }
        
        private static AutoResetEvent WaitForExit(Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };
        
        // Reads input from the observer terminal and writes it to the PTY input pipe
        public async void ObserverInput()
        {
            const int bufferLength = 4096;
            var byteBuffer = new byte[bufferLength];
            int bytesRead;
            observerInput = new NamedPipeServerStream(observerInputPipeName, PipeDirection.In);

            await observerInput.WaitForConnectionAsync();
            while ((bytesRead = await observerInput.ReadAsync(byteBuffer, 0, bufferLength)) > 0)
            {
                string outputChunk = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
                CopyInputToPipe(outputChunk, true);
            }
        }
        
        private static string GenerateSessionId() => $"PE-{Guid.NewGuid()}-{RandomNumberGenerator.GetInt32(100000)}";
        
        public void ShowObserver() => ShowWindow(observerHandle, 5);
        
        public void HideObserver() => ShowWindow(observerHandle, 0);

        public void CreateObserver()
        {
            readPaused = true;
            
            terminalBuffer.Flush();
            
            observerProcess = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    FileName = "pwsh.exe",
                    Arguments = $"-NoExit -ExecutionPolicy Bypass -Command {GetObserverScript()}",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false
                }
            };
            
            observerProcess.Start();
            
            // Create a named pipe server to send PTY output to the observer terminal
            observerOutput = new NamedPipeServerStream(observerOutputPipeName, PipeDirection.Out, 1 , PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            observerOutput.WaitForConnection();
            
            Task.Run(ObserverInput);
            
            // TODO: Instead of using sleep, maybe check to see if output and input pipes are connected, then resume output
            // Ensure the terminal has time to initialize before resuming output
            Thread.Sleep(2000);
            
            readPaused = false;
        }
        
        private string GetObserverScript() => $@"
            Import-Module '{dllPath}'
            
            $Host.UI.RawUI.WindowSize = @{{ Width = 120; Height = 30 }}
            $Host.UI.RawUI.BufferSize = @{{ Width = 120; Height = 30 }}

            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8

            Get-Content -Path '{terminalBuffer.Path}'

            $observer = New-Object PowershellExpectDriver.ObserverInternals('{observerOutputPipeName}', '{observerInputPipeName}')
        ";
    }
}