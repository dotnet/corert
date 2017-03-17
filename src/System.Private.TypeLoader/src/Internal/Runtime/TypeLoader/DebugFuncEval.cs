// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection.Runtime.General;

using Internal.TypeSystem;
using Internal.Runtime.Augments;
using Internal.Runtime.CallInterceptor;
using Internal.TypeSystem.NativeFormat;
using Internal.NativeFormat;

namespace System.Runtime.InteropServices
{
    [McgIntrinsics]
    internal static class AddrofIntrinsics
    {
        // This method is implemented elsewhere in the toolchain
        internal static IntPtr AddrOf<T>(T ftn) { throw new PlatformNotSupportedException(); }
    }

    internal class AutoPinner : IDisposable
    {
        private GCHandle _pinnedArray;

        public AutoPinner(Object obj)
        {
            _pinnedArray = GCHandle.Alloc(obj, GCHandleType.Pinned);
        }

        public static implicit operator IntPtr(AutoPinner ap)
        {
            return ap._pinnedArray.AddrOfPinnedObject();
        }
        
        public void Dispose()
        {
            _pinnedArray.Free();
        }
    }
}

namespace Internal.Runtime.TypeLoader
{
    internal class DebugFuncEval
    {

        private static void HighLevelDebugFuncEvalHelperWithVariables(ref int param, ref LocalVariableSet arguments)
        {
            // Hard coding the argument integer here!
            arguments.SetVar<int>(1, param);

            // For now, fill this variable in the debugger, any static void method with a single integer argument would work
            IntPtr targetAddress = RuntimeImports.RhpGetFuncEvalTargetAddress();

            // Hard coding a single void return here
            LocalVariableType[] returnAndArgumentTypes = new LocalVariableType[2];
            returnAndArgumentTypes[0] = new LocalVariableType(typeof(void).TypeHandle, false, false);
            returnAndArgumentTypes[1] = new LocalVariableType(typeof(int).TypeHandle, false, false);

            // 2) Invoke the target method

            // Hard coding static here
            DynamicCallSignature dynamicCallSignature = new DynamicCallSignature(Internal.Runtime.CallConverter.CallingConvention.ManagedStatic, returnAndArgumentTypes, returnAndArgumentTypes.Length);

            // Go and execute!
            Internal.Runtime.CallInterceptor.CallInterceptor.MakeDynamicCall(targetAddress, dynamicCallSignature, arguments);

            FuncEvalCompleteCommand funcEvalCompleteCommand = new FuncEvalCompleteCommand();
            funcEvalCompleteCommand.commandCode = 0;

            using (AutoPinner ap = new AutoPinner(funcEvalCompleteCommand))
            {
                RuntimeImports.RhpSendCustomEventToDebugger(ap, Unsafe.SizeOf<FuncEvalCompleteCommand>());
            }
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
            byte[] debuggerBuffer = new byte[parameterBufferSize];

            using (AutoPinner ap = new AutoPinner(debuggerBuffer))
            {
                IntPtr debuggerBufferPointer = ap;

                WriteParameterCommand writeParameterCommand = new WriteParameterCommand();
                writeParameterCommand.commandCode = 1;
                writeParameterCommand.bufferAddress = debuggerBufferPointer.ToInt64();
                
                using (AutoPinner ap2 = new AutoPinner(writeParameterCommand))
                {
                    RuntimeImports.RhpSendCustomEventToDebugger(ap2, Unsafe.SizeOf<WriteParameterCommand>());
                }

                unsafe
                {
                    integerParameterValue = Unsafe.Read<int>(debuggerBufferPointer.ToPointer());
                }
            }

            // Hard coding a single argument of type int here
            LocalVariableType[] argumentTypes = new LocalVariableType[2];
            argumentTypes[0] = new LocalVariableType(typeof(void).TypeHandle, false, false);
            argumentTypes[1] = new LocalVariableType(typeof(int).TypeHandle, false, false);
            LocalVariableSet.SetupArbitraryLocalVariableSet<int>(HighLevelDebugFuncEvalHelperWithVariables, ref integerParameterValue, argumentTypes);
        }

        [McgIntrinsics]
        public static void Initialize()
        {
            // We needed this function only because the McgIntrinsics attribute cannot be applied on the static constructor
            RuntimeImports.RhpSetHighLevelDebugFuncEvalHelper(AddrofIntrinsics.AddrOf<Action>(HighLevelDebugFuncEvalHelper));
        }
    }
}