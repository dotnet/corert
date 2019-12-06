// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMTypeRef : IEquatable<LLVMTypeRef>
    {
        public LLVMTypeRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMTypeRef(LLVMOpaqueType* value)
        {
            return new LLVMTypeRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueType*(LLVMTypeRef value)
        {
            return (LLVMOpaqueType*)value.Handle;
        }

        public static LLVMTypeRef Double => LLVM.DoubleType();

        public static LLVMTypeRef Float => LLVM.FloatType();

        public static LLVMTypeRef FP128 => LLVM.FP128Type();

        public static LLVMTypeRef Half => LLVM.HalfType();

        public static LLVMTypeRef Int1 => LLVM.Int1Type();

        public static LLVMTypeRef Int8 => LLVM.Int8Type();

        public static LLVMTypeRef Int16 => LLVM.Int16Type();

        public static LLVMTypeRef Int32 => LLVM.Int32Type();

        public static LLVMTypeRef Int64 => LLVM.Int64Type();

        public static LLVMTypeRef Label => LLVM.LabelType();

        public static LLVMTypeRef PPCFP128 => LLVM.PPCFP128Type();

        public static LLVMTypeRef Void => LLVM.VoidType();

        public static LLVMTypeRef X86FP80 => LLVM.X86FP80Type();

        public static LLVMTypeRef X86MMX => LLVM.X86MMXType();

        public LLVMValueRef AlignOf => (Handle != IntPtr.Zero) ? LLVM.AlignOf(this) : default;

        public uint ArrayLength => (Handle != IntPtr.Zero) ? LLVM.GetArrayLength(this) : default;

        public LLVMContextRef Context => (Handle != IntPtr.Zero) ? LLVM.GetTypeContext(this) : default;

        public LLVMTypeRef ElementType => (Handle != IntPtr.Zero) ? LLVM.GetElementType(this) : default;

        public uint IntWidth => (Handle != IntPtr.Zero) ? LLVM.GetIntTypeWidth(this) : default;

        public bool IsFunctionVarArg => (Handle != IntPtr.Zero) ? LLVM.IsFunctionVarArg(this) != 0 : default;

        public bool IsOpaqueStruct => (Handle != IntPtr.Zero) ? LLVM.IsOpaqueStruct(this) != 0 : default;

        public bool IsPackedStruct => (Handle != IntPtr.Zero) ? LLVM.IsPackedStruct(this) != 0 : default;

        public bool IsSized => (Handle != IntPtr.Zero) ? LLVM.TypeIsSized(this) != 0 : default;

        public LLVMTypeKind Kind => (Handle != IntPtr.Zero) ? LLVM.GetTypeKind(this) : default;

        public LLVMTypeRef[] ParamTypes
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return Array.Empty<LLVMTypeRef>();
                }

                var Dest = new LLVMTypeRef[ParamTypesCount];

                fixed (LLVMTypeRef* pDest = Dest)
                {
                    LLVM.GetParamTypes(this, (LLVMOpaqueType**)&pDest);
                }

                return Dest;
            }
        }

        public uint ParamTypesCount => (Handle != IntPtr.Zero) ? LLVM.CountParamTypes(this) : default;

        public uint PointerAddressSpace => (Handle != IntPtr.Zero) ? LLVM.GetPointerAddressSpace(this) : default;

        public LLVMTypeRef ReturnType => (Handle != IntPtr.Zero) ? LLVM.GetReturnType(this) : default;

        public LLVMValueRef SizeOf => (Handle != IntPtr.Zero) ? LLVM.SizeOf(this) : default;

        public LLVMTypeRef[] StructElementTypes
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return Array.Empty<LLVMTypeRef>();
                }

                var Dest = new LLVMTypeRef[StructElementTypesCount];

                fixed (LLVMTypeRef* pDest = Dest)
                {
                    LLVM.GetStructElementTypes(this, (LLVMOpaqueType**)&pDest);
                }

                return Dest;
            }
        }

        public uint StructElementTypesCount => (Handle != IntPtr.Zero) ? LLVM.CountStructElementTypes(this) : default;

        public string StructName
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pStructName = LLVM.GetStructName(this);

                if (pStructName is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pStructName, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }
        }

        public LLVMTypeRef[] Subtypes
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return Array.Empty<LLVMTypeRef>();
                }

                var Arr = new LLVMTypeRef[SubtypesCount];

                fixed (LLVMTypeRef* pArr = Arr)
                {
                    LLVM.GetSubtypes(this, (LLVMOpaqueType**)&pArr);
                }

                return Arr;
            }
        }

        public uint SubtypesCount => (Handle != IntPtr.Zero) ? LLVM.GetNumContainedTypes(this) : default;

        public LLVMValueRef Undef => (Handle != IntPtr.Zero) ? LLVM.GetUndef(this) : default;

        public uint VectorSize => (Handle != IntPtr.Zero) ? LLVM.GetVectorSize(this) : default;

        public static bool operator ==(LLVMTypeRef left, LLVMTypeRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMTypeRef left, LLVMTypeRef right) => !(left == right);

        public static LLVMTypeRef CreateFunction(LLVMTypeRef ReturnType, LLVMTypeRef[] ParamTypes, bool IsVarArg = false)
        {
            fixed (LLVMTypeRef* pParamTypes = ParamTypes)
            {
                return LLVM.FunctionType(ReturnType, (LLVMOpaqueType**)pParamTypes, (uint)ParamTypes?.Length, IsVarArg ? 1 : 0);
            }
        }

        public static LLVMTypeRef CreateArray(LLVMTypeRef ElementType, uint ElementCount) => LLVM.ArrayType(ElementType, ElementCount);

        public static LLVMTypeRef CreateInt(uint NumBits) => LLVM.IntType(NumBits);

        public static LLVMTypeRef CreateIntPtr(LLVMTargetDataRef TD) => LLVM.IntPtrType(TD);

        public static LLVMTypeRef CreateIntPtrForAS(LLVMTargetDataRef TD, uint AS) => LLVM.IntPtrTypeForAS(TD, AS);

        public static LLVMTypeRef CreatePointer(LLVMTypeRef ElementType, uint AddressSpace) => LLVM.PointerType(ElementType, AddressSpace);

        public static LLVMTypeRef CreateStruct(LLVMTypeRef[] ElementTypes, bool Packed)
        {
            fixed (LLVMTypeRef* pElementTypes = ElementTypes)
            {
                return LLVM.StructType((LLVMOpaqueType**)pElementTypes, (uint)ElementTypes?.Length, Packed ? 1 : 0);
            }
        }

        public static LLVMTypeRef CreateVector(LLVMTypeRef ElementType, uint ElementCount) => LLVM.VectorType(ElementType, ElementCount);

        public void Dump() => LLVM.DumpType(this);

        public override bool Equals(object obj) => obj is LLVMTypeRef other && Equals(other);

        public bool Equals(LLVMTypeRef other) => Handle == other.Handle;

        public double GenericValueToFloat(LLVMGenericValueRef GenVal) => LLVM.GenericValueToFloat(this, GenVal);

        public override int GetHashCode() => Handle.GetHashCode();

        public string PrintToString()
        {
            var pStr = LLVM.PrintTypeToString(this);

            if (pStr is null)
            {
                return string.Empty;
            }
            var span = new ReadOnlySpan<byte>(pStr, int.MaxValue);

            var result = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            LLVM.DisposeMessage(pStr);
            return result;
        }

        public LLVMTypeRef StructGetTypeAtIndex(uint index) => LLVM.StructGetTypeAtIndex(this, index);

        public void StructSetBody(LLVMTypeRef[] ElementTypes, bool Packed)
        {
            fixed (LLVMTypeRef* pElementTypes = ElementTypes)
            {
                LLVM.StructSetBody(this, (LLVMOpaqueType**)pElementTypes, (uint)ElementTypes?.Length, Packed ? 1 : 0);
            }
        }

        public override string ToString() => (Handle != IntPtr.Zero) ? PrintToString() : string.Empty;
    }
}
