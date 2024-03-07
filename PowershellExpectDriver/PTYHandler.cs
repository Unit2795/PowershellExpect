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

        public void Dispose() => ClosePseudoConsole(Handle);
    }
}