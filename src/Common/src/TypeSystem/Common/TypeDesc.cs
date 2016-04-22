// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public struct Instantiation
    {
        private TypeDesc[] _genericParameters;

        public Instantiation(params TypeDesc[] genericParameters)
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

        /// <summary>
        /// Combines the given generic definition's hash code with the hashes
        /// of the generic parameters in this instantiation
        /// </summary>
        public int ComputeGenericInstanceHashCode(int genericDefinitionHashCode)
        {
            return Internal.NativeFormat.TypeHashingAlgorithms.ComputeGenericInstanceHashCode(genericDefinitionHashCode, _genericParameters);
        }

        public static readonly Instantiation Empty = new Instantiation(TypeDesc.EmptyTypes);

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_genericParameters);
        }

        /// <summary>
        /// Enumerator for iterating over the types in an instantiation
        /// </summary>
        public struct Enumerator
        {
            private TypeDesc[] _collection;
            private int _currentIndex;

            public Enumerator(TypeDesc[] collection)
            {
                _collection = collection;
                _currentIndex = -1;
            }

            public TypeDesc Current
            {
                get
                {
                    return _collection[_currentIndex];
                }
            }

            public bool MoveNext()
            {
                _currentIndex++;
                if (_currentIndex >= _collection.Length)
                {
                    return false;
                }
                return true;
            }
        }
    }

    public abstract partial class TypeDesc
    {
        public static readonly TypeDesc[] EmptyTypes = new TypeDesc[0];

        /// Inherited types are required to override, and should use the algorithms
        /// in TypeHashingAlgorithms in their implementation.
        public abstract override int GetHashCode();

        public override bool Equals(Object o)
        {
            // Its only valid to compare two TypeDescs in the same context
            Debug.Assert(o == null || !(o is TypeDesc) || Object.ReferenceEquals(((TypeDesc)o).Context, this.Context));
            return Object.ReferenceEquals(this, o);
        }

#if DEBUG
        public static bool operator ==(TypeDesc left, TypeDesc right)
        {
            // Its only valid to compare two TypeDescs in the same context
            Debug.Assert(Object.ReferenceEquals(left, null) || Object.ReferenceEquals(right, null) || Object.ReferenceEquals(left.Context, right.Context));
            return Object.ReferenceEquals(left, right);
        }

        public static bool operator !=(TypeDesc left, TypeDesc right)
        {
            // Its only valid to compare two TypeDescs in the same context
            Debug.Assert(Object.ReferenceEquals(left, null) || Object.ReferenceEquals(right, null) || Object.ReferenceEquals(left.Context, right.Context));
            return !Object.ReferenceEquals(left, right);
        }
#endif

        // The most frequently used type properties are cached here to avoid excesive virtual calls
        private TypeFlags _typeFlags;

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
        private TypeFlags InitializeTypeFlags(TypeFlags mask)
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
                return this.Context.IsWellKnownType(GetTypeDefinition(), WellKnownType.Nullable);
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

        public bool IsGenericParameter
        {
            get
            {
                return Variety == TypeKind.GenericParameter;
            }
        }

        public bool IsDefType
        {
            get
            {
                return Variety == TypeKind.DefType;
            }
        }

        public bool ContainsGenericVariables
        {
            get
            {
                return (GetTypeFlags(TypeFlags.ContainsGenericVariables | TypeFlags.ContainsGenericVariablesComputed) & TypeFlags.ContainsGenericVariables) != 0;
            }
        }

        public virtual DefType BaseType
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

        public virtual bool HasStaticConstructor
        {
            get
            {
                return false;
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

        public virtual MethodDesc GetStaticConstructor()
        {
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

        /// <summary>
        /// Determine if two types share the same type definition
        /// </summary>
        public bool HasSameTypeDefinition(TypeDesc otherType)
        {
            return GetTypeDefinition() == otherType.GetTypeDefinition();
        }

        public virtual bool HasFinalizer
        {
            get
            {
                return false;
            }
        }

        public virtual MethodDesc GetFinalizer()
        {
            return null;
        }

        /// <summary>
        /// Gets a value indicating whether this type has generic variance (the definition of the type
        /// has a generic parameter that is co- or contravariant).
        /// </summary>
        public bool HasVariance
        {
            get
            {
                return (GetTypeFlags(TypeFlags.HasGenericVariance | TypeFlags.HasGenericVarianceComputed) & TypeFlags.HasGenericVariance) != 0;
            }
        }

        /// <summary>
        /// Gets the kind of this type.
        /// </summary>
        public abstract TypeKind Variety
        {
            get;
        }
    }
}
