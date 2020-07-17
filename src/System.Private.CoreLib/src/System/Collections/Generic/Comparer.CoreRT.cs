// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Runtime.CompilerServices;
using System.Threading;

using Internal.IntrinsicSupport;
using Internal.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    public abstract partial class Comparer<T> : IComparer, IComparer<T>
    {
        private static Comparer<T> s_default;

        [Intrinsic]
        private static Comparer<T> Create()
        {
            // The compiler will overwrite the Create method with optimized
            // instantiation-specific implementation.
            // This body serves as a fallback when instantiation-specific implementation is unavailable.
            Interlocked.CompareExchange(ref s_default, Unsafe.As<Comparer<T>>(ComparerHelpers.GetComparer(typeof(T).TypeHandle)), null);
            return s_default;
        }

        public static Comparer<T> Default
        {
            get
            {
                // Lazy initialization produces smaller code for CoreRT than initialization in constructor
                return s_default ?? Create();
            }
        }
    }

    internal sealed partial class EnumComparer<T> : Comparer<T> where T : struct, Enum
    {
        public override int Compare(T x, T y)
        {
            // CORERT-TODO: EnumComparer<T>
            throw new NotImplementedException();
        }
    }
}
