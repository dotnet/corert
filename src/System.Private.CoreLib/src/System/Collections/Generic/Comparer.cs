// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

namespace System.Collections.Generic
{
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public abstract class Comparer<T> : IComparer, IComparer<T>
    {
        protected Comparer()
        {
        }

        // .NET Native for UWP toolchain overwrites the Default property with optimized 
        // instantiation-specific implementation.

        // TODO: Initialize the _default field via implicit static constructor for better performance
        // (https://github.com/dotnet/coreclr/pull/4340).

        public static Comparer<T> Default
        {
            get
            {
                if (_default == null)
                    _default = new DefaultComparer<T>();
                return _default;
            }
        }

        public abstract int Compare(T x, T y);

        public static Comparer<T> Create(Comparison<T> comparison)
        {
            if (comparison == null)
                throw new ArgumentNullException(nameof(comparison));

            return new ComparisonComparer<T>(comparison);
        }

        int System.Collections.IComparer.Compare(object x, object y)
        {
            if (x == null) return y == null ? 0 : -1;
            if (y == null) return 1;
            if (x is T && y is T) return Compare((T)x, (T)y);
            throw new ArgumentException(SR.Argument_InvalidArgumentForComparison);
        }

        // WARNING: We allow diagnostic tools to directly inspect this member (_default). 
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        private static Comparer<T> _default;
    }

    internal class DefaultComparer<T> : Comparer<T>
    {
        public override int Compare(T x, T y)
        {
            // Desktop compat note: If either x or y are null, this api must not invoke either IComparable.Compare or IComparable<T>.Compare on either
            // x or y.
            if (x == null)
            {
                if (y == null)
                    return 0;
                else
                    return -1;
            }
            if (y == null)
                return 1;

            IComparable<T> igcx = x as IComparable<T>;
            if (igcx != null)
                return igcx.CompareTo(y);
            IComparable<T> igcy = y as IComparable<T>;
            if (igcy != null)
                return -igcy.CompareTo(x);

            return CompareUsingIComparable(x, y);
        }

        private int CompareUsingIComparable(object a, object b)
        {
            if (a == b) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            IComparable ia = a as IComparable;
            if (ia != null)
                return ia.CompareTo(b);

            IComparable ib = b as IComparable;
            if (ib != null)
                return -ib.CompareTo(a);

            throw new ArgumentException(SR.Argument_ImplementIComparable);
        }
    }

    internal class ComparisonComparer<T> : Comparer<T>
    {
        private readonly Comparison<T> _comparison;

        public ComparisonComparer(Comparison<T> comparison)
        {
            _comparison = comparison;
        }

        public override int Compare(T x, T y)
        {
            return _comparison(x, y);
        }
    }
}
