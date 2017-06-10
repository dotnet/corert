// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections;

namespace System.Collections.Generic
{
    [Serializable]
    public abstract class EqualityComparer<T> : IEqualityComparer, IEqualityComparer<T>
    {
        protected EqualityComparer()
        {
        }

        // .NET Native for UWP toolchain overwrites the Default property with optimized 
        // instantiation-specific implementation.

        // TODO: Change the _default field to non-volatile and initialize it via implicit static 
        // constructor for better performance (https://github.com/dotnet/coreclr/pull/4340).

        public static EqualityComparer<T> Default
        {
            get
            {
                if (_default == null)
                    _default = new DefaultEqualityComparer<T>();
                return _default;
            }
        }

        // WARNING: We allow diagnostic tools to directly inspect this member (_default). 
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        private static volatile EqualityComparer<T> _default;

        public abstract bool Equals(T x, T y);

        public abstract int GetHashCode(T obj);

        int IEqualityComparer.GetHashCode(object obj)
        {
            if (obj == null)
                return 0;
            if (obj is T)
                return GetHashCode((T)obj);
            throw new ArgumentException(SR.Argument_InvalidArgumentForComparison, nameof(obj));
        }

        bool IEqualityComparer.Equals(object x, object y)
        {
            if (x == y)
                return true;
            if (x == null || y == null)
                return false;
            if ((x is T) && (y is T))
                return Equals((T)x, (T)y);
            throw new ArgumentException(SR.Argument_InvalidArgumentForComparison);
        }
    }

    internal sealed class DefaultEqualityComparer<T> : EqualityComparer<T>
    {
        public DefaultEqualityComparer()
        {
        }

        public sealed override bool Equals(T x, T y)
        {
            if (x == null)
                return y == null;
            if (y == null)
                return false;

            return x.Equals(y);
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
            if (obj == null)
            {
                return false;
            }

            // This needs to use GetType instead of typeof to avoid infinite recursion in the type loader
            return obj.GetType().Equals(GetType());
        }


        // This needs to use GetType instead of typeof to avoid infinite recursion in the type loader
        public sealed override int GetHashCode() => GetType().GetHashCode();
    }
}
