// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// These helper methods are known to a NUTC intrinsic used to implement the Comparer<T> class.

// the compiler will instead replace the IL code within get_Default to call one of GetUnknownComparer, GetKnownGenericComparer,
// GetKnownNullableComparer, GetKnownEnumComparer or GetKnownObjectComparer based on what sort of
// type is being compared.

using System;
using System.Collections.Generic;
using System.Runtime;

using Internal.IntrinsicSupport;
using Internal.Runtime.Augments;

namespace Internal.IntrinsicSupport
{
    internal static class ComparerHelpers
    {
        private static bool ImplementsIComparable(RuntimeTypeHandle t)
        {
            EETypePtr objectType = t.ToEETypePtr();
            EETypePtr icomparableType = typeof(IComparable<>).TypeHandle.ToEETypePtr();
            int interfaceCount = objectType.Interfaces.Count;
            for (int i = 0; i < interfaceCount; i++)
            {
                EETypePtr interfaceType = objectType.Interfaces[i];

                if (!interfaceType.IsGeneric)
                    continue;

                if (interfaceType.GenericDefinition == icomparableType)
                {
                    var instantiation = interfaceType.Instantiation;
                    if (instantiation.Length != 1)
                        continue;

                    if (objectType.IsValueType)
                    {
                        if (instantiation[0] == objectType)
                        {
                            return true;
                        }
                    }
                    else if (RuntimeImports.AreTypesAssignable(objectType, instantiation[0]))
                    {
                        return true;
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

            bool success = RuntimeAugments.TypeLoaderCallbacks.TryGetConstructedGenericTypeForComponents(openComparerType, new RuntimeTypeHandle[] { comparerTypeArgument }, out comparerType);
            if (!success)
            {
                Environment.FailFast("Unable to create comparer");
            }

            return RuntimeAugments.NewObject(comparerType);
        }

        internal static Comparer<T> GetUnknownComparer<T>()
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
        internal static int CompareObjects(object x, object y)
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
    }
}

namespace System.Collections.Generic
{ 
    //-----------------------------------------------------------------------
    // Implementations of EqualityComparer<T> for the various possible scenarios
    // Because these are serializable, they must not be renamed
    //-----------------------------------------------------------------------
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class GenericComparer<T> : Comparer<T> where T : IComparable<T>
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

        // Equals method for the comparer itself. 
        public sealed override bool Equals(object obj) => obj != null && GetType() == obj.GetType();

        public sealed override int GetHashCode() => GetType().GetHashCode();
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class NullableComparer<T> : Comparer<Nullable<T>> where T : struct, IComparable<T>
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

        // Equals method for the comparer itself. 
        public sealed override bool Equals(object obj) => obj != null && GetType() == obj.GetType();

        public sealed override int GetHashCode() => GetType().GetHashCode();
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class ObjectComparer<T> : Comparer<T>
    {
        public sealed override int Compare(T x, T y)
        {
            return ComparerHelpers.CompareObjects(x, y);
        }

        // Equals method for the comparer itself. 
        public sealed override bool Equals(object obj) => obj != null && GetType() == obj.GetType();

        public sealed override int GetHashCode() => GetType().GetHashCode();
    }
}

