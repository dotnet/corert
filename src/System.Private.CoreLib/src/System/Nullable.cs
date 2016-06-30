// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.CompilerServices;
using Internal.Reflection.Core.NonPortable;

namespace System
{
    public struct Nullable<T> where T : struct
    {
        // Changing the name of this field will break MDbg and Debugger tests
        private bool hasValue;
        internal T value;

        public Nullable(T value)
        {
            this.value = value;
            this.hasValue = true;
        }

        public bool HasValue
        {
            get { return hasValue; }
        }

        public T Value
        {
            get
            {
                if (!HasValue)
                    throw new InvalidOperationException(SR.InvalidOperation_NoValue);
                return value;
            }
        }

        public T GetValueOrDefault()
        {
            return value;
        }

        public T GetValueOrDefault(T defaultValue)
        {
            return HasValue ? value : defaultValue;
        }

        public override bool Equals(object other)
        {
            if (!HasValue) return other == null;
            if (other == null) return false;
            return value.Equals(other);
        }

        public override int GetHashCode()
        {
            return HasValue ? value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return HasValue ? value.ToString() : "";
        }

        public static implicit operator Nullable<T>(T value)
        {
            return new Nullable<T>(value);
        }

        public static explicit operator T(Nullable<T> value)
        {
            return value.Value;
        }
    }

    public static class Nullable
    {
        public static int Compare<T>(Nullable<T> n1, Nullable<T> n2) where T : struct
        {
            if (n1.HasValue)
            {
                if (n2.HasValue) return LowLevelComparer<T>.Default.Compare(n1.value, n2.value);
                return 1;
            }
            if (n2.HasValue) return -1;
            return 0;
        }

        public static bool Equals<T>(Nullable<T> n1, Nullable<T> n2) where T : struct
        {
            if (n1.HasValue)
            {
#if CORERT
                if (n2.HasValue) return EqualOnlyComparer<T>.Equals(n1.value, n2.value);
#else
                // See comment above Array.GetComparerForReferenceTypesOnly for details
                if (n2.HasValue) return LowLevelEqualityComparer<T>.Default.Equals(n1.value, n2.value);
#endif
                return false;
            }
            if (n2.HasValue) return false;
            return true;
        }

        public static Type GetUnderlyingType(Type nullableType)
        {
            if ((object)nullableType == null)
            {
                throw new ArgumentNullException("nullableType");
            }
            Contract.EndContractBlock();

            Type result = null;

            EETypePtr nullableEEType;
            if (nullableType.TryGetEEType(out nullableEEType))
            {
                if (nullableEEType.IsGeneric)
                {
                    if (nullableEEType.IsNullable)
                    {
                        EETypePtr underlyingEEType = nullableEEType.NullableType;
                        result = ReflectionCoreNonPortable.GetRuntimeTypeForEEType(underlyingEEType);
                    }
                }
            }
            else
            {
                // If we got here, the type was not statically bound in the image. However, it may still be a browsable metadata-based type.
                // Fall back to using Reflection for these.
                if (nullableType.IsConstructedGenericType && nullableType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    result = nullableType.GenericTypeArguments[0];
            }
            return result;
        }
    }
}
