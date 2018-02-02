// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace System.Diagnostics
{
    public static class Eval
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [RuntimeExport("RhpDebugFuncEvalHelper")]
        public unsafe static void RhpDebugFuncEvalHelper(IntPtr unusedTransitionBlock, IntPtr classlibAddress)
        {
            IntPtr pDebugFuncEvalHelper = (IntPtr)InternalCalls.RhpGetClasslibFunctionFromCodeAddress(classlibAddress, ClassLibFunctionId.DebugFuncEvalHelper);
            Debug.Assert(pDebugFuncEvalHelper != IntPtr.Zero);
            CalliIntrinsics.CallVoid(pDebugFuncEvalHelper);
        }
    }
}