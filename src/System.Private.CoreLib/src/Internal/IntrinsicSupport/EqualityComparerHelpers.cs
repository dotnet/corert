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
using System.Runtime.Serialization;
using Internal.IntrinsicSupport;
using Internal.Runtime.Augments;

namespace Internal.IntrinsicSupport
{
    internal static class EqualityComparerHelpers
    {
        private static bool ImplementsIEquatable(RuntimeTypeHandle t)
        {
            int interfaceCount = RuntimeAugments.GetInterfaceCount(t);
            for (int i = 0; i < interfaceCount; i++)
            {
                RuntimeTypeHandle interfaceType = RuntimeAugments.GetInterface(t, i);

                if (!RuntimeAugments.IsGenericType(interfaceType))
                    continue;

                RuntimeTypeHandle genericDefinition;
                RuntimeTypeHandle[] genericTypeArgs;
                genericDefinition = RuntimeAugments.GetGenericInstantiation(interfaceType,
                                                                            out genericTypeArgs);

                if (genericDefinition.Equals(typeof(IEquatable<>).TypeHandle))
                {
                    if (genericTypeArgs.Length != 1)
                        continue;

                    if (genericTypeArgs[0].Equals(t))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsEnum(RuntimeTypeHandle t)
        {
            RuntimeTypeHandle baseType;
            bool success = RuntimeAugments.TryGetBaseType(t, out baseType);
            if (!success)
                return false;

            return baseType.Equals(typeof(System.Enum).TypeHandle);
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
        private static EqualityComparer<T> GetUnknownEquatableComparer<T>()
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
    internal sealed class GenericEqualityComparer<T> : EqualityComparer<T> where T : IEquatable<T>
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
        public sealed override bool Equals(Object obj) => obj is GenericEqualityComparer<T>;
        
        public sealed override int GetHashCode() => typeof(GenericEqualityComparer<T>).GetHashCode();
    }

    [Serializable]
    internal sealed class NullableEqualityComparer<T> : EqualityComparer<Nullable<T>> where T : struct, IEquatable<T>
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
        public sealed override bool Equals(Object obj) => obj is NullableEqualityComparer<T>;

        public sealed override int GetHashCode() => typeof(NullableEqualityComparer<T>).GetHashCode();
    }

    [Serializable]
    public sealed class EnumEqualityComparer<T> : EqualityComparer<T>, ISerializable where T : struct
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
        public sealed override bool Equals(Object obj) => obj is EnumEqualityComparer<T>;

        public sealed override int GetHashCode() => typeof(EnumEqualityComparer<T>).GetHashCode();
    }

    [Serializable]
    internal sealed class ObjectEqualityComparer<T> : EqualityComparer<T>
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
        public sealed override bool Equals(Object obj)
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
