// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public struct Instantiation
    {
        TypeDesc[] _genericParameters;

        public Instantiation(TypeDesc[] genericParameters)
        {
            _genericParameters = genericParameters;
        }

        [System.Runtime.CompilerServices.IndexerName("GenericParameters")]
        public TypeDesc this[int index]
        {
            get
            {
                return _genericParameters[index];
            }
        }

        public int Length
        {
            get
            {
                return _genericParameters.Length;
            }
        }

        public bool IsNull
        {
            get
            {
                return _genericParameters == null;
            }
        }

        public IEnumerable<TypeDesc> GetEnumerator()
        {
            return _genericParameters;
        }

        public static readonly Instantiation Empty = new Instantiation(TypeDesc.EmptyTypes);
    }

    public abstract partial class TypeDesc
    {
        public static readonly TypeDesc[] EmptyTypes = new TypeDesc[0];

        public override int GetHashCode()
        {
            // Inherited types are expected to override
            return RuntimeHelpers.GetHashCode(this);
        }

        public override bool Equals(Object o)
        {
            return Object.ReferenceEquals(this, o);
        }

        // The most frequently used type properties are cached here to avoid excesive virtual calls
        TypeFlags _typeFlags;

        public abstract TypeSystemContext Context
        {
            get;
        }

        public virtual Instantiation Instantiation
        {
            get
            {
                return Instantiation.Empty;
            }
        }

        public bool HasInstantiation
        {
            get
            {
                return this.Instantiation.Length != 0;
            }
        }

        public void SetWellKnownType(WellKnownType wellKnownType)
        {
            TypeFlags flags;

            switch (wellKnownType)
            {
                case WellKnownType.Void:
                case WellKnownType.Boolean:
                case WellKnownType.Char:
                case WellKnownType.SByte:
                case WellKnownType.Byte:
                case WellKnownType.Int16:
                case WellKnownType.UInt16:
                case WellKnownType.Int32:
                case WellKnownType.UInt32:
                case WellKnownType.Int64:
                case WellKnownType.UInt64:
                case WellKnownType.IntPtr:
                case WellKnownType.UIntPtr:
                case WellKnownType.Single:
                case WellKnownType.Double:
                    flags = (TypeFlags)wellKnownType;
                    break;

                case WellKnownType.ValueType:
                case WellKnownType.Enum:
                    flags = TypeFlags.Class;
                    break;

                case WellKnownType.Nullable:
                    flags = TypeFlags.Nullable;
                    break;

                case WellKnownType.Object:
                case WellKnownType.String:
                case WellKnownType.Array:
                case WellKnownType.MulticastDelegate:
                case WellKnownType.Exception:
                    flags = TypeFlags.Class;
                    break;

                case WellKnownType.RuntimeTypeHandle:
                case WellKnownType.RuntimeMethodHandle:
                case WellKnownType.RuntimeFieldHandle:
                    flags = TypeFlags.ValueType;
                    break;

                default:
                    throw new ArgumentException();
            }

            _typeFlags = flags;
        }

        protected abstract TypeFlags ComputeTypeFlags(TypeFlags mask);

        [MethodImpl(MethodImplOptions.NoInlining)]
        TypeFlags InitializeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = ComputeTypeFlags(mask);

            Debug.Assert((flags & mask) != 0);
            _typeFlags |= flags;

            return flags & mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected TypeFlags GetTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = _typeFlags & mask;
            if (flags != 0)
                return flags;
            return InitializeTypeFlags(mask);
        }

        public TypeFlags Category
        {
            get
            {
                return GetTypeFlags(TypeFlags.CategoryMask);
            }
        }

        public bool IsInterface
        {
            get
            {
                return GetTypeFlags(TypeFlags.CategoryMask) == TypeFlags.Interface;
            }
        }

        public bool IsValueType
        {
            get
            {
                return GetTypeFlags(TypeFlags.CategoryMask) < TypeFlags.Class;
            }
        }

        public bool IsPrimitive
        {
            get
            {
                return GetTypeFlags(TypeFlags.CategoryMask) < TypeFlags.ValueType;
            }
        }

        public bool IsEnum
        {
            get
            {
                return GetTypeFlags(TypeFlags.CategoryMask) == TypeFlags.Enum;
            }
        }

        public bool IsDelegate
        {
            get
            {
                return this.Context.IsWellKnownType(this.BaseType, WellKnownType.MulticastDelegate);
            }
        }

        public bool IsVoid
        {
            get
            {
                return GetTypeFlags(TypeFlags.CategoryMask) == TypeFlags.Void;
            }
        }

        public bool IsString
        {
            get
            {
                return this.Context.IsWellKnownType(this, WellKnownType.String);
            }
        }

        public bool IsObject
        {
            get
            {
                return this.Context.IsWellKnownType(this, WellKnownType.Object);
            }
        }

        public bool IsNullable
        {
            get
            {
                return this.Context.IsWellKnownType(this, WellKnownType.Nullable);
            }
        }

        public bool IsArray
        {
            get
            {
                return this.GetType() == typeof(ArrayType);
            }
        }

        public bool IsSzArray
        {
            get
            {
                return this.IsArray && ((ArrayType)this).IsSzArray;
            }
        }

        public bool IsByRef
        {
            get
            {
                return this.GetType() == typeof(ByRefType);
            }
        }

        public bool IsPointer
        {
            get
            {
                return this.GetType() == typeof(PointerType);
            }
        }

        public bool ContainsGenericVariables
        {
            get
            {
                return (GetTypeFlags(TypeFlags.ContainsGenericVariables | TypeFlags.ContainsGenericVariablesComputed) & TypeFlags.ContainsGenericVariables) != 0;
            }
        }

        public virtual MetadataType BaseType
        {
            get
            {
                return null;
            }
        }

        public bool HasBaseType
        {
            get
            {
                return BaseType != null;
            }
        }

        public virtual TypeDesc[] ImplementedInterfaces
        {
            get
            {
                return TypeDesc.EmptyTypes;
            }
        }

        public virtual TypeDesc UnderlyingType // For enums
        {
            get
            {
                if (!this.IsEnum)
                    return this;

                // TODO: Cache the result?
                foreach (var field in this.GetFields())
                {
                    if (!field.IsStatic)
                        return field.FieldType;
                }

                throw new BadImageFormatException();
            }
        }

        public virtual string Name
        {
            get
            {
                return null;
            }
        }

        public virtual IEnumerable<MethodDesc> GetMethods()
        {
            return MethodDesc.EmptyMethods;
        }

        // TODO: Substitutions, generics, modopts, ...
        public virtual MethodDesc GetMethod(string name, MethodSignature signature)
        {
            foreach (var method in GetMethods())
            {
                if (method.Name == name)
                {
                    if (signature == null || signature.Equals(method.Signature))
                        return method;
                }
            }
            return null;
        }

        public virtual IEnumerable<FieldDesc> GetFields()
        {
            return FieldDesc.EmptyFields;
        }

        // TODO: Substitutions, generics, modopts, ...
        public virtual FieldDesc GetField(string name)
        {
            foreach (var field in GetFields())
            {
                if (field.Name == name)
                    return field;
            }
            return null;
        }

        public virtual TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            return this;
        }

        // Strips instantiation. E.g C<int> -> C<T>
        public virtual TypeDesc GetTypeDefinition()
        {
            return this;
        }

        public bool IsTypeDefinition
        {
            get
            {
                return GetTypeDefinition() == this;
            }
        }
    }
}
