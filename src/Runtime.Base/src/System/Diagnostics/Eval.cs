// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace System.Diagnostics
{
    public static class Eval
    {
        private static IntPtr s_highLevelDebugFuncEvalHelper;

        [MethodImpl(MethodImplOptions.NoInlining)]
        [RuntimeExport("RhpSetHighLevelDebugFuncEvalHelper")]
        public static void SetHighLevelDebugFuncEvalHelper(IntPtr ptr)
        {
            s_highLevelDebugFuncEvalHelper = ptr;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [RuntimeExport("RhpDebugFuncEvalHelper")]
        public static void RhpDebugFuncEvalHelper()
        {
            CalliIntrinsics.CallVoid(s_highLevelDebugFuncEvalHelper);
        }
    }
}