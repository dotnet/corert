// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// These helper methods are known to a NUTC intrinsic used to implement EqualityComparer<T> class. We don't use them directly
// from the framework and they have nothing to do with Reflection.
//
// These methods need to be housed in a framework assembly that's part of SharedLibrary. System.Private.Reflection.Execution is part of
// the SharedLibrary so it got picked to be the host. 
//

// The general issue here is that EqualityComparer<T>.get_Default is not written in a manner which fully supports IEquatable
// and Nullable types. Due to point in time restrictions it is not possible to change that code. So, the compiler will instead
// replace the IL code within get_Default to call one of GetUnknownEquatableComparer, GetKnownGenericEquatableComparer,
// GetKnownNullableEquatableComparer, GetKnownEnumEquatableComparer or GetKnownObjectEquatableComparer based on what sort of
// type is being compared.
//
// In addition, there are a set of generic functions which are used by Array.IndexOf<T> to perform equality checking
// in a similar manner. Array.IndexOf<T> uses these functions instead of the EqualityComparer<T> infrastructure because constructing
// a full EqualityComparer<T> has substantial size costs due to Array.IndexOf<T> use within all arrays.

using System;
using System.Collections.Generic;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;

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
                bool success = TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeComponents(interfaceType,
                                                                                                    out genericDefinition,
                                                                                                    out genericTypeArgs);

                if (success)
                {
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

            bool success = TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeForComponents(openComparerType, new RuntimeTypeHandle[] { comparerTypeArgument }, out comparerType);
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
        private static bool EnumOnlyEquals<T>(T x, T y) where T : struct
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


        //-----------------------------------------------------------------------
        // Implementations of EqualityComparer<T> for the various possible scenarios
        //-----------------------------------------------------------------------

        // The methods in this class look identical to the inherited methods, but the calls
        // to Equal bind to IEquatable<T>.Equals(T) instead of Object.Equals(Object)
        private sealed class GenericEqualityComparer<T> : EqualityComparer<T> where T : IEquatable<T>
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
        }

        private sealed class NullableEqualityComparer<T> : EqualityComparer<Nullable<T>> where T : struct, IEquatable<T>
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
        }

        private sealed class EnumEqualityComparer<T> : EqualityComparer<T> where T : struct
        {
            public sealed override bool Equals(T x, T y)
            {
                return EqualityComparerHelpers.EnumOnlyEquals(x, y);
            }

            public sealed override int GetHashCode(T obj)
            {
                return obj.GetHashCode();
            }
        }

        private sealed class ObjectEqualityComparer<T> : EqualityComparer<T>
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
        }
    }
}
