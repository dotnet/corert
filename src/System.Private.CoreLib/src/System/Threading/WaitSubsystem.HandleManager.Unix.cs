// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    internal static partial class WaitSubsystem
    {
        [EagerStaticClassConstruction] // used during lazy class construction
        private static class HandleManager
        {
            private const int InitialCapacity = 4;

            /// <summary>
            /// Prevents reusing a deleted index until at least there are this many deleted indexes in the free list. The
            /// purpose is to allow free handles to remain free for some time so that we can discover invalid attempts at using
            /// free handles.
            /// </summary>
            private const int MinimumFreeIndexCountBeforeReuse = 64;

            private static WaitableObject[] s_objects = new WaitableObject[InitialCapacity];
            private static int s_count;
            private static LowLevelQueue<int> s_freeIndexes;

            public static IntPtr NewHandle(WaitableObject waitableObject)
            {
                s_lock.VerifyIsLocked();
                Debug.Assert(waitableObject != null);

                int index = s_count;
                if (index < s_objects.Length)
                {
                    s_count = index + 1;
                }
                else if (s_freeIndexes != null && s_freeIndexes.Count >= MinimumFreeIndexCountBeforeReuse)
                {
                    index = s_freeIndexes.Dequeue();
                }
                else
                {
                    GrowCapacity();
                    s_count = index + 1;
                }

                Debug.Assert(index < s_count);
                Debug.Assert(s_objects[index] == null);

                s_objects[index] = waitableObject;

                /// <see cref="Microsoft.Win32.SafeHandles.SafeWaitHandle"/> uses 0 or -1 to indicate an invalid handle, so skip
                /// index 0 (handle value == index + 1)
                return new IntPtr(index + 1);
            }

            private static void GrowCapacity()
            {
                s_lock.VerifyIsLocked();

                Debug.Assert(s_count == s_objects.Length);
                Debug.Assert(s_freeIndexes == null || s_freeIndexes.Count < MinimumFreeIndexCountBeforeReuse);

                int oldCapacity = s_objects.Length;
                int newCapacity = s_objects.Length << 1;
                if (newCapacity <= oldCapacity)
                {
                    throw new OutOfMemoryException();
                }

                var newObjects = new WaitableObject[newCapacity];
                s_objects.CopyTo(newObjects, 0);
                s_objects = newObjects;
            }

            public static WaitableObject FromHandle(IntPtr handle)
            {
                s_lock.VerifyIsLocked();

                WaitableObject waitableObject = FromHandle_NoThrow(handle.ToInt32() - 1);
                if (waitableObject != null)
                {
                    return waitableObject;
                }
                throw InvalidOperationException.NewInvalidHandle();
            }

            private static WaitableObject FromHandle_NoThrow(int index)
            {
                s_lock.VerifyIsLocked();
                return (uint)index < (uint)s_count ? s_objects[index] : null;
            }

            public static void DeleteHandle(IntPtr handle)
            {
                s_lock.VerifyIsLocked();

                int index = handle.ToInt32() - 1;
                WaitableObject waitableObject = FromHandle_NoThrow(index);
                if (waitableObject == null)
                {
                    return;
                }

                waitableObject.OnDeleteHandle();
                s_objects[index] = waitableObject = null;

                try
                {
                    if (s_freeIndexes == null)
                    {
                        s_freeIndexes = new LowLevelQueue<int>();
                    }
                    s_freeIndexes.Enqueue(index);
                }
                catch (OutOfMemoryException)
                {
                    // Ignore, this index won't be reused anymore
                }
            }
        }
    }
}
