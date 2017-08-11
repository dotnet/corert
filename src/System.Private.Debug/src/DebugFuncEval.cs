// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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

    internal static class DebugFuncEval
    {
        private struct InvokeFunctionData
        {
            public object thisObj;
            public RuntimeTypeHandle[] types;
            public byte[][] parameterValues;
            public FuncEvalResult result;
        }

        private struct FuncEvalResult
        {
            public FuncEvalResult(object returnValue, bool isException)
            {
                if (returnValue != null)
                {
                    GCHandle returnValueHandle = GCHandle.Alloc(returnValue);
                    this.ReturnValueHandlePointer = GCHandle.ToIntPtr(returnValueHandle);
                    this.ReturnHandleIdentifier = RuntimeAugments.RhpRecordDebuggeeInitiatedHandle(this.ReturnValueHandlePointer);
                }
                else
                {
                    this.ReturnValueHandlePointer = IntPtr.Zero;
                    this.ReturnHandleIdentifier = 0;
                }

                this.IsException = isException;
            }

            public readonly uint ReturnHandleIdentifier;
            public readonly IntPtr ReturnValueHandlePointer;
            public readonly bool IsException;
        }

        private static long s_funcEvalId = -1;
        private static IntPtr s_funcEvalThread;

        /// <summary>
        /// When the module initializes, we register ourselves as the high level debug func eval helpers.
        /// The runtime will call into these functions when apppropriate.
        /// </summary>
        public static void Initialize()
        {
            RuntimeAugments.RhpSetHighLevelDebugFuncEvalHelper(AddrofIntrinsics.AddrOf<Action>(HighLevelDebugFuncEvalHelper));
            RuntimeAugments.RhpSetHighLevelDebugFuncEvalAbortHelper(AddrofIntrinsics.AddrOf<Action<ulong>>(HighLevelDebugFuncEvalAbortHelper));
        }

        private unsafe static void HighLevelDebugFuncEvalHelper()
        {
            long lastFuncEvalId = s_funcEvalId;
            long myFuncEvalId = 0;
            FuncEvalResult funcEvalResult = new FuncEvalResult();
            s_funcEvalThread = RuntimeAugments.RhpGetCurrentThread();
            Exception ex = null;
            try
            {
                uint parameterBufferSize = RuntimeAugments.RhpGetFuncEvalParameterBufferSize();

                IntPtr debuggerFuncEvalParameterBufferReadyResponsePointer;
                IntPtr parameterBufferPointer;

                byte* parameterBuffer = stackalloc byte[(int)parameterBufferSize];
                parameterBufferPointer = new IntPtr(parameterBuffer);

                DebuggerFuncEvalParameterBufferReadyResponse* debuggerFuncEvalParameterBufferReadyResponse = stackalloc DebuggerFuncEvalParameterBufferReadyResponse[1];
                debuggerFuncEvalParameterBufferReadyResponse->kind = DebuggerResponseKind.FuncEvalParameterBufferReady;
                debuggerFuncEvalParameterBufferReadyResponse->bufferAddress = parameterBufferPointer.ToInt64();

                debuggerFuncEvalParameterBufferReadyResponsePointer = new IntPtr(debuggerFuncEvalParameterBufferReadyResponse);

                RuntimeAugments.RhpSendCustomEventToDebugger(debuggerFuncEvalParameterBufferReadyResponsePointer, Unsafe.SizeOf<DebuggerFuncEvalParameterBufferReadyResponse>());

                // .. debugger magic ... the parameterBuffer will be filled with parameter data

                FuncEvalMode mode = (FuncEvalMode)RuntimeAugments.RhpGetFuncEvalMode();

                switch (mode)
                {
                    case FuncEvalMode.CallParameterizedFunction:
                        funcEvalResult = CallParameterizedFunction(ref myFuncEvalId, parameterBuffer, parameterBufferSize);
                        break;
                    case FuncEvalMode.NewStringWithLength:
                        funcEvalResult = NewStringWithLength(ref myFuncEvalId, parameterBuffer, parameterBufferSize);
                        break;
                    case FuncEvalMode.NewParameterizedArray:
                        funcEvalResult = NewParameterizedArray(ref myFuncEvalId, parameterBuffer, parameterBufferSize);
                        break;
                    case FuncEvalMode.NewParameterizedObjectNoConstructor:
                        funcEvalResult = NewParameterizedObjectNoConstructor(ref myFuncEvalId, parameterBuffer, parameterBufferSize);
                        break;
                    case FuncEvalMode.NewParameterizedObject:
                        funcEvalResult = NewParameterizedObject(ref myFuncEvalId, parameterBuffer, parameterBufferSize);
                        break;
                    default:
                        Debug.Assert(false, "Debugger provided an unexpected func eval mode.");
                        break;
                }

                if (Interlocked.CompareExchange(ref s_funcEvalId, lastFuncEvalId, myFuncEvalId) == myFuncEvalId)
                {
                    ReturnToDebuggerWithReturn(funcEvalResult);
                    // ... debugger magic ... the process will go back to wherever it was before the func eval
                }
                else
                {
                    // Wait for the abort to complete
                    while (true)
                    {
                        RuntimeAugments.RhYield();
                    }
                }
            }
            catch (Exception e)
            {
                RuntimeAugments.RhpCancelThreadAbort(s_funcEvalThread);
                ex = e;

                // It is important that we let the try block complete to clean up runtime data structures,
                // therefore, while the result is already available, we still need to let the runtime exit the catch block 
                // cleanly before we return to the debugger.
            }

            s_funcEvalId = lastFuncEvalId;
            ReturnToDebuggerWithReturn(new FuncEvalResult(ex, true));

            // ... debugger magic ... the process will go back to wherever it was before the func eval

        }

        [NativeCallable]
        private unsafe static void HighLevelDebugFuncEvalAbortHelper(ulong pointerValueFromDebugger)
        {
            ulong pointerFromDebugger = pointerValueFromDebugger & ~((ulong)1);
            long myFuncEvalId = (long)pointerFromDebugger;
            bool rude = (pointerValueFromDebugger & 1) == 1;
            if (Interlocked.CompareExchange(ref s_funcEvalId, 0, myFuncEvalId) == myFuncEvalId)
            {
                DebuggerFuncEvalCrossThreadDependencyNotification* debuggerFuncEvalCrossThreadDependencyNotification = stackalloc DebuggerFuncEvalCrossThreadDependencyNotification[1];
                debuggerFuncEvalCrossThreadDependencyNotification->kind = DebuggerResponseKind.FuncEvalCrossThreadDependency;
                debuggerFuncEvalCrossThreadDependencyNotification->payload = pointerFromDebugger;
                IntPtr debuggerFuncEvalCrossThreadDependencyNotificationPointer = new IntPtr(debuggerFuncEvalCrossThreadDependencyNotification);
                RuntimeAugments.RhpSendCustomEventToDebugger(debuggerFuncEvalCrossThreadDependencyNotificationPointer, Unsafe.SizeOf<DebuggerFuncEvalCrossThreadDependencyNotification>());

                RuntimeAugments.RhpInitiateThreadAbort(s_funcEvalThread, rude);
            }
        }

        private unsafe static FuncEvalResult CallParameterizedFunction(ref long myFuncEvalId, byte* parameterBuffer, uint parameterBufferSize)
        {
            return CallParameterizedFunctionOrNewParameterizedObject(ref myFuncEvalId, parameterBuffer, parameterBufferSize, isConstructor: false);
        }

        private unsafe static FuncEvalResult NewParameterizedObject(ref long myFuncEvalId, byte* parameterBuffer, uint parameterBufferSize)
        {
            return CallParameterizedFunctionOrNewParameterizedObject(ref myFuncEvalId, parameterBuffer, parameterBufferSize, isConstructor: true);
        }

        private unsafe static FuncEvalResult CallParameterizedFunctionOrNewParameterizedObject(ref long myFuncEvalId, byte* parameterBuffer, uint parameterBufferSize, bool isConstructor)
        {
            InvokeFunctionData invokeFunctionData = new InvokeFunctionData();

            LowLevelNativeFormatReader reader = new LowLevelNativeFormatReader(parameterBuffer, parameterBufferSize);
            myFuncEvalId = s_funcEvalId = (long)reader.GetUnsignedLong();
            uint parameterCount = reader.GetUnsigned();
            invokeFunctionData.parameterValues = new byte[parameterCount][];
            for (int i = 0; i < parameterCount; i++)
            {
                uint parameterValueSize = reader.GetUnsigned();
                byte[] parameterValue = new byte[parameterValueSize];
                for (int j = 0; j < parameterValueSize; j++)
                {
                    uint parameterByte = reader.GetUnsigned();
                    parameterValue[j] = (byte)parameterByte;
                }
                invokeFunctionData.parameterValues[i] = parameterValue;
            }
            ulong[] debuggerPreparedExternalReferences;
            BuildDebuggerPreparedExternalReferences(reader, out debuggerPreparedExternalReferences);

            bool hasThis;
            TypeDesc[] parameters;
            bool[] parametersWithGenericDependentLayout;
            bool result = TypeSystemHelper.CallingConverterDataFromMethodSignature(
                reader,
                debuggerPreparedExternalReferences,
                out hasThis,
                out parameters,
                out parametersWithGenericDependentLayout
                );

            invokeFunctionData.types = new RuntimeTypeHandle[parameters.Length];

            for (int i = 0; i < invokeFunctionData.types.Length; i++)
            {
                invokeFunctionData.types[i] = parameters[i].GetRuntimeTypeHandle();
            }

            LocalVariableType[] argumentTypes = new LocalVariableType[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                // TODO, FuncEval, what these false really means? Need to make sure our format contains those information
                argumentTypes[i] = new LocalVariableType(invokeFunctionData.types[i], false, false);
            }

            if (isConstructor)
            {
                // TODO, FuncEval, deal with Nullable objects
                invokeFunctionData.thisObj = RuntimeAugments.RawNewObject(invokeFunctionData.types[1]);
            }

            LocalVariableSet.SetupArbitraryLocalVariableSet<InvokeFunctionData>(InvokeFunction, ref invokeFunctionData, argumentTypes);

            return invokeFunctionData.result;
        }

        private unsafe static FuncEvalResult NewParameterizedObjectNoConstructor(ref long myFuncEvalId, byte* parameterBuffer, uint parameterBufferSize)
        {
            LowLevelNativeFormatReader reader = new LowLevelNativeFormatReader(parameterBuffer, parameterBufferSize);
            myFuncEvalId = s_funcEvalId = (long)reader.GetUnsignedLong();
            ulong[] debuggerPreparedExternalReferences;
            BuildDebuggerPreparedExternalReferences(reader, out debuggerPreparedExternalReferences);
            RuntimeTypeHandle objectTypeHandle = TypeSystemHelper.GetConstructedRuntimeTypeHandle(reader, debuggerPreparedExternalReferences);
            // TODO, FuncEval, deal with Nullable objects
            object returnValue = RuntimeAugments.RawNewObject(objectTypeHandle);
            return new FuncEvalResult(returnValue, false);
        }

        private unsafe static FuncEvalResult NewParameterizedArray(ref long myFuncEvalId, byte* parameterBuffer, uint parameterBufferSize)
        {
            LowLevelNativeFormatReader reader = new LowLevelNativeFormatReader(parameterBuffer, parameterBufferSize);
            myFuncEvalId = s_funcEvalId = (long)reader.GetUnsignedLong();
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

            Array newArray = null;
            RuntimeTypeHandle arrayTypeHandle = default(RuntimeTypeHandle);

            Exception ex = null;
            try
            {
                // Get an array RuntimeTypeHandle given an element's RuntimeTypeHandle and rank.
                // Pass false for isMdArray, and rank == -1 for SzArrays
                if (rank == 1 && lowerBounds[0] == 0)
                {
                    // TODO : throw exception with loc message
                    bool success = TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(arrElmTypeHandle, false, -1, out arrayTypeHandle);
                    Debug.Assert(success);
                    newArray = Internal.Runtime.Augments.RuntimeAugments.NewArray(arrayTypeHandle, dims[0]);
                }
                else
                {
                    // TODO : throw exception with loc message
                    bool success = TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(arrElmTypeHandle, true, (int)rank, out arrayTypeHandle);
                    Debug.Assert(success);
                    newArray = Internal.Runtime.Augments.RuntimeAugments.NewMultiDimArray(
                                  arrayTypeHandle,
                                  dims,
                                  lowerBounds);
                }
            }
            catch (Exception e)
            {
                ex = e;
            }

            object returnValue;
            if (ex != null)
            {
                returnValue = ex;
            }
            else
            {
                returnValue = newArray;
            }

            return new FuncEvalResult(returnValue, ex != null);
        }

        private unsafe static FuncEvalResult NewStringWithLength(ref long myFuncEvalId, byte* parameterBuffer, uint parameterBufferSize)
        {
            long* pFuncEvalId = (long*)parameterBuffer;
            myFuncEvalId = s_funcEvalId = *pFuncEvalId;
            parameterBuffer += 8;
            parameterBufferSize -= 8;
            string returnValue = Encoding.Unicode.GetString(parameterBuffer, (int)parameterBufferSize);
            return new FuncEvalResult(returnValue, false);
        }

        private unsafe static void InvokeFunction(ref InvokeFunctionData invokeFunctionData, ref LocalVariableSet arguments)
        {
            // Offset begins with 1 because we always skip setting the return value before we call the function
            int offset = 1;
            if (invokeFunctionData.thisObj != null)
            {
                // For constructors - caller does not pass the this pointer, instead, we constructed param.thisObj and pass it as the first argument
                arguments.SetVar<object>(offset, invokeFunctionData.thisObj);
                offset++;
            }
            for (int i = 0; i < invokeFunctionData.parameterValues.Length; i++)
            {
                IntPtr input = arguments.GetAddressOfVarData(i + offset);
                byte* pInput = (byte*)input;
                fixed (byte* pParam = invokeFunctionData.parameterValues[i])
                {
                    for (int j = 0; j < invokeFunctionData.parameterValues[i].Length; j++)
                    {
                        pInput[j] = pParam[j];
                    }
                }
            }

            // Obtain the target method address from the runtime
            IntPtr targetAddress = RuntimeAugments.RhpGetFuncEvalTargetAddress();

            LocalVariableType[] returnAndArgumentTypes = new LocalVariableType[invokeFunctionData.types.Length];
            for (int i = 0; i < returnAndArgumentTypes.Length; i++)
            {
                returnAndArgumentTypes[i] = new LocalVariableType(invokeFunctionData.types[i], false, false);
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

            bool isVoid = (RuntimeTypeHandle.Equals(invokeFunctionData.types[0], typeof(void).TypeHandle));

            object returnValue = null;
            if (ex != null)
            {
                returnValue = ex;
            }
            else if (invokeFunctionData.thisObj != null)
            {
                // For constructors - the debugger would like to get 'this' back
                returnValue = invokeFunctionData.thisObj;
            }
            else if (!isVoid)
            {
                IntPtr input = arguments.GetAddressOfVarData(0);
                returnValue = RuntimeAugments.RhBoxAny(input, invokeFunctionData.types[0].Value);
            }

            // Note that the return value could be null if the target function returned null
            invokeFunctionData.result = new FuncEvalResult(returnValue, ex != null);
        }

        private unsafe static void BuildDebuggerPreparedExternalReferences(LowLevelNativeFormatReader reader, out ulong[] debuggerPreparedExternalReferences)
        {
            uint eeTypeCount = reader.GetUnsigned();
            debuggerPreparedExternalReferences = new ulong[eeTypeCount];
            for (int i = 0; i < eeTypeCount; i++)
            {
                ulong eeType = reader.GetUnsignedLong();
                debuggerPreparedExternalReferences[i] = eeType;
            }
        }

        private unsafe static void ReturnToDebuggerWithReturn(FuncEvalResult funcEvalResult)
        {
            uint returnHandleIdentifier = funcEvalResult.ReturnHandleIdentifier;
            IntPtr returnValueHandlePointer = funcEvalResult.ReturnValueHandlePointer;
            bool isException = funcEvalResult.IsException;

            // Signal to the debugger the func eval completes

            DebuggerFuncEvalCompleteWithReturnResponse* debuggerFuncEvalCompleteWithReturnResponse = stackalloc DebuggerFuncEvalCompleteWithReturnResponse[1];
            debuggerFuncEvalCompleteWithReturnResponse->kind = isException ? DebuggerResponseKind.FuncEvalCompleteWithException : DebuggerResponseKind.FuncEvalCompleteWithReturn;
            debuggerFuncEvalCompleteWithReturnResponse->returnHandleIdentifier = returnHandleIdentifier;
            debuggerFuncEvalCompleteWithReturnResponse->returnAddress = (long)returnValueHandlePointer;
            IntPtr debuggerFuncEvalCompleteWithReturnResponsePointer = new IntPtr(debuggerFuncEvalCompleteWithReturnResponse);
            RuntimeAugments.RhpSendCustomEventToDebugger(debuggerFuncEvalCompleteWithReturnResponsePointer, Unsafe.SizeOf<DebuggerFuncEvalCompleteWithReturnResponse>());

            // debugger magic will make sure this function never returns, instead control will be transferred back to the point where the FuncEval begins
        }

    }
}
