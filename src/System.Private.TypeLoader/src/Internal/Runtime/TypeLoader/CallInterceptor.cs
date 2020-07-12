// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#if TARGET_ARM
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define CALLDESCR_FPARGREGSARERETURNREGS           // The return value floating point registers are the same as the argument registers
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define FEATURE_HFA
#elif TARGET_ARM64
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define CALLDESCR_FPARGREGSARERETURNREGS           // The return value floating point registers are the same as the argument registers
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define ENREGISTERED_PARAMTYPE_MAXSIZE
#define FEATURE_HFA
#elif TARGET_X86
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#define CALLINGCONVENTION_CALLEE_POPS
#elif TARGET_AMD64
#if UNIXAMD64
#define UNIX_AMD64_ABI
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#else
#endif
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define CALLDESCR_FPARGREGSARERETURNREGS           // The return value floating point registers are the same as the argument registers
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define ENREGISTERED_PARAMTYPE_MAXSIZE
#elif TARGET_WASM
#else
#error Unknown architecture!
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.Runtime.Augments;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerServices;
using Internal.NativeFormat;
using Internal.TypeSystem;
using Internal.Runtime.TypeLoader;
using Internal.Runtime.CallConverter;

using ArgIterator = Internal.Runtime.CallConverter.ArgIterator;
using CallingConvention = Internal.Runtime.CallConverter.CallingConvention;

namespace Internal.Runtime.CallInterceptor
{
    public delegate void LocalVariableSetFunc<T>(ref T param, ref LocalVariableSet variables);

    /// <summary>
    /// Represents a set of local variables. Must be used as if it has local variable scoping
    /// </summary>
    public struct LocalVariableSet
    {
        private unsafe IntPtr* _pbMemory;
        private LocalVariableType[] _types;

        /// <summary>
        /// Construct from memory. Must not be used with LocalVariableType[] where the types contains 
        /// GC references, or is itself a reference type, unless the memory is externally protected
        /// pbMemory MUST point to a region of memory of at least IntPtr.Size*types.Length, and must 
        /// be aligned for IntPtr access. Callers of this constructor are responsible for
        /// ensuring that the memory is appropriately gc-protected
        /// </summary>
        unsafe public LocalVariableSet(IntPtr* pbMemory, LocalVariableType[] types)
        {
            _pbMemory = pbMemory;
            _types = types;
        }

        /// <summary>
        /// Get the variable at index. Error checking is not performed
        /// </summary>
        public unsafe T GetVar<T>(int index)
        {
            IntPtr address = _pbMemory[index];
            if (_types[index].ByRef)
            {
                address = *(IntPtr*)address.ToPointer();
            }
#if CCCONVERTER_TRACE
            CallingConventionConverterLogger.WriteLine("READ " + (_types[index].ByRef ? "ByRef " : "") + "LocalVariableSet, _pbMemory:" + new IntPtr(_pbMemory).LowLevelToString() + "[" + index.LowLevelToString() + "]. " + _types[index].TypeInstanceFieldSize.LowLevelToString() + " bytes <- [" + address.LowLevelToString() + "]");
#endif
            return Unsafe.Read<T>((void*)address);
        }

        /// <summary>
        /// Set the variable at index. Error checking is not performed
        /// </summary>
        public unsafe void SetVar<T>(int index, T value)
        {
            IntPtr address = _pbMemory[index];
            if (_types[index].ByRef)
            {
                address = *(IntPtr*)address.ToPointer();
            }
#if CCCONVERTER_TRACE
            CallingConventionConverterLogger.WriteLine("WRITE " + (_types[index].ByRef ? "ByRef " : "") + "LocalVariableSet, _pbMemory:" + new IntPtr(_pbMemory).LowLevelToString() + "[" + index.LowLevelToString() + "]. " + _types[index].TypeInstanceFieldSize.LowLevelToString() + " bytes -> [" + address.LowLevelToString() + "]");
#endif
            Unsafe.Write<T>((void*)address, value);
        }

        /// <summary>
        /// Copy a byref from source to to this local variable set. Instead of copying the data a, la Get/Set,
        /// copy the actual byref pointer. This function may be used with pointer types as well, (although 
        /// more interesting is the case where its a translation between a pinned byref and a pointer)
        /// </summary>
        public unsafe void SetByRef(int targetIndex, ref LocalVariableSet sourceLocalSet, int sourceIndex)
        {
            if ((targetIndex >= _types.Length) || (sourceIndex >= sourceLocalSet._types.Length))
                throw new ArgumentOutOfRangeException();

            *((IntPtr*)_pbMemory[targetIndex]) = *((IntPtr*)sourceLocalSet._pbMemory[sourceIndex]);
        }

        /// <summary>
        /// Get the address of the variable data. This function must not be used with a non-pinned byref type 
        /// (IntPtr isn't GC protected in that case)
        /// </summary>
        public unsafe IntPtr GetAddressOfVarData(int index)
        {
            if (index >= _types.Length)
                throw new ArgumentOutOfRangeException();

            if (_types[index].ByRef)
            {
                return *((IntPtr*)_pbMemory[index]);
            }
            else
            {
                return _pbMemory[index];
            }
        }

        internal unsafe IntPtr* GetRawMemoryPointer()
        {
            return (IntPtr*)_pbMemory;
        }


        private struct SetupLocalVariableSetInfo<T>
        {
            public LocalVariableType[] Types;
            public LocalVariableSetFunc<T> Callback;
        }

        private unsafe delegate void SetupArbitraryLocalVariableSet_InnerDel<T>(IntPtr* localData, ref T context, ref SetupLocalVariableSetInfo<T> localVarSetInfo);
        private static unsafe void SetupArbitraryLocalVariableSet_Inner<T>(IntPtr* localData, ref T context, ref SetupLocalVariableSetInfo<T> localVarSetInfo)
        {
            LocalVariableSet localVars = new LocalVariableSet(localData, localVarSetInfo.Types);
            DefaultInitializeLocalVariableSet(ref localVars);
            localVarSetInfo.Callback(ref context, ref localVars);
        }

        /// <summary>
        /// Helper api to setup a space where a LocalVariableSet is defined. Note that the lifetime of the variable 
        /// set is the lifetime of until the callback function returns
        /// </summary>
        unsafe public static void SetupArbitraryLocalVariableSet<T>(LocalVariableSetFunc<T> callback, ref T param, LocalVariableType[] types) where T : struct
        {
            SetupLocalVariableSetInfo<T> localVarSetInfo = new SetupLocalVariableSetInfo<T>();
            localVarSetInfo.Callback = callback;
            localVarSetInfo.Types = types;

            RuntimeAugments.RunFunctionWithConservativelyReportedBuffer(
                ComputeNecessaryMemoryForStackLocalVariableSet(types),
                Intrinsics.AddrOf<SetupArbitraryLocalVariableSet_InnerDel<T>>(SetupArbitraryLocalVariableSet_Inner<T>),
                ref param,
                ref localVarSetInfo);
        }

        /// <summary>
        /// Helper api to initialize a local variable set initialized with as much memory as 
        /// ComputeNecessaryMemoryForStackLocalVariableSet specifies. Used as part of pattern for manual construction
        /// of LocalVariableSet
        /// </summary>
        public static unsafe void DefaultInitializeLocalVariableSet(ref LocalVariableSet localSet)
        {
            int localRegionOffset = IntPtr.Size * localSet._types.Length;
            byte* baseAddress = (byte*)localSet._pbMemory;

            for (int i = 0; i < localSet._types.Length; i++)
            {
                LocalVariableType type = localSet._types[i];

                // If the type is a byref, then the pointer in the pointers region actually will point to the byref, but
                // also allocate a target for the byref, and have the byref point to that
                if (type.ByRef)
                {
                    localRegionOffset = localRegionOffset.AlignUp(IntPtr.Size);
                    localSet._pbMemory[i] = (IntPtr)(baseAddress + localRegionOffset);
                    localRegionOffset += IntPtr.Size;
                    localRegionOffset = localRegionOffset.AlignUp(type.TypeInstanceFieldAlignment);
                    *((IntPtr*)localSet._pbMemory[i]) = (IntPtr)(baseAddress + localRegionOffset);
                }
                else
                {
                    localRegionOffset = localRegionOffset.AlignUp(type.TypeInstanceFieldAlignment);
                    localSet._pbMemory[i] = (IntPtr)(baseAddress + localRegionOffset);
                }

                localRegionOffset += type.TypeInstanceFieldSize;
            }
        }

