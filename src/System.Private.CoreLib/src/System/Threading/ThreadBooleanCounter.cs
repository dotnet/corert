// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    internal sealed class ThreadBooleanCounter
    {
        private readonly ThreadLocal<bool> _threadLocalFlag = new ThreadLocal<bool>(trackAllValues: true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set()
        {
            Debug.Assert(!_threadLocalFlag.Value);

            try
            {
                _threadLocalFlag.Value = true;
            }
            catch (OutOfMemoryException)
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Debug.Assert(!_threadLocalFlag.IsValueCreated || _threadLocalFlag.Value);
            _threadLocalFlag.Value = false;
        }

        public int Count
        {
            get
            {
                int count = 0;
                try
                {
                    foreach (bool isSet in _threadLocalFlag.ValuesAsEnumerable)
                    {
                        if (isSet)
                        {
                            ++count;
                            Debug.Assert(count > 0);
                        }
                    }
                    return count;
                }
                catch (OutOfMemoryException)
                {
                    // Some allocation occurs above and it may be a bit awkward to get an OOM from this property getter
                    return count;
                }
            }
        }
    }
}
