// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// These helper methods are known to a NUTC intrinsic used to implement the Comparer<T> class. We don't use them directly
// from the framework and they have nothing to do with Reflection.
//
// These methods need to be housed in a framework assembly that's part of SharedLibrary. System.Private.Reflection.Execution is part of
// the SharedLibrary so it got picked to be the host. 
//

// The general issue here is that Comparer<T>.get_Default is not written in a manner which fully supports IEquatable
// and Nullable types. Due to point in time restrictions it is not possible to change that code. So, the compiler will instead
// replace the IL code within get_Default to call one of GetUnknownComparer, GetKnownGenericComparer,
// GetKnownNullableComparer, GetKnownEnumComparer or GetKnownObjectComparer based on what sort of
// type is being compared.

using System;
using System.Collections;
using System.Collections.Generic;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;

namespace Internal.IntrinsicSupport
{
    internal static class ComparerHelpers
    {
        private static bool ImplementsIComparable(RuntimeTypeHandle t)
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
                    if (genericDefinition.Equals(typeof(IComparable<>).TypeHandle))
                    {
                        if (genericTypeArgs.Length != 1)
                            continue;

                        if (RuntimeAugments.IsValueType(t))
                        {
                            if (genericTypeArgs[0].Equals(t))
                            {
                                return true;
                            }
                        }
                        else if (RuntimeAugments.IsAssignableFrom(genericTypeArgs[0], t))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static object GetComparer(RuntimeTypeHandle t)
        {
            RuntimeTypeHandle comparerType;
            RuntimeTypeHandle openComparerType = default(RuntimeTypeHandle);
            RuntimeTypeHandle comparerTypeArgument = default(RuntimeTypeHandle);

            if (RuntimeAugments.IsNullable(t))
            {
                RuntimeTypeHandle nullableType = RuntimeAugments.GetNullableType(t);
                if (ImplementsIComparable(nullableType))
                {
                    openComparerType = typeof(NullableComparer<>).TypeHandle;
                    comparerTypeArgument = nullableType;
                }
            }

            if (openComparerType.Equals(default(RuntimeTypeHandle)))
            {
                if (ImplementsIComparable(t))
                {
                    openComparerType = typeof(GenericComparer<>).TypeHandle;
                    comparerTypeArgument = t;
                }
                else
                {
                    openComparerType = typeof(ObjectComparer<>).TypeHandle;
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

        private static Comparer<T> GetUnknownComparer<T>()
        {
            return (Comparer<T>)GetComparer(typeof(T).TypeHandle);
        }

        private static Comparer<T> GetKnownGenericComparer<T>() where T : IComparable<T>
        {
            return new GenericComparer<T>();
        }

        private static Comparer<Nullable<U>> GetKnownNullableComparer<U>() where U : struct, IComparable<U>
        {
            return new NullableComparer<U>();
        }

        private static Comparer<T> GetKnownObjectComparer<T>()
        {
            return new ObjectComparer<T>();
        }

        // This routine emulates System.Collection.Comparer.Default.Compare(), which lives in the System.Collections.NonGenerics contract.
        // To avoid adding a reference to that contract just for this hack, we'll replicate the implementation here.
        private static int CompareObjects(object x, object y)
        {
            if (x == y)
                return 0;

            if (x == null)
                return -1;

            if (y == null)
                return 1;

            {
                // System.Collection.Comparer.Default.Compare() compares strings using the CurrentCulture.
                string sx = x as string;
                string sy = y as string;
                if (sx != null && sy != null)
                    return string.Compare(sx, sy, StringComparison.CurrentCulture);
            }

            IComparable ix = x as IComparable;
            if (ix != null)
                return ix.CompareTo(y);

            IComparable iy = y as IComparable;
            if (iy != null)
                return -iy.CompareTo(x);

            throw new ArgumentException(SR.Argument_ImplementIComparable);
        }

        //-----------------------------------------------------------------------
        // Implementations of EqualityComparer<T> for the various possible scenarios
        //-----------------------------------------------------------------------

        private sealed class GenericComparer<T> : Comparer<T> where T : IComparable<T>
        {
            public sealed override int Compare(T x, T y)
            {
                if (x != null)
                {
                    if (y != null)
                        return x.CompareTo(y);

                    return 1;
                }

                if (y != null)
                    return -1;

                return 0;
            }
        }

        private sealed class NullableComparer<T> : Comparer<Nullable<T>> where T : struct, IComparable<T>
        {
            public sealed override int Compare(Nullable<T> x, Nullable<T> y)
            {
                if (x.HasValue)
                {
                    if (y.HasValue)
                        return x.Value.CompareTo(y.Value);

                    return 1;
                }

                if (y.HasValue)
                    return -1;

                return 0;
            }
        }

        private sealed class ObjectComparer<T> : Comparer<T>
        {
            public sealed override int Compare(T x, T y)
            {
                return ComparerHelpers.CompareObjects(x, y);
            }
        }
    }
}

