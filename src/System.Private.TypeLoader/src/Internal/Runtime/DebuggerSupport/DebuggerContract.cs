// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

// The following definitions are required for interop with the VS Debugger
// Prior to making any changes to these, please reach out to the VS Debugger 
// team to make sure that your changes are not going to prevent the debugger
// from working.
namespace Internal.Runtime.DebuggerSupport
{
    internal enum FuncEvalMode : uint
    {
        RegularFuncEval = 1,
        NewStringWithLength = 2,
        NewArray = 3,
        NewParameterizedObjectNoConstructor = 4,
    }

    internal enum DebuggerResponseKind : uint
    {
        FuncEvalCompleteWithReturn = 0,
        FuncEvalCompleteWithException = 1,
        FuncEvalParameterBufferReady = 2,
        RequestBufferReady = 3,
        ConservativeReportingBufferReady = 4,
        HandleReady = 5,
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct DebuggerFuncEvalParameterBufferReadyResponse
    {
        [FieldOffset(0)]
        public DebuggerResponseKind kind;
        [FieldOffset(4)]
        public int unused;
        [FieldOffset(8)]
        public long bufferAddress;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct DebuggerFuncEvalCompleteWithReturnResponse
    {
        [FieldOffset(0)]
        public DebuggerResponseKind kind;
        [FieldOffset(4)]
        public uint returnHandleIdentifier;
        [FieldOffset(8)]
        public long returnAddress;
    }
}
