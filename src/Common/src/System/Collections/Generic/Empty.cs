// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

namespace System.Collections.Generic
{
    //
    // Helper class to store reusable empty IEnumerables.
    //
    internal static class Empty<T>
    {
        //
        // Returns a reusable empty IEnumerable<T> (that does not secretly implement more advanced collection interfaces.)
        //
        public static IEnumerable<T> Enumerable
        {
            get
            {
                return _enumerable;
            }
        }

        private sealed class EmptyEnumImpl : IEnumerable<T>, IEnumerator<T>
        {
            public IEnumerator<T> GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            public T Current
            {
                get { throw new InvalidOperationException(); }
            }

            Object IEnumerator.Current
            {
                get { throw new InvalidOperationException(); }
            }

            public bool MoveNext()
            {
                return false;
            }

            public void Reset()
            {
            }

            public void Dispose()
            {
            }
        }

        private static IEnumerable<T> _enumerable = new EmptyEnumImpl();
    }
}

