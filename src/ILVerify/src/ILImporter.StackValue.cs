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

        private StackValue(StackValueKind kind, TypeDesc type = null, StackValueFlags flags = StackValueFlags.None)
        {
            this.Kind = kind;
            this.Type = type;
            this.Flags = flags;
        }

        public void SetIsReadOnly()
        {
            Flags |= StackValueFlags.ReadOnly;
        }

        public bool IsReadOnly
        {
            get { return (Flags & StackValueFlags.ReadOnly) == StackValueFlags.ReadOnly; }
        }

        public bool IsNullReference
        {
            get { return Kind == StackValueKind.ObjRef && Type == null; }
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

        static public StackValue CreatePrimitive(StackValueKind kind, TypeSystemContext typeSystemContext)
        {
            switch (kind)
            {
                case StackValueKind.Int32:
                    return new StackValue(kind, typeSystemContext.GetWellKnownType(WellKnownType.Int32));
                case StackValueKind.Int64:
                    return new StackValue(kind, typeSystemContext.GetWellKnownType(WellKnownType.Int64));
                case StackValueKind.NativeInt:
                    return new StackValue(kind, typeSystemContext.GetWellKnownType(WellKnownType.IntPtr));
                case StackValueKind.Float:
                    return new StackValue(kind, typeSystemContext.GetWellKnownType(WellKnownType.Double));
                default:
                    Debug.Assert(false);
                    return new StackValue(kind);
            }
        }

        static private StackValue CreatePrimitive(StackValueKind kind, TypeDesc type)
        {
            return new StackValue(kind, type);
        }

        static public StackValue CreateObjRef(TypeDesc type)
        {
            return new StackValue(StackValueKind.ObjRef, type);
        }

        static public StackValue CreateValueType(TypeDesc type)
        {
            return new StackValue(StackValueKind.ValueType, type);
        }

        static public StackValue CreateByRef(TypeDesc type, bool readOnly = false)
        {
            return new StackValue(StackValueKind.ByRef, type, readOnly ? StackValueFlags.ReadOnly : StackValueFlags.None);
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
                    return CreatePrimitive(StackValueKind.Int32, type);
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return CreatePrimitive(StackValueKind.Int64, type);
                case TypeFlags.Single:
                case TypeFlags.Double:
                    return CreatePrimitive(StackValueKind.Float, type);
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Pointer:
                    return CreatePrimitive(StackValueKind.NativeInt, type);
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
        /// <summary>
        /// Returns the reduced type as defined in the ECMA-335 standard (I.8.7).
        /// </summary>
        static TypeDesc GetReducedType(TypeDesc type)
        {
            var category = type.UnderlyingType.Category;

            switch (type.Category)
            {
                case TypeFlags.Byte: return type.Context.GetWellKnownType(WellKnownType.SByte);
                case TypeFlags.UInt16: return type.Context.GetWellKnownType(WellKnownType.Int16);
                case TypeFlags.UInt32: return type.Context.GetWellKnownType(WellKnownType.Int32);
                case TypeFlags.UInt64: return type.Context.GetWellKnownType(WellKnownType.Int64);
                case TypeFlags.UIntPtr: return type.Context.GetWellKnownType(WellKnownType.IntPtr);

                default:
                    return type; //Reduced type is type itself
            }
        }

        /// <summary>
        /// Returns the "verification type" based on the definition in the ECMA-335 standard (I.8.7).
        /// </summary>
        static TypeDesc GetVerificationType(TypeDesc type)
        {
            if (type.IsByRef)
            {
                var parameterVerificationType = GetVerificationType(type.GetParameterType());
                return type.Context.GetByRefType(parameterVerificationType);
            }
            else
            {
                var reducedType = GetReducedType(type);
                switch (reducedType.Category)
                {
                    case TypeFlags.Boolean:
                        return type.Context.GetWellKnownType(WellKnownType.SByte);

                    case TypeFlags.Char:
                        return type.Context.GetWellKnownType(WellKnownType.Int16);

                    default:
                        return reducedType; // Verifcation type is reduced type
                }
            }
        }

        /// <summary>
        /// Returns the "intermediate type" based on the definition in the ECMA-335 standard (I.8.7).
        /// </summary>
        static public TypeDesc GetIntermediateType(TypeDesc type)
        {
            var verificationType = GetVerificationType(type);

            switch (verificationType.Category)
            {
                case TypeFlags.SByte:
                case TypeFlags.Int16:
                case TypeFlags.Int32:
                    return type.Context.GetWellKnownType(WellKnownType.Int32);
                case TypeFlags.Single:
                case TypeFlags.Double:
                    return type.Context.GetWellKnownType(WellKnownType.Double);
                default:
                    return verificationType;
            }
        }

        static bool IsSameReducedType(TypeDesc src, TypeDesc dst)
        {
            return GetReducedType(src) == GetReducedType(dst);
        }

        static bool IsSameVerificationType(TypeDesc src, TypeDesc dst)
        {
            return GetVerificationType(src) == GetVerificationType(dst);
        }

        /// <summary>
        /// Returns whether the given source type is compatible with the given destination type
        /// based on the definition of "compatible-with" in the ECMA-335 standard (I.8.7.1).
        /// </summary>
        bool IsCompatibleWith(TypeDesc src, TypeDesc dst, bool allowSizeEquivalence = false)
        {
            if (src == dst)
                return true;

            if (!src.IsValueType && src.CanCastTo(dst))
                return true;

            if (src.IsValueType || dst.IsValueType)
            {
                if (allowSizeEquivalence && IsSameVerificationType(src, dst))
                    return true;

                // TODO IsEquivalent
                return false;
            }

            return false;
        }

        /// <summary>
        /// Returns whether the given source type is array-element-compatible with the given destination type
        /// based on the definition of "array-element-compatible-with" in the ECMA-335 standard(I.8.7.1).
        /// </summary>
        bool IsArrayElementCompatibleWith(TypeDesc src, TypeDesc dst)
        {
            src = src.UnderlyingType;
            dst = dst.UnderlyingType;

            if (IsCompatibleWith(src, dst, true))
                return true;

            return IsSameReducedType(src, dst);
        }

        /// <summary>
        /// Returns whether the given source type is assignable to the given destination type based on
        /// the definition of "assignable-to" in the ECMA-355 standard (I.8.7.3).
        /// </summary>
        bool IsAssignable(TypeDesc src, TypeDesc dst)
        {
            if (src == dst)
                return true;

            // Check intermediate types
            var srcIntermediate = GetIntermediateType(src);
            var dstIntermediate = GetIntermediateType(dst);
            if (srcIntermediate == dstIntermediate)
                return true;

            if (srcIntermediate.Category == TypeFlags.IntPtr && dstIntermediate.Category == TypeFlags.Int32 ||
                srcIntermediate.Category == TypeFlags.Int32 && dstIntermediate.Category == TypeFlags.IntPtr)
                return true;

            // Transitivity check already performed by IsCompatibleWith
            return IsCompatibleWith(src, dst);
        }

        /// <summary>
        /// Returns whether the given source type is assignable to the given destination type based
        /// on the definition of "verifier-assignable-to" in the ECMA-355 standard (I.8.1.2.3).
        /// </summary>
        bool IsVerifierAssignable(TypeDesc src, TypeDesc dst)
        {
            // null is always assignable to reference types
            if (!dst.IsValueType && src == null)
                return true;

            var srcVerType = GetVerificationType(src);
            var dstVerType = GetVerificationType(dst);

            if (IsAssignable(srcVerType, dstVerType))
                return true;

            // TODO: Handle other cases: controlled-mutability pointer types, boxed types
            return false;
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
                            // ECMA-335 III.1.5 Operand type table, P. 303:
                            // __cgt.un__ is allowed and verifiable on ObjectRefs (O). This is commonly used when 
                            // comparing an ObjectRef with null(there is no "compare - not - equal" instruction, which 
                            // would otherwise be a more obvious solution)
                            return op == ILOpcode.beq || op == ILOpcode.beq_s ||
                                   op == ILOpcode.bne_un || op == ILOpcode.bne_un_s ||
                                   op == ILOpcode.ceq || op == ILOpcode.cgt_un;
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
