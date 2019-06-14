// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Pointer Type to a EEType in the runtime.
**
** 
===========================================================*/

using System.Runtime;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using EEType = Internal.Runtime.EEType;
using EETypeRef = Internal.Runtime.EETypeRef;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EETypePtr : IEquatable<EETypePtr>
    {
        private EEType* _value;

        public EETypePtr(IntPtr value)
        {
            _value = (EEType*)value;
        }

        internal EETypePtr(EEType* value)
        {
            _value = value;
        }

        internal EEType* ToPointer()
        {
            return _value;
        }

        public override bool Equals(object obj)
        {
            if (obj is EETypePtr)
            {
                return this == (EETypePtr)obj;
            }
            return false;
        }

        public bool Equals(EETypePtr p)
        {
            return this == p;
        }

        public static bool operator ==(EETypePtr value1, EETypePtr value2)
        {
            if (value1.IsNull)
                return value2.IsNull;
            else if (value2.IsNull)
                return false;
            else
                return RuntimeImports.AreTypesEquivalent(value1, value2);
        }

        public static bool operator !=(EETypePtr value1, EETypePtr value2)
        {
            return !(value1 == value2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return (int)_value->HashCode;
        }

        // 
        // Faster version of Equals for use on EETypes that are known not to be null and where the "match" case is the hot path.
        //
        public bool FastEquals(EETypePtr other)
        {
            Debug.Assert(!this.IsNull);
            Debug.Assert(!other.IsNull);

            // Fast check for raw equality before making call to helper.
            if (this.RawValue == other.RawValue)
                return true;
            return RuntimeImports.AreTypesEquivalent(this, other);
        }

        //
        // An even faster version of FastEquals that only checks if two EEType pointers are identical.
        // Note: this method might return false for cases where FastEquals would return true.
        // Only use if you know what you're doing.
        //
        internal bool FastEqualsUnreliable(EETypePtr other)
        {
            Debug.Assert(!this.IsNull);
            Debug.Assert(!other.IsNull);

            return this.RawValue == other.RawValue;
        }

        // Caution: You cannot safely compare RawValue's as RH does NOT unify EETypes. Use the == or Equals() methods exposed by EETypePtr itself.
        internal IntPtr RawValue
        {
            get
            {
                return (IntPtr)_value;
            }
        }

        internal bool IsNull
        {
            get
            {
                return _value == null;
            }
        }

        internal bool IsArray
        {
            get
            {
                return _value->IsArray;
            }
        }

        internal bool IsSzArray
        {
            get
            {
                return _value->IsSzArray;
            }
        }

        internal bool IsPointer
        {
            get
            {
                return _value->IsPointerType;
            }
        }

        internal bool IsByRef
        {
            get
            {
                return _value->IsByRefType;
            }
        }

        internal bool IsValueType
        {
            get
            {
                return _value->IsValueType;
            }
        }

        internal bool IsString
        {
            get
            {
                return _value->IsString;
            }
        }

        /// <summary>
        /// Warning! UNLIKE the similarly named Reflection api, this method also returns "true" for Enums.
        /// </summary>
        internal bool IsPrimitive
        {
            get
            {
                RuntimeImports.RhCorElementType et = CorElementType;
                return ((et >= RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN) && (et <= RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8)) ||
                    (et == RuntimeImports.RhCorElementType.ELEMENT_TYPE_I) ||
                    (et == RuntimeImports.RhCorElementType.ELEMENT_TYPE_U);
            }
        }

        /// <summary>
        /// WARNING: Never call unless the EEType came from an instanced object. Nested enums can be open generics (typeof(Outer<>).NestedEnum) 
        /// and this helper has undefined behavior when passed such as a enum.
        /// </summary>
        internal bool IsEnum
        {
            get
            {
                // Q: When is an enum type a constructed generic type?
                // A: When it's nested inside a generic type.
                if (!(IsDefType))
                    return false;

                // Generic type definitions that return true for IsPrimitive are type definitions of generic enums.
                // Otherwise check the base type.
                return
                    // RHBind doesn't emit CorElementType on generic type definitions, so this only works for
                    // open generics outside ProjectN. When we fix this, also remove the N-specific fallback for getting
                    // the underlying type of open enums in RuntimeAugments.
#if !PROJECTN
                    (IsGenericTypeDefinition && IsPrimitive) ||
#endif
                    this.BaseType == EETypePtr.EETypePtrOf<Enum>();
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a generic type definition (an uninstantiated generic type).
        /// </summary>
        internal bool IsGenericTypeDefinition
        {
            get
            {
                return _value->IsGenericTypeDefinition;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is an instantiated generic type.
        /// </summary>
        internal bool IsGeneric
        {
            get
            {
                return _value->IsGeneric;
            }
        }

        internal GenericArgumentCollection Instantiation
        {
            get
            {
                return new GenericArgumentCollection(_value->GenericArity, _value->GenericArguments);
            }
        }

        internal EETypePtr GenericDefinition
        {
            get
            {
                return new EETypePtr(_value->GenericDefinition);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a class, a struct, an enum, or an interface.
        /// </summary>
        internal bool IsDefType
        {
            get
            {
                return !_value->IsParameterizedType;
            }
        }

        internal bool IsDynamicType
        {
            get
            {
                return _value->IsDynamicType;
            }
        }

        internal bool IsInterface
        {
            get
            {
                return _value->IsInterface;
            }
        }

        internal bool IsAbstract
        {
            get
            {
                return _value->IsAbstract;
            }
        }

        internal bool IsByRefLike
        {
            get
            {
                return _value->IsByRefLike;
            }
        }

        internal bool IsNullable
        {
            get
            {
                return _value->IsNullable;
            }
        }

        internal bool HasCctor
        {
            get
            {
                return _value->HasCctor;
            }
        }

        internal EETypePtr NullableType
        {
            get
            {
                return new EETypePtr(_value->NullableType);
            }
        }

        internal EETypePtr ArrayElementType
        {
            get
            {
                return new EETypePtr(_value->RelatedParameterType);
            }
        }

        internal int ArrayRank
        {
            get
            {
                return _value->ArrayRank;
            }
        }

        internal InterfaceCollection Interfaces
        {
            get
            {
                return new InterfaceCollection(_value);
            }
        }

        internal EETypePtr BaseType
        {
            get
            {
                if (IsArray)
                    return EETypePtr.EETypePtrOf<Array>();

                if (IsPointer || IsByRef)
                    return new EETypePtr(default(IntPtr));

                EETypePtr baseEEType = new EETypePtr(_value->NonArrayBaseType);
                return baseEEType;
            }
        }

        internal ushort ComponentSize
        {
            get
            {
                return _value->ComponentSize;
            }
        }

        internal uint BaseSize
        {
            get
            {
                return _value->BaseSize;
            }
        }

        // Has internal gc pointers. 
        internal bool HasPointers
        {
            get
            {
                return _value->HasGCPointers;
            }
        }

        internal uint ValueTypeSize
        {
            get
            {
                return _value->ValueTypeSize;
            }
        }

        internal RuntimeImports.RhCorElementType CorElementType
        {
            get
            {
                Debug.Assert((int)Internal.Runtime.CorElementType.ELEMENT_TYPE_I1 == (int)RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1);
                Debug.Assert((int)Internal.Runtime.CorElementType.ELEMENT_TYPE_R8 == (int)RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8);
                return (RuntimeImports.RhCorElementType)_value->CorElementType;
            }
        }

        internal RuntimeImports.RhCorElementTypeInfo CorElementTypeInfo
        {
            get
            {
                RuntimeImports.RhCorElementType corElementType = this.CorElementType;
                return RuntimeImports.GetRhCorElementTypeInfo(corElementType);
            }
        }

        [Intrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static EETypePtr EETypePtrOf<T>()
        {
            // Compilers are required to provide a low level implementation of this method.
            // This can be achieved by optimizing away the reflection part of this implementation
            // by optimizing typeof(!!0).TypeHandle into "ldtoken !!0", or by
            // completely replacing the body of this method.
            return typeof(T).TypeHandle.ToEETypePtr();
        }

        public struct InterfaceCollection
        {
            private EEType* _value;

            internal InterfaceCollection(EEType* value)
            {
                _value = value;
            }

            public int Count
            {
                get
                {
                    return _value->NumInterfaces;
                }
            }

            public EETypePtr this[int index]
            {
                get
                {
                    Debug.Assert((uint)index < _value->NumInterfaces);

                    return new EETypePtr(_value->InterfaceMap[index].InterfaceType);
                }
            }
        }

        public struct GenericArgumentCollection
        {
            private EETypeRef* _arguments;
            private uint _argumentCount;

            internal GenericArgumentCollection(uint argumentCount, EETypeRef* arguments)
            {
                _argumentCount = argumentCount;
                _arguments = arguments;
            }

            public int Length
            {
                get
                {
                    return (int)_argumentCount;
                }
            }

            public EETypePtr this[int index]
            {
                get
                {
                    Debug.Assert((uint)index < _argumentCount);
                    return new EETypePtr(_arguments[index].Value);
                }
            }
        }
    }
}
