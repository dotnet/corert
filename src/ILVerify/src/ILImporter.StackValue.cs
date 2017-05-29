// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace Internal.IL
{
    struct StackValue
    {
        [Flags]
        public enum StackValueFlags
        {
            None = 0,
            ReadOnly = 1 << 1
        }
        private StackValueFlags Flags;

        public readonly StackValueKind Kind;
        public readonly TypeDesc Type;

        private StackValue(StackValueKind kind, TypeDesc type = null)
        {
            this.Kind = kind;
            this.Type = type;
            this.Flags = StackValueFlags.None;
        }

        public void SetIsReadOnly()
        {
            Flags |= StackValueFlags.ReadOnly;
        }

        public bool IsReadOnly
        {
            get { return (Flags & StackValueFlags.ReadOnly) == StackValueFlags.ReadOnly; }
        }

        public StackValue DereferenceByRef()
        {
            Debug.Assert(Kind == StackValueKind.ByRef && Type != null, "Cannot dereference");
            return CreateFromType(Type);
        }


        static public StackValue CreateUnknown()
        {
            return new StackValue(StackValueKind.Unknown);
        }

        static public StackValue CreatePrimitive(StackValueKind kind)
        {
            Debug.Assert(kind == StackValueKind.Int32 || 
                         kind == StackValueKind.Int64 || 
                         kind == StackValueKind.NativeInt ||
                         kind == StackValueKind.Float);

            return new StackValue(kind);
        }

        static public StackValue CreateObjRef(TypeDesc type)
        {
            return new StackValue(StackValueKind.ObjRef, type);
        }

        static public StackValue CreateValueType(TypeDesc type)
        {
            return new StackValue(StackValueKind.ValueType, type);
        }

        static public StackValue CreateByRef(TypeDesc type)
        {
            return new StackValue(StackValueKind.ByRef, type);
        }

        static public StackValue CreateFromType(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                case TypeFlags.Char:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return CreatePrimitive(StackValueKind.Int32);
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return CreatePrimitive(StackValueKind.Int64);
                case TypeFlags.Single:
                case TypeFlags.Double:
                    return CreatePrimitive(StackValueKind.Float);
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Pointer:
                    return CreatePrimitive(StackValueKind.NativeInt);
                case TypeFlags.Enum:
                    return CreateFromType(type.UnderlyingType);
                case TypeFlags.ByRef:
                    return CreateByRef(((ByRefType)type).ParameterType);
                default:
                    if (type.IsValueType)
                        return CreateValueType(type);
                    else
                        return CreateObjRef(type);
            }
        }

        // For now, match PEVerify type formating to make it easy to compare with baseline
        static string TypeToStringForByRef(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean: return "Boolean";
                case TypeFlags.Char:    return "Char";
                case TypeFlags.SByte:   return "SByte";
                case TypeFlags.Byte:    return "Byte";
                case TypeFlags.Int16:   return "Int16";
                case TypeFlags.UInt16:  return "UInt16";
                case TypeFlags.Int32:   return "Int32";
                case TypeFlags.UInt32:  return "UInt32";
                case TypeFlags.Int64:   return "Int64";
                case TypeFlags.UInt64:  return "UInt64";
                case TypeFlags.Single:  return "Single";
                case TypeFlags.Double:  return "Double";
                case TypeFlags.IntPtr:  return "IntPtr";
                case TypeFlags.UIntPtr: return "UIntPtr";
            }

            return "'" + type.ToString() + "'";
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case StackValueKind.Int32:
                    return "Int32";
                case StackValueKind.Int64:
                    return "Long";
                case StackValueKind.NativeInt:
                    return "Native Int";
                case StackValueKind.Float:
                    return "Double";
                case StackValueKind.ByRef:
                    return "address of " + TypeToStringForByRef(Type);
                case StackValueKind.ObjRef:
                    return (Type != null) ? "ref '" + Type.ToString() + "'" : "Nullobjref 'NullReference'";
                case StackValueKind.ValueType:
                    return "value '" + Type.ToString() + "'";
                default:
                    return "unknown";
            }
        }
    }

    partial class ILImporter
    {
        static TypeFlags GetReducedTypeCategory(TypeDesc type)
        {
            var category = type.Category;

            switch (type.Category)
            {
                case TypeFlags.Byte: return TypeFlags.SByte;
                case TypeFlags.UInt16: return TypeFlags.Int16;
                case TypeFlags.UInt32: return TypeFlags.Int32;
                case TypeFlags.UInt64: return TypeFlags.Int64;
                case TypeFlags.UIntPtr: return TypeFlags.IntPtr;

                case TypeFlags.SByte:
                case TypeFlags.Int16:
                case TypeFlags.Int32:
                case TypeFlags.Int64:
                case TypeFlags.IntPtr:
                case TypeFlags.Boolean:
                case TypeFlags.Char:
                case TypeFlags.Single:
                case TypeFlags.Double:
                    return category;
            }

            return TypeFlags.Unknown;
        }

        static bool IsSameReducedType(TypeDesc src, TypeDesc dst)
        {
            var srcCategory = GetReducedTypeCategory(src);
            if (srcCategory == TypeFlags.Unknown)
                return false;
            return srcCategory == GetReducedTypeCategory(dst);
        }

        bool IsAssignable(TypeDesc src, TypeDesc dst, bool allowSizeEquivalence = false)
        {
            if (src == dst)
                return true;

            if (src.IsValueType || dst.IsValueType)
            {
                if (allowSizeEquivalence && IsSameReducedType(src, dst))
                    return true;

                // TODO IsEquivalent
                return false;
            }

            return CastingHelper.CanCastTo(src, dst);
        }

        bool IsAssignable(StackValue src, StackValue dst)
        {
            if (src.Kind == dst.Kind && src.Type == dst.Type)
                return true;

            switch (src.Kind)
            {
            case StackValueKind.ObjRef:
                if (dst.Kind != StackValueKind.ObjRef)
                    return false;

                // null is always assignable
                if (src.Type == null)
                    return true;

                return CastingHelper.CanCastTo(src.Type, dst.Type);

            case StackValueKind.ValueType:

                // TODO: Other cases - variance, etc.

                return false;

            case StackValueKind.ByRef:

                // TODO: Other cases - variance, etc.

                return false;

            case StackValueKind.Int32:
                return (dst.Kind == StackValueKind.Int64 || dst.Kind == StackValueKind.NativeInt);

            case StackValueKind.Int64:
                return false;

            case StackValueKind.NativeInt:
                return (dst.Kind == StackValueKind.Int64);

            case StackValueKind.Float:
                return false;

            default:
                // TODO:
                // return false;
                throw new NotImplementedException();
            }

#if false
    if (child == parent)
    {
        return(TRUE);
    }

    // Normally we just let the runtime sort it out but we wish to be more strict
    // than the runtime wants to be.  For backwards compatibility, the runtime considers 
    // int32[] and nativeInt[] to be the same on 32-bit machines.  It also is OK with
    // int64[] and nativeInt[] on a 64-bit machine.  

    if (child.IsType(TI_REF) && parent.IsType(TI_REF)
        && jitInfo->isSDArray(child.GetClassHandleForObjRef())
        && jitInfo->isSDArray(parent.GetClassHandleForObjRef()))
    {
        BOOL runtime_OK;

        // never be more lenient than the runtime
        runtime_OK = jitInfo->canCast(child.m_cls, parent.m_cls);
        if (!runtime_OK)
            return false;

        CORINFO_CLASS_HANDLE handle;
        CorInfoType pType = jitInfo->getChildType(child.GetClassHandleForObjRef(),  &handle);
        CorInfoType cType = jitInfo->getChildType(parent.GetClassHandleForObjRef(), &handle);

        // don't care whether it is signed
        if (cType == CORINFO_TYPE_NATIVEUINT)
            cType = CORINFO_TYPE_NATIVEINT;
        if (pType == CORINFO_TYPE_NATIVEUINT)
            pType = CORINFO_TYPE_NATIVEINT;
        
        if (cType == CORINFO_TYPE_NATIVEINT)
            return pType == CORINFO_TYPE_NATIVEINT;
            
        if (pType == CORINFO_TYPE_NATIVEINT)
            return cType == CORINFO_TYPE_NATIVEINT;

        return runtime_OK;
    }
   
    if (parent.IsUnboxedGenericTypeVar() || child.IsUnboxedGenericTypeVar())
    {
        return (FALSE);  // need to have had child == parent
    }
    else if (parent.IsType(TI_REF))
    {
        // An uninitialized objRef is not compatible to initialized.
        if (child.IsUninitialisedObjRef() && !parent.IsUninitialisedObjRef())
            return FALSE;

        if (child.IsNullObjRef())                   // NULL can be any reference type
            return TRUE;
        if (!child.IsType(TI_REF))
            return FALSE;

        return jitInfo->canCast(child.m_cls, parent.m_cls);

    }
    else if (parent.IsType(TI_METHOD))
    {
        if (!child.IsType(TI_METHOD))
            return FALSE;

        // Right now we don't bother merging method handles
        return FALSE;
    }
    else if (parent.IsType(TI_STRUCT))
    {
        if (!child.IsType(TI_STRUCT))
            return FALSE;

        // Structures are compatible if they are equivalent
        return jitInfo->areTypesEquivalent(child.m_cls, parent.m_cls);
    }
    else if (parent.IsByRef())
    {
        return tiCompatibleWithByRef(jitInfo, child, parent);
    }

    return FALSE;
#endif 
        }


        bool IsBinaryComparable(StackValue src, StackValue dst, ILOpcode op)
        {
            if (src.Kind == dst.Kind && src.Type == dst.Type)
                return true;

            switch (src.Kind)
            {
                case StackValueKind.ObjRef:
                    switch (dst.Kind)
                    {
                        case StackValueKind.ObjRef:
                            return op == ILOpcode.beq || op == ILOpcode.beq_s ||
                                   op == ILOpcode.bne_un || op == ILOpcode.bne_un_s ||
                                   op == ILOpcode.ceq;
                        default:
                            return false;
                    }

                case StackValueKind.ValueType:
                    return false;

                case StackValueKind.ByRef:
                    switch (dst.Kind)
                    {
                        case StackValueKind.ByRef:
                            return true;
                        case StackValueKind.NativeInt:
                            return op == ILOpcode.beq || op == ILOpcode.beq_s ||
                                   op == ILOpcode.bne_un || op == ILOpcode.bne_un_s ||
                                   op == ILOpcode.ceq;
                        default:
                            return false;
                    }

                case StackValueKind.Int32:
                    return (dst.Kind == StackValueKind.Int64 || dst.Kind == StackValueKind.NativeInt);

                case StackValueKind.Int64:
                    return (dst.Kind == StackValueKind.Int64);

                case StackValueKind.NativeInt:
                    switch (dst.Kind)
                    {
                        case StackValueKind.Int32:
                        case StackValueKind.NativeInt:
                            return true;
                        case StackValueKind.ByRef:
                            return op == ILOpcode.beq || op == ILOpcode.beq_s ||
                                   op == ILOpcode.bne_un || op == ILOpcode.bne_un_s ||
                                   op == ILOpcode.ceq;
                        default:
                            return false;
                    }

                case StackValueKind.Float:
                    return dst.Kind == StackValueKind.Float;

                default:
                    throw new NotImplementedException();
            }
        }

        bool IsByRefLike(StackValue value)
        {
            if (value.Kind == StackValueKind.ByRef)
                return true;

            // TODO: Check for other by-ref like types Slice<T>, ArgIterator, TypedReference

            return false;
        }
    }
}
