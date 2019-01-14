// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


//
// This file is a line by line port of CallingConvention.h from the desktop CLR. See reference source in the ReferenceSource directory
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
#elif WASM
#define _TARGET_WASM_
#else
#error Unknown architecture!
#endif

// Provides an abstraction over platform specific calling conventions (specifically, the calling convention
// utilized by the JIT on that platform). The caller enumerates each argument of a signature in turn, and is 
// provided with information mapping that argument into registers and/or stack locations.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler;

using Internal.Runtime;
using Internal.NativeFormat;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public enum CORCOMPILE_GCREFMAP_TOKENS : byte
    {
        GCREFMAP_SKIP = 0,
        GCREFMAP_REF = 1,
        GCREFMAP_INTERIOR = 2,
        GCREFMAP_METHOD_PARAM = 3,
        GCREFMAP_TYPE_PARAM = 4,
        GCREFMAP_VASIG_COOKIE = 5,
    };

    public enum CallingConventions
    {
        ManagedInstance,
        ManagedStatic,
        StdCall,
        /*FastCall, CDecl */
    }

    /// <summary>
    /// System V struct passing
    /// The Classification types are described in the ABI spec at http://www.x86-64.org/documentation/abi.pdf
    /// </summary>
    public enum SystemVClassificationType : byte
    {
        SystemVClassificationTypeUnknown            = 0,
        SystemVClassificationTypeStruct             = 1,
        SystemVClassificationTypeNoClass            = 2,
        SystemVClassificationTypeMemory             = 3,
        SystemVClassificationTypeInteger            = 4,
        SystemVClassificationTypeIntegerReference   = 5,
        SystemVClassificationTypeIntegerByRef       = 6,
        SystemVClassificationTypeSSE                = 7,
        // SystemVClassificationTypeSSEUp           = Unused, // Not supported by the CLR.
        // SystemVClassificationTypeX87             = Unused, // Not supported by the CLR.
        // SystemVClassificationTypeX87Up           = Unused, // Not supported by the CLR.
        // SystemVClassificationTypeComplexX87      = Unused, // Not supported by the CLR.

        // Internal flags - never returned outside of the classification implementation.

        // This value represents a very special type with two eightbytes. 
        // First ByRef, second Integer (platform int).
        // The VM has a special Elem type for this type - ELEMENT_TYPE_TYPEDBYREF.
        // This is the classification counterpart for that element type. It is used to detect 
        // the special TypedReference type and specialize its classification.
        // This type is represented as a struct with two fields. The classification needs to do
        // special handling of it since the source/methadata type of the fieds is IntPtr. 
        // The VM changes the first to ByRef. The second is left as IntPtr (TYP_I_IMPL really). The classification needs to match this and
        // special handling is warranted (similar thing is done in the getGCLayout function for this type).
        SystemVClassificationTypeTypedReference     = 8,
        SystemVClassificationTypeMAX                = 9,
    }

    internal unsafe struct TypeHandle
    {
        public TypeHandle(TypeDesc type)
        {
            _type = type;
            _isByRef = _type.IsByRef;
            if (_isByRef)
            {
                _type = ((ByRefType)_type).ParameterType;
            }
        }

        private readonly TypeDesc _type;
        private readonly bool _isByRef;

        public bool Equals(TypeHandle other)
        {
            return _isByRef == other._isByRef && _type == other._type;
        }

        public override int GetHashCode() { return (int)_type.GetHashCode(); }

        public bool IsNull() { return _type == null && !_isByRef; }
        public bool IsValueType() { if (_isByRef) return false; return _type.IsValueType; }
        public bool IsPointerType() { if (_isByRef) return false; return _type.IsPointer; }

        public unsafe uint GetSize()
        {
            if (IsValueType())
                return (uint)_type.GetElementSize().AsInt;
            else
                return (uint)IntPtr.Size;
        }

        public bool RequiresAlign8()
        {
            if (_type.Context.Target.Architecture != TargetArchitecture.ARM)
            {
                return false;
            }
            if (_isByRef)
            {
                return false;
            }
            return _type.RequiresAlign8();
        }
        public bool IsHFA()
        {
            if (_type.Context.Target.Architecture != TargetArchitecture.ARM &&
                _type.Context.Target.Architecture != TargetArchitecture.ARM64)
            {
                return false;
            }
            if (_isByRef)
            {
                return false;
            }
            return _type is DefType defType && defType.IsHfa;
        }

        public CorElementType GetHFAType()
        {
            Debug.Assert(IsHFA());
            switch (_type.Context.Target.Architecture)
            {
                case TargetArchitecture.ARM:
                    if (RequiresAlign8())
                    {
                        return CorElementType.ELEMENT_TYPE_R8;
                    }
                    break;

                case TargetArchitecture.ARM64:
                    if (_type is DefType defType && defType.InstanceFieldAlignment.Equals(new LayoutInt(IntPtr.Size)))
                    {
                        return CorElementType.ELEMENT_TYPE_R8;
                    }
                    break;
            }
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
            Internal.TypeSystem.TypeFlags typeFlags = _type.Category;

            if (((typeFlags >= Internal.TypeSystem.TypeFlags.Boolean) && (typeFlags <= Internal.TypeSystem.TypeFlags.Double)) ||
                    (typeFlags == Internal.TypeSystem.TypeFlags.IntPtr) ||
                    (typeFlags == Internal.TypeSystem.TypeFlags.UIntPtr))
            {
                return (CorElementType)typeFlags; // If Redhawk thinks the corelementtype is a primitive type, then it agree with the concept of corelement type needed in this codebase.
            }
            else if (_type.IsVoid)
            {
                return CorElementType.ELEMENT_TYPE_VOID;
            }
            else if (IsValueType())
            {
                return CorElementType.ELEMENT_TYPE_VALUETYPE;
            }
            else if (_type.IsPointer)
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

        public TypeDesc GetRuntimeTypeHandle() { return _type; }
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
        public short m_cGenReg;      // Count of general registers used (or 0)

        public bool m_isSinglePrecision; // ARM64 - For determining if HFA is single or double precision
        public bool m_fRequires64BitAlignment;  // ARM - True if the argument should always be aligned (in registers or on the stack

        public int m_idxStack;     // First stack slot used (or -1)
        public int m_cStack;       // Count of stack slots used (or 0)

        // Initialize to represent a non-placed argument (no register or stack slots referenced).
        public void Init()
        {
            m_idxFloatReg = -1;
            m_cFloatReg = 0;
            m_idxGenReg = -1;
            m_cGenReg = 0;
            m_idxStack = -1;
            m_cStack = 0;

            m_isSinglePrecision = false;
            m_fRequires64BitAlignment = false;
        }
    };

    // The ArgDestination class represents a destination location of an argument.
    internal class ArgDestination
    {
        /// <summary>
        /// Transition block context.
        /// </summary>
        private readonly TransitionBlock _transitionBlock;

        // Offset of the argument relative to the base. On AMD64 on Unix, it can have a special
        // value that represent a struct that contain both general purpose and floating point fields 
        // passed in registers.
        private readonly int _offset;

        // For structs passed in registers, this member points to an ArgLocDesc that contains
        // details on the layout of the struct in general purpose and floating point registers.
        private readonly ArgLocDesc? _argLocDescForStructInRegs;

        // Construct the ArgDestination
        public ArgDestination(TransitionBlock transitionBlock, int offset, ArgLocDesc? argLocDescForStructInRegs)
        {
            _transitionBlock = transitionBlock;
            _offset = offset;
            _argLocDescForStructInRegs = argLocDescForStructInRegs;
        }

        public void GcMark(CORCOMPILE_GCREFMAP_TOKENS[] frame, int delta, bool interior)
        {
            frame[_offset + delta] = interior ? CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_INTERIOR : CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_REF;
        }

        // Returns true if the ArgDestination represents an HFA struct
        bool IsHFA()
        {
            return _argLocDescForStructInRegs.HasValue;
        }

        // Unix AMD64 ABI: Returns true if the ArgDestination represents a struct passed in registers.
        public bool IsStructPassedInRegs()
        {
            return _offset == TransitionBlock.StructInRegsOffset;
        }

        private int GetStructFloatRegDestinationAddress()
        {
            Debug.Assert(IsStructPassedInRegs());
            return _transitionBlock.OffsetOfFloatArgumentRegisters + _argLocDescForStructInRegs.Value.m_idxFloatReg * 16;
        }

        // Get destination address for non-floating point fields of a struct passed in registers.
        private int GetStructGenRegDestinationAddress()
        {
            Debug.Assert(IsStructPassedInRegs());
            return _transitionBlock.OffsetOfArgumentRegisters + _argLocDescForStructInRegs.Value.m_idxGenReg * 8;
        }

        private SystemVClassificationType GetTypeClassification(TypeDesc type)
        {
            switch (type.Category)
            {
                case Internal.TypeSystem.TypeFlags.Void:
                    return SystemVClassificationType.SystemVClassificationTypeUnknown;

                case Internal.TypeSystem.TypeFlags.Boolean:
                case Internal.TypeSystem.TypeFlags.Char:
                case Internal.TypeSystem.TypeFlags.SByte:
                case Internal.TypeSystem.TypeFlags.Byte:
                case Internal.TypeSystem.TypeFlags.Int16:
                case Internal.TypeSystem.TypeFlags.UInt16:
                case Internal.TypeSystem.TypeFlags.Int32:
                case Internal.TypeSystem.TypeFlags.UInt32:
                case Internal.TypeSystem.TypeFlags.Int64:
                case Internal.TypeSystem.TypeFlags.UInt64:
                case Internal.TypeSystem.TypeFlags.IntPtr:
                case Internal.TypeSystem.TypeFlags.UIntPtr:
                case Internal.TypeSystem.TypeFlags.Enum:
                case Internal.TypeSystem.TypeFlags.Pointer:
                case Internal.TypeSystem.TypeFlags.FunctionPointer:
                    return SystemVClassificationType.SystemVClassificationTypeInteger;

                case Internal.TypeSystem.TypeFlags.Single:
                case Internal.TypeSystem.TypeFlags.Double:
                    return SystemVClassificationType.SystemVClassificationTypeSSE;

                case Internal.TypeSystem.TypeFlags.ByRef:
                    return SystemVClassificationType.SystemVClassificationTypeIntegerByRef;

                case Internal.TypeSystem.TypeFlags.ValueType:
                    return SystemVClassificationType.SystemVClassificationTypeStruct;

                case Internal.TypeSystem.TypeFlags.Class:
                case Internal.TypeSystem.TypeFlags.GenericParameter:
                case Internal.TypeSystem.TypeFlags.Array:
                case Internal.TypeSystem.TypeFlags.SzArray:
                case Internal.TypeSystem.TypeFlags.Interface:
                    return SystemVClassificationType.SystemVClassificationTypeIntegerReference;

                default:
                    return SystemVClassificationType.SystemVClassificationTypeUnknown;
            }
        }

        // Report managed object pointers in the struct in registers
        // Arguments:
        //  fn - promotion function to apply to each managed object pointer
        //  sc - scan context to pass to the promotion function
        //  fieldBytes - size of the structure
        void ReportPointersFromStructInRegisters(TypeDesc type, int delta, CORCOMPILE_GCREFMAP_TOKENS[] frame)
        {
            // SPAN-TODO: GC reporting - https://github.com/dotnet/coreclr/issues/8517

            Debug.Assert(IsStructPassedInRegs());

            int genRegDest = GetStructGenRegDestinationAddress();
            foreach (FieldDesc field in type.GetFields())
            {
                if (field.IsStatic)
                {
                    continue;
                }
                SystemVClassificationType eightByteClassification = GetTypeClassification(field.FieldType);

                if (eightByteClassification != SystemVClassificationType.SystemVClassificationTypeSSE)
                {
                    if ((eightByteClassification == SystemVClassificationType.SystemVClassificationTypeIntegerReference) ||
                        (eightByteClassification == SystemVClassificationType.SystemVClassificationTypeIntegerByRef))
                    {
                        int eightByteSize = field.FieldType.GetElementSize().AsInt;
                        Debug.Assert((genRegDest & 7) == 0);

                        CORCOMPILE_GCREFMAP_TOKENS token;
                        if (eightByteClassification == SystemVClassificationType.SystemVClassificationTypeIntegerByRef)
                        {
                            token = CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_INTERIOR;
                        }
                        else
                        {
                            token = CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_REF;
                        }
                        int eightByteIndex = (genRegDest + field.Offset.AsInt) >> 3;
                        frame[delta + eightByteIndex] = token;
                    }
                }
            }
        }
    }

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
            if (this == obj)
                return true;

            ArgIteratorData other = obj as ArgIteratorData;
            if (other == null)
                return false;

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
        public CorElementType GetArgumentType(int argNum, out TypeHandle thArgType)
        {
            thArgType = _parameterTypes[argNum];
            CorElementType returnValue = thArgType.GetCorElementType();
            return returnValue;
        }

        public TypeHandle GetByRefArgumentType(int argNum)
        {
            return (argNum < _parameterTypes.Length && _parameterTypes[argNum].GetCorElementType() == CorElementType.ELEMENT_TYPE_BYREF) ?
                _parameterTypes[argNum] :
                default(TypeHandle);
        }

        public CorElementType GetReturnType(out TypeHandle thRetType)
        {
            thRetType = _returnType;
            return thRetType.GetCorElementType();
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
        private readonly TypeSystemContext _context;

        private bool _hasThis;
        private bool _hasParamType;
        private bool _extraFunctionPointerArg;
        private ArgIteratorData _argData;
        private bool[] _forcedByRefParams;
        private bool _skipFirstArg;
        private bool _extraObjectFirstArg;
        private CallingConventions _interpreterCallingConvention;
        private TransitionBlock _transitionBlock;

        public bool HasThis() { return _hasThis; }
        public bool IsVarArg() { return _argData.IsVarArg(); }
        public bool HasParamType() { return _hasParamType; }
        public int NumFixedArgs() { return _argData.NumFixedArgs() + (_extraFunctionPointerArg ? 1 : 0) + (_extraObjectFirstArg ? 1 : 0); }

        // Argument iteration.
        public CorElementType GetArgumentType(int argNum, out TypeHandle thArgType, out bool forceByRefReturn)
        {
            forceByRefReturn = false;

            if (_extraObjectFirstArg && argNum == 0)
            {
                thArgType = new TypeHandle(_context.GetWellKnownType(WellKnownType.Object));
                return CorElementType.ELEMENT_TYPE_CLASS;
            }

            argNum = _extraObjectFirstArg ? argNum - 1 : argNum;
            Debug.Assert(argNum >= 0);

            if (_forcedByRefParams != null && (argNum + 1) < _forcedByRefParams.Length)
                forceByRefReturn = _forcedByRefParams[argNum + 1];

            if (_extraFunctionPointerArg && argNum == _argData.NumFixedArgs())
            {
                thArgType = new TypeHandle(_context.GetWellKnownType(WellKnownType.IntPtr));
                return CorElementType.ELEMENT_TYPE_I;
            }

            return _argData.GetArgumentType(argNum, out thArgType);
        }

        public CorElementType GetReturnType(out TypeHandle thRetType, out bool forceByRefReturn)
        {
            if (_forcedByRefParams != null && _forcedByRefParams.Length > 0)
                forceByRefReturn = _forcedByRefParams[0];
            else
                forceByRefReturn = false;

            return _argData.GetReturnType(out thRetType);
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
        public ArgIterator(
            TypeSystemContext context,
            ArgIteratorData argData, 
            CallingConventions callConv, 
            bool hasParamType, 
            bool extraFunctionPointerArg, 
            bool[] forcedByRefParams, 
            bool skipFirstArg, 
            bool extraObjectFirstArg)
        {
            this = default(ArgIterator);
            _context = context;
            _argData = argData;
            _hasThis = callConv == CallingConventions.ManagedInstance;
            _hasParamType = hasParamType;
            _extraFunctionPointerArg = extraFunctionPointerArg;
            _forcedByRefParams = forcedByRefParams;
            _skipFirstArg = skipFirstArg;
            _extraObjectFirstArg = extraObjectFirstArg;
            _interpreterCallingConvention = callConv;
            _transitionBlock = TransitionBlock.FromTarget(context.Target);
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
            size += (uint)_transitionBlock.SizeOfArgumentRegisters;
#endif

            return (int)size;
        }

        //------------------------------------------------------------------------

        public uint CbStackPop()
        {
#if _TARGET_X86_
            //        WRAPPER_NO_CONTRACT;

            if (this.IsVarArg())
                return 0;
            else
                return SizeOfArgStack();
#else
            throw new NotImplementedException();
#endif
        }

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
        //                       will updateit.
        //
        //  typ:                 the signature type
        //=========================================================================
        private static bool IsArgumentInRegister(ref int pNumRegistersUsed, CorElementType typ, TypeHandle thArgType)
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
                            int structSize = TypeHandle.GetElemSize(typ, thArgType);
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
        public static bool IsArgPassedByRef(TransitionBlock transitionBlock, TypeHandle th)
        {
            //        LIMITED_METHOD_CONTRACT;

            Debug.Assert(!th.IsNull());

            // This method only works for valuetypes. It includes true value types, 
            // primitives, enums and TypedReference.
            Debug.Assert(th.IsValueType());

            uint size = th.GetSize();
#if _TARGET_AMD64_
            return IsArgPassedByRef(transitionBlock, (int)size);
#elif _TARGET_ARM64_
            // Composites greater than 16 bytes are passed by reference
            return ((size > ArchitectureConstants.ENREGISTERED_PARAMTYPE_MAXSIZE) && !th.IsHFA());
#else
#error ArgIterator::IsArgPassedByRef
#endif
        }

#if _TARGET_AMD64_
        // This overload should only be used in AMD64-specific code only.
        private static bool IsArgPassedByRef(TransitionBlock transitionBlock, int size)
        {
            //        LIMITED_METHOD_CONTRACT;

            // If the size is bigger than ENREGISTERED_PARAM_TYPE_MAXSIZE, or if the size is NOT a power of 2, then
            // the argument is passed by reference.
            return (size > transitionBlock.EnregisteredParamTypeMaxSize) || ((size & (size - 1)) != 0);
        }
#endif

        // This overload should be used for varargs only.
        private bool IsVarArgPassedByRef(int size)
        {
            //        LIMITED_METHOD_CONTRACT;

#if _TARGET_AMD64_
            return IsArgPassedByRef(_transitionBlock, size);
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
            return IsArgPassedByRef(_transitionBlock, _argSize);
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

        public int GetThisOffset()
        {
            return _transitionBlock.ThisOffset;
        }

        public unsafe int GetRetBuffArgOffset()
        {
            //            WRAPPER_NO_CONTRACT;

            Debug.Assert(this.HasRetBuffArg());

#if _TARGET_X86_
            // x86 is special as always
            // DESKTOP BEHAVIOR            ret += this.HasThis() ? ArgumentRegisters.GetOffsetOfEdx() : ArgumentRegisters.GetOffsetOfEcx();
            int ret = TransitionBlock.GetOffsetOfArgs();
#else
            // RetBuf arg is in the first argument register by default
            int ret = _transitionBlock.OffsetOfArgumentRegisters;

#if _TARGET_ARM64_
            ret += ArgumentRegisters.GetOffsetOfx8();
#else
            // But if there is a this pointer, push it to the second.
            if (this.HasThis())
                ret += IntPtr.Size;
#endif  // _TARGET_ARM64_
#endif  // _TARGET_X86_

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
            int ret = _transitionBlock.OffsetOfArgumentRegisters;

            if (this.HasThis())
            {
                ret += IntPtr.Size;
            }

            if (this.HasRetBuffArg() && IsRetBuffPassedAsFirstArg())
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
            int ret = _transitionBlock.OffsetOfArgumentRegisters;

            if (this.HasThis())
            {
                ret += IntPtr.Size;
            }

            if (this.HasRetBuffArg() && IsRetBuffPassedAsFirstArg())
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

                if (this.HasRetBuffArg() && IsRetBuffPassedAsFirstArg())
                {
#if !_TARGET_X86_
                    numRegistersUsed++;
#else
                    // DESKTOP BEHAVIOR is to do nothing here, as ret buf is never reached by the scan algorithm that walks backwards
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
                    case CallingConventions.StdCall:
                        _numRegistersUsed = ArchitectureConstants.NUM_ARGUMENT_REGISTERS;
                        _curOfs = TransitionBlock.GetOffsetOfArgs() + numRegistersUsed * IntPtr.Size + initialArgOffset;
                        break;

                    case CallingConventions.ManagedStatic:
                    case CallingConventions.ManagedInstance:
                        _numRegistersUsed = numRegistersUsed;
                        // DESKTOP BEHAVIOR     _curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + SizeOfArgStack());
                        _curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + initialArgOffset);
                        break;

                    default:
                        Environment.FailFast("Unsupported calling convention.");
                        break;
                }
#else
                        _numRegistersUsed = numRegistersUsed;
// DESKTOP BEHAVIOR     _curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + SizeOfArgStack());
                        _curOfs = (int)(TransitionBlock.GetOffsetOfArgs() + initialArgOffset);
#endif

#elif _TARGET_AMD64_
#if UNIX_AMD64_ABI
                _idxGenReg = numRegistersUsed;
                _idxStack = 0;
                _idxFPReg = 0;
#else
                _curOfs = _transitionBlock.OffsetOfArgs + numRegistersUsed * IntPtr.Size;
#endif
#elif _TARGET_ARM_
                _idxGenReg = numRegistersUsed;
                _idxStack = 0;

                _wFPRegs = 0;
#elif _TARGET_ARM64_
                _idxGenReg = numRegistersUsed;
                _idxStack = 0;

                _idxFPReg = 0;
#elif _TARGET_WASM_
                throw new NotImplementedException();
#else
                PORTABILITY_ASSERT("ArgIterator::GetNextOffset");
#endif

#if !_TARGET_WASM_
                _argNum = (_skipFirstArg ? 1 : 0);

                _ITERATION_STARTED = true;
#endif // !_TARGET_WASM_
            }

            if (_argNum >= this.NumFixedArgs())
                return TransitionBlock.InvalidOffset;

            CorElementType argType = this.GetArgumentType(_argNum, out _argTypeHandle, out _argForceByRef);

            _argTypeHandleOfByRefParam = (argType == CorElementType.ELEMENT_TYPE_BYREF ? _argData.GetByRefArgumentType(_argNum) : default(TypeHandle));

            _argNum++;

            int argSize = TypeHandle.GetElemSize(argType, _argTypeHandle);

#if _TARGET_ARM64_
            // NOT DESKTOP BEHAVIOR: The S and D registers overlap, and the UniversalTransitionThunk copies D registers to the transition blocks. We'll need
            // to work with the D registers here as well.
            bool processingFloatsAsDoublesFromTransitionBlock = false;
            if (argType == CorElementType.ELEMENT_TYPE_VALUETYPE && _argTypeHandle.IsHFA() && _argTypeHandle.GetHFAType() == CorElementType.ELEMENT_TYPE_R4)
            {
                if ((argSize / sizeof(float)) + _idxFPReg <= 8)
                {
                    argSize *= 2;
                    processingFloatsAsDoublesFromTransitionBlock = true;
                }
            }
#endif

            _argType = argType;
            _argSize = argSize;

            argType = _argForceByRef ? CorElementType.ELEMENT_TYPE_BYREF : argType;
            argSize = _argForceByRef ? IntPtr.Size : argSize;

#pragma warning disable 219,168 // Unused local
            int argOfs;
#pragma warning restore 219,168

#if _TARGET_X86_
#if FEATURE_INTERPRETER
            if (_interpreterCallingConvention != CallingConventions.ManagedStatic && _interpreterCallingConvention != CallingConventions.ManagedInstance)
            {
                argOfs = _curOfs;
                _curOfs += ArchitectureConstants.StackElemSize(argSize);
                return argOfs;
            }
#endif
            if (IsArgumentInRegister(ref _numRegistersUsed, argType, _argTypeHandle))
            {
                return TransitionBlock.GetOffsetOfArgumentRegisters() + (ArchitectureConstants.NUM_ARGUMENT_REGISTERS - _numRegistersUsed) * IntPtr.Size;
            }

            // DESKTOP BEHAVIOR _curOfs -= ArchitectureConstants.StackElemSize(argSize);
            // DESKTOP BEHAVIOR return _curOfs;
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
                        // UNIXTODO: FEATURE_UNIX_AMD64_STRUCT_PASSING: Passing of structs, HFAs. For now, use the Windows convention.
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
            argOfs = _curOfs - _transitionBlock.OffsetOfArgs;
            _curOfs += IntPtr.Size;

            if ((cFPRegs == 0) || (argOfs >= _transitionBlock.SizeOfArgumentRegisters))
            {
                return argOfs + _transitionBlock.OffsetOfArgs;
            }
            else
            {
                int idxFpReg = argOfs / IntPtr.Size;
                return _transitionBlock.OffsetOfFloatArgumentRegisters + idxFpReg * TransitionBlock.SizeOfM128A;
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
                        fRequiresAlign64Bit = _argTypeHandle.RequiresAlign8();

                        // Handle HFAs: packed structures of 1-4 floats or doubles that are passed in FP argument
                        // registers if possible.
                        if (_argTypeHandle.IsHFA())
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
                        if (_argTypeHandle.IsHFA())
                        {
                            CorElementType type = _argTypeHandle.GetHFAType();
                            if (processingFloatsAsDoublesFromTransitionBlock)
                                cFPRegs = argSize / sizeof(double);
                            else
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
#elif _TARGET_WASM_
            throw new NotImplementedException();
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

            if (this.HasRetBuffArg() && IsRetBuffPassedAsFirstArg())
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
                case CallingConventions.StdCall:
                    numRegistersUsed = ArchitectureConstants.NUM_ARGUMENT_REGISTERS;
                    break;

                case CallingConventions.ManagedStatic:
                case CallingConventions.ManagedInstance:
                    break;

                default:
                    Environment.FailFast("Unsupported calling convention.");
                    break;
            }
#endif // FEATURE_INTERPRETER

            int nArgs = this.NumFixedArgs();
            for (int i = (_skipFirstArg ? 1 : 0); i < nArgs; i++)
            {
                TypeHandle thArgType;
                bool argForcedToBeByref;
                CorElementType type = this.GetArgumentType(i, out thArgType, out argForcedToBeByref);
                if (argForcedToBeByref)
                    type = CorElementType.ELEMENT_TYPE_BYREF;

                if (!IsArgumentInRegister(ref numRegistersUsed, type, thArgType))
                {
                    int structSize = TypeHandle.GetElemSize(type, thArgType);

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

            int maxOffset = _transitionBlock.OffsetOfArgs;

            int ofs;
            while (TransitionBlock.InvalidOffset != (ofs = GetNextOffset()))
            {
                int stackElemSize;

#if _TARGET_AMD64_
                // All stack arguments take just one stack slot on AMD64 because of arguments bigger 
                // than a stack slot are passed by reference. 
                stackElemSize = _transitionBlock.StackElemSize;
#else
                stackElemSize = ArchitectureConstants.StackElemSize(GetArgSize());
                if (IsArgPassedByRef())
                    stackElemSize = ArchitectureConstants.STACK_ELEM_SIZE;
#endif

                int endOfs = ofs + stackElemSize;
                if (endOfs > maxOffset)
                {
                    if (endOfs > TransitionBlock.MaxArgSize)
                    {
                        throw new NotSupportedException();
                    }
                    maxOffset = endOfs;
                }
            }
            // Clear the iterator started flag
            _ITERATION_STARTED = false;

            int nSizeOfArgStack = maxOffset - _transitionBlock.OffsetOfArgs;

#if _TARGET_AMD64_ && !UNIX_AMD64_ABI
            nSizeOfArgStack = (nSizeOfArgStack > (int)_transitionBlock.SizeOfArgumentRegisters) ?
                (nSizeOfArgStack - _transitionBlock.SizeOfArgumentRegisters) : 0;
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
        public ArgLocDesc? GetArgLoc(int argOffset)
        {
            //        LIMITED_METHOD_CONTRACT;

            ArgLocDesc pLoc = new ArgLocDesc();

            pLoc.m_fRequires64BitAlignment = _fRequires64BitAlignment;

            int cSlots = (GetArgSize() + 3) / 4;

            if (TransitionBlock.IsFloatArgumentRegisterOffset(argOffset))
            {
                pLoc.m_idxFloatReg = (argOffset - TransitionBlock.GetOffsetOfFloatArgumentRegisters()) / 4;
                pLoc.m_cFloatReg = cSlots;
                return;
            }

            if (!TransitionBlock.IsStackArgumentOffset(argOffset))
            {
                pLoc.m_idxGenReg = TransitionBlock.GetArgumentIndexFromOffset(argOffset);

                if (cSlots <= (4 - pLoc.m_idxGenReg))
                {
                    pLoc.m_cGenReg = cSlots;
                }
                else
                {
                    pLoc.m_cGenReg = 4 - pLoc->m_idxGenReg;

                    pLoc.m_idxStack = 0;
                    pLoc.m_cStack = cSlots - pLoc->m_cGenReg;
                }
            }
            else
            {
                pLoc.m_idxStack = TransitionBlock.GetArgumentIndexFromOffset(argOffset) - 4;
                pLoc.m_cStack = cSlots;
            }
            return argLocDesc;
        }
#endif // _TARGET_ARM_

#if _TARGET_ARM64_
        // Get layout information for the argument that the ArgIterator is currently visiting.
        public ArgLocDesc? GetArgLoc(int argOffset)
        {
            //        LIMITED_METHOD_CONTRACT;

            ArgLocDesc pLoc = new ArgLocDesc();

            if (TransitionBlock.IsFloatArgumentRegisterOffset(argOffset))
            {
                // Dividing by 8 as size of each register in FloatArgumentRegisters is 8 bytes.
                pLoc->m_idxFloatReg = (argOffset - TransitionBlock.GetOffsetOfFloatArgumentRegisters()) / 8;

                if (!_argTypeHandle.IsNull() && _argTypeHandle.IsHFA())
                {
                    CorElementType type = _argTypeHandle.GetHFAType();
                    bool isFloatType = (type == CorElementType.ELEMENT_TYPE_R4);

                    // DESKTOP BEHAVIOR pLoc->m_cFloatReg = isFloatType ? GetArgSize() / sizeof(float) : GetArgSize() / sizeof(double);
                    pLoc->m_cFloatReg = GetArgSize() / sizeof(double);
                    pLoc->m_isSinglePrecision = isFloatType;
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
                pLoc->m_idxStack = TransitionBlock.GetStackArgumentIndexFromOffset(argOffset);
                pLoc->m_cStack = cSlots;
            }
            return pLoc;
        }
#endif // _TARGET_ARM64_

#if _TARGET_AMD64_ && UNIX_AMD64_ABI
        // Get layout information for the argument that the ArgIterator is currently visiting.
        public ArgLocDesc? GetArgLoc(int argOffset)
        {
            //        LIMITED_METHOD_CONTRACT;

            if (argOffset == TransitionBlock.StructInRegsOffset)
            {
                // We always already have argLocDesc for structs passed in registers, we 
                // compute it in the GetNextOffset for those since it is always needed.
                Debug.Assert(false);
                return null;
            }
        
            ArgLocDesc pLoc = new ArgLocDesc();

            if (TransitionBlock.IsFloatArgumentRegisterOffset(argOffset))
            {
                // Dividing by 8 as size of each register in FloatArgumentRegisters is 8 bytes.
                pLoc.m_idxFloatReg = (argOffset - TransitionBlock.GetOffsetOfFloatArgumentRegisters()) / 8;

                // UNIXTODO: Passing of structs, HFAs. For now, use the Windows convention.
                pLoc.m_cFloatReg = 1;
                return;
            }

            // UNIXTODO: Passing of structs, HFAs. For now, use the Windows convention.
            int cSlots = 1;

            if (!TransitionBlock.IsStackArgumentOffset(argOffset))
            {
                pLoc.m_idxGenReg = TransitionBlock.GetArgumentIndexFromOffset(argOffset);
                pLoc.m_cGenReg = cSlots;
            }
            else
            {
                pLoc.m_idxStack = (argOffset - TransitionBlock.GetOffsetOfArgs()) / 8;
                pLoc.m_cStack = cSlots;
            }
            return pLoc;
        }
#endif // _TARGET_AMD64_ && UNIX_AMD64_ABI

#if (_TARGET_AMD64_ && !UNIX_AMD64_ABI) || _TARGET_X86_
        // Get layout information for the argument that the ArgIterator is currently visiting.
        public ArgLocDesc? GetArgLoc(int argOffset)
        {
            return null;
        }
#endif

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
        int _idxGenReg;
        int _idxStack;
        int _idxFPReg;
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

        // These are enum flags in CallingConventions.h, but that's really ugly in C#, so I've changed them to bools.
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

        internal static void ComputeReturnValueTreatment(TransitionBlock transitionBlock, CorElementType type, TypeHandle thRetType, bool isVarArgMethod, out bool usesRetBuffer, out uint fpReturnSize)

        {
            usesRetBuffer = false;
            fpReturnSize = 0;

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
                    fpReturnSize = sizeof(float);
                    break;

                case CorElementType.ELEMENT_TYPE_R8:
                    fpReturnSize = sizeof(double);
                    break;

                case CorElementType.ELEMENT_TYPE_VALUETYPE:
#if ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE
                    {
                        Debug.Assert(!thRetType.IsNull() && thRetType.IsValueType());

#if FEATURE_HFA
                        if (thRetType.IsHFA() && !isVarArgMethod)
                        {
                            CorElementType hfaType = thRetType.GetHFAType();

#if _TARGET_ARM64_
                            // DESKTOP BEHAVIOR fpReturnSize = (hfaType == CorElementType.ELEMENT_TYPE_R4) ? (4 * (uint)sizeof(float)) : (4 * (uint)sizeof(double));
                            // S and D registers overlap. Since we copy D registers in the UniversalTransitionThunk, we'll
                            // thread floats like doubles during copying.
                            fpReturnSize = 4 * (uint)sizeof(double);
#else
                            fpReturnSize = (hfaType == CorElementType.ELEMENT_TYPE_R4) ?
                                (4 * (uint)sizeof(float)) :
                                (4 * (uint)sizeof(double));
#endif

                            break;
                        }
#endif

                        uint size = thRetType.GetSize();

#if _TARGET_X86_ || _TARGET_AMD64_
                        // Return value types of size which are not powers of 2 using a RetBuffArg
                        if ((size & (size - 1)) != 0)
                        {
                            usesRetBuffer = true;
                            break;
                        }
#endif

                        if (size <= transitionBlock.EnregisteredReturnTypeIntegerMaxSize)
                            break;
                    }
#endif // ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE

                    // Value types are returned using return buffer by default
                    usesRetBuffer = true;
                    break;

                default:
                    break;
            }
        }

        private void ComputeReturnFlags()
        {
            TypeHandle thRetType;
            CorElementType type = this.GetReturnType(out thRetType, out _RETURN_HAS_RET_BUFFER);

            if (!_RETURN_HAS_RET_BUFFER)
            {
                ComputeReturnValueTreatment(_transitionBlock, type, thRetType, this.IsVarArg(), out _RETURN_HAS_RET_BUFFER, out _fpReturnSize);
            }

            _RETURN_FLAGS_COMPUTED = true;
        }


#if !_TARGET_X86_
        private unsafe void GetSimpleLoc(int offset, ArgLocDesc* pLoc)
        {
            //        WRAPPER_NO_CONTRACT; 
            pLoc->Init();
            pLoc->m_idxGenReg = _transitionBlock.GetArgumentIndexFromOffset(offset);
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

        public static bool IsRetBuffPassedAsFirstArg()
        {
            //        WRAPPER_NO_CONTRACT; 
#if !_TARGET_ARM64_
            return true;
#else
            return false;
#endif
        }
    };
}