        /// <summary>
        /// Compute the size of the memory region needed to hold the set of types provided
        /// </summary>
        public static int ComputeNecessaryMemoryForStackLocalVariableSet(LocalVariableType[] types)
        {
            int memoryNeeded = IntPtr.Size * types.Length;

            foreach (var type in types)
            {
                if (type.ByRef)
                {
                    memoryNeeded = memoryNeeded.AlignUp(IntPtr.Size);
                    memoryNeeded += IntPtr.Size;
                }

                memoryNeeded = memoryNeeded.AlignUp(type.TypeInstanceFieldAlignment);
                memoryNeeded += type.TypeInstanceFieldSize;
            }

            memoryNeeded = memoryNeeded.AlignUp(IntPtr.Size);

            return memoryNeeded;
        }

#if CCCONVERTER_TRACE
        public unsafe void DumpDebugInfo()
        {
            TypeSystemContext context = TypeSystemContextFactory.Create();
            {
                CallingConventionConverterLogger.WriteLine("LocalVariableSet @ 0x" + new IntPtr(_pbMemory).LowLevelToString());
                for (int i = 0; i < _types.Length; i++)
                {
                    CallingConventionConverterLogger.WriteLine("    " +
                        (_types[i].ByRef ? "byref @ 0x" : "      @ 0x") + GetAddressOfVarData(i).LowLevelToString() +
                        " - RTTH = " + context.ResolveRuntimeTypeHandle(_types[i].TypeHandle).ToString());
                }
            }
            TypeSystemContextFactory.Recycle(context);
        }
#endif
    }

    /// <summary>
    /// Abstraction for the type information needed to be a local
    /// </summary>
    public struct LocalVariableType
    {
        public LocalVariableType(RuntimeTypeHandle typeHandle, bool pinned, bool byRef)
        {
            TypeHandle = typeHandle;
            Pinned = pinned;
            ByRef = byRef;
        }

        public RuntimeTypeHandle TypeHandle;
        public bool Pinned;
        public bool ByRef;

        internal int TypeInstanceFieldSize
        {
            get
            {
                unsafe
                {
                    if (IsValueType)
                        return (int)TypeHandle.ToEETypePtr()->ValueTypeSize;
                    else
                        return IntPtr.Size;
                }
            }
        }

        internal int TypeInstanceFieldAlignment
        {
            get
            {
                unsafe
                {
                    return (int)TypeHandle.ToEETypePtr()->FieldAlignmentRequirement;
                }
            }
        }

        internal bool IsValueType
        {
            get
            {
                unsafe
                {
                    return TypeHandle.ToEETypePtr()->IsValueType;
                }
            }
        }
    }

    /// <summary>
    /// Arguments passed into CallInterceptor.ExecuteThunk
    /// </summary>
    public struct CallInterceptorArgs
    {
        /// <summary>
        /// The set of arguments and return value. The return value is located at the Zero-th index.
        /// </summary>
        public LocalVariableSet ArgumentsAndReturnValue;
        /// <summary>
        /// Convenience set of locals, most like for use with MakeDynamicCall
        /// </summary>
        public LocalVariableSet Locals;
    }

    /// <summary>
    /// Cache of information on how to make a dynamic call
    /// </summary>
    public class DynamicCallSignature
    {
        private CallConversionOperation[] _callConversionOpsNormal;
        private CallConversionOperation[] _callConversionOpsFatPtr;
        private CallingConvention _callingConvention;

        internal CallConversionOperation[] NormalOps
        {
            get
            {
                return _callConversionOpsNormal;
            }
        }

        internal CallConversionOperation[] FatOps
        {
            get
            {
                return _callConversionOpsFatPtr;
            }
        }

        internal CallingConvention CallingConvention
        {
            get
            {
                return _callingConvention;
            }
        }

        internal static LocalVariableType[] s_returnBlockDescriptor = new LocalVariableType[1] { new LocalVariableType() { ByRef = false, Pinned = true, TypeHandle = typeof(ReturnBlock).TypeHandle } };

        /// <summary>
        ///  Construct a DynamicCallSignature. This is a somewhat expensive object to create, so please consider caching it
        /// </summary>
        public DynamicCallSignature(CallingConvention callingConvention, LocalVariableType[] returnAndArgumentTypes, int returnAndArgTypesToUse)
        {
            _callConversionOpsNormal = ProduceOpcodesForDynamicCall(callingConvention, returnAndArgumentTypes, returnAndArgTypesToUse, false);
            if (callingConvention == CallingConvention.ManagedInstance || callingConvention == CallingConvention.ManagedStatic)
                _callConversionOpsFatPtr = ProduceOpcodesForDynamicCall(callingConvention, returnAndArgumentTypes, returnAndArgTypesToUse, true);

            _callingConvention = callingConvention;
        }

        private static CallConversionOperation[] ProduceOpcodesForDynamicCall(CallingConvention callingConvention, LocalVariableType[] returnAndArgumentTypes, int returnAndArgTypesToUse, bool fatFunctionPointer)
        {
            ArrayBuilder<CallConversionOperation> callConversionOps = new ArrayBuilder<CallConversionOperation>();

            bool hasThis = callingConvention == CallingConvention.ManagedInstance;
            int firstArgumentOffset = 1 + (hasThis ? 1 : 0);
            Debug.Assert(returnAndArgTypesToUse >= 1);
            Debug.Assert(returnAndArgTypesToUse <= returnAndArgumentTypes.Length);

            TypeHandle[] args = new TypeHandle[returnAndArgTypesToUse - firstArgumentOffset];
            TypeHandle returnType = new TypeHandle(returnAndArgumentTypes[0].ByRef, returnAndArgumentTypes[0].TypeHandle);

            for (int i = firstArgumentOffset; i < returnAndArgTypesToUse; i++)
            {
                args[i - firstArgumentOffset] = new TypeHandle(returnAndArgumentTypes[i].ByRef, returnAndArgumentTypes[i].TypeHandle);
            }

            ArgIteratorData data = new ArgIteratorData(hasThis, false, args, returnType);

            ArgIterator calleeArgs = new ArgIterator(data, callingConvention, fatFunctionPointer, false, null, false, false);

            // Ensure return block is setup
            int returnBlockSize = LocalVariableSet.ComputeNecessaryMemoryForStackLocalVariableSet(s_returnBlockDescriptor);
            callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y, returnBlockSize, CallConversionInterpreter.LocalBlock
#if CCCONVERTER_TRACE
                , "ReturnBlock"
#endif
                ));
            callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.DEFAULT_INIT_LOCALBLOCK_X, CallConversionInterpreter.LocalBlock));

            // Allocate transition block
            int nStackBytes = calleeArgs.SizeOfFrameArgumentArray();
            unsafe
            {
                int transitionBlockAllocSize = TransitionBlock.GetNegSpaceSize() + sizeof(TransitionBlock) + nStackBytes;
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.ALLOC_X_TRANSITIONBLOCK_BYTES, transitionBlockAllocSize));
            }

            if (calleeArgs.HasRetBuffArg())
            {
                // Setup ret buffer
                int ofsRetBuffArg = calleeArgs.GetRetBuffArgOffset();
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_TO_OFFSET_W_IN_TRANSITION_BLOCK, IntPtr.Size, CallConversionInterpreter.ArgBlock, 0, ofsRetBuffArg
#if CCCONVERTER_TRACE
                    , "ReturnBuffer"
#endif
                    ));
            }

            if (hasThis)
            {
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK, IntPtr.Size, CallConversionInterpreter.ArgBlock, 1, ArgIterator.GetThisOffset()
#if CCCONVERTER_TRACE
                    , "ThisPointer"
#endif
                    ));
            }

            if (calleeArgs.HasParamType())
            {
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.COPY_GENERIC_CONTEXT_TO_OFFSET_X_IN_TRANSITION_BLOCK, calleeArgs.GetParamTypeArgOffset()));
            }

            bool needsFloatArgs = false;

            for (int i = firstArgumentOffset; i < returnAndArgTypesToUse; i++)
            {
                int ofsCallee = calleeArgs.GetNextOffset();
                if (ofsCallee < 0)
                    needsFloatArgs = true;

                TypeHandle argTypeHandle;
                CorElementType argType = calleeArgs.GetArgType(out argTypeHandle);
                if (calleeArgs.IsArgPassedByRef() && argType != CorElementType.ELEMENT_TYPE_BYREF)
                {
                    callConversionOps.Add(new CallConversionOperation(
                        CallConversionOperation.OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_TO_OFFSET_W_IN_TRANSITION_BLOCK,
                        IntPtr.Size,
                        CallConversionInterpreter.ArgBlock,
                        IntPtr.Size * i,
                        ofsCallee
#if CCCONVERTER_TRACE
                        , "ByRef Arg #" + i.LowLevelToString()
#endif
                        ));
                }
                else
                {
                    //
                    // Converting by-ref values to non-by-ref form requires the converter to be capable of taking a pointer to a small integer
                    // value anywhere in memory and then copying the referenced value into an ABI-compliant pointer-sized "slot" which
                    // faithfully communicates the value.  In such cases, the argument slot prepared by the converter must conform to all
                    // sign/zero-extension rules mandated by the ABI.
                    //
                    // ARM32 requires all less-than-pointer-sized values to be sign/zero-extended when they are placed into pointer-sized
                    // slots (i.e., requires "producer-oriented" sign/zero-extension).  x86/amd64 do not have this requirement (i.e., the
                    // unused high bytes of the pointer-sized slot are ignored by the consumer and are allowed to take on any value); however
                    // to reduce the need for ever more #ifs in this file, this behavior will not be #if'd away. (Its not wrong, its just unnecessary)
                    //

                    switch (argType)
                    {
                        case CorElementType.ELEMENT_TYPE_I1:
                        case CorElementType.ELEMENT_TYPE_I2:
#if TARGET_64BIT
                        case CorElementType.ELEMENT_TYPE_I4:
#endif
                            callConversionOps.Add(new CallConversionOperation(
                                CallConversionOperation.OpCode.SIGNEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
                                calleeArgs.GetArgSize(),
                                CallConversionInterpreter.ArgBlock,
                                i,
                                ofsCallee
#if CCCONVERTER_TRACE
                                , "Arg #" + i.LowLevelToString()
#endif
                            ));
                            break;

                        case CorElementType.ELEMENT_TYPE_U1:
                        case CorElementType.ELEMENT_TYPE_BOOLEAN:
                        case CorElementType.ELEMENT_TYPE_U2:
                        case CorElementType.ELEMENT_TYPE_CHAR:
#if TARGET_64BIT
                        case CorElementType.ELEMENT_TYPE_U4:
#endif
                            callConversionOps.Add(new CallConversionOperation(
                                CallConversionOperation.OpCode.ZEROEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
                                calleeArgs.GetArgSize(),
                                CallConversionInterpreter.ArgBlock,
                                i,
                                ofsCallee
#if CCCONVERTER_TRACE
                                , "Arg #" + i.LowLevelToString()
#endif
                            ));
                            break;

                        default:
                            {
#if TARGET_ARM64
                                if (ofsCallee < 0 && argTypeHandle.IsHFA() && argTypeHandle.GetHFAType() == CorElementType.ELEMENT_TYPE_R4)
                                {
                                    // S and D registers overlap. The FP block of the transition block has 64-bit slots that are used for both floats and doubles.
                                    // When dealing with float HFAs, we need to copy the 32-bit floats into the 64-bit slots to match the format of the transition block's FP block.

                                    callConversionOps.Add(new CallConversionOperation(
                                        CallConversionOperation.OpCode.ARM64_COPY_X_HFA_FLOATS_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
                                        (int)argTypeHandle.GetSize() / 4,
                                        CallConversionInterpreter.ArgBlock,
                                        i,
                                        ofsCallee
#if CCCONVERTER_TRACE
                                        , "Arg #" + i.LowLevelToString()
#endif
                                    ));
                                    break;
                                }
#endif

                                callConversionOps.Add(new CallConversionOperation(
                                    CallConversionOperation.OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
                                    calleeArgs.GetArgSize(),
                                    CallConversionInterpreter.ArgBlock,
                                    i,
                                    ofsCallee
#if CCCONVERTER_TRACE
                                    , "Arg #" + i.LowLevelToString()
#endif
                                ));
                            }
                            break;
                    }
                }
            }

            int fpArgInfo = checked((int)calleeArgs.GetFPReturnSize());
            if (fpArgInfo >= CallConversionOperation.HasFPArgsFlag)
                throw new OverflowException();

            if (needsFloatArgs)
                fpArgInfo |= CallConversionOperation.HasFPArgsFlag;

            switch (callingConvention)
            {
                case CallingConvention.ManagedInstance:
                case CallingConvention.ManagedStatic:
                    callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.CALL_DESCR_MANAGED_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W, CallConversionInterpreter.LocalBlock, 0, calleeArgs.SizeOfFrameArgumentArray() / ArchitectureConstants.STACK_ELEM_SIZE, fpArgInfo));
                    break;

                case CallingConvention.StdCall:
                    callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.CALL_DESCR_NATIVE_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W, CallConversionInterpreter.LocalBlock, 0, calleeArgs.SizeOfFrameArgumentArray() / ArchitectureConstants.STACK_ELEM_SIZE, fpArgInfo));
                    break;

                default:
                    Debug.Fail("Unknown calling convention");
                    break;
            }

            if (!calleeArgs.HasRetBuffArg())
            {
                if (returnType.GetCorElementType() == CorElementType.ELEMENT_TYPE_VOID)
                {
                    // do nothing
                }
                else
                {
#if TARGET_ARM64
                    if (returnType.IsHFA() && returnType.GetHFAType() == CorElementType.ELEMENT_TYPE_R4)
                    {
                        // S and D registers overlap. The return buffer has 64-bit slots that are used for both floats and doubles.
                        // When dealing with float HFAs, we need to copy 32-bit float values from the 64-bit slots of the return buffer (A simple memcopy won't work here).

                        callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.ARM64_COPY_X_HFA_FLOATS_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z, checked((int)returnType.GetSize() / 4), CallConversionInterpreter.ArgBlock, 0));
                    }
                    else
#endif
                    {
                        // Copy from return buffer into return value local
                        callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.COPY_X_BYTES_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z, checked((int)returnType.GetSize()), CallConversionInterpreter.ArgBlock, 0));
                    }
                }
            }

            return callConversionOps.ToArray();
        }
    }

    internal struct CallConversionOperation
    {
        public const int HasFPArgsFlag = 0x40000000;

#if CCCONVERTER_TRACE
        public string Comment;

        public CallConversionOperation(OpCode op, int X, int Y, int Z, int W, string Comment = null)
        {
            this.Op = op;
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.W = W;
            this.Comment = Comment;
        }
        public CallConversionOperation(OpCode op, int X, int Y, int Z, string Comment = null) : this(op, X, Y, Z, 0, Comment) { }
        public CallConversionOperation(OpCode op, int X, int Y, string Comment = null) : this(op, X, Y, 0, 0, Comment) { }
        public CallConversionOperation(OpCode op, int X, string Comment = null) : this(op, X, 0, 0, 0, Comment) { }
        public CallConversionOperation(OpCode op, string Comment = null) : this(op, 0, 0, 0, 0, Comment) { }

        public string DebugString
        {
            get
            {
                string s = "";

                switch (Op)
                {
                    case OpCode.ALLOC_X_TRANSITIONBLOCK_BYTES:
                        s = "ALLOC_X_TRANSITIONBLOCK_BYTES";
                        break;
                    case OpCode.ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y:
                        s = "ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y";
                        break;
                    case OpCode.DEFAULT_INIT_LOCALBLOCK_X:
                        s = "DEFAULT_INIT_LOCALBLOCK_X";
                        break;
                    case OpCode.SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_TRANSITION_BLOCK:
                        s = "SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_LOCALBLOCK:
                        s = "SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_LOCALBLOCK";
                        break;
                    case OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        s = "COPY_X_BYTES_FROM_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_TO_OFFSET_W_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        s = "COPY_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.ZEROEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        s = "ZEROEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.SIGNEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        s = "SIGNEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.COPY_X_BYTES_TO_LOCALBLOCK_Y_POINTER_Z_FROM_OFFSET_W_IN_TRANSITION_BLOCK:
                        s = "COPY_X_BYTES_TO_LOCALBLOCK_Y_POINTER_Z_FROM_OFFSET_W_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.COPY_X_BYTES_TO_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_FROM_OFFSET_W_IN_TRANSITION_BLOCK:
                        s = "COPY_X_BYTES_TO_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_FROM_OFFSET_W_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.CALL_INTERCEPTOR:
                        s = "CALL_INTERCEPTOR";
                        break;
                    case OpCode.SETUP_CALLERPOP_X_BYTES:
                        s = "SETUP_CALLERPOP_X_BYTES";
                        break;
                    case OpCode.RETURN_VOID:
                        s = "RETURN_VOID";
                        break;
                    case OpCode.RETURN_RETBUF_FROM_OFFSET_X_IN_TRANSITION_BLOCK:
                        s = "RETURN_RETBUF_FROM_OFFSET_X_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.RETURN_FLOATINGPOINT_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        s = "RETURN_FLOATINGPOINT_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z";
                        break;
                    case OpCode.RETURN_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        s = "RETURN_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z";
                        break;
                    case OpCode.RETURN_SIGNEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        s = "RETURN_SIGNEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z";
                        break;
                    case OpCode.RETURN_ZEROEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        s = "RETURN_ZEROEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z";
                        break;
                    case OpCode.CALL_DESCR_MANAGED_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W:
                        s = "CALL_DESCR_MANAGED_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W";
                        break;
                    case OpCode.CALL_DESCR_NATIVE_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W:
                        s = "CALL_DESCR_NATIVE_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W";
                        break;
                    case OpCode.COPY_X_BYTES_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z:
                        s = "COPY_X_BYTES_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z";
                        break;
                    case OpCode.COPY_GENERIC_CONTEXT_TO_OFFSET_X_IN_TRANSITION_BLOCK:
                        s = "COPY_GENERIC_CONTEXT_TO_OFFSET_X_IN_TRANSITION_BLOCK";
                        break;
#if TARGET_ARM64
                    case OpCode.ARM64_COMPACT_X_FLOATS_INTO_HFA_AT_OFFSET_Y_IN_TRANSITION_BLOCK:
                        s = "ARM64_COMPACT_X_FLOATS_INTO_HFA_AT_OFFSET_Y_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.ARM64_EXPAND_X_FLOATS_INTO_HFA_IN_RETURN_BLOCK:
                        s = "ARM64_EXPAND_X_FLOATS_INTO_HFA_IN_RETURN_BLOCK";
                        break;
                    case OpCode.ARM64_COPY_X_HFA_FLOATS_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        s = "ARM64_COPY_X_HFA_FLOATS_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK";
                        break;
                    case OpCode.ARM64_COPY_X_HFA_FLOATS_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z:
                        s = "ARM64_COPY_X_HFA_FLOATS_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z";
                        break;
#endif
                    default:
                        s = "";
                        break;
                }

                s = s.Replace("_X_", "_" + X.LowLevelToString() + "_");
                s = s.Replace("_Y_", "_" + Y.LowLevelToString() + "_");
                s = s.Replace("_Z_", "_" + Z.LowLevelToString() + "_");
                s = s.Replace("_W_", "_" + W.LowLevelToString() + "_");

                if (s.EndsWith("_X"))
                    s = s.Substring(0, s.Length - 1) + X.LowLevelToString();
                if (s.EndsWith("_Y"))
                    s = s.Substring(0, s.Length - 1) + Y.LowLevelToString();
                if (s.EndsWith("_Z"))
                    s = s.Substring(0, s.Length - 1) + Z.LowLevelToString();
                if (s.EndsWith("_W"))
                    s = s.Substring(0, s.Length - 1) + W.LowLevelToString();

                if (!String.IsNullOrEmpty(Comment))
                    s += " - Comment: " + Comment;

                return s;
            }
        }

#else
        public CallConversionOperation(OpCode op, int X, int Y, int Z, int W)
        {
            this.Op = op;
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.W = W;
        }
        public CallConversionOperation(OpCode op, int X, int Y, int Z)
        {
            this.Op = op;
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.W = 0;
        }
        public CallConversionOperation(OpCode op, int X, int Y)
        {
            this.Op = op;
            this.X = X;
            this.Y = Y;
            this.Z = 0;
            this.W = 0;
        }
        public CallConversionOperation(OpCode op, int X)
        {
            this.Op = op;
            this.X = X;
            this.Y = 0;
            this.Z = 0;
            this.W = 0;
        }
        public CallConversionOperation(OpCode op)
        {
            this.Op = op;
            this.X = 0;
            this.Y = 0;
            this.Z = 0;
            this.W = 0;
        }
#endif
        public enum OpCode
        {
            ALLOC_X_TRANSITIONBLOCK_BYTES,
            ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y,
            DEFAULT_INIT_LOCALBLOCK_X,
            SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_TRANSITION_BLOCK,
            SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_LOCALBLOCK,
            COPY_X_BYTES_FROM_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_TO_OFFSET_W_IN_TRANSITION_BLOCK,
            COPY_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
            SIGNEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
            ZEROEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
            COPY_X_BYTES_TO_LOCALBLOCK_Y_POINTER_Z_FROM_OFFSET_W_IN_TRANSITION_BLOCK,
            COPY_X_BYTES_TO_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_FROM_OFFSET_W_IN_TRANSITION_BLOCK,
            CALL_INTERCEPTOR,
            SETUP_CALLERPOP_X_BYTES,
            RETURN_VOID,
            RETURN_RETBUF_FROM_OFFSET_X_IN_TRANSITION_BLOCK,
            RETURN_FLOATINGPOINT_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z,
            RETURN_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z,
            RETURN_SIGNEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z,
            RETURN_ZEROEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z,
            CALL_DESCR_MANAGED_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W,
            CALL_DESCR_NATIVE_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W,
            COPY_X_BYTES_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z,
            COPY_GENERIC_CONTEXT_TO_OFFSET_X_IN_TRANSITION_BLOCK,
#if TARGET_ARM64
            ARM64_COMPACT_X_FLOATS_INTO_HFA_AT_OFFSET_Y_IN_TRANSITION_BLOCK,
            ARM64_EXPAND_X_FLOATS_INTO_HFA_IN_RETURN_BLOCK,
            ARM64_COPY_X_HFA_FLOATS_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK,
            ARM64_COPY_X_HFA_FLOATS_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z
#endif
        }

        public OpCode Op;
        public int X;
        public int Y;
        public int Z;
        public int W;
    }

    internal static class CallConversionInterpreter
    {
        public const int ArgBlock = 1;
        public const int LocalBlock = 2;

        internal unsafe struct CallConversionInterpreterLocals
        {
            public LocalVariableSet Locals1;
            public LocalVariableSet Locals2;
            public byte* TransitionBlockPtr;
            public int Index;
            public CallConversionOperation[] Opcodes;
            public CallInterceptor Interceptor;

            public LocalVariableType[] LocalVarSetTypes1;
            public LocalVariableType[] LocalVarSetTypes2;

            public IntPtr IntPtrReturnVal;
            public IntPtr IntPtrFnPtr;
            public IntPtr IntPtrGenericContextArg;

            public LocalVariableSet GetLocalBlock(int blockNum)
            {
                if (blockNum == 1)
                {
                    return Locals1;
                }
                else
                {
                    Debug.Assert(blockNum == 2);
                    return Locals2;
                }
            }
        }

        private unsafe delegate void SetupBlockDelegate(void* pBuffer, ref CallConversionInterpreterLocals locals);
        private static unsafe void SetupLocalsBlock1(void* pBuffer, ref CallConversionInterpreterLocals locals)
        {
#if CCCONVERTER_TRACE
            CallingConventionConverterLogger.WriteLine("     -> Setup Locals Block 1 @ " + new IntPtr(pBuffer).LowLevelToString());
#endif

            locals.Locals1 = new LocalVariableSet((IntPtr*)pBuffer, locals.LocalVarSetTypes1);
            Interpret(ref locals);
        }

        private static unsafe void SetupLocalsBlock2(void* pBuffer, ref CallConversionInterpreterLocals locals)
        {
#if CCCONVERTER_TRACE
            CallingConventionConverterLogger.WriteLine("     -> Setup Locals Block 2 @ " + new IntPtr(pBuffer).LowLevelToString());
#endif

            locals.Locals2 = new LocalVariableSet((IntPtr*)pBuffer, locals.LocalVarSetTypes2);
            Interpret(ref locals);
        }

        private static unsafe void SetupTransitionBlock(void* pBuffer, ref CallConversionInterpreterLocals locals)
        {
#if CCCONVERTER_TRACE
            CallingConventionConverterLogger.WriteLine("     -> Setup Transition Block @ " + new IntPtr(pBuffer).LowLevelToString());
#endif

            locals.TransitionBlockPtr = ((byte*)pBuffer) + TransitionBlock.GetNegSpaceSize();
            Interpret(ref locals);
        }

        public static unsafe void Interpret(ref CallConversionInterpreterLocals locals)
        {
            while (locals.Index < locals.Opcodes.Length)
            {
                CallConversionOperation op = locals.Opcodes[locals.Index++];

#if CCCONVERTER_TRACE
                CallingConventionConverterLogger.WriteLine("  " + op.DebugString);
#endif

                switch (op.Op)
                {
                    case CallConversionOperation.OpCode.DEFAULT_INIT_LOCALBLOCK_X:
                        {
                            LocalVariableSet localBlock = locals.GetLocalBlock(op.X);
                            LocalVariableSet.DefaultInitializeLocalVariableSet(ref localBlock);
                        }
                        break;

                    case CallConversionOperation.OpCode.ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y:
                        if (op.Y == 1)
                        {
                            RuntimeAugments.RunFunctionWithConservativelyReportedBuffer(op.X, Intrinsics.AddrOf<SetupBlockDelegate>(SetupLocalsBlock1), ref locals);
                        }
                        else
                        {
                            Debug.Assert(op.Y == 2);
                            RuntimeAugments.RunFunctionWithConservativelyReportedBuffer(op.X, Intrinsics.AddrOf<SetupBlockDelegate>(SetupLocalsBlock2), ref locals);
                        }
                        break;

                    case CallConversionOperation.OpCode.ALLOC_X_TRANSITIONBLOCK_BYTES:
                        RuntimeAugments.RunFunctionWithConservativelyReportedBuffer(op.X, Intrinsics.AddrOf<SetupBlockDelegate>(SetupTransitionBlock), ref locals);
                        break;

                    case CallConversionOperation.OpCode.CALL_INTERCEPTOR:
                        {
                            CallInterceptorArgs args = new CallInterceptorArgs();
                            args.ArgumentsAndReturnValue = locals.Locals1;
                            args.Locals = locals.Locals2;
                            locals.Interceptor.ThunkExecute(ref args);
                        }
                        break;

#if TARGET_ARM64
                    case CallConversionOperation.OpCode.ARM64_COMPACT_X_FLOATS_INTO_HFA_AT_OFFSET_Y_IN_TRANSITION_BLOCK:
                        {
                            Debug.Assert(op.X > 0 && op.X <= 4);

                            float* pFPRegs = (float*)(locals.TransitionBlockPtr + op.Y);
                            for (int i = 1; i < op.X; i++)
                                pFPRegs[i] = pFPRegs[i * 2];

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Compact " + op.X.LowLevelToString() + " ARM64 HFA floats at [" + new IntPtr(pFPRegs).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.ARM64_EXPAND_X_FLOATS_INTO_HFA_IN_RETURN_BLOCK:
                        {
                            Debug.Assert(op.X > 0 && op.X <= 4);

                            byte* pReturnBlock = locals.TransitionBlockPtr + TransitionBlock.GetOffsetOfFloatArgumentRegisters();
                            for (int i = op.X - 1; i >= 0; i--)
                            {
                                float value = ((float*)pReturnBlock)[i];
                                *((IntPtr*)pReturnBlock + i) = IntPtr.Zero;     // Clear destination slot to zeros before copying the float value
                                *((float*)((IntPtr*)pReturnBlock + i)) = value;
                            }

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Expand " + op.X.LowLevelToString() + " ARM64 HFA floats at [" + new IntPtr(pReturnBlock).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.ARM64_COPY_X_HFA_FLOATS_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        {
                            Debug.Assert(op.X > 0 && op.X <= 4);

                            float* pSrc = (float*)(locals.GetLocalBlock(op.Y).GetRawMemoryPointer()[op.Z]);
                            float* pDst = (float*)(locals.TransitionBlockPtr + op.W);
                            for (int i = 0; i < op.X; i++)
                            {
                                ((IntPtr*)pDst)[i] = IntPtr.Zero;           // Clear destination slot to zeros before copying the float value
                                pDst[i * 2] = pSrc[i];
                            }

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.X.LowLevelToString() + " ARM64 HFA floats from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.ARM64_COPY_X_HFA_FLOATS_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z:
                        {
                            Debug.Assert(op.X > 0 && op.X <= 4);

                            float* pSrc = (float*)locals.IntPtrReturnVal.ToPointer();
                            float* pDst = (float*)(locals.GetLocalBlock(op.Y).GetRawMemoryPointer()[op.Z].ToPointer());
                            for (int i = 0; i < op.X; i++)
                                pDst[i] = pSrc[i * 2];

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.X.LowLevelToString() + " ARM64 HFA floats from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;
#endif

                    case CallConversionOperation.OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        {
                            void* pSrc = ((byte*)locals.GetLocalBlock(op.Y).GetRawMemoryPointer()) + op.Z;
                            void* pDst = locals.TransitionBlockPtr + op.W;
                            Buffer.MemoryCopy(pSrc, pDst, op.X, op.X);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.X.LowLevelToString() + " bytes from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.COPY_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        {
                            void* pSrc = locals.GetLocalBlock(op.Y).GetRawMemoryPointer()[op.Z].ToPointer();
                            void* pDst = locals.TransitionBlockPtr + op.W;
                            Buffer.MemoryCopy(pSrc, pDst, op.X, op.X);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.X.LowLevelToString() + " bytes from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.SIGNEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        {
                            void* pSrc = locals.GetLocalBlock(op.Y).GetRawMemoryPointer()[op.Z].ToPointer();
                            void* pDst = locals.TransitionBlockPtr + op.W;
                            CallConverterThunk.SignExtend(pSrc, pDst, op.X);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> SignExtend " + op.X.LowLevelToString() + " bytes from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.ZEROEXTEND_X_BYTES_FROM_LOCALBLOCK_Y_POINTER_Z_TO_OFFSET_W_IN_TRANSITION_BLOCK:
                        {
                            void* pSrc = locals.GetLocalBlock(op.Y).GetRawMemoryPointer()[op.Z].ToPointer();
                            void* pDst = locals.TransitionBlockPtr + op.W;
                            CallConverterThunk.ZeroExtend(pSrc, pDst, op.X);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> ZeroExtend " + op.X.LowLevelToString() + " bytes from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.COPY_X_BYTES_TO_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_FROM_OFFSET_W_IN_TRANSITION_BLOCK:
                        {
                            void* pSrc = locals.TransitionBlockPtr + op.W;
                            void* pDst = ((byte*)locals.GetLocalBlock(op.Y).GetRawMemoryPointer()) + op.Z;
                            Buffer.MemoryCopy(pSrc, pDst, op.X, op.X);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.X.LowLevelToString() + " bytes " + new IntPtr(pSrc).LowLevelToString() + " -> " + new IntPtr(pDst).LowLevelToString());
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.COPY_X_BYTES_TO_LOCALBLOCK_Y_POINTER_Z_FROM_OFFSET_W_IN_TRANSITION_BLOCK:
                        {
                            void* pSrc = locals.TransitionBlockPtr + op.W;
                            void* pDst = locals.GetLocalBlock(op.Y).GetRawMemoryPointer()[op.Z].ToPointer();
                            Buffer.MemoryCopy(pSrc, pDst, op.X, op.X);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.X.LowLevelToString() + " bytes from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_LOCALBLOCK:
                        {
                            locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y] = (IntPtr)(((byte*)locals.GetLocalBlock(op.X).GetRawMemoryPointer()) + op.Z);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Set " +
                                new IntPtr(locals.GetLocalBlock(op.X).GetRawMemoryPointer()).LowLevelToString() + "[" + op.Y.LowLevelToString() + "] = " +
                                new IntPtr(((byte*)locals.GetLocalBlock(op.X).GetRawMemoryPointer()) + op.Z).LowLevelToString());
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_TRANSITION_BLOCK:
                        {
                            locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y] = (IntPtr)(locals.TransitionBlockPtr + op.Z);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Set " +
                                new IntPtr(locals.GetLocalBlock(op.X).GetRawMemoryPointer()).LowLevelToString() + "[" + op.Y.LowLevelToString() + "] = " +
                                new IntPtr(locals.TransitionBlockPtr + op.Z).LowLevelToString());
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.RETURN_VOID:
                        locals.IntPtrReturnVal = CallConverterThunk.ReturnVoidReturnThunk;
                        break;

                    case CallConversionOperation.OpCode.SETUP_CALLERPOP_X_BYTES:
#if TARGET_X86
                        ((TransitionBlock*)locals.TransitionBlockPtr)->m_argumentRegisters.ecx = new IntPtr(op.X);
#else
                        Debug.Assert(false);
#endif
                        break;

                    case CallConversionOperation.OpCode.RETURN_RETBUF_FROM_OFFSET_X_IN_TRANSITION_BLOCK:
                        {
#if TARGET_X86
                            CallConverterThunk.SetupCallerActualReturnData(locals.TransitionBlockPtr);
                            // On X86 the return buffer pointer is returned in eax.
                            CallConverterThunk.t_NonArgRegisterReturnSpace.returnValue = *(IntPtr*)(locals.TransitionBlockPtr + op.X);
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnIntegerPointReturnThunk;
#else
                            // Because the return value was really returned on the heap, simply return as if void was returned.
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnVoidReturnThunk;
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.RETURN_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        {
#if TARGET_X86
                            CallConverterThunk.SetupCallerActualReturnData(locals.TransitionBlockPtr);
                            fixed (ReturnBlock* retBlk = &CallConverterThunk.t_NonArgRegisterReturnSpace)
                            {
                                Buffer.MemoryCopy(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), retBlk, op.Z, op.Z);
                            }
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnIntegerPointReturnThunk;
#else
                            byte* returnBlock = locals.TransitionBlockPtr + TransitionBlock.GetOffsetOfArgumentRegisters();
                            MemoryHelpers.Memset((IntPtr)returnBlock, IntPtr.Size, 0);
                            Buffer.MemoryCopy(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), returnBlock, op.Z, op.Z);
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnIntegerPointReturnThunk;
#endif

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.Z.LowLevelToString() + " bytes from [" + new IntPtr(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer()).LowLevelToString() + "] to return block");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.RETURN_SIGNEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        {
#if TARGET_X86
                            CallConverterThunk.SetupCallerActualReturnData(locals.TransitionBlockPtr);
                            fixed (ReturnBlock* retBlk = &CallConverterThunk.t_NonArgRegisterReturnSpace)
                            {
                                CallConverterThunk.SignExtend(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), retBlk, op.Z);
                            }
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnIntegerPointReturnThunk;
#else
                            byte* returnBlock = locals.TransitionBlockPtr + TransitionBlock.GetOffsetOfArgumentRegisters();
                            CallConverterThunk.SignExtend(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), returnBlock, op.Z);
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnIntegerPointReturnThunk;
#endif

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> SignExtend " + op.Z.LowLevelToString() + " bytes from [" + new IntPtr(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer()).LowLevelToString() + "] to return block");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.RETURN_ZEROEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        {
#if TARGET_X86
                            CallConverterThunk.SetupCallerActualReturnData(locals.TransitionBlockPtr);
                            fixed (ReturnBlock* retBlk = &CallConverterThunk.t_NonArgRegisterReturnSpace)
                            {
                                CallConverterThunk.ZeroExtend(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), retBlk, op.Z);
                            }
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnIntegerPointReturnThunk;
#else
                            byte* returnBlock = locals.TransitionBlockPtr + TransitionBlock.GetOffsetOfArgumentRegisters();
                            CallConverterThunk.ZeroExtend(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), returnBlock, op.Z);
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnIntegerPointReturnThunk;
#endif

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> ZeroExtend " + op.Z.LowLevelToString() + " bytes from [" + new IntPtr(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer()).LowLevelToString() + "] to return block");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.RETURN_FLOATINGPOINT_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z:
                        {
#if CALLDESCR_FPARGREGSARERETURNREGS
                            byte* pReturnBlock = locals.TransitionBlockPtr + TransitionBlock.GetOffsetOfFloatArgumentRegisters();
                            MemoryHelpers.Memset((IntPtr)pReturnBlock, IntPtr.Size, 0);
                            Buffer.MemoryCopy(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), pReturnBlock, op.Z, op.Z);
                            locals.IntPtrReturnVal = CallConverterThunk.ReturnVoidReturnThunk;
#elif TARGET_X86
                            CallConverterThunk.SetupCallerActualReturnData(locals.TransitionBlockPtr);
                            fixed (ReturnBlock* pReturnBlock = &CallConverterThunk.t_NonArgRegisterReturnSpace)
                            {
                                Buffer.MemoryCopy(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer(), pReturnBlock, op.Z, op.Z);
                            }
                            if (op.Z == 4)
                            {
                                locals.IntPtrReturnVal = CallConverterThunk.ReturnFloatingPointReturn4Thunk;
                            }
                            else
                            {
                                Debug.Assert(op.Z == 8);
                                locals.IntPtrReturnVal = CallConverterThunk.ReturnFloatingPointReturn8Thunk;
                            }
#else
                            Debug.Assert(false);
#endif

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.Z.LowLevelToString() + " bytes from [" + new IntPtr(locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y].ToPointer()).LowLevelToString() + "] to return block");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.CALL_DESCR_MANAGED_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W:
                    case CallConversionOperation.OpCode.CALL_DESCR_NATIVE_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W:
                        {
                            locals.IntPtrReturnVal = locals.GetLocalBlock(op.X).GetRawMemoryPointer()[op.Y];
                            CallConverterThunk.CallDescrData callDescrData = new CallConverterThunk.CallDescrData();
                            callDescrData.fpReturnSize = (uint)(op.W & ~CallConversionOperation.HasFPArgsFlag);
                            callDescrData.numStackSlots = op.Z;
                            callDescrData.pArgumentRegisters = (ArgumentRegisters*)(locals.TransitionBlockPtr + TransitionBlock.GetOffsetOfArgumentRegisters());
#if CALLDESCR_FPARGREGS
                            // Under CALLDESCR_FPARGREGS -ve offsets indicate arguments in floating point registers. If we
                            // have at least one such argument we point the call worker at the floating point area of the
                            // frame (we leave it null otherwise since the worker can perform a useful optimization if it
                            // knows no floating point registers need to be set up).
                            if ((op.W & CallConversionOperation.HasFPArgsFlag) != 0)
                                callDescrData.pFloatArgumentRegisters = (FloatArgumentRegisters*)(locals.TransitionBlockPtr + TransitionBlock.GetOffsetOfFloatArgumentRegisters());
#endif
                            callDescrData.pReturnBuffer = locals.IntPtrReturnVal.ToPointer();
                            callDescrData.pSrc = locals.TransitionBlockPtr + sizeof(TransitionBlock);
                            callDescrData.pTarget = locals.IntPtrFnPtr.ToPointer();
                            if (op.Op == CallConversionOperation.OpCode.CALL_DESCR_MANAGED_WITH_RETBUF_AS_LOCALBLOCK_X_POINTER_Y_STACKSLOTS_Z_FPCALLINFO_W)
                                RuntimeAugments.CallDescrWorker(new IntPtr(&callDescrData));
                            else
                                RuntimeAugments.CallDescrWorkerNative(new IntPtr(&callDescrData));
                        }
                        break;

                    case CallConversionOperation.OpCode.COPY_X_BYTES_FROM_RETBUF_TO_LOCALBLOCK_Y_POINTER_Z:
                        {
                            void* pSrc = locals.IntPtrReturnVal.ToPointer();
                            void* pDst = locals.GetLocalBlock(op.Y).GetRawMemoryPointer()[op.Z].ToPointer();
                            Buffer.MemoryCopy(pSrc, pDst, op.X, op.X);

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Copy " + op.X.LowLevelToString() + " bytes from [" + new IntPtr(pSrc).LowLevelToString() + "] to [" + new IntPtr(pDst).LowLevelToString() + "]");
#endif
                        }
                        break;

                    case CallConversionOperation.OpCode.COPY_GENERIC_CONTEXT_TO_OFFSET_X_IN_TRANSITION_BLOCK:
                        {
                            *(IntPtr*)(locals.TransitionBlockPtr + op.X) = locals.IntPtrGenericContextArg;

#if CCCONVERTER_TRACE
                            CallingConventionConverterLogger.WriteLine("     -> Set [" + new IntPtr(locals.TransitionBlockPtr + op.X).LowLevelToString() + "] = " + locals.IntPtrGenericContextArg.LowLevelToString());
#endif
                        }
                        break;

                    default:
                        Debug.Fail("Unknown call convention interpreter opcode");
                        break;
                }
            }
        }
    }

    /// <summary>
    /// CallInterceptor abstract class. To implement a call interceptor, derive from this class and implement the abstract methods
    /// </summary>
    public abstract class CallInterceptor
    {
        private bool _nativeToManaged;
        private int _id;
        private IntPtr _thunkAddress;

        private static object s_thunkPoolHeap;

        /// <summary>
        /// Construct a call interceptor object. At time of construction whether it is a native to managed, or managed to managed 
        /// call interceptor must be known. Derive from this type to implement custom call interceptors.
        /// </summary>
        protected CallInterceptor(bool nativeToManaged)
        {
            _nativeToManaged = nativeToManaged;
        }

        /// <summary>
        /// Array of size >= 1
        /// Return type is the first type, the rest are parameters
        /// This function is called when the thunk is executed at least once.
        /// </summary>
        /// 
        public abstract LocalVariableType[] ArgumentAndReturnTypes { get; }
        /// <summary>
        /// Calling convention of the interceptor.
        /// This function is called when the thunk is executed at least once.
        /// </summary>
        public abstract CallingConvention CallingConvention { get; }

        /// <summary>
        /// Extra local variables to create. This is intended as a convenience feature for developers of CallInterceptors that 
        /// immediately make dynamic calls.
        /// This function is called when the thunk is executed at least once.
        /// </summary>
        public abstract LocalVariableType[] LocalVariableTypes { get; }

        internal static CallConverterThunk.CallingConventionConverter_CommonCallingStub_PointerData s_managedToManagedCommonStubData;
        internal static CallConverterThunk.CallingConventionConverter_CommonCallingStub_PointerData s_nativeToManagedCommonStubData;
        internal static LowLevelList<CallInterceptor> s_callInterceptors = new LowLevelList<CallInterceptor>();
        internal static LowLevelList<int> s_freeCallInterceptorIds = new LowLevelList<int>();
        private static int s_countFreeCallInterceptorId = 0;

        static CallInterceptor()
        {
            s_managedToManagedCommonStubData = CallConverterThunk.s_commonStubData;
            s_managedToManagedCommonStubData.ManagedCallConverterThunk = Intrinsics.AddrOf<CallInterceptorThunkDelegate>(CallInterceptorThunk);
            s_nativeToManagedCommonStubData = CallConverterThunk.s_commonStubData;
            s_nativeToManagedCommonStubData.ManagedCallConverterThunk = Intrinsics.AddrOf<CallInterceptorThunkDelegate>(CallInterceptorThunkUnmanagedCallersOnly);
        }

        /// <summary>
        /// Callback executed when the function pointer returned by GetThunkAddress is called
        /// </summary>
        public abstract void ThunkExecute(ref CallInterceptorArgs callInterceptor);

        private int GetThunkId()
        {
            int newId = 0;
            if (s_countFreeCallInterceptorId > 0)
            {
                newId = s_freeCallInterceptorIds[s_countFreeCallInterceptorId - 1];
                s_countFreeCallInterceptorId--;
            }
            else
            {
                newId = s_callInterceptors.Count;
                s_callInterceptors.Add(null);
            }

            _id = newId;
            s_callInterceptors[newId] = this;
            return newId;
        }


        /// <summary>
        /// Get the function pointer for this call interceptor. It will create the function pointer on 
        /// first access, or after it has been freed via FreeThunk()
        /// </summary>
        public IntPtr GetThunkAddress()
        {
            if (_thunkAddress == IntPtr.Zero)
            {
                lock (s_callInterceptors)
                {
                    if (_thunkAddress == IntPtr.Zero)
                    {
                        int thunkId = GetThunkId();

                        if (s_thunkPoolHeap == null)
                        {
                            s_thunkPoolHeap = RuntimeAugments.CreateThunksHeap(CallConverterThunk.CommonInputThunkStub);
                            Debug.Assert(s_thunkPoolHeap != null);
                        }

                        _thunkAddress = RuntimeAugments.AllocateThunk(s_thunkPoolHeap);
                        Debug.Assert(_thunkAddress != IntPtr.Zero);

                        unsafe
                        {
                            if (_nativeToManaged)
                            {
                                fixed (CallConverterThunk.CallingConventionConverter_CommonCallingStub_PointerData* commonStubData = &s_nativeToManagedCommonStubData)
                                {
                                    RuntimeAugments.SetThunkData(s_thunkPoolHeap, _thunkAddress, new IntPtr(thunkId), new IntPtr(commonStubData));
                                }
                            }
                            else
                            {
                                fixed (CallConverterThunk.CallingConventionConverter_CommonCallingStub_PointerData* commonStubData = &s_managedToManagedCommonStubData)
                                {
                                    RuntimeAugments.SetThunkData(s_thunkPoolHeap, _thunkAddress, new IntPtr(thunkId), new IntPtr(commonStubData));
                                }
                            }
                        }
                    }
                }
            }

            return _thunkAddress;
        }

        /// <summary>
        /// Free the underlying memory associated with the thunk. Once this is called, the old thunk address
        /// is invalid.
        /// </summary>
        public void FreeThunk()
        {
            FreeThunk(_thunkAddress);
            _thunkAddress = IntPtr.Zero;
            _id = 0;
        }

        /// <summary>
        /// Free the specified thunk. Once this is called, the old thunk address is invalid.
        /// </summary>
        /// <param name="thunkAddress"></param>
        public static void FreeThunk(IntPtr thunkAddress)
        {
            if (thunkAddress != IntPtr.Zero)
            {
                lock (s_callInterceptors)
                {
                    if (thunkAddress != IntPtr.Zero)
                    {
                        IntPtr context;
                        if (RuntimeAugments.TryGetThunkData(s_thunkPoolHeap, thunkAddress, out context, out _))
                        {
                            int id = context.ToInt32();
                            s_callInterceptors[id] = null;
                            if (s_countFreeCallInterceptorId == s_freeCallInterceptorIds.Count)
                            {
                                s_freeCallInterceptorIds.Add(id);
                            }
                            else
                            {
                                s_freeCallInterceptorIds[s_countFreeCallInterceptorId] = id;
                            }

                            s_countFreeCallInterceptorId++;

                            RuntimeAugments.FreeThunk(s_thunkPoolHeap, thunkAddress);
                        }
                    }
                }
            }
        }

        private static LocalVariableType[] s_ReturnBlockTypes = new LocalVariableType[1] { new LocalVariableType(typeof(ReturnBlock).TypeHandle, false, false) };

        /// <summary>
        /// Make a dynamic call to a function pointer passing arguments from arguments, using the signature described in callSignature
        /// </summary>
        public static unsafe void MakeDynamicCall(IntPtr address, DynamicCallSignature callSignature, LocalVariableSet arguments)
        {
#if CCCONVERTER_TRACE
            CallingConventionConverterLogger.WriteLine("MakeDynamicCall executing... ");
            arguments.DumpDebugInfo();
#endif

            CallConversionInterpreter.CallConversionInterpreterLocals locals = new CallConversionInterpreter.CallConversionInterpreterLocals();
            locals.Locals1 = arguments;
            locals.LocalVarSetTypes2 = s_ReturnBlockTypes;

            if ((callSignature.CallingConvention == CallingConvention.ManagedInstance || callSignature.CallingConvention == CallingConvention.ManagedStatic) &&
                FunctionPointerOps.IsGenericMethodPointer(address))
            {
                locals.Opcodes = callSignature.FatOps;
                var genericFunctionPointerDescriptor = FunctionPointerOps.ConvertToGenericDescriptor(address);
                locals.IntPtrFnPtr = genericFunctionPointerDescriptor->MethodFunctionPointer;
                locals.IntPtrGenericContextArg = genericFunctionPointerDescriptor->InstantiationArgument;
            }
            else
            {
                locals.Opcodes = callSignature.NormalOps;
                locals.IntPtrFnPtr = address;
            }

            CallConversionInterpreter.Interpret(ref locals);
        }


        private static CallInterceptor GetInterceptorFromId(IntPtr id)
        {
            lock (s_callInterceptors)
            {
                return s_callInterceptors[id.ToInt32()];
            }
        }

        private CallConversionInterpreter.CallConversionInterpreterLocals GetInterpreterLocals()
        {
            CallConversionInterpreter.CallConversionInterpreterLocals locals = new CallConversionInterpreter.CallConversionInterpreterLocals();
            locals.LocalVarSetTypes1 = ArgumentAndReturnTypes;
            locals.LocalVarSetTypes2 = LocalVariableTypes;
            locals.Interceptor = this;
            locals.Opcodes = BuildOpsListForThunk(CallingConvention, locals.LocalVarSetTypes1, locals.LocalVarSetTypes2);
            return locals;
        }

        private CallConversionOperation[] BuildOpsListForThunk(CallingConvention callingConvention, LocalVariableType[] returnAndArgumentTypes, LocalVariableType[] locals)
        {
            ArrayBuilder<CallConversionOperation> callConversionOps = new ArrayBuilder<CallConversionOperation>();

            bool hasThis = callingConvention == CallingConvention.ManagedInstance;
            int firstArgumentOffset = 1 + (hasThis ? 1 : 0);

            TypeHandle[] args = new TypeHandle[returnAndArgumentTypes.Length - firstArgumentOffset];
            TypeHandle returnType = new TypeHandle(returnAndArgumentTypes[0].ByRef, returnAndArgumentTypes[0].TypeHandle);

            for (int i = firstArgumentOffset; i < returnAndArgumentTypes.Length; i++)
            {
                args[i - firstArgumentOffset] = new TypeHandle(returnAndArgumentTypes[i].ByRef, returnAndArgumentTypes[i].TypeHandle);
            }

            ArgIteratorData data = new ArgIteratorData(hasThis, false, args, returnType);

            ArgIterator callerArgs = new ArgIterator(data, callingConvention, false, false, null, false, false);
#if CALLINGCONVENTION_CALLEE_POPS
            // CbStackPop must be executed before general argument iteration begins
            int cbStackToPop = callerArgs.CbStackPop();
#endif

            int localBlockSize = IntPtr.Size * returnAndArgumentTypes.Length;
            callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y, localBlockSize, CallConversionInterpreter.ArgBlock));

            // Handle locals block
            if (locals.Length > 0)
            {
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y, LocalVariableSet.ComputeNecessaryMemoryForStackLocalVariableSet(locals), CallConversionInterpreter.LocalBlock
#if CCCONVERTER_TRACE
                    , "Locals"
#endif
                    ));
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.DEFAULT_INIT_LOCALBLOCK_X, CallConversionInterpreter.LocalBlock));
            }

            if (callerArgs.HasRetBuffArg())
            {
                int ofsRetBuffArg = callerArgs.GetRetBuffArgOffset();
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.COPY_X_BYTES_TO_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_FROM_OFFSET_W_IN_TRANSITION_BLOCK, IntPtr.Size, CallConversionInterpreter.ArgBlock, 0, ofsRetBuffArg
#if CCCONVERTER_TRACE
                    , "ReturnBuffer"
#endif
                    ));
            }
            else if (returnType.GetCorElementType() == CorElementType.ELEMENT_TYPE_VOID)
            {
                // Do nothing for void
            }
            else
            {
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_LOCALBLOCK, CallConversionInterpreter.ArgBlock, 0, IntPtr.Size * returnAndArgumentTypes.Length
#if CCCONVERTER_TRACE
                    , "ReturnValue"
#endif
                    ));
                localBlockSize += checked((int)returnType.GetSize());
            }

            if (hasThis)
            {
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_TRANSITION_BLOCK, CallConversionInterpreter.ArgBlock, 1, ArgIterator.GetThisOffset()
#if CCCONVERTER_TRACE
                    , "ThisPointer"
#endif
                    ));
            }

            for (int i = firstArgumentOffset; i < returnAndArgumentTypes.Length; i++)
            {
                int ofsCaller = callerArgs.GetNextOffset();

                TypeHandle argTypeHandle;
                CorElementType argType = callerArgs.GetArgType(out argTypeHandle);

                if (callerArgs.IsArgPassedByRef() && argType != CorElementType.ELEMENT_TYPE_BYREF)
                {
                    callConversionOps.Add(new CallConversionOperation(
                        CallConversionOperation.OpCode.COPY_X_BYTES_TO_LOCALBLOCK_Y_OFFSET_Z_IN_LOCALBLOCK_FROM_OFFSET_W_IN_TRANSITION_BLOCK,
                        IntPtr.Size,
                        CallConversionInterpreter.ArgBlock,
                        i * IntPtr.Size,
                        ofsCaller
#if CCCONVERTER_TRACE
                        , "ByRef Arg #" + i.LowLevelToString()
#endif
                        ));
                }
                else
                {
#if TARGET_ARM64
                    if (ofsCaller < 0 && argTypeHandle.IsHFA() && argTypeHandle.GetHFAType() == CorElementType.ELEMENT_TYPE_R4)
                    {
                        // S and D registers overlap. The FP block of the transition block will have the float values of the HFA struct stored in 64-bit slots. We cannot directly
                        // memcopy or point at these values without first re-writing them as consecutive 32-bit float values

                        callConversionOps.Add(new CallConversionOperation(
                            CallConversionOperation.OpCode.ARM64_COMPACT_X_FLOATS_INTO_HFA_AT_OFFSET_Y_IN_TRANSITION_BLOCK,
                            (int)argTypeHandle.GetSize() / 4,
                            ofsCaller,
                            0
#if CCCONVERTER_TRACE
                            , "Arg #" + i.LowLevelToString()
#endif
                        ));
                    }
#endif

                    callConversionOps.Add(new CallConversionOperation(
                        CallConversionOperation.OpCode.SET_LOCALBLOCK_X_POINTER_Y_TO_OFFSET_Z_IN_TRANSITION_BLOCK,
                        CallConversionInterpreter.ArgBlock,
                        i,
                        ofsCaller
#if CCCONVERTER_TRACE
                        , "Arg #" + i.LowLevelToString()
#endif
                        ));
                }
            }

            callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.CALL_INTERCEPTOR));

#if CALLINGCONVENTION_CALLEE_POPS
            callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.SETUP_CALLERPOP_X_BYTES, cbStackToPop));
#endif

            if (returnType.GetCorElementType() == CorElementType.ELEMENT_TYPE_VOID)
            {
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.RETURN_VOID));
            }
            else if (callerArgs.HasRetBuffArg())
            {
                int ofsRetBuffArg = callerArgs.GetRetBuffArgOffset();
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.RETURN_RETBUF_FROM_OFFSET_X_IN_TRANSITION_BLOCK, ofsRetBuffArg));
            }
            else if (callerArgs.GetFPReturnSize() > 0)
            {
                callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.RETURN_FLOATINGPOINT_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z, CallConversionInterpreter.ArgBlock, 0, checked((int)callerArgs.GetFPReturnSize())));

#if TARGET_ARM64
                if (returnType.IsHFA() && returnType.GetHFAType() == CorElementType.ELEMENT_TYPE_R4)
                {
                    // S and D registers overlap, so we need to re-write the float values into 64-bit slots to match the format of the UniversalTransitionBlock's FP return block
                    callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.ARM64_EXPAND_X_FLOATS_INTO_HFA_IN_RETURN_BLOCK, (int)returnType.GetSize() / 4, 0, 0));
                }
#endif
            }
            else
            {
                //
                // Converting by-ref values to non-by-ref form requires the converter to be capable of taking a pointer to a small integer
                // value anywhere in memory and then copying the referenced value into an ABI-compliant pointer-sized "slot" which
                // faithfully communicates the value.  In such cases, the argument slot prepared by the converter must conform to all
                // sign/zero-extension rules mandated by the ABI.
                //
                // ARM32 requires all less-than-pointer-sized values to be sign/zero-extended when they are placed into pointer-sized
                // slots (i.e., requires "producer-oriented" sign/zero-extension).  x86/amd64 do not have this requirement (i.e., the
                // unused high bytes of the pointer-sized slot are ignored by the consumer and are allowed to take on any value); however
                // to reduce the need for ever more #ifs in this file, this behavior will not be #if'd away. (Its not wrong, its just unnecessary)
                //
                switch (returnType.GetCorElementType())
                {
                    case CorElementType.ELEMENT_TYPE_I1:
                    case CorElementType.ELEMENT_TYPE_I2:
#if TARGET_64BIT
                    case CorElementType.ELEMENT_TYPE_I4:
#endif
                        callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.RETURN_SIGNEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z, CallConversionInterpreter.ArgBlock, 0, checked((int)returnType.GetSize())));
                        break;

                    case CorElementType.ELEMENT_TYPE_U1:
                    case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    case CorElementType.ELEMENT_TYPE_U2:
                    case CorElementType.ELEMENT_TYPE_CHAR:
