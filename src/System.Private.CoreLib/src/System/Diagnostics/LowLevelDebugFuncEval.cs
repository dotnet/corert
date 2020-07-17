// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using System.Runtime.InteropServices;

namespace System.Diagnostics
{
    public static class LowLevelDebugFuncEval
    {
        private static Action s_highLevelDebugFuncEvalHelper;
        private static Action<long> s_highLevelDebugFuncEvalAbortHelper;

        [RuntimeExport("DebugFuncEvalHelper")]
        public static void DebugFuncEvalHelper()
        {
            Debug.Assert(s_highLevelDebugFuncEvalHelper != null);
            s_highLevelDebugFuncEvalHelper();
        }

        [UnmanagedCallersOnly(EntryPoint="DebugFuncEvalAbortHelper")]
        public static void DebugFuncEvalAbortHelper(long pointerFromDebugger)
        {
            Debug.Assert(s_highLevelDebugFuncEvalHelper != null);
            s_highLevelDebugFuncEvalAbortHelper(pointerFromDebugger);
        }

        public static void SetHighLevelDebugFuncEvalHelper(Action highLevelDebugFuncEvalHelper)
        {
            s_highLevelDebugFuncEvalHelper = highLevelDebugFuncEvalHelper;
        }

        public static void SetHighLevelDebugFuncEvalAbortHelper(Action<long> highLevelDebugFuncEvalAbortHelper)
        {
            s_highLevelDebugFuncEvalAbortHelper = highLevelDebugFuncEvalAbortHelper;
        }
    }
}
