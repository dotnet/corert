// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Runtime.InteropServices;

namespace System.Diagnostics
{
    public partial class StackTrace
    {
        private readonly StringBuilder _builder = new StringBuilder();

        [DllImport("*")]
        static unsafe extern int emscripten_get_callstack(int flags, byte* outBuf, int maxBytes);

        private unsafe void InitializeForCurrentThread(int skipFrames, bool needFileInfo)
        {
            var backtraceBuffer = new byte[8192];
            int callstackLen;
            // skip these 2:
            // at S_P_CoreLib_System_Diagnostics_StackTrace__InitializeForCurrentThread (wasm-function[12314]:275)
            // at S_P_CoreLib_System_Diagnostics_StackTrace___ctor_0(wasm-function[12724]:118)
            skipFrames += 2; // METHODS_TO_SKIP is a constant so just change here
            // at S_P_CoreLib_System_Exception__DispatchExWasm(wasm-function[2360]:39)

            fixed (byte* curChar = backtraceBuffer)
            {
                callstackLen = emscripten_get_callstack(0, curChar, backtraceBuffer.Length);
            }
            int _numOfFrames = 1;
            int lineStartIx = 0;
            int ix = 0;
            for (; ix < callstackLen; ix++)
            {
                if (backtraceBuffer[ix] == '\n')
                {
                    if (_numOfFrames > skipFrames)
                    {
                        _builder.Append(Encoding.Default.GetString(backtraceBuffer, lineStartIx, ix - lineStartIx + 1));
                    }
                    _numOfFrames++;
                    lineStartIx = ix + 1;
                }
            }
            if (lineStartIx < ix)
            {
                _builder.AppendLine(Encoding.Default.GetString(backtraceBuffer, lineStartIx, ix - lineStartIx));
            }
            _methodsToSkip = 0;
        }


        internal string ToString(TraceFormat traceFormat)
        {
            var stackTraceString = _builder.ToString();
            if (traceFormat == TraceFormat.Normal && stackTraceString.EndsWith(Environment.NewLine))
                return stackTraceString.Substring(0, stackTraceString.Length - Environment.NewLine.Length);

            return stackTraceString;
        }
    }
}