#if TARGET_64BIT
                    case CorElementType.ELEMENT_TYPE_U4:
#endif
                        callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.RETURN_ZEROEXTENDED_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z, CallConversionInterpreter.ArgBlock, 0, checked((int)returnType.GetSize())));
                        break;

                    default:
                        callConversionOps.Add(new CallConversionOperation(CallConversionOperation.OpCode.RETURN_INTEGER_BYVALUE_FROM_LOCALBLOCK_X_POINTER_Y_OF_SIZE_Z, CallConversionInterpreter.ArgBlock, 0, checked((int)returnType.GetSize())));
                        break;
                }
            }

            Debug.Assert(callConversionOps[0].Op == CallConversionOperation.OpCode.ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y);
            Debug.Assert(callConversionOps[0].Y == CallConversionInterpreter.ArgBlock);
            callConversionOps[0] = new CallConversionOperation(CallConversionOperation.OpCode.ALLOC_X_LOCALBLOCK_BYTES_FOR_BLOCK_Y, localBlockSize, CallConversionInterpreter.ArgBlock
#if CCCONVERTER_TRACE
                , "ReturnAndArguments"
#endif
                );

            return callConversionOps.ToArray();
        }

        private delegate IntPtr CallInterceptorThunkDelegate(IntPtr callerTransitionBlockParam, IntPtr thunkId);

        private static unsafe IntPtr CallInterceptorThunk(IntPtr callerTransitionBlockParam, IntPtr thunkId)
        {
#if CCCONVERTER_TRACE
            CallingConventionConverterLogger.WriteLine("CallInterceptorThunk executing... ID = " + thunkId.LowLevelToString());
#endif

            CallInterceptor interceptor = GetInterceptorFromId(thunkId);
            var locals = interceptor.GetInterpreterLocals();
            locals.TransitionBlockPtr = (byte*)callerTransitionBlockParam;
            CallConversionInterpreter.Interpret(ref locals);
            return locals.IntPtrReturnVal;
        }

#if TARGET_X86
        [UnmanagedCallersOnly(CallingConvention = System.Runtime.InteropServices.CallingConvention.FastCall)]
#else
        [UnmanagedCallersOnly]
#endif
        private static IntPtr CallInterceptorThunkUnmanagedCallersOnly(IntPtr callerTransitionBlockParam, IntPtr thunkId)
        {
            return CallInterceptorThunk(callerTransitionBlockParam, thunkId);
        }
    }
}
