// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Interlocked = System.Threading.Interlocked;

namespace Internal.TypeSystem
{
    struct ThreadSafeFlags
    {
        private volatile int _value;

        public int Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFlags(int value)
        {
            return (_value & value) == value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFlags(int flagsToAdd)
        {
            var originalFlags = _value;
            while (Interlocked.CompareExchange(ref _value, originalFlags | flagsToAdd, originalFlags) != originalFlags)
            {
                originalFlags = _value;
            }
        }
    }
}
