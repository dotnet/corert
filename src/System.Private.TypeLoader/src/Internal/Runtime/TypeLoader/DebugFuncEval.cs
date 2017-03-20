// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime.CallInterceptor;

namespace Internal.Runtime.TypeLoader
{
    [McgIntrinsics]
    internal static class AddrofIntrinsics
    {
        // This method is implemented elsewhere in the toolchain
        internal static IntPtr AddrOf<T>(T ftn) { throw new PlatformNotSupportedException(); }
    }

    internal class DebugFuncEval
    {
        private static void HighLevelDebugFuncEvalHelperWithVariables(ref int param, ref LocalVariableSet arguments)
        {
            // Hard coding the argument integer here!
            arguments.SetVar<int>(1, param);

            // Obtain the target method address from the runtime
            IntPtr targetAddress = RuntimeImports.RhpGetFuncEvalTargetAddress();

            // Hard coding a single void return here
            LocalVariableType[] returnAndArgumentTypes = new LocalVariableType[2];
            returnAndArgumentTypes[0] = new LocalVariableType(typeof(void).TypeHandle, false, false);
            returnAndArgumentTypes[1] = new LocalVariableType(typeof(int).TypeHandle, false, false);

            // Hard coding static here
            DynamicCallSignature dynamicCallSignature = new DynamicCallSignature(Internal.Runtime.CallConverter.CallingConvention.ManagedStatic, returnAndArgumentTypes, returnAndArgumentTypes.Length);

            // Invoke the target method
            Internal.Runtime.CallInterceptor.CallInterceptor.MakeDynamicCall(targetAddress, dynamicCallSignature, arguments);

            // Signal to the debugger the func eval completes
            IntPtr funcEvalCompleteCommandPointer;
            unsafe
            {
                FuncEvalCompleteCommand funcEvalCompleteCommand = new FuncEvalCompleteCommand
                {
                    commandCode = 0
                };

                funcEvalCompleteCommandPointer = new IntPtr(&funcEvalCompleteCommand);
            }

            RuntimeImports.RhpSendCustomEventToDebugger(funcEvalCompleteCommandPointer, Unsafe.SizeOf<FuncEvalCompleteCommand>());

            // debugger magic will make sure this function never returns, instead control will be transferred back to the point where the FuncEval begins
        }

        [StructLayout(LayoutKind.Explicit, Size=16)]
        struct WriteParameterCommand
        {
            [FieldOffset(0)]
            public int commandCode;
            [FieldOffset(4)]
            public int unused;
            [FieldOffset(8)]
            public long bufferAddress;
        }

        [StructLayout(LayoutKind.Explicit, Size=4)]
        struct FuncEvalCompleteCommand
        {
            [FieldOffset(0)]
            public int commandCode;
        }

        private static void HighLevelDebugFuncEvalHelper()
        {
            int integerParameterValue = 0;
            uint parameterBufferSize = RuntimeImports.RhpGetFuncEvalParameterBufferSize();

            IntPtr writeParameterCommandPointer;
            IntPtr debuggerBufferPointer;
            unsafe
            {
                byte* debuggerBufferRawPointer = stackalloc byte[(int)parameterBufferSize];
                debuggerBufferPointer = new IntPtr(debuggerBufferRawPointer);

                WriteParameterCommand writeParameterCommand = new WriteParameterCommand
                {
                    commandCode = 1,
                    bufferAddress = debuggerBufferPointer.ToInt64()
                };

                writeParameterCommandPointer = new IntPtr(&writeParameterCommand);
            }

            RuntimeImports.RhpSendCustomEventToDebugger(writeParameterCommandPointer, Unsafe.SizeOf<WriteParameterCommand>());

            // .. debugger magic ... the debuggerBuffer will be filled with parameter data

            unsafe
            {
                integerParameterValue = Unsafe.Read<int>(debuggerBufferPointer.ToPointer());
            }

            // Hard coding a single argument of type int here
            LocalVariableType[] argumentTypes = new LocalVariableType[2];
            argumentTypes[0] = new LocalVariableType(typeof(void).TypeHandle, false, false);
            argumentTypes[1] = new LocalVariableType(typeof(int).TypeHandle, false, false);
            LocalVariableSet.SetupArbitraryLocalVariableSet<int>(HighLevelDebugFuncEvalHelperWithVariables, ref integerParameterValue, argumentTypes);
        }

        public static void Initialize()
        {
            // We needed this function only because the McgIntrinsics attribute cannot be applied on the static constructor
            RuntimeImports.RhpSetHighLevelDebugFuncEvalHelper(AddrofIntrinsics.AddrOf<Action>(HighLevelDebugFuncEvalHelper));
        }
    }
}
