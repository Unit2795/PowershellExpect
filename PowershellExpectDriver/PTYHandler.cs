using Microsoft.Win32.SafeHandles;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    internal sealed class PTYHandler : IDisposable 
    {
        public static readonly IntPtr PseudoConsoleThreadAttribute = (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        public IntPtr Handle { get; }

        private PTYHandler(IntPtr handle) => Handle = handle;

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
                throw new InvalidOperationException("Could not create pseudo console. Error Code " + createResult);
            
            return new PTYHandler(hPC);
        }
        
        public void Resize(int width, int height)
        {
            var resizeResult = ResizePseudoConsole(Handle, new COORD { X = (short)width, Y = (short)height });
            if(resizeResult != 0)
                throw new InvalidOperationException("Could not resize pseudo console. Error Code " + resizeResult);
        }

        public void Dispose() => ClosePseudoConsole(Handle);
    }
}