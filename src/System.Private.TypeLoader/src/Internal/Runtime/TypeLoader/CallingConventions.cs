// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


//
// This file is a line by line port of callingconvention.h from the desktop CLR. See reference source in the ReferenceSource directory
//
#if ARM
#define _TARGET_ARM_
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define FEATURE_HFA
#elif ARM64
#define _TARGET_ARM64_
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define ENREGISTERED_PARAMTYPE_MAXSIZE
#define FEATURE_HFA
#elif X86
#define _TARGET_X86_
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#elif AMD64
#if UNIXAMD64
#define UNIX_AMD64_ABI
#define CALLDESCR_ARGREGS                          // CallDescrWorker has ArgumentRegister parameter
#else
#endif
#define CALLDESCR_FPARGREGS                        // CallDescrWorker has FloatArgumentRegisters parameter
#define _TARGET_AMD64_
#define ENREGISTERED_RETURNTYPE_MAXSIZE
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
#define ENREGISTERED_PARAMTYPE_MAXSIZE
#else
#error Unknown architecture!
#endif

// Provides an abstraction over platform specific calling conventions (specifically, the calling convention
// utilized by the JIT on that platform). The caller enumerates each argument of a signature in turn, and is 
// provided with information mapping that argument into registers and/or stack locations.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using Internal.NativeFormat;

namespace Internal.Runtime.CallConverter
{
    public enum CallingConvention
    {
        ManagedInstance,
        ManagedStatic,
        StdCall,
        /*FastCall, CDecl */
    }

    internal unsafe struct TypeHandle
    {
        public TypeHandle(bool isByRef, RuntimeTypeHandle eeType)
        {
            _eeType = eeType.ToEETypePtr();
            _isByRef = isByRef;
        }

        private readonly EEType* _eeType;
        private readonly bool _isByRef;

        public bool Equals(TypeHandle other)
        {
            return _isByRef == other._isByRef && _eeType == other._eeType;
        }

        public override int GetHashCode() { return (int)_eeType->HashCode; }

        public bool IsNull() { return _eeType == null && !_isByRef; }
        public bool IsValueType() { if (_isByRef) return false; return _eeType->IsValueType; }

        public unsafe uint GetSize()
        {
            if (IsValueType())
                return _eeType->ValueTypeSize;
            else
                return (uint)IntPtr.Size;
        }

        public bool RequiresAlign8()
        {
#if !ARM
            return false;
#else
            if (_isByRef)
            {
                return false;
            }
            return _eeType->RequiresAlign8;
#endif
        }
        public bool IsHFA()
        {
#if !ARM
            return false;
#else
            if (_isByRef)
            {
                return false;
            }
            return _eeType->IsHFA;
#endif
        }

        public CorElementType GetHFAType()
        {
#if ARM
            if (RequiresAlign8())
            {
                return CorElementType.ELEMENT_TYPE_R8;
            }
#endif
#if ARM64
            Debug.Assert(false); // HFA8 detection not yet implemented for this platform
#endif
            return CorElementType.ELEMENT_TYPE_R4;
        }

        public CorElementType GetCorElementType()
        {
            if (_isByRef)
            {
                return CorElementType.ELEMENT_TYPE_BYREF;
            }

            // The core redhawk runtime has a slightly different concept of what CorElementType should be for a type. It matches for primitive and enum types
            // but for other types, it doesn't match the needs in this file.
            CorElementType rhCorElementType = _eeType->CorElementType;

            if (((rhCorElementType >= CorElementType.ELEMENT_TYPE_BOOLEAN) && (rhCorElementType <= CorElementType.ELEMENT_TYPE_R8)) ||
                    (rhCorElementType == CorElementType.ELEMENT_TYPE_I) ||
                    (rhCorElementType == CorElementType.ELEMENT_TYPE_U))
            {
                return rhCorElementType; // If Redhawk thinks the corelementtype is a primitive type, then it agree with the concept of corelement type needed in this codebase.
            }
            else if (_eeType == typeof(void).TypeHandle.ToEETypePtr())
            {
                return CorElementType.ELEMENT_TYPE_VOID;
            }
            else if (IsValueType())
            {
                return CorElementType.ELEMENT_TYPE_VALUETYPE;
            }
            else if (_eeType->IsPointerType)
            {
                return CorElementType.ELEMENT_TYPE_PTR;
            }
            else
            {
                return CorElementType.ELEMENT_TYPE_CLASS;
            }
        }

        private static int[] s_elemSizes = new int[]
            {
                0, //ELEMENT_TYPE_END          0x0
                0, //ELEMENT_TYPE_VOID         0x1
                1, //ELEMENT_TYPE_BOOLEAN      0x2
                2, //ELEMENT_TYPE_CHAR         0x3
                1, //ELEMENT_TYPE_I1           0x4
                1, //ELEMENT_TYPE_U1           0x5
                2, //ELEMENT_TYPE_I2           0x6
                2, //ELEMENT_TYPE_U2           0x7
                4, //ELEMENT_TYPE_I4           0x8
                4, //ELEMENT_TYPE_U4           0x9
                8, //ELEMENT_TYPE_I8           0xa
                8, //ELEMENT_TYPE_U8           0xb
                4, //ELEMENT_TYPE_R4           0xc
                8, //ELEMENT_TYPE_R8           0xd
                -2,//ELEMENT_TYPE_STRING       0xe
                -2,//ELEMENT_TYPE_PTR          0xf
                -2,//ELEMENT_TYPE_BYREF        0x10
                -1,//ELEMENT_TYPE_VALUETYPE    0x11
                -2,//ELEMENT_TYPE_CLASS        0x12
                0, //ELEMENT_TYPE_VAR          0x13
                -2,//ELEMENT_TYPE_ARRAY        0x14
                0, //ELEMENT_TYPE_GENERICINST  0x15
                0, //ELEMENT_TYPE_TYPEDBYREF   0x16
                0, // UNUSED                   0x17
                -2,//ELEMENT_TYPE_I            0x18
                -2,//ELEMENT_TYPE_U            0x19
                0, // UNUSED                   0x1a
                -2,//ELEMENT_TYPE_FPTR         0x1b
                -2,//ELEMENT_TYPE_OBJECT       0x1c
                -2,//ELEMENT_TYPE_SZARRAY      0x1d
            };

        unsafe public static int GetElemSize(CorElementType t, TypeHandle thValueType)
        {
            if (((int)t) <= 0x1d)
            {
                int elemSize = s_elemSizes[(int)t];
                if (elemSize == -1)
                {
                    return (int)thValueType.GetSize();
                }
                if (elemSize == -2)
                {
                    return IntPtr.Size;
                }
                return elemSize;
            }
            return 0;
        }

        public RuntimeTypeHandle GetRuntimeTypeHandle() { return _eeType->ToRuntimeTypeHandle(); }
    }

