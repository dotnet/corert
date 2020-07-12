// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Internal.TypeSystem;

namespace Internal.Runtime.JitSupport
{
    public static class FrozenStrings
    {
        private class FrozenStringHashTable : LockFreeReaderHashtableOfPointers<string, GCHandle>
        {
            /// <summary>
            /// Given a key, compute a hash code. This function must be thread safe.
            /// </summary>
            protected override int GetKeyHashCode(string key)
            {
                return key.GetHashCode();
            }

            /// <summary>
            /// Given a value, compute a hash code which would be identical to the hash code
            /// for a key which should look up this value. This function must be thread safe.
            /// </summary>
            protected override int GetValueHashCode(GCHandle value)
            {
                return value.Target.GetHashCode();
            }

            /// <summary>
            /// Compare a key and value. If the key refers to this value, return true.
            /// This function must be thread safe.
            /// </summary>
            protected override bool CompareKeyToValue(string key, GCHandle value)
            {
                return key.Equals((string)value.Target);
            }

            /// <summary>
            /// Compare a value with another value. Return true if values are equal.
            /// This function must be thread safe.
            /// </summary>
            protected override bool CompareValueToValue(GCHandle value1, GCHandle value2)
            {
                return value1.Target.Equals(value2.Target);
            }

            /// <summary>
            /// Create a new value from a key. Must be threadsafe. Value may or may not be added
            /// to collection. Return value must not be null.
            /// </summary>
            protected override GCHandle CreateValueFromKey(string key)
            {
                return GCHandle.Alloc(key, GCHandleType.Pinned);
            }

            /// <summary>
            /// Convert a value to an IntPtr for storage into the hashtable
            /// </summary>
            protected override IntPtr ConvertValueToIntPtr(GCHandle value)
            {
                return GCHandle.ToIntPtr(value);
            }

            /// <summary>
            /// Convert an IntPtr into a value for comparisions, or for returning.
            /// </summary>
            protected override GCHandle ConvertIntPtrToValue(IntPtr pointer)
            {
                return GCHandle.FromIntPtr(pointer);
            }
        }

        private static FrozenStringHashTable s_stringHash = new FrozenStringHashTable();

        public static IntPtr GetRawPointer(string str)
        {
            GCHandle gcHandle = s_stringHash.GetOrCreateValue(str);

            // Manual reading out of pointer to target object from pinned GCHandle
            IntPtr gcHandleAsIntPtr = GCHandle.ToIntPtr(gcHandle) - 1;
            unsafe
            {
                return *(IntPtr*)gcHandleAsIntPtr.ToPointer();
            }
        }
    }
}
