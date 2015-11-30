// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
**
** Purpose: Pointer Type to a EEType in the runtime.
**
** 
===========================================================*/

using System;
using System.Runtime;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct EETypePtr : IEquatable<EETypePtr>
    {
        private IntPtr _value;

        public EETypePtr(IntPtr value)
        {
            _value = value;
        }

        public override bool Equals(Object obj)
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
            return (int)Runtime.RuntimeImports.RhGetEETypeHash(this);
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

        // Caution: You cannot safely compare RawValue's as RH does NOT unify EETypes. Use the == or Equals() methods exposed by EETypePtr itself.
        internal IntPtr RawValue
        {
            get
            {
                return _value;
            }
        }

        internal bool IsNull
        {
            get
            {
                return _value == default(IntPtr);
            }
        }

        internal bool IsArray
        {
            get
            {
                return RuntimeImports.RhIsArray(this);
            }
        }

        internal bool IsPointer
        {
            get
            {
                RuntimeImports.RhEETypeClassification classification = RuntimeImports.RhGetEETypeClassification(_value);
                return classification == RuntimeImports.RhEETypeClassification.UnmanagedPointer;
            }
        }

        internal bool IsValueType
        {
            get
            {
                return RuntimeImports.RhIsValueType(this);
            }
        }

        internal bool IsString
        {
            get
            {
                return RuntimeImports.RhIsString(this);
            }
        }

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

        internal bool IsEnum
        {
            get
            {
                RuntimeImports.RhEETypeClassification classification = RuntimeImports.RhGetEETypeClassification(this);

                // Q: When is an enum type a constructed generic type?
                // A: When it's nested inside a generic type.
                if (!(classification == RuntimeImports.RhEETypeClassification.Regular || classification == RuntimeImports.RhEETypeClassification.Generic))
                    return false;
                EETypePtr baseType = this.BaseType;
                return baseType == typeof(Enum).TypeHandle.ToEETypePtr();
            }
        }

        internal EETypePtr ArrayElementType
        {
            get
            {
                return RuntimeImports.RhGetRelatedParameterType(this);
            }
        }

        internal EETypePtr BaseType
        {
            get
            {
                if (IsArray)
                    return typeof(Array).TypeHandle.ToEETypePtr();

                if (IsPointer)
                    return new EETypePtr(default(IntPtr));

                EETypePtr baseEEType = RuntimeImports.RhGetNonArrayBaseType(this);
                if (baseEEType == typeof(MDArrayRank2).TypeHandle.ToEETypePtr() ||
                    baseEEType == typeof(MDArrayRank3).TypeHandle.ToEETypePtr() ||
                    baseEEType == typeof(MDArrayRank4).TypeHandle.ToEETypePtr())
                {
                    return typeof(Array).TypeHandle.ToEETypePtr();
                }

                return baseEEType;
            }
        }

        internal ushort ComponentSize
        {
            get
            {
                return RuntimeImports.RhGetComponentSize(this);
            }
        }

        // Has internal gc pointers. 
        internal bool HasPointers
        {
            get
            {
                return RuntimeImports.RhHasReferenceFields(this);
            }
        }

        internal uint ValueTypeSize
        {
            get
            {
                Debug.Assert(IsValueType, "ValueTypeSize should only be used on value types");
                return RuntimeImports.RhGetValueTypeSize(this);
            }
        }

        internal RuntimeImports.RhCorElementType CorElementType
        {
            get
            {
                return RuntimeImports.RhGetCorElementType(this);
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
    }
}