    // Describes how a single argument is laid out in registers and/or stack locations when given as an input to a
    // managed method as part of a larger signature.
    //
    // Locations are split into floating point registers, general registers and stack offsets. Registers are
    // obviously architecture dependent but are represented as a zero-based index into the usual sequence in which
    // such registers are allocated for input on the platform in question. For instance:
    //      X86: 0 == ecx, 1 == edx
    //      ARM: 0 == r0, 1 == r1, 2 == r2 etc.
    //
    // Stack locations are represented as offsets from the stack pointer (at the point of the call). The offset is
    // given as an index of a pointer sized slot. Similarly the size of data on the stack is given in slot-sized
    // units. For instance, given an index of 2 and a size of 3:
    //      X86:   argument starts at [ESP + 8] and is 12 bytes long
    //      AMD64: argument starts at [RSP + 16] and is 24 bytes long
    //
    // The structure is flexible enough to describe an argument that is split over several (consecutive) registers
    // and possibly on to the stack as well.
    internal struct ArgLocDesc
    {
        public int m_idxFloatReg;  // First floating point register used (or -1)
        public int m_cFloatReg;    // Count of floating point registers used (or 0)

        public int m_idxGenReg;    // First general register used (or -1)
        public int m_cGenReg;      // Count of general registers used (or 0)

        public int m_idxStack;     // First stack slot used (or -1)
        public int m_cStack;       // Count of stack slots used (or 0)

#if _TARGET_ARM_
        public bool m_fRequires64BitAlignment; // True if the argument should always be aligned (in registers or on the stack
#endif

        // Initialize to represent a non-placed argument (no register or stack slots referenced).
        public void Init()
        {
            m_idxFloatReg = -1;
            m_cFloatReg = 0;
            m_idxGenReg = -1;
            m_cGenReg = 0;
            m_idxStack = -1;
            m_cStack = 0;
#if _TARGET_ARM_
            m_fRequires64BitAlignment = false;
#endif
        }
    };

    internal class ArgIteratorData
    {
        public ArgIteratorData(bool hasThis,
                        bool isVarArg,
                        TypeHandle[] parameterTypes,
                        TypeHandle returnType)
        {
            _hasThis = hasThis;
            _isVarArg = isVarArg;
            _parameterTypes = parameterTypes;
            _returnType = returnType;
        }

        private bool _hasThis;
        private bool _isVarArg;
        private TypeHandle[] _parameterTypes;
        private TypeHandle _returnType;

        public override bool Equals(object obj)
        {
            if (this == obj) return true;

            ArgIteratorData other = obj as ArgIteratorData;
            if (other == null) return false;

            if (_hasThis != other._hasThis || _isVarArg != other._isVarArg || !_returnType.Equals(other._returnType))
                return false;

            if (_parameterTypes == null)
                return other._parameterTypes == null;

            if (other._parameterTypes == null || _parameterTypes.Length != other._parameterTypes.Length)
                return false;

            for (int i = 0; i < _parameterTypes.Length; i++)
                if (!_parameterTypes[i].Equals(other._parameterTypes[i]))
                    return false;

            return true;
        }

        public override int GetHashCode()
        {
            return 37 + (_parameterTypes == null ?
                _returnType.GetHashCode() :
                TypeHashingAlgorithms.ComputeGenericInstanceHashCode(_returnType.GetHashCode(), _parameterTypes));
        }

        public bool HasThis() { return _hasThis; }
        public bool IsVarArg() { return _isVarArg; }
        public int NumFixedArgs() { return _parameterTypes != null ? _parameterTypes.Length : 0; }

        // Argument iteration.
        public CorElementType GetArgumentType(int argNum, out TypeHandle thValueType)
        {
            thValueType = _parameterTypes[argNum];
            CorElementType returnValue = thValueType.GetCorElementType();
            if (!thValueType.IsValueType())
            {
                thValueType = default(TypeHandle);
            }
            return returnValue;
        }

        public TypeHandle GetByRefArgumentType(int argNum)
        {
            return (argNum < _parameterTypes.Length && _parameterTypes[argNum].GetCorElementType() == CorElementType.ELEMENT_TYPE_BYREF) ?
                _parameterTypes[argNum] :
                default(TypeHandle);
        }

        public CorElementType GetReturnType(out TypeHandle thValueType)
        {
            thValueType = _returnType;
            CorElementType returnValue = thValueType.GetCorElementType();
            if (!thValueType.IsValueType())
            {
                thValueType = default(TypeHandle);
            }
            return returnValue;
        }

#if CCCONVERTER_TRACE
        public string GetEETypeDebugName(int argNum)
        {
            Internal.TypeSystem.TypeSystemContext context = TypeSystemContextFactory.Create();
            var result = context.ResolveRuntimeTypeHandle(_parameterTypes[argNum].GetRuntimeTypeHandle()).ToString();
            TypeSystemContextFactory.Recycle(context);
            return result;
        }
#endif
    }

    //-----------------------------------------------------------------------
    // ArgIterator is helper for dealing with calling conventions.
    // It is tightly coupled with TransitionBlock. It uses offsets into
    // TransitionBlock to represent argument locations for efficiency
    // reasons. Alternatively, it can also return ArgLocDesc for less
    // performance critical code.
    //
    // The ARGITERATOR_BASE argument of the template is provider of the parsed
    // method signature. Typically, the arg iterator works on top of MetaSig. 
    // Reflection invoke uses alternative implementation to save signature parsing
    // time because of it has the parsed signature available.
    //-----------------------------------------------------------------------
    //template<class ARGITERATOR_BASE>
    internal unsafe struct ArgIterator //: public ARGITERATOR_BASE
    {
        private bool _hasThis;
        private bool _hasParamType;
        private bool _extraFunctionPointerArg;
        private ArgIteratorData _argData;
        private bool[] _forcedByRefParams;
        private bool _skipFirstArg;
        private bool _extraObjectFirstArg;
        private CallingConvention _interpreterCallingConvention;

        public bool HasThis() { return _hasThis; }
        public bool IsVarArg() { return _argData.IsVarArg(); }
        public bool HasParamType() { return _hasParamType; }
        public int NumFixedArgs() { return _argData.NumFixedArgs() + (_extraFunctionPointerArg ? 1 : 0) + (_extraObjectFirstArg ? 1 : 0); }

        // Argument iteration.
        public CorElementType GetArgumentType(int argNum, out TypeHandle thValueType, out bool forceByRefReturn)
        {
            forceByRefReturn = false;

            if (_extraObjectFirstArg && argNum == 0)
            {
                thValueType = default(TypeHandle);
                return CorElementType.ELEMENT_TYPE_CLASS;
            }

            argNum = _extraObjectFirstArg ? argNum - 1 : argNum;
            Debug.Assert(argNum >= 0);

            if (_forcedByRefParams != null && (argNum + 1) < _forcedByRefParams.Length)
                forceByRefReturn = _forcedByRefParams[argNum + 1];

            if (_extraFunctionPointerArg && argNum == _argData.NumFixedArgs())
            {
                thValueType = default(TypeHandle);
                return CorElementType.ELEMENT_TYPE_I;
            }
            return _argData.GetArgumentType(argNum, out thValueType);
        }

        public CorElementType GetReturnType(out TypeHandle thValueType, out bool forceByRefReturn)
        {
            if (_forcedByRefParams != null && _forcedByRefParams.Length > 0)
                forceByRefReturn = _forcedByRefParams[0];
            else
                forceByRefReturn = false;

            return _argData.GetReturnType(out thValueType);
        }

#if CCCONVERTER_TRACE
        public string GetEETypeDebugName(int argNum)
        {
            if (_extraObjectFirstArg && argNum == 0)
                return "System.Object";
            return _argData.GetEETypeDebugName(_extraObjectFirstArg ? argNum - 1 : argNum);
        }
#endif

        public void Reset()
        {
            _argType = default(CorElementType);
            _argTypeHandle = default(TypeHandle);
            _argSize = 0;
            _argNum = 0;
            _argForceByRef = false;
            _ITERATION_STARTED = false;
        }

        //public:
        //------------------------------------------------------------
        // Constructor
        //------------------------------------------------------------
        public ArgIterator(ArgIteratorData argData, CallingConvention callConv, bool hasParamType, bool extraFunctionPointerArg, bool[] forcedByRefParams, bool skipFirstArg, bool extraObjectFirstArg)
        {
            this = default(ArgIterator);
            _argData = argData;
            _hasThis = callConv == CallingConvention.ManagedInstance;
            _hasParamType = hasParamType;
            _extraFunctionPointerArg = extraFunctionPointerArg;
            _forcedByRefParams = forcedByRefParams;
            _skipFirstArg = skipFirstArg;
            _extraObjectFirstArg = extraObjectFirstArg;
            _interpreterCallingConvention = callConv;
        }

        public void SetHasParamTypeAndReset(bool value)
        {
            _hasParamType = value;
            Reset();
        }

        public void SetHasThisAndReset(bool value)
        {
            _hasThis = value;
            Reset();
        }

        private uint SizeOfArgStack()
        {
            //        WRAPPER_NO_CONTRACT;
            if (!_SIZE_OF_ARG_STACK_COMPUTED)
                ForceSigWalk();
            Debug.Assert(_SIZE_OF_ARG_STACK_COMPUTED);
            return (uint)_nSizeOfArgStack;
        }

        // For use with ArgIterator. This function computes the amount of additional
        // memory required above the TransitionBlock.  The parameter offsets
        // returned by ArgIterator::GetNextOffset are relative to a
        // FramedMethodFrame, and may be in either of these regions.
        public int SizeOfFrameArgumentArray()
        {
            //        WRAPPER_NO_CONTRACT;

            uint size = SizeOfArgStack();

#if _TARGET_AMD64_ && !UNIX_AMD64_ABI
            // The argument registers are not included in the stack size on AMD64
            size += ArchitectureConstants.ARGUMENTREGISTERS_SIZE;
#endif

            return (int)size;
        }

        //------------------------------------------------------------------------

#if _TARGET_X86_
        public int CbStackPop()
        {
            //        WRAPPER_NO_CONTRACT;

            if (this.IsVarArg())
                return 0;
            else
                return (int)SizeOfArgStack();
        }
#endif

        // Is there a hidden parameter for the return parameter? 
        //
        public bool HasRetBuffArg()
        {
            //        WRAPPER_NO_CONTRACT;
            if (!_RETURN_FLAGS_COMPUTED)
                ComputeReturnFlags();
            return _RETURN_HAS_RET_BUFFER;
        }

        public uint GetFPReturnSize()
        {
            //        WRAPPER_NO_CONTRACT;
            if (!_RETURN_FLAGS_COMPUTED)
                ComputeReturnFlags();
            return _fpReturnSize;
        }

#if _TARGET_X86_
        //=========================================================================
        // Indicates whether an argument is to be put in a register using the
        // default IL calling convention. This should be called on each parameter
        // in the order it appears in the call signature. For a non-static meethod,
        // this function should also be called once for the "this" argument, prior
        // to calling it for the "real" arguments. Pass in a typ of ELEMENT_TYPE_CLASS.
        //
        //  *pNumRegistersUsed:  [in,out]: keeps track of the number of argument
        //                       registers assigned previously. The caller should
        //                       initialize this variable to 0 - then each call
        //                       will update it.
        //
        //  typ:                 the signature type
        //=========================================================================
        private static bool IsArgumentInRegister(ref int pNumRegistersUsed, CorElementType typ, TypeHandle thValueType)
        {
            //        LIMITED_METHOD_CONTRACT;
            if ((pNumRegistersUsed) < ArchitectureConstants.NUM_ARGUMENT_REGISTERS)
            {
                switch (typ)
                {
                    case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    case CorElementType.ELEMENT_TYPE_CHAR:
                    case CorElementType.ELEMENT_TYPE_I1:
                    case CorElementType.ELEMENT_TYPE_U1:
                    case CorElementType.ELEMENT_TYPE_I2:
                    case CorElementType.ELEMENT_TYPE_U2:
                    case CorElementType.ELEMENT_TYPE_I4:
                    case CorElementType.ELEMENT_TYPE_U4:
                    case CorElementType.ELEMENT_TYPE_STRING:
                    case CorElementType.ELEMENT_TYPE_PTR:
                    case CorElementType.ELEMENT_TYPE_BYREF:
                    case CorElementType.ELEMENT_TYPE_CLASS:
                    case CorElementType.ELEMENT_TYPE_ARRAY:
                    case CorElementType.ELEMENT_TYPE_I:
                    case CorElementType.ELEMENT_TYPE_U:
                    case CorElementType.ELEMENT_TYPE_FNPTR:
                    case CorElementType.ELEMENT_TYPE_OBJECT:
                    case CorElementType.ELEMENT_TYPE_SZARRAY:
                        pNumRegistersUsed++;
                        return true;

                    case CorElementType.ELEMENT_TYPE_VALUETYPE:
                        {
                            // On ProjectN valuetypes of integral size are passed enregistered
                            int structSize = TypeHandle.GetElemSize(typ, thValueType);
                            switch (structSize)
                            {
                                case 1:
                                case 2:
                                case 4:
                                    pNumRegistersUsed++;
                                    return true;
                            }
                            break;
                        }
                }
            }

            return (false);
        }
#endif // _TARGET_X86_

#if ENREGISTERED_PARAMTYPE_MAXSIZE

        // Note that this overload does not handle varargs
        private static bool IsArgPassedByRef(TypeHandle th)
        {
            //        LIMITED_METHOD_CONTRACT;

            Debug.Assert(!th.IsNull());

            // This method only works for valuetypes. It includes true value types, 
            // primitives, enums and TypedReference.
            Debug.Assert(th.IsValueType());

            uint size = th.GetSize();
#if _TARGET_AMD64_
            return IsArgPassedByRef((int)size);
#elif _TARGET_ARM64_
            // Composites greater than 16 bytes are passed by reference
            return ((size > ArchitectureConstants.ENREGISTERED_PARAMTYPE_MAXSIZE) && !th.IsHFA());
#else
#error ArgIterator::IsArgPassedByRef
#endif
        }

#if _TARGET_AMD64_
        // This overload should only be used in AMD64-specific code only.
        private static bool IsArgPassedByRef(int size)
        {
            //        LIMITED_METHOD_CONTRACT;

            // If the size is bigger than ENREGISTERED_PARAM_TYPE_MAXSIZE, or if the size is NOT a power of 2, then
            // the argument is passed by reference.
            return (size > ArchitectureConstants.ENREGISTERED_PARAMTYPE_MAXSIZE) || ((size & (size - 1)) != 0);
        }
#endif

        // This overload should be used for varargs only.
        private static bool IsVarArgPassedByRef(int size)
        {
            //        LIMITED_METHOD_CONTRACT;

#if _TARGET_AMD64_
            return IsArgPassedByRef(size);
#else
            return (size > ArchitectureConstants.ENREGISTERED_PARAMTYPE_MAXSIZE);
#endif
        }
#endif // ENREGISTERED_PARAMTYPE_MAXSIZE

        public bool IsArgPassedByRef()
        {
            //        LIMITED_METHOD_CONTRACT;
            if (IsArgForcedPassedByRef())
            {
                return true;
            }

            if (_argType == CorElementType.ELEMENT_TYPE_BYREF)
            {
                return true;
            }
#if ENREGISTERED_PARAMTYPE_MAXSIZE
#if _TARGET_AMD64_
            return IsArgPassedByRef(_argSize);
#elif _TARGET_ARM64_
            if (_argType == CorElementType.ELEMENT_TYPE_VALUETYPE)
            {
                Debug.Assert(!_argTypeHandle.IsNull());
                return ((_argSize > ArchitectureConstants.ENREGISTERED_PARAMTYPE_MAXSIZE) && (!_argTypeHandle.IsHFA() || IsVarArg()));
            }
            return false;
#else
#error PORTABILITY_ASSERT("ArgIterator::IsArgPassedByRef");
#endif
#else // ENREGISTERED_PARAMTYPE_MAXSIZE
            return false;
#endif // ENREGISTERED_PARAMTYPE_MAXSIZE
        }

        private bool IsArgForcedPassedByRef()
        {
            // This should be true for valuetypes instantiated over T in a generic signature using universal shared generic calling convention
            return _argForceByRef;
        }

        //------------------------------------------------------------
        // Return the offsets of the special arguments
        //------------------------------------------------------------

        public static int GetThisOffset()
        {
            return TransitionBlock.GetThisOffset();
        }

        public unsafe int GetRetBuffArgOffset()
        {
            //            WRAPPER_NO_CONTRACT;

            Debug.Assert(this.HasRetBuffArg());

#if _TARGET_X86_
            // x86 is special as always
            //    ret += this.HasThis() ? offsetof(ArgumentRegisters, EDX) : offsetof(ArgumentRegisters, ECX);
            // DESKTOP BEHAVIOR            ret += this.HasThis() ? ArgumentRegisters.GetOffsetOfEdx() : ArgumentRegisters.GetOffsetOfEcx();
            int ret = TransitionBlock.GetOffsetOfArgs();
#else
            // RetBuf arg is in the first argument register by default
            int ret = TransitionBlock.GetOffsetOfArgumentRegisters();

            // But if there is a this pointer, push it to the second.
            if (this.HasThis())
                ret += IntPtr.Size;
#endif

            return ret;
        }

        unsafe public int GetVASigCookieOffset()
        {
            //            WRAPPER_NO_CONTRACT;

            Debug.Assert(this.IsVarArg());

#if _TARGET_X86_
            // x86 is special as always
            return sizeof(TransitionBlock);
#else
            // VaSig cookie is after this and retbuf arguments by default.
            int ret = TransitionBlock.GetOffsetOfArgumentRegisters();

            if (this.HasThis())
            {
                ret += IntPtr.Size;
            }

            if (this.HasRetBuffArg())
            {
                ret += IntPtr.Size;
            }

            return ret;
#endif
        }

        unsafe public int GetParamTypeArgOffset()
        {
            Debug.Assert(this.HasParamType());

#if _TARGET_X86_
            // x86 is special as always
            if (!_SIZE_OF_ARG_STACK_COMPUTED)
                ForceSigWalk();

            switch (_paramTypeLoc)
            {
                case ParamTypeLocation.Ecx:// PARAM_TYPE_REGISTER_ECX:
                    return TransitionBlock.GetOffsetOfArgumentRegisters() + ArgumentRegisters.GetOffsetOfEcx();
                case ParamTypeLocation.Edx:
                    return TransitionBlock.GetOffsetOfArgumentRegisters() + ArgumentRegisters.GetOffsetOfEdx();
                default:
                    break;
            }

            // The param type arg is last stack argument otherwise
            return sizeof(TransitionBlock);
#else
            // The hidden arg is after this and retbuf arguments by default.
            int ret = TransitionBlock.GetOffsetOfArgumentRegisters();

            if (this.HasThis())
            {
                ret += IntPtr.Size;
            }

            if (this.HasRetBuffArg())
            {
                ret += IntPtr.Size;
            }

            return ret;
#endif
        }

        //------------------------------------------------------------
        // Each time this is called, this returns a byte offset of the next
        // argument from the TransitionBlock* pointer. This offset can be positive *or* negative.
        //
        // Returns TransitionBlock::InvalidOffset once you've hit the end 
        // of the list.
        //------------------------------------------------------------
        public unsafe int GetNextOffset()
        {
            //            WRAPPER_NO_CONTRACT;
            //            SUPPORTS_DAC;

            if (!_ITERATION_STARTED)
            {
                int numRegistersUsed = 0;
#if _TARGET_X86_
                int initialArgOffset = 0;
#endif 
                if (this.HasThis())
                    numRegistersUsed++;

                if (this.HasRetBuffArg())
                {
#if !_TARGET_X86_
                    numRegistersUsed++;
#else
                    // DESKTOP BEHAVIOR is to do nothing here, as ret buf is never reached by the scan algortithm that walks backwards
                    // but in .NET Native, the x86 argument scan is a forward scan, so we need to skip the ret buf arg (which is always
                    // on the stack)
                    initialArgOffset = IntPtr.Size;
#endif
                }

                Debug.Assert(!this.IsVarArg() || !this.HasParamType());

                // DESKTOP BEHAVIOR - This block is disabled for x86 as the param arg is the last argument on desktop x86.
                if (this.HasParamType())
                {
                    numRegistersUsed++;
                }

#if !_TARGET_X86_
                if (this.IsVarArg())
                {
                    numRegistersUsed++;
                }
#endif

#if _TARGET_X86_
                if (this.IsVarArg())
                {
                    numRegistersUsed = ArchitectureConstants.NUM_ARGUMENT_REGISTERS; // Nothing else gets passed in registers for varargs
                }

#if FEATURE_INTERPRETER
                switch (_interpreterCallingConvention)
                {
                    case CallingConvention.StdCall:
                        _numRegistersUsed = ArchitectureConstants.NUM_ARGUMENT_REGISTERS;
                        _curOfs = TransitionBlock.GetOffsetOfArgs() + numRegistersUsed * IntPtr.Size + initialArgOffset;
                        break;

                    case CallingConvention.ManagedStatic:
                    case CallingConvention.ManagedInstance:
                        _numRegistersUsed = numRegistersUsed;
                        // DESKTOP BEHAVIOR     m_curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + SizeOfArgStack());
                        _curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + initialArgOffset);
                        break;

                    default:
                        Environment.FailFast("Unsupported calling convention.");
                        break;
                }
#else
                        m_numRegistersUsed = numRegistersUsed;
// DESKTOP BEHAVIOR     m_curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + SizeOfArgStack());
                        m_curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + initialArgOffset);
#endif

#elif _TARGET_AMD64_
#if UNIX_AMD64_ABI
                m_idxGenReg = numRegistersUsed;
                m_idxStack = 0;
                m_idxFPReg = 0;
#else
                _curOfs = TransitionBlock.GetOffsetOfArgs() + numRegistersUsed * IntPtr.Size;
#endif
#elif _TARGET_ARM_
                _idxGenReg = numRegistersUsed;
                _idxStack = 0;

                _wFPRegs = 0;
#elif _TARGET_ARM64_
                _idxGenReg = numRegistersUsed;
                _idxStack = 0;

                _idxFPReg = 0;
#else
                PORTABILITY_ASSERT("ArgIterator::GetNextOffset");
#endif

                _argNum = (_skipFirstArg ? 1 : 0);

                _ITERATION_STARTED = true;
            }

            if (_argNum >= this.NumFixedArgs())
                return TransitionBlock.InvalidOffset;

            TypeHandle thValueType;
            CorElementType argType = this.GetArgumentType(_argNum, out thValueType, out _argForceByRef);

            _argTypeHandleOfByRefParam = (argType == CorElementType.ELEMENT_TYPE_BYREF ? _argData.GetByRefArgumentType(_argNum) : default(TypeHandle));

            _argNum++;

            int argSize = TypeHandle.GetElemSize(argType, thValueType);

            _argType = argType;
            _argSize = argSize;
            _argTypeHandle = thValueType;

            argType = _argForceByRef ? CorElementType.ELEMENT_TYPE_BYREF : argType;
            argSize = _argForceByRef ? IntPtr.Size : argSize;

            int argOfs;

#if _TARGET_X86_
#if FEATURE_INTERPRETER
            if (_interpreterCallingConvention != CallingConvention.ManagedStatic && _interpreterCallingConvention != CallingConvention.ManagedInstance)
            {
                argOfs = _curOfs;
                _curOfs += ArchitectureConstants.StackElemSize(argSize);
                return argOfs;
            }
#endif
            if (IsArgumentInRegister(ref _numRegistersUsed, argType, thValueType))
            {
                return TransitionBlock.GetOffsetOfArgumentRegisters() + (ArchitectureConstants.NUM_ARGUMENT_REGISTERS - _numRegistersUsed) * IntPtr.Size;
            }

            // DESKTOP BEHAVIOR m_curOfs -= ArchitectureConstants.StackElemSize(argSize);
            // DESKTOP BEHAVIOR return m_curOfs;
            argOfs = _curOfs;
            _curOfs += ArchitectureConstants.StackElemSize(argSize);
            Debug.Assert(argOfs >= TransitionBlock.GetOffsetOfArgs());
            return argOfs;
#elif _TARGET_AMD64_
#if UNIX_AMD64_ABI
            int cFPRegs = 0;

            switch (argType)
            {

                case CorElementType.ELEMENT_TYPE_R4:
                    // 32-bit floating point argument.
                    cFPRegs = 1;
                    break;

                case CorElementType.ELEMENT_TYPE_R8:
                    // 64-bit floating point argument.
                    cFPRegs = 1;
                    break;

                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    {
                        // UNIXTODO: Passing of structs, HFAs. For now, use the Windows convention.
                        argSize = IntPtr.Size;
                        break;
                    }

                default:
                    break;
            }

            int cbArg = ArchitectureConstants.StackElemSize(argSize);
            int cArgSlots = cbArg / ArchitectureConstants.STACK_ELEM_SIZE;

            if (cFPRegs > 0)
            {
                if (cFPRegs + m_idxFPReg <= 8)
                {
                    int argOfsInner = TransitionBlock.GetOffsetOfFloatArgumentRegisters() + m_idxFPReg * 8;
                    m_idxFPReg += cFPRegs;
                    return argOfsInner;
                }
            }
            else
            {
                if (m_idxGenReg + cArgSlots <= 6)
                {
                    int argOfsInner = TransitionBlock.GetOffsetOfArgumentRegisters() + m_idxGenReg * 8;
                    m_idxGenReg += cArgSlots;
                    return argOfsInner;
                }
            }

            argOfs = TransitionBlock.GetOffsetOfArgs() + m_idxStack * 8;
            m_idxStack += cArgSlots;
            return argOfs;
#else
            int cFPRegs = 0;

            switch (argType)
            {
                case CorElementType.ELEMENT_TYPE_R4:
                    // 32-bit floating point argument.
                    cFPRegs = 1;
                    break;

                case CorElementType.ELEMENT_TYPE_R8:
                    // 64-bit floating point argument.
                    cFPRegs = 1;
                    break;
            }

            // Each argument takes exactly one slot on AMD64
            argOfs = _curOfs - TransitionBlock.GetOffsetOfArgs();
            _curOfs += IntPtr.Size;

            if ((cFPRegs == 0) || (argOfs >= sizeof(ArgumentRegisters)))
            {
                return argOfs + TransitionBlock.GetOffsetOfArgs();
            }
            else
            {
                int idxFpReg = argOfs / IntPtr.Size;
                return TransitionBlock.GetOffsetOfFloatArgumentRegisters() + idxFpReg * sizeof(M128A);
            }
#endif
#elif _TARGET_ARM_
            // First look at the underlying type of the argument to determine some basic properties:
            //  1) The size of the argument in bytes (rounded up to the stack slot size of 4 if necessary).
            //  2) Whether the argument represents a floating point primitive (ELEMENT_TYPE_R4 or ELEMENT_TYPE_R8).
            //  3) Whether the argument requires 64-bit alignment (anything that contains a Int64/UInt64).

            bool fFloatingPoint = false;
            bool fRequiresAlign64Bit = false;

            switch (argType)
            {
                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U8:
                    // 64-bit integers require 64-bit alignment on ARM.
                    fRequiresAlign64Bit = true;
                    break;

                case CorElementType.ELEMENT_TYPE_R4:
                    // 32-bit floating point argument.
                    fFloatingPoint = true;
                    break;

                case CorElementType.ELEMENT_TYPE_R8:
                    // 64-bit floating point argument.
                    fFloatingPoint = true;
                    fRequiresAlign64Bit = true;
                    break;

                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    {
                        // Value type case: extract the alignment requirement, note that this has to handle 
                        // the interop "native value types".
                        fRequiresAlign64Bit = thValueType.RequiresAlign8();

                        // Handle HFAs: packed structures of 1-4 floats or doubles that are passed in FP argument
                        // registers if possible.
                        if (thValueType.IsHFA())
                            fFloatingPoint = true;

                        break;
                    }

                default:
                    // The default is are 4-byte arguments (or promoted to 4 bytes), non-FP and don't require any
                    // 64-bit alignment.
                    break;
            }

            // Now attempt to place the argument into some combination of floating point or general registers and
            // the stack.

            // Save the alignment requirement
            _fRequires64BitAlignment = fRequiresAlign64Bit;

            int cbArg = ArchitectureConstants.StackElemSize(argSize);
            int cArgSlots = cbArg / 4;

            // Ignore floating point argument placement in registers if we're dealing with a vararg function (the ABI
            // specifies this so that vararg processing on the callee side is simplified).
            if (fFloatingPoint && !this.IsVarArg())
            {
                // Handle floating point (primitive) arguments.

                // First determine whether we can place the argument in VFP registers. There are 16 32-bit
                // and 8 64-bit argument registers that share the same register space (e.g. D0 overlaps S0 and
                // S1). The ABI specifies that VFP values will be passed in the lowest sequence of registers that
                // haven't been used yet and have the required alignment. So the sequence (float, double, float)
                // would be mapped to (S0, D1, S1) or (S0, S2/S3, S1).
                //
                // We use a 16-bit bitmap to record which registers have been used so far.
                //
                // So we can use the same basic loop for each argument type (float, double or HFA struct) we set up
                // the following input parameters based on the size and alignment requirements of the arguments:
                //   wAllocMask : bitmask of the number of 32-bit registers we need (1 for 1, 3 for 2, 7 for 3 etc.)
                //   cSteps     : number of loop iterations it'll take to search the 16 registers
                //   cShift     : how many bits to shift the allocation mask on each attempt

                ushort wAllocMask = checked((ushort)((1 << (cbArg / 4)) - 1));
                ushort cSteps = (ushort)(fRequiresAlign64Bit ? 9 - (cbArg / 8) : 17 - (cbArg / 4));
                ushort cShift = fRequiresAlign64Bit ? (ushort)2 : (ushort)1;

                // Look through the availability bitmask for a free register or register pair.
                for (ushort i = 0; i < cSteps; i++)
                {
                    if ((_wFPRegs & wAllocMask) == 0)
                    {
                        // We found one, mark the register or registers as used. 
                        _wFPRegs |= wAllocMask;

                        // Indicate the registers used to the caller and return.
                        return TransitionBlock.GetOffsetOfFloatArgumentRegisters() + (i * cShift * 4);
                    }
                    wAllocMask <<= cShift;
                }

                // The FP argument is going to live on the stack. Once this happens the ABI demands we mark all FP
                // registers as unavailable.
                _wFPRegs = 0xffff;

                // Doubles or HFAs containing doubles need the stack aligned appropriately.
                if (fRequiresAlign64Bit)
                    _idxStack = ALIGN_UP(_idxStack, 2);

                // Indicate the stack location of the argument to the caller.
                int argOfsInner = TransitionBlock.GetOffsetOfArgs() + _idxStack * 4;

                // Record the stack usage.
                _idxStack += cArgSlots;

                return argOfsInner;
            }

            //
            // Handle the non-floating point case.
            //

            if (_idxGenReg < 4)
            {
                if (fRequiresAlign64Bit)
                {
                    // The argument requires 64-bit alignment. Align either the next general argument register if
                    // we have any left.  See step C.3 in the algorithm in the ABI spec.       
                    _idxGenReg = ALIGN_UP(_idxGenReg, 2);
                }

                int argOfsInner = TransitionBlock.GetOffsetOfArgumentRegisters() + _idxGenReg * 4;

                int cRemainingRegs = 4 - _idxGenReg;
                if (cArgSlots <= cRemainingRegs)
                {
                    // Mark the registers just allocated as used.
                    _idxGenReg += cArgSlots;
                    return argOfsInner;
                }

                // The ABI supports splitting a non-FP argument across registers and the stack. But this is
                // disabled if the FP arguments already overflowed onto the stack (i.e. the stack index is not
                // zero). The following code marks the general argument registers as exhausted if this condition
                // holds.  See steps C.5 in the algorithm in the ABI spec.

                _idxGenReg = 4;

                if (_idxStack == 0)
                {
                    _idxStack += cArgSlots - cRemainingRegs;
                    return argOfsInner;
                }
            }

            if (fRequiresAlign64Bit)
            {
                // The argument requires 64-bit alignment. If it is going to be passed on the stack, align
                // the next stack slot.  See step C.6 in the algorithm in the ABI spec.  
                _idxStack = ALIGN_UP(_idxStack, 2);
            }

            argOfs = TransitionBlock.GetOffsetOfArgs() + _idxStack * 4;

            // Advance the stack pointer over the argument just placed.
            _idxStack += cArgSlots;

            return argOfs;
#elif _TARGET_ARM64_

            int cFPRegs = 0;

            switch (argType)
            {
                case CorElementType.ELEMENT_TYPE_R4:
                    // 32-bit floating point argument.
                    cFPRegs = 1;
                    break;

                case CorElementType.ELEMENT_TYPE_R8:
                    // 64-bit floating point argument.
                    cFPRegs = 1;
                    break;

                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    {
                        // Handle HFAs: packed structures of 2-4 floats or doubles that are passed in FP argument
                        // registers if possible.
                        if (thValueType.IsHFA())
                        {
                            CorElementType type = thValueType.GetHFAType();
                            cFPRegs = (type == CorElementType.ELEMENT_TYPE_R4) ? (argSize / sizeof(float)) : (argSize / sizeof(double));
                        }
                        else
                        {
                            // Composite greater than 16bytes should be passed by reference
                            if (argSize > ArchitectureConstants.ENREGISTERED_PARAMTYPE_MAXSIZE)
                            {
                                argSize = IntPtr.Size;
                            }
                        }

                        break;
                    }

                default:
                    break;
            }

            int cbArg = ArchitectureConstants.StackElemSize(argSize);
            int cArgSlots = cbArg / ArchitectureConstants.STACK_ELEM_SIZE;

            if (cFPRegs > 0 && !this.IsVarArg())
            {
                if (cFPRegs + _idxFPReg <= 8)
                {
                    int argOfsInner = TransitionBlock.GetOffsetOfFloatArgumentRegisters() + _idxFPReg * 8;
                    _idxFPReg += cFPRegs;
                    return argOfsInner;
                }
                else
                {
                    _idxFPReg = 8;
                }
            }
            else
            {
                if (_idxGenReg + cArgSlots <= 8)
                {
                    int argOfsInner = TransitionBlock.GetOffsetOfArgumentRegisters() + _idxGenReg * 8;
                    _idxGenReg += cArgSlots;
                    return argOfsInner;
                }
                else
                {
                    _idxGenReg = 8;
                }
            }

            argOfs = TransitionBlock.GetOffsetOfArgs() + _idxStack * 8;
            _idxStack += cArgSlots;
            return argOfs;
#else
#error            PORTABILITY_ASSERT("ArgIterator::GetNextOffset");
#endif
        }


        public CorElementType GetArgType(out TypeHandle pTypeHandle)
        {
            //        LIMITED_METHOD_CONTRACT;
            pTypeHandle = _argTypeHandle;
            return _argType;
        }

        public CorElementType GetByRefArgType(out TypeHandle pByRefArgTypeHandle)
        {
            //        LIMITED_METHOD_CONTRACT;
            pByRefArgTypeHandle = _argTypeHandleOfByRefParam;
            return _argType;
        }

        public int GetArgSize()
        {
            //        LIMITED_METHOD_CONTRACT;
            return _argSize;
        }

        private unsafe void ForceSigWalk()
        {
            // This can be only used before the actual argument iteration started
            Debug.Assert(!_ITERATION_STARTED);

#if _TARGET_X86_
            //
            // x86 is special as always
            //

            int numRegistersUsed = 0;
            int nSizeOfArgStack = 0;

            if (this.HasThis())
                numRegistersUsed++;

            if (this.HasRetBuffArg())
            {
                // DESKTOP BEHAVIOR                numRegistersUsed++;
                // On ProjectN ret buff arg is passed on the call stack as the top stack arg
                nSizeOfArgStack += IntPtr.Size;
            }

            // DESKTOP BEHAVIOR - This block is disabled for x86 as the param arg is the last argument on desktop x86.
            if (this.HasParamType())
            {
                numRegistersUsed++;
                _paramTypeLoc = (numRegistersUsed == 1) ?
                    ParamTypeLocation.Ecx : ParamTypeLocation.Edx;
                Debug.Assert(numRegistersUsed <= 2);
            }

            if (this.IsVarArg())
            {
                nSizeOfArgStack += IntPtr.Size;
                numRegistersUsed = ArchitectureConstants.NUM_ARGUMENT_REGISTERS; // Nothing else gets passed in registers for varargs
            }

#if FEATURE_INTERPRETER
            switch (_interpreterCallingConvention)
            {
                case CallingConvention.StdCall:
                    numRegistersUsed = ArchitectureConstants.NUM_ARGUMENT_REGISTERS;
                    break;

                case CallingConvention.ManagedStatic:
                case CallingConvention.ManagedInstance:
                    break;

                default:
                    Environment.FailFast("Unsupported calling convention.");
                    break;
            }
#endif // FEATURE_INTERPRETER

            int nArgs = this.NumFixedArgs();
            for (int i = (_skipFirstArg ? 1 : 0); i < nArgs; i++)
            {
                TypeHandle thValueType;
                bool argForcedToBeByref;
                CorElementType type = this.GetArgumentType(i, out thValueType, out argForcedToBeByref);
                if (argForcedToBeByref)
                    type = CorElementType.ELEMENT_TYPE_BYREF;

                if (!IsArgumentInRegister(ref numRegistersUsed, type, thValueType))
                {
                    int structSize = TypeHandle.GetElemSize(type, thValueType);

                    nSizeOfArgStack += ArchitectureConstants.StackElemSize(structSize);

                    if (nSizeOfArgStack > ArchitectureConstants.MAX_ARG_SIZE)
                    {
                        throw new NotSupportedException();
                    }
                }
            }

#if DESKTOP            // DESKTOP BEHAVIOR
            if (this.HasParamType())
            {
                if (numRegistersUsed < ArchitectureConstants.NUM_ARGUMENT_REGISTERS)
                {
                    numRegistersUsed++;
                    paramTypeLoc = (numRegistersUsed == 1) ?
                        ParamTypeLocation.Ecx : ParamTypeLocation.Edx;
                }
                else
                {
                    nSizeOfArgStack += IntPtr.Size;
                    paramTypeLoc = ParamTypeLocation.Stack;
                }
            }
#endif // DESKTOP BEHAVIOR

#else // _TARGET_X86_

            int maxOffset = TransitionBlock.GetOffsetOfArgs();

            int ofs;
            while (TransitionBlock.InvalidOffset != (ofs = GetNextOffset()))
            {
                int stackElemSize;

#if _TARGET_AMD64_
                // All stack arguments take just one stack slot on AMD64 because of arguments bigger 
                // than a stack slot are passed by reference. 
                stackElemSize = ArchitectureConstants.STACK_ELEM_SIZE;
#else
                stackElemSize = ArchitectureConstants.StackElemSize(GetArgSize());
                if (IsArgPassedByRef())
                    stackElemSize = ArchitectureConstants.STACK_ELEM_SIZE;
#endif

                int endOfs = ofs + stackElemSize;
                if (endOfs > maxOffset)
                {
                    if (endOfs > ArchitectureConstants.MAX_ARG_SIZE)
                    {
                        throw new NotSupportedException();
                    }
                    maxOffset = endOfs;
                }
            }
            // Clear the iterator started flag
            _ITERATION_STARTED = false;

            int nSizeOfArgStack = maxOffset - TransitionBlock.GetOffsetOfArgs();

#if _TARGET_AMD64_ && !UNIX_AMD64_ABI
            nSizeOfArgStack = (nSizeOfArgStack > (int)sizeof(ArgumentRegisters)) ?
                (nSizeOfArgStack - sizeof(ArgumentRegisters)) : 0;
#endif

#endif // _TARGET_X86_

            // Cache the result
            _nSizeOfArgStack = nSizeOfArgStack;
            _SIZE_OF_ARG_STACK_COMPUTED = true;

            this.Reset();
        }


#if !_TARGET_X86_
        // Accessors for built in argument descriptions of the special implicit parameters not mentioned directly
        // in signatures (this pointer and the like). Whether or not these can be used successfully before all the
        // explicit arguments have been scanned is platform dependent.
        public unsafe void GetThisLoc(ArgLocDesc* pLoc) { GetSimpleLoc(GetThisOffset(), pLoc); }
        public unsafe void GetRetBuffArgLoc(ArgLocDesc* pLoc) { GetSimpleLoc(GetRetBuffArgOffset(), pLoc); }
        public unsafe void GetParamTypeLoc(ArgLocDesc* pLoc) { GetSimpleLoc(GetParamTypeArgOffset(), pLoc); }
        public unsafe void GetVASigCookieLoc(ArgLocDesc* pLoc) { GetSimpleLoc(GetVASigCookieOffset(), pLoc); }
#endif // !_TARGET_X86_

#if _TARGET_ARM_
        // Get layout information for the argument that the ArgIterator is currently visiting.
        private unsafe void GetArgLoc(int argOffset, ArgLocDesc* pLoc)
        {
            //        LIMITED_METHOD_CONTRACT;

            pLoc->Init();

            pLoc->m_fRequires64BitAlignment = _fRequires64BitAlignment;

            int cSlots = (GetArgSize() + 3) / 4;

            if (TransitionBlock.IsFloatArgumentRegisterOffset(argOffset))
            {
                pLoc->m_idxFloatReg = (argOffset - TransitionBlock.GetOffsetOfFloatArgumentRegisters()) / 4;
                pLoc->m_cFloatReg = cSlots;
                return;
            }

            if (!TransitionBlock.IsStackArgumentOffset(argOffset))
            {
                pLoc->m_idxGenReg = TransitionBlock.GetArgumentIndexFromOffset(argOffset);

                if (cSlots <= (4 - pLoc->m_idxGenReg))
                {
                    pLoc->m_cGenReg = cSlots;
                }
                else
                {
                    pLoc->m_cGenReg = 4 - pLoc->m_idxGenReg;

                    pLoc->m_idxStack = 0;
                    pLoc->m_cStack = cSlots - pLoc->m_cGenReg;
                }
            }
            else
            {
                pLoc->m_idxStack = TransitionBlock.GetArgumentIndexFromOffset(argOffset) - 4;
                pLoc->m_cStack = cSlots;
            }
        }
#endif // _TARGET_ARM_

#if _TARGET_ARM64_
        // Get layout information for the argument that the ArgIterator is currently visiting.
        private unsafe void GetArgLoc(int argOffset, ArgLocDesc* pLoc)
        {
            //        LIMITED_METHOD_CONTRACT;

            pLoc->Init();

            if (TransitionBlock.IsFloatArgumentRegisterOffset(argOffset))
            {
                // Dividing by 8 as size of each register in FloatArgumentRegisters is 8 bytes.
                pLoc->m_idxFloatReg = (argOffset - TransitionBlock.GetOffsetOfFloatArgumentRegisters()) / 8;

                if (!_argTypeHandle.IsNull() && _argTypeHandle.IsHFA())
                {
                    CorElementType type = _argTypeHandle.GetHFAType();
                    pLoc->m_cFloatReg = (type == CorElementType.ELEMENT_TYPE_R4) ? GetArgSize() / sizeof(float) : GetArgSize() / sizeof(double);
                }
                else
                {
                    pLoc->m_cFloatReg = 1;
                }
                return;
            }

            int cSlots = (GetArgSize() + 7) / 8;

            // Composites greater than 16bytes are passed by reference
            TypeHandle dummy;
            if (GetArgType(out dummy) == CorElementType.ELEMENT_TYPE_VALUETYPE && GetArgSize() > ArchitectureConstants.ENREGISTERED_PARAMTYPE_MAXSIZE)
            {
                cSlots = 1;
            }

            if (!TransitionBlock.IsStackArgumentOffset(argOffset))
            {
                pLoc->m_idxGenReg = TransitionBlock.GetArgumentIndexFromOffset(argOffset);
                pLoc->m_cGenReg = cSlots;
            }
            else
            {
                pLoc->m_idxStack = TransitionBlock.GetArgumentIndexFromOffset(argOffset) - 8;
                pLoc->m_cStack = cSlots;
            }
        }
#endif // _TARGET_ARM64_

#if _TARGET_AMD64_ && UNIX_AMD64_ABI
        // Get layout information for the argument that the ArgIterator is currently visiting.
        unsafe void GetArgLoc(int argOffset, ArgLocDesc* pLoc)
        {
            //        LIMITED_METHOD_CONTRACT;

            pLoc->Init();

            if (TransitionBlock.IsFloatArgumentRegisterOffset(argOffset))
            {
                // Dividing by 8 as size of each register in FloatArgumentRegisters is 8 bytes.
                pLoc->m_idxFloatReg = (argOffset - TransitionBlock.GetOffsetOfFloatArgumentRegisters()) / 8;

                // UNIXTODO: Passing of structs, HFAs. For now, use the Windows convention.
                pLoc->m_cFloatReg = 1;
                return;
            }

            // UNIXTODO: Passing of structs, HFAs. For now, use the Windows convention.
            int cSlots = 1;

            if (!TransitionBlock.IsStackArgumentOffset(argOffset))
            {
                pLoc->m_idxGenReg = TransitionBlock.GetArgumentIndexFromOffset(argOffset);
                pLoc->m_cGenReg = cSlots;
            }
            else
            {
                pLoc->m_idxStack = (argOffset - TransitionBlock.GetOffsetOfArgs()) / 8;
                pLoc->m_cStack = cSlots;
            }
        }
#endif // _TARGET_ARM64_ && UNIX_AMD64_ABI

        private int _nSizeOfArgStack;      // Cached value of SizeOfArgStack

        private int _argNum;

        // Cached information about last argument
        private CorElementType _argType;
        private int _argSize;
        private TypeHandle _argTypeHandle;
        private TypeHandle _argTypeHandleOfByRefParam;
        private bool _argForceByRef;

#if _TARGET_X86_
        private int _curOfs;           // Current position of the stack iterator
        private int _numRegistersUsed;
#endif

#if _TARGET_AMD64_
#if UNIX_AMD64_ABI
        int m_idxGenReg;
        int m_idxStack;
        int m_idxFPReg;
#else
        private int _curOfs;           // Current position of the stack iterator
#endif
#endif

#if _TARGET_ARM_
        private int _idxGenReg;        // Next general register to be assigned a value
        private int _idxStack;         // Next stack slot to be assigned a value

        private ushort _wFPRegs;          // Bitmask of available floating point argument registers (s0-s15/d0-d7)
        private bool _fRequires64BitAlignment; // Cached info about the current arg
#endif

#if _TARGET_ARM64_
        private int _idxGenReg;        // Next general register to be assigned a value
        private int _idxStack;         // Next stack slot to be assigned a value
        private int _idxFPReg;         // Next FP register to be assigned a value
#endif

        // These are enum flags in CallingConvention.h, but that's really ugly in C#, so I've changed them to bools.
        private bool _ITERATION_STARTED; // Started iterating over arguments
        private bool _SIZE_OF_ARG_STACK_COMPUTED;
        private bool _RETURN_FLAGS_COMPUTED;
        private bool _RETURN_HAS_RET_BUFFER; // Cached value of HasRetBuffArg
        private uint _fpReturnSize;

        //        enum {
        /*        ITERATION_STARTED               = 0x0001,   
                SIZE_OF_ARG_STACK_COMPUTED      = 0x0002,
                RETURN_FLAGS_COMPUTED           = 0x0004,
                RETURN_HAS_RET_BUFFER           = 0x0008,   // Cached value of HasRetBuffArg
        */
#if _TARGET_X86_
        private enum ParamTypeLocation
        {
            Stack,
            Ecx,
            Edx
        }
        private ParamTypeLocation _paramTypeLoc;
        /*        PARAM_TYPE_REGISTER_MASK        = 0x0030,
                PARAM_TYPE_REGISTER_STACK       = 0x0010,
                PARAM_TYPE_REGISTER_ECX         = 0x0020,
                PARAM_TYPE_REGISTER_EDX         = 0x0030,*/
#endif

        //        METHOD_INVOKE_NEEDS_ACTIVATION  = 0x0040,   // Flag used by ArgIteratorForMethodInvoke

        //        RETURN_FP_SIZE_SHIFT            = 8,        // The rest of the flags is cached value of GetFPReturnSize
        //    };

        private void ComputeReturnFlags()
        {
            TypeHandle thValueType;
            CorElementType type = this.GetReturnType(out thValueType, out _RETURN_HAS_RET_BUFFER);

            if (!_RETURN_HAS_RET_BUFFER)
            {
                switch (type)
                {
                    case CorElementType.ELEMENT_TYPE_TYPEDBYREF:
                        throw new NotSupportedException();
#if ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
                    //                    if (sizeof(TypedByRef) > ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE)
                    //                        flags |= RETURN_HAS_RET_BUFFER;
#else
//                    flags |= RETURN_HAS_RET_BUFFER;
#endif
                    //                    break;

                    case CorElementType.ELEMENT_TYPE_R4:
                        _fpReturnSize = sizeof(float);
                        break;

                    case CorElementType.ELEMENT_TYPE_R8:
                        _fpReturnSize = sizeof(double);
                        break;

                    case CorElementType.ELEMENT_TYPE_VALUETYPE:
#if ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
                        {
                            Debug.Assert(!thValueType.IsNull());

#if FEATURE_HFA
                            if (thValueType.IsHFA() && !this.IsVarArg())
                            {
                                CorElementType hfaType = thValueType.GetHFAType();

                                _fpReturnSize = (hfaType == CorElementType.ELEMENT_TYPE_R4) ?
                                    (4 * (uint)sizeof(float)) :
                                    (4 * (uint)sizeof(double));

                                break;
                            }
#endif

                            uint size = thValueType.GetSize();

#if _TARGET_X86_ || _TARGET_AMD64_
                            // Return value types of size which are not powers of 2 using a RetBuffArg
                            if ((size & (size - 1)) != 0)
                            {
                                _RETURN_HAS_RET_BUFFER = true;
                                break;
                            }
#endif

                            if (size <= ArchitectureConstants.ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE)
                                break;
                        }
#endif // ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE

                        // Value types are returned using return buffer by default
                        _RETURN_HAS_RET_BUFFER = true;
                        break;

                    default:
                        break;
                }
            }

            _RETURN_FLAGS_COMPUTED = true;
        }


#if !_TARGET_X86_
        private unsafe void GetSimpleLoc(int offset, ArgLocDesc* pLoc)
        {
            //        WRAPPER_NO_CONTRACT; 
            pLoc->Init();
            pLoc->m_idxGenReg = TransitionBlock.GetArgumentIndexFromOffset(offset);
            pLoc->m_cGenReg = 1;
        }
#endif

        public static int ALIGN_UP(int input, int align_to)
        {
            return (input + (align_to - 1)) & ~(align_to - 1);
        }

        public static bool IS_ALIGNED(IntPtr val, int alignment)
        {
            Debug.Assert(0 == (alignment & (alignment - 1)));
            return 0 == (val.ToInt64() & (alignment - 1));
        }
    };
}
