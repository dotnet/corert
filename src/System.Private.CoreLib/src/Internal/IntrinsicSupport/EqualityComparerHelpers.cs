// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// These helper methods are known to a NUTC intrinsic used to implement EqualityComparer<T> class.

// The compiler will instead replace the IL code within get_Default to call one of GetUnknownEquatableComparer, GetKnownGenericEquatableComparer,
// GetKnownNullableEquatableComparer, GetKnownEnumEquatableComparer or GetKnownObjectEquatableComparer based on what sort of
// type is being compared.
//
// In addition, there are a set of generic functions which are used by Array.IndexOf<T> to perform equality checking
// in a similar manner. Array.IndexOf<T> uses these functions instead of the EqualityComparer<T> infrastructure because constructing
// a full EqualityComparer<T> has substantial size costs due to Array.IndexOf<T> use within all arrays.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Internal.IntrinsicSupport;
using Internal.Runtime.Augments;

namespace Internal.IntrinsicSupport
{
    internal static class EqualityComparerHelpers
    {
        private static bool ImplementsIEquatable(RuntimeTypeHandle t)
        {
            EETypePtr objectType = t.ToEETypePtr();
            EETypePtr iequatableType = typeof(IEquatable<>).TypeHandle.ToEETypePtr();
            int interfaceCount = objectType.Interfaces.Count;
            for (int i = 0; i < interfaceCount; i++)
            {
                EETypePtr interfaceType = objectType.Interfaces[i];

                if (!interfaceType.IsGeneric)
                    continue;

                if (interfaceType.GenericDefinition == iequatableType)
                {
                    var instantiation = interfaceType.Instantiation;

                    if (instantiation.Length != 1)
                        continue;

                    if (instantiation[0] == objectType)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsEnum(RuntimeTypeHandle t)
        {
            return t.ToEETypePtr().IsEnum;
        }

        // this function utilizes the template type loader to generate new
        // EqualityComparer types on the fly
        private static object GetComparer(RuntimeTypeHandle t)
        {
            RuntimeTypeHandle comparerType;
            RuntimeTypeHandle openComparerType = default(RuntimeTypeHandle);
            RuntimeTypeHandle comparerTypeArgument = default(RuntimeTypeHandle);

            if (RuntimeAugments.IsNullable(t))
            {
                RuntimeTypeHandle nullableType = RuntimeAugments.GetNullableType(t);
                if (ImplementsIEquatable(nullableType))
                {
                    openComparerType = typeof(NullableEqualityComparer<>).TypeHandle;
                    comparerTypeArgument = nullableType;
                }
            }
            if (IsEnum(t))
            {
                openComparerType = typeof(EnumEqualityComparer<>).TypeHandle;
                comparerTypeArgument = t;
            }

            if (openComparerType.Equals(default(RuntimeTypeHandle)))
            {
                if (ImplementsIEquatable(t))
                {
                    openComparerType = typeof(GenericEqualityComparer<>).TypeHandle;
                    comparerTypeArgument = t;
                }
                else
                {
                    openComparerType = typeof(ObjectEqualityComparer<>).TypeHandle;
                    comparerTypeArgument = t;
                }
            }

            bool success = RuntimeAugments.TypeLoaderCallbacks.TryGetConstructedGenericTypeForComponents(openComparerType, new RuntimeTypeHandle[] { comparerTypeArgument }, out comparerType);
            if (!success)
            {
                Environment.FailFast("Unable to create comparer");
            }

            return RuntimeAugments.NewObject(comparerType);
        }

        //----------------------------------------------------------------------
        // target functions of intrinsic replacement in EqualityComparer.get_Default
        //----------------------------------------------------------------------
        internal static EqualityComparer<T> GetUnknownEquatableComparer<T>()
        {
            return (EqualityComparer<T>)GetComparer(typeof(T).TypeHandle);
        }

        private static EqualityComparer<T> GetKnownGenericEquatableComparer<T>() where T : IEquatable<T>
        {
            return new GenericEqualityComparer<T>();
        }

        private static EqualityComparer<Nullable<U>> GetKnownNullableEquatableComparer<U>() where U : struct, IEquatable<U>
        {
            return new NullableEqualityComparer<U>();
        }

        private static EqualityComparer<T> GetKnownObjectEquatableComparer<T>()
        {
            return new ObjectEqualityComparer<T>();
        }

        private static EqualityComparer<T> GetKnownEnumEquatableComparer<T>() where T : struct
        {
            return new EnumEqualityComparer<T>();
        }

        //-----------------------------------------------------------------------
        // Redirection target functions for redirecting behavior of Array.IndexOf
        //-----------------------------------------------------------------------

        // This one is an intrinsic that is used to make enum comparisions more efficient.
        [Intrinsic]
        internal static bool EnumOnlyEquals<T>(T x, T y) where T : struct
        {
            return x.Equals(y);
        }

        private static bool StructOnlyEqualsIEquatable<T>(T x, T y) where T : IEquatable<T>
        {
            return x.Equals(y);
        }

        private static bool StructOnlyEqualsNullable<T>(Nullable<T> x, Nullable<T> y) where T : struct, IEquatable<T>
        {
            if (x.HasValue)
            {
                if (y.HasValue)
                    return x.Value.Equals(y.Value);
                return false;
            }

            if (y.HasValue)
                return false;

            return true;
        }

        // These functions look odd, as they are part of a complex series of compiler intrinsics
        // designed to produce very high quality code for equality comparison cases without utilizing
        // reflection like other platforms. The major complication is that the specification of
        // IndexOf is that it is supposed to use IEquatable<T> if possible, but that requirement
        // cannot be expressed in IL directly due to the lack of constraints.
        // Instead, specialization at call time is used within the compiler. 
        // 
        // General Approach
        // - Perform fancy redirection for EqualityComparerHelpers.GetComparerForReferenceTypesOnly<T>(). If T is a reference 
        //   type or UniversalCanon, have this redirect to EqualityComparer<T>.get_Default, Otherwise, use 
        //   the function as is. (will return null in that case)
        // - Change the contents of the IndexOf functions to have a pair of loops. One for if 
        //   GetComparerForReferenceTypesOnly returns null, and one for when it does not. 
        //   - If it does not return null, call the EqualityComparer<T> code.
        //   - If it does return null, use a special function StructOnlyEquals<T>(). 
        //     - Calls to that function result in calls to a pair of helper function in 
        //       EqualityComparerHelpers (StructOnlyEqualsIEquatable, or StructOnlyEqualsNullable) 
        //       depending on whether or not they are the right function to call.
        // - The end result is that in optimized builds, we have the same single function compiled size 
        //   characteristics that the old EqualsOnlyComparer<T>.Equals function had, but we maintain 
        //   correctness as well.
        [Intrinsic]
        internal static EqualityComparer<T> GetComparerForReferenceTypesOnly<T>()
        {
#if PROJECTN
            // When T is a reference type or a universal canon type, then this will redirect to EqualityComparer<T>.Default.
            return null;
#else
            return EqualityComparer<T>.Default;
#endif
        }

        private static bool StructOnlyNormalEquals<T>(T left, T right)
        {
            return left.Equals(right);
        }

        [Intrinsic]
        internal static bool StructOnlyEquals<T>(T left, T right)
        {
            return EqualityComparer<T>.Default.Equals(left, right);
        }
    }
}

namespace System.Collections.Generic
{
    //-----------------------------------------------------------------------
    // Implementations of EqualityComparer<T> for the various possible scenarios
    // Names must match other runtimes for serialization
    //-----------------------------------------------------------------------

    // The methods in this class look identical to the inherited methods, but the calls
    // to Equal bind to IEquatable<T>.Equals(T) instead of Object.Equals(Object)
    [Serializable]
    public sealed class GenericEqualityComparer<T> : EqualityComparer<T> where T : IEquatable<T>
    {
        public sealed override bool Equals(T x, T y)
        {
            if (x != null)
            {
                if (y != null)
                    return x.Equals(y);
                return false;
            }

            if (y != null)
                return false;

            return true;
        }

        public sealed override int GetHashCode(T obj)
        {
            if (obj == null)
                return 0;

            return obj.GetHashCode();
        }

        // Equals method for the comparer itself.
        public sealed override bool Equals(object obj) => obj is GenericEqualityComparer<T>;
        
        public sealed override int GetHashCode() => typeof(GenericEqualityComparer<T>).GetHashCode();
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class NullableEqualityComparer<T> : EqualityComparer<Nullable<T>> where T : struct, IEquatable<T>
    {
        public sealed override bool Equals(Nullable<T> x, Nullable<T> y)
        {
            if (x.HasValue)
            {
                if (y.HasValue)
                    return x.Value.Equals(y.Value);
                return false;
            }

            if (y.HasValue)
                return false;

            return true;
        }

        public sealed override int GetHashCode(Nullable<T> obj)
        {
            return obj.GetHashCode();
        }


        // Equals method for the comparer itself.
        public sealed override bool Equals(object obj) => obj is NullableEqualityComparer<T>;

        public sealed override int GetHashCode() => typeof(NullableEqualityComparer<T>).GetHashCode();
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public class EnumEqualityComparer<T> : EqualityComparer<T>, ISerializable where T : struct
    {
        public sealed override bool Equals(T x, T y)
        {
            return EqualityComparerHelpers.EnumOnlyEquals(x, y);
        }

        public sealed override int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }

        internal EnumEqualityComparer() { }

        private EnumEqualityComparer(SerializationInfo info, StreamingContext context) { }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // For back-compat we need to serialize the comparers for enums with underlying types other than int as ObjectEqualityComparer 
            if (Type.GetTypeCode(Enum.GetUnderlyingType(typeof(T))) != TypeCode.Int32)
            {
                info.SetType(typeof(ObjectEqualityComparer<T>));
            }
        }

        // Equals method for the comparer itself.
        public override bool Equals(object obj) => obj is EnumEqualityComparer<T>;

        public override int GetHashCode() => typeof(EnumEqualityComparer<T>).GetHashCode();
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class ObjectEqualityComparer<T> : EqualityComparer<T>
    {
        public sealed override bool Equals(T x, T y)
        {
            if (x != null)
            {
                if (y != null)
                    return x.Equals(y);
                return false;
            }

            if (y != null)
                return false;

            return true;
        }

        public sealed override int GetHashCode(T obj)
        {
            if (obj == null)
                return 0;
            return obj.GetHashCode();
        }

        // Equals method for the comparer itself.
        public sealed override bool Equals(object obj)
        {
            if(obj == null)
            {
                return false;
            }

            // This needs to use GetType instead of typeof to avoid infinite recursion in the type loader
            return obj.GetType().Equals(GetType());
        }

        // This needs to use GetType instead of typeof to avoid infinite recursion in the type loader
        public sealed override int GetHashCode() =>  GetType().GetHashCode();
    }
}
