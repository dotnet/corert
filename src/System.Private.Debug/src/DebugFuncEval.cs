// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Internal.Runtime.Augments;
using Internal.Runtime.CallInterceptor;
using Internal.Runtime.CompilerServices;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;
using Internal.Runtime.DebuggerSupport;


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
            // Offset begins with 1 because we always skip setting the return value before we call the function
            int offset = 1;
            if (param.thisObj != null)
            {
                // For constructors - caller does not pass the this pointer, instead, we constructed param.thisObj and pass it as the first argument
                arguments.SetVar<object>(offset, param.thisObj);
                offset++;
            }
            for (int i = 0; i < param.parameterValues.Length; i++)
            {
                unsafe
                {
                    IntPtr input = arguments.GetAddressOfVarData(i + offset);
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
                else if (param.thisObj != null)
                {
                    // For constructors - the debugger would like to get 'this' back
                    returnValue = param.thisObj;
                }
                else if (!isVoid)
                {
                    IntPtr input = arguments.GetAddressOfVarData(0);
                    returnValue = TypeSystemHelper.BoxAnyType(input, param.types[0]);
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
            public object thisObj;
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
                case FuncEvalMode.CallParameterizedFunction:
                    CallParameterizedFunction(parameterBuffer, parameterBufferSize);
                    break;
                case FuncEvalMode.NewStringWithLength:
                    NewStringWithLength(parameterBuffer, parameterBufferSize);
                    break;
                case FuncEvalMode.NewParameterizedArray:
                    NewParameterizedArray(parameterBuffer, parameterBufferSize);
                    break;
                case FuncEvalMode.NewParameterizedObjectNoConstructor:
                    NewParameterizedObjectNoConstructor(parameterBuffer, parameterBufferSize);
                    break;
                case FuncEvalMode.NewParameterizedObject:
                    NewParameterizedObject(parameterBuffer, parameterBufferSize);
                    break;
                default:
                    Debug.Assert(false, "Debugger provided an unexpected func eval mode.");
                    break;
            }
        }

        private unsafe static void CallParameterizedFunction(byte* parameterBuffer, uint parameterBufferSize)
        {
            CallParameterizedFunctionOrNewParameterizedObject(parameterBuffer, parameterBufferSize, isConstructor: false);
        }

        private unsafe static void NewParameterizedObject(byte* parameterBuffer, uint parameterBufferSize)
        {
            CallParameterizedFunctionOrNewParameterizedObject(parameterBuffer, parameterBufferSize, isConstructor: true);
        }

        private unsafe static void CallParameterizedFunctionOrNewParameterizedObject(byte* parameterBuffer, uint parameterBufferSize, bool isConstructor)
        {
            TypesAndValues typesAndValues = new TypesAndValues();

            LowLevelNativeFormatReader reader = new LowLevelNativeFormatReader(parameterBuffer, parameterBufferSize);
            uint parameterCount = reader.GetUnsigned();
            typesAndValues.parameterValues = new byte[parameterCount][];
            for (int i = 0; i < parameterCount; i++)
            {
                uint parameterValueSize = reader.GetUnsigned();
                byte[] parameterValue = new byte[parameterValueSize];
                for (int j = 0; j < parameterValueSize; j++)
                {
                    uint parameterByte = reader.GetUnsigned();
                    parameterValue[j] = (byte)parameterByte;
                }
                typesAndValues.parameterValues[i] = parameterValue;
            }
            ulong[] debuggerPreparedExternalReferences;
            BuildDebuggerPreparedExternalReferences(reader, out debuggerPreparedExternalReferences);

            
            bool hasThis;
            TypeDesc[] parameters;
            bool[] parametersWithGenericDependentLayout;
            bool result = TypeSystemHelper.CallingConverterDataFromMethodSignature (
                reader,
                debuggerPreparedExternalReferences,
                out hasThis,
                out parameters,
                out parametersWithGenericDependentLayout
                );

            typesAndValues.types = new RuntimeTypeHandle[parameters.Length];

            for (int i = 0; i < typesAndValues.types.Length; i++)
            {
                typesAndValues.types[i] = parameters[i].GetRuntimeTypeHandle();
            }

            LocalVariableType[] argumentTypes = new LocalVariableType[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                // TODO, FuncEval, what these false really means? Need to make sure our format contains those information
                argumentTypes[i] = new LocalVariableType(typesAndValues.types[i], false, false);
            }

            if (isConstructor)
            {
                // TODO, FuncEval, deal with Nullable objects
                typesAndValues.thisObj = RuntimeAugments.RawNewObject(typesAndValues.types[1]);
            }

            LocalVariableSet.SetupArbitraryLocalVariableSet<TypesAndValues>(HighLevelDebugFuncEvalHelperWithVariables, ref typesAndValues, argumentTypes);
        }

        private unsafe static void NewParameterizedObjectNoConstructor(byte* parameterBuffer, uint parameterBufferSize)
        {
            LowLevelNativeFormatReader reader = new LowLevelNativeFormatReader(parameterBuffer, parameterBufferSize);
            ulong[] debuggerPreparedExternalReferences;
            BuildDebuggerPreparedExternalReferences(reader, out debuggerPreparedExternalReferences);
            RuntimeTypeHandle objectTypeHandle = TypeSystemHelper.GetConstructedRuntimeTypeHandle(reader, debuggerPreparedExternalReferences);
            // TODO, FuncEval, deal with Nullable objects
            object returnValue = RuntimeAugments.RawNewObject(objectTypeHandle);

            GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
            IntPtr returnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
            uint returnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(returnValueHandlePointer);
            ReturnToDebuggerWithReturn(returnHandleIdentifier, returnValueHandlePointer, false);
        }

        private unsafe static void NewParameterizedArray(byte* parameterBuffer, uint parameterBufferSize)
        {
            LowLevelNativeFormatReader reader = new LowLevelNativeFormatReader(parameterBuffer, parameterBufferSize);
            ulong[] debuggerPreparedExternalReferences;
            BuildDebuggerPreparedExternalReferences(reader, out debuggerPreparedExternalReferences);

            RuntimeTypeHandle arrElmTypeHandle =
                TypeSystemHelper.GetConstructedRuntimeTypeHandle(reader, debuggerPreparedExternalReferences);

            uint rank = reader.GetUnsigned();
            int[] dims = new int[rank];
            int[] lowerBounds = new int[rank];

            for (uint i = 0; i < rank; ++i)
            {
                dims[i] = (int)reader.GetUnsigned();
            }

            for (uint i = 0; i < rank; ++i)
            {
                lowerBounds[i] = (int)reader.GetUnsigned();
            }

            Array returnValue;
            // Get an array RuntimeTypeHandle given an element's RuntimeTypeHandle and rank.
            // Pass false for isMdArray, and rank == -1 for SzArrays
            if (rank == 1 && lowerBounds[0] == 0)
            {
                returnValue = TypeSystemHelper.NewArray(arrElmTypeHandle, dims[0]);
            }
            else
            {
                returnValue = TypeSystemHelper.NewMultiDimArray(arrElmTypeHandle, (int)rank, dims, lowerBounds);
            }
            GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
            IntPtr returnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
            uint returnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(returnValueHandlePointer);
            ReturnToDebuggerWithReturn(returnHandleIdentifier, returnValueHandlePointer, false);
        }

        private unsafe static void BuildDebuggerPreparedExternalReferences(LowLevelNativeFormatReader reader,
                                                                           out ulong[] debuggerPreparedExternalReferences)
        {
            uint eeTypeCount = reader.GetUnsigned();
            debuggerPreparedExternalReferences = new ulong[eeTypeCount];
            for (int i = 0; i < eeTypeCount; i++)
            {
                ulong eeType = reader.GetUnsignedLong();
                debuggerPreparedExternalReferences[i] = eeType;
            }
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
