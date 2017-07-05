// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Internal.NativeFormat;
using Internal.Runtime.Augments;
using Internal.Runtime.CallInterceptor;
using Internal.Runtime.CompilerServices;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;

namespace Internal.Runtime.DebuggerSupport
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
                    fixed (byte* pParam = param.parameterValues[i])
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
            Exception ex = null;
            try
            {
                Internal.Runtime.CallInterceptor.CallInterceptor.MakeDynamicCall(targetAddress, dynamicCallSignature, arguments);
            }
            catch (Exception e)
            {
                ex = e;
            }

            unsafe
            {
                bool isVoid = (RuntimeTypeHandle.Equals(param.types[0], typeof(void).TypeHandle));

                object returnValue = null;
                IntPtr returnValueHandlePointer = IntPtr.Zero;
                uint returnHandleIdentifier = 0;
                if (ex != null)
                {
                    returnValue = ex;
                }
                else if (!isVoid)
                {
                    IntPtr input = arguments.GetAddressOfVarData(0);
                    returnValue = RuntimeAugments.RhBoxAny(input, (IntPtr)param.types[0].ToEETypePtr());
                }

                // The return value could be null if the target function returned null
                if (returnValue != null)
                {
                    GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
                    returnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
                    returnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(returnValueHandlePointer);
                }

                ReturnToDebuggerWithReturn(returnHandleIdentifier, returnValueHandlePointer, ex != null);
            }
        }

        struct TypesAndValues
        {
            public RuntimeTypeHandle[] types;
            public byte[][] parameterValues;
        }

        private unsafe static void HighLevelDebugFuncEvalHelper()
        {
            uint parameterBufferSize = RuntimeAugments.RhpGetFuncEvalParameterBufferSize();

            IntPtr debuggerFuncEvalParameterBufferReadyResponsePointer;
            IntPtr parameterBufferPointer;

            byte* parameterBuffer = stackalloc byte[(int)parameterBufferSize];
            parameterBufferPointer = new IntPtr(parameterBuffer);

            DebuggerFuncEvalParameterBufferReadyResponse debuggerFuncEvalParameterBufferReadyResponse = new DebuggerFuncEvalParameterBufferReadyResponse
            {
                kind = DebuggerResponseKind.FuncEvalParameterBufferReady,
                bufferAddress = parameterBufferPointer.ToInt64()
            };

            debuggerFuncEvalParameterBufferReadyResponsePointer = new IntPtr(&debuggerFuncEvalParameterBufferReadyResponse);

            RuntimeAugments.RhpSendCustomEventToDebugger(debuggerFuncEvalParameterBufferReadyResponsePointer, Unsafe.SizeOf<DebuggerFuncEvalParameterBufferReadyResponse>());

            // .. debugger magic ... the parameterBuffer will be filled with parameter data

            FuncEvalMode mode = (FuncEvalMode)RuntimeAugments.RhpGetFuncEvalMode();

            switch (mode)
            {
                case FuncEvalMode.RegularFuncEval:
                    RegularFuncEval(parameterBuffer, parameterBufferSize);
                    break;
                case FuncEvalMode.NewStringWithLength:
                    NewStringWithLength(parameterBuffer, parameterBufferSize);
                    break;
                case FuncEvalMode.NewArray:
                    CreateNewArray(parameterBuffer, parameterBufferSize);
                    break;
                case FuncEvalMode.NewParameterizedObjectNoConstructor:
                    NewParameterizedObjectNoConstructor(parameterBuffer, parameterBufferSize);
                    break;

                default:
                    Debug.Assert(false, "Debugger provided an unexpected func eval mode.");
                    break;
            }
        }

        private unsafe static void RegularFuncEval(byte* parameterBuffer, uint parameterBufferSize)
        {
            TypesAndValues typesAndValues = new TypesAndValues();
            uint offset = 0;

            NativeReader reader = new NativeReader(parameterBuffer, parameterBufferSize);
            uint parameterCount;
            offset = reader.DecodeUnsigned(offset, out parameterCount);
            typesAndValues.parameterValues = new byte[parameterCount][];
            for (int i = 0; i < parameterCount; i++)
            {
                uint parameterValueSize;
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
            ulong[] debuggerPreparedExternalReferences;
            offset = BuildDebuggerPreparedExternalReferences(reader, offset, out debuggerPreparedExternalReferences);

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

        private unsafe static void NewParameterizedObjectNoConstructor(byte* parameterBuffer, uint parameterBufferSize)
        {
            uint offset = 0;
            NativeReader reader = new NativeReader(parameterBuffer, parameterBufferSize);
            ulong[] debuggerPreparedExternalReferences;
            offset = BuildDebuggerPreparedExternalReferences(reader, offset, out debuggerPreparedExternalReferences);

            NativeLayoutInfoLoadContext nativeLayoutContext = new NativeLayoutInfoLoadContext();
            TypeSystemContext typeSystemContext = TypeSystemContextFactory.Create();
            nativeLayoutContext._module = null;
            nativeLayoutContext._typeSystemContext = typeSystemContext;
            nativeLayoutContext._typeArgumentHandles = Instantiation.Empty;
            nativeLayoutContext._methodArgumentHandles = Instantiation.Empty;
            nativeLayoutContext._debuggerPreparedExternalReferences = debuggerPreparedExternalReferences;

            NativeParser parser = new NativeParser(reader, offset);
            TypeDesc objectTypeDesc = TypeLoaderEnvironment.Instance.GetConstructedTypeFromParserAndNativeLayoutContext(ref parser, nativeLayoutContext);
            TypeSystemContextFactory.Recycle(typeSystemContext);

            RuntimeTypeHandle objectTypeHandle = objectTypeDesc.GetRuntimeTypeHandle();
            object returnValue = RuntimeAugments.NewObject(objectTypeHandle);

            GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
            IntPtr returnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
            uint returnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(returnValueHandlePointer);
            ReturnToDebuggerWithReturn(returnHandleIdentifier, returnValueHandlePointer, false);
        }

        private unsafe static void CreateNewArray(byte* parameterBuffer, uint parameterBufferSize)
        {
            uint offset = 0;
            NativeReader reader = new NativeReader(parameterBuffer, parameterBufferSize);
            ulong[] debuggerPreparedExternalReferences;
            offset = BuildDebuggerPreparedExternalReferences(reader, offset, out debuggerPreparedExternalReferences);

            NativeLayoutInfoLoadContext nativeLayoutContext = new NativeLayoutInfoLoadContext();
            TypeSystemContext typeSystemContext = TypeSystemContextFactory.Create();
            nativeLayoutContext._module = null;
            nativeLayoutContext._typeSystemContext = typeSystemContext;
            nativeLayoutContext._typeArgumentHandles = Instantiation.Empty;
            nativeLayoutContext._methodArgumentHandles = Instantiation.Empty;
            nativeLayoutContext._debuggerPreparedExternalReferences = debuggerPreparedExternalReferences;

            NativeParser parser = new NativeParser(reader, offset);
            TypeDesc arrElementType = TypeLoaderEnvironment.Instance.GetConstructedTypeFromParserAndNativeLayoutContext(ref parser, nativeLayoutContext);
            TypeSystemContextFactory.Recycle(typeSystemContext);

            uint rank = parser.GetUnsigned();
            int[] dims = new int[rank];
            int[] lowerBounds = new int[rank];

            for (uint i = 0; i < rank; ++i)
            {
                dims[i] = (int)parser.GetUnsigned();
            }

            for (uint i = 0; i < rank; ++i)
            {
                lowerBounds[i] = (int)parser.GetUnsigned();
            }

            RuntimeTypeHandle typeHandle = arrElementType.GetRuntimeTypeHandle();
            RuntimeTypeHandle arrayTypeHandle = default(RuntimeTypeHandle);
            Array returnValue;
            // Get an array RuntimeTypeHandle given an element's RuntimeTypeHandle and rank.
            // Pass false for isMdArray, and rank == -1 for SzArrays
            bool succeed = false;
            if (rank == 1 && lowerBounds[0] == 0)
            {
                succeed = TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(
                    typeHandle,
                    false,
                    -1,
                    out arrayTypeHandle);
                Debug.Assert(succeed);

                returnValue = Internal.Runtime.Augments.RuntimeAugments.NewArray(
                              arrayTypeHandle,
                              dims[0]);
            }
            else
            {
                succeed = TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(
                               typeHandle,
                               true,
                               (int)rank,
                               out arrayTypeHandle
                               );
                Debug.Assert(succeed);
                returnValue = Internal.Runtime.Augments.RuntimeAugments.NewMultiDimArray(
                               arrayTypeHandle,
                               dims,
                               lowerBounds
                               );
            }
            GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
            IntPtr returnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
            uint returnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(returnValueHandlePointer);
            ReturnToDebuggerWithReturn(returnHandleIdentifier, returnValueHandlePointer, false);
        }

        private unsafe static uint BuildDebuggerPreparedExternalReferences(NativeReader reader, uint offset, out ulong[] debuggerPreparedExternalReferences)
        {
            uint eeTypeCount;
            offset = reader.DecodeUnsigned(offset, out eeTypeCount);
            debuggerPreparedExternalReferences = new ulong[eeTypeCount];
            for (int i = 0; i < eeTypeCount; i++)
            {
                ulong eeType;
                offset = reader.DecodeUnsignedLong(offset, out eeType);
                debuggerPreparedExternalReferences[i] = eeType;
            }

            return offset;
        }

        private unsafe static void NewStringWithLength(byte* parameterBuffer, uint parameterBufferSize)
        {
            IntPtr returnValueHandlePointer = IntPtr.Zero;
            uint returnHandleIdentifier = 0;

            string returnValue = Encoding.Unicode.GetString(parameterBuffer, (int)parameterBufferSize);

            GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
            returnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
            returnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(returnValueHandlePointer);

            // TODO, FuncEval, what if we don't have sufficient memory to create the string?
            ReturnToDebuggerWithReturn(returnHandleIdentifier, returnValueHandlePointer, false);
        }

        private unsafe static void ReturnToDebuggerWithReturn(uint returnHandleIdentifier, IntPtr returnValueHandlePointer, bool isException)
        {
            // Signal to the debugger the func eval completes

            DebuggerFuncEvalCompleteWithReturnResponse* debuggerFuncEvalCompleteWithReturnResponse = stackalloc DebuggerFuncEvalCompleteWithReturnResponse[1];
            debuggerFuncEvalCompleteWithReturnResponse->kind = isException ? DebuggerResponseKind.FuncEvalCompleteWithException : DebuggerResponseKind.FuncEvalCompleteWithReturn;
            debuggerFuncEvalCompleteWithReturnResponse->returnHandleIdentifier = returnHandleIdentifier;
            debuggerFuncEvalCompleteWithReturnResponse->returnAddress = (long)returnValueHandlePointer;
            IntPtr debuggerFuncEvalCompleteWithReturnResponsePointer = new IntPtr(debuggerFuncEvalCompleteWithReturnResponse);
            RuntimeAugments.RhpSendCustomEventToDebugger(debuggerFuncEvalCompleteWithReturnResponsePointer, Unsafe.SizeOf<DebuggerFuncEvalCompleteWithReturnResponse>());

            // debugger magic will make sure this function never returns, instead control will be transferred back to the point where the FuncEval begins
        }

        public static void Initialize()
        {
            // We needed this function only because the McgIntrinsics attribute cannot be applied on the static constructor
            RuntimeAugments.RhpSetHighLevelDebugFuncEvalHelper(AddrofIntrinsics.AddrOf<Action>(HighLevelDebugFuncEvalHelper));
        }
    }
}
