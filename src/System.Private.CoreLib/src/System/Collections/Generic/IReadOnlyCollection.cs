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
    [ContractClass(typeof(IReadOnlyCollectionContract<>))]
#endif
    public interface IReadOnlyCollection<out T> : IEnumerable<T>
    {
        int Count { get; }
    }
#if CONTRACTS_FULL
    [ContractClassFor(typeof(IReadOnlyCollection<>))]
    internal abstract class IReadOnlyCollectionContract<T> : IReadOnlyCollection<T>
    {
        int IReadOnlyCollection<T>.Count
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
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
