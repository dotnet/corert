// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.NativeFormat;
using Internal.Runtime.Augments;
using Internal.Runtime.CallInterceptor;
using Internal.Runtime.CompilerServices;
using Internal.TypeSystem;

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
        private static void HighLevelDebugFuncEvalHelperWithVariables(ref TypesAndValues param, ref LocalVariableSet arguments)
        {
            for (int i = 0; i < param.parameterValues.Length; i++)
            {
                unsafe
                {
                    IntPtr input = arguments.GetAddressOfVarData(i + 1);
                    byte* pInput = (byte*)input;
                    fixed(byte* pParam = param.parameterValues[i])
                    {
                        for (int j = 0; j < param.parameterValues[i].Length; j++)
                        {
                            pInput[j] = pParam[j];
                        }
                    }
                }
            }

            // Obtain the target method address from the runtime
            IntPtr targetAddress = RuntimeAugments.RhpGetFuncEvalTargetAddress();

            LocalVariableType[] returnAndArgumentTypes = new LocalVariableType[param.types.Length];
            for (int i = 0; i < returnAndArgumentTypes.Length; i++)
            {
                returnAndArgumentTypes[i] = new LocalVariableType(param.types[i], false, false);
            }

            // Hard coding static here
            DynamicCallSignature dynamicCallSignature = new DynamicCallSignature(Internal.Runtime.CallConverter.CallingConvention.ManagedStatic, returnAndArgumentTypes, returnAndArgumentTypes.Length);

            // Invoke the target method
            Internal.Runtime.CallInterceptor.CallInterceptor.MakeDynamicCall(targetAddress, dynamicCallSignature, arguments);

            unsafe
            {
                // Box the return
                IntPtr input = arguments.GetAddressOfVarData(0);
                object returnValue = RuntimeAugments.RhBoxAny(input, (IntPtr)param.types[0].ToEETypePtr());
                GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
                IntPtr returnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
                uint returnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(returnValueHandlePointer);

                // Signal to the debugger the func eval completes
                FuncEvalCompleteCommand* funcEvalCompleteCommand = stackalloc FuncEvalCompleteCommand[1];
                funcEvalCompleteCommand->commandCode = 0;
                funcEvalCompleteCommand->returnHandleIdentifier = returnHandleIdentifier;
                funcEvalCompleteCommand->returnAddress = (long)returnValueHandlePointer;
                IntPtr funcEvalCompleteCommandPointer = new IntPtr(funcEvalCompleteCommand);
                RuntimeAugments.RhpSendCustomEventToDebugger(funcEvalCompleteCommandPointer, Unsafe.SizeOf<FuncEvalCompleteCommand>());
            }

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

        [StructLayout(LayoutKind.Explicit, Size=16)]
        struct FuncEvalCompleteCommand
        {
            [FieldOffset(0)]
            public int commandCode;
            [FieldOffset(4)]
            public uint returnHandleIdentifier;
            [FieldOffset(8)]
            public long returnAddress;
        }

        struct TypesAndValues
        {
            public RuntimeTypeHandle[] types;
            public byte[][] parameterValues;
        }

        private static void HighLevelDebugFuncEvalHelper()
        {
            uint parameterBufferSize = RuntimeAugments.RhpGetFuncEvalParameterBufferSize();

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

                RuntimeAugments.RhpSendCustomEventToDebugger(writeParameterCommandPointer, Unsafe.SizeOf<WriteParameterCommand>());

                // .. debugger magic ... the debuggerBuffer will be filled with parameter data

                TypesAndValues typesAndValues = new TypesAndValues();

                uint trash;
                uint parameterCount;
                uint parameterValueSize;
                uint eeTypeCount;
                ulong eeType;
                uint offset = 0;

                NativeReader reader = new NativeReader(debuggerBufferRawPointer, parameterBufferSize);
                offset = reader.DecodeUnsigned(offset, out trash); // The VertexSequence always generate a length, I don't really need it.
                offset = reader.DecodeUnsigned(offset, out parameterCount);

                typesAndValues.parameterValues = new byte[parameterCount][];
                for (int i = 0; i < parameterCount; i++)
                {
                    offset = reader.DecodeUnsigned(offset, out parameterValueSize);
                    byte[] parameterValue = new byte[parameterValueSize];
                    for (int j = 0; j < parameterValueSize; j++)
                    {
                        uint parameterByte;
                        offset = reader.DecodeUnsigned(offset, out parameterByte);
                        parameterValue[j] = (byte)parameterByte;
                    }
                    typesAndValues.parameterValues[i] = parameterValue;
                }
                offset = reader.DecodeUnsigned(offset, out eeTypeCount);
                ulong[] debuggerPreparedExternalReferences = new ulong[eeTypeCount];
                for (int i = 0; i < eeTypeCount; i++)
                {
                    offset = reader.DecodeUnsignedLong(offset, out eeType);
                    debuggerPreparedExternalReferences[i] = eeType;
                }

                TypeSystemContext typeSystemContext = TypeSystemContextFactory.Create();
                bool hasThis;
                TypeDesc[] parameters;
                bool[] parametersWithGenericDependentLayout;
                bool result = TypeLoaderEnvironment.Instance.GetCallingConverterDataFromMethodSignature_NativeLayout_Debugger(typeSystemContext, RuntimeSignature.CreateFromNativeLayoutSignatureForDebugger(offset), Instantiation.Empty, Instantiation.Empty, out hasThis, out parameters, out parametersWithGenericDependentLayout, reader, debuggerPreparedExternalReferences);

                typesAndValues.types = new RuntimeTypeHandle[parameters.Length];

                bool needToDynamicallyLoadTypes = false;
                for (int i = 0; i < typesAndValues.types.Length; i++)
                {
                    if (!parameters[i].RetrieveRuntimeTypeHandleIfPossible())
                    {
                        needToDynamicallyLoadTypes = true;
                        break;
                    }

                    typesAndValues.types[i] = parameters[i].GetRuntimeTypeHandle();
                }

                if (needToDynamicallyLoadTypes)
                {
                    TypeLoaderEnvironment.Instance.RunUnderTypeLoaderLock(() =>
                    {
                        typeSystemContext.FlushTypeBuilderStates();

                        GenericDictionaryCell[] cells = new GenericDictionaryCell[parameters.Length];
                        for (int i = 0; i < cells.Length; i++)
                        {
                            cells[i] = GenericDictionaryCell.CreateTypeHandleCell(parameters[i]);
                        }
                        IntPtr[] eetypePointers;
                        TypeBuilder.ResolveMultipleCells(cells, out eetypePointers);

                        for (int i = 0; i < parameters.Length; i++)
                        {
                            typesAndValues.types[i] = ((EEType*)eetypePointers[i])->ToRuntimeTypeHandle();
                        }
                    });
                }

                TypeSystemContextFactory.Recycle(typeSystemContext);

                LocalVariableType[] argumentTypes = new LocalVariableType[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    // TODO, FuncEval, what these false really means? Need to make sure our format contains those information
                    argumentTypes[i] = new LocalVariableType(typesAndValues.types[i], false, false);
                }

                LocalVariableSet.SetupArbitraryLocalVariableSet<TypesAndValues>(HighLevelDebugFuncEvalHelperWithVariables, ref typesAndValues, argumentTypes);
            }
        }

        public static void Initialize()
        {
            // We needed this function only because the McgIntrinsics attribute cannot be applied on the static constructor
            RuntimeAugments.RhpSetHighLevelDebugFuncEvalHelper(AddrofIntrinsics.AddrOf<Action>(HighLevelDebugFuncEvalHelper));
        }
    }
}
