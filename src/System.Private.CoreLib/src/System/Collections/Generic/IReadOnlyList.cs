// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    // Provides a read-only, covariant view of a generic list.

#if CONTRACTS_FULL
    [ContractClass(typeof(IReadOnlyListContract<>))]
#endif
    public interface IReadOnlyList<out T> : IReadOnlyCollection<T>
    {
        T this[int index] { get; }
    }
#if CONTRACTS_FULL
    [ContractClassFor(typeof(IReadOnlyList<>))]
    internal abstract class IReadOnlyListContract<T> : IReadOnlyList<T>
    {
        T IReadOnlyList<T>.this[int index]
        {
            get
            {
                //Contract.Requires(index >= 0);
                //Contract.Requires(index < ((ICollection<T>)this).Count);
                return default(T);
            }
        }

        int IReadOnlyCollection<T>.Count
        {
            get
            {
                return default(int);
            }
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return default(IEnumerator<T>);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return default(IEnumerator);
        }
    }
#endif
}
