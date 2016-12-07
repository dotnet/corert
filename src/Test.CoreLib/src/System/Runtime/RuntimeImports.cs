// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace System.Runtime
{
    // CONTRACT with Runtime
    // This class lists all the static methods that the redhawk runtime exports to a class library
    // These are not expected to change much but are needed by the class library to implement its functionality
    //
    //      The contents of this file can be modified if needed by the class library
    //      E.g., the class and methods are marked internal assuming that only the base class library needs them
    //            but if a class library wants to factor differently (such as putting the GCHandle methods in an
    //            optional library, those methods can be moved to a different file/namespace/dll

    public static class RuntimeImports
    {
        private const string RuntimeLibrary = "[MRT]";

        //
        // calls for GCHandle.
        // These methods are needed to implement GCHandle class like functionality (optional)
        //

        // Allocate handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpHandleAlloc")]
        private static extern IntPtr RhpHandleAlloc(Object value, GCHandleType type);

        internal static IntPtr RhHandleAlloc(Object value, GCHandleType type)
        {
            IntPtr h = RhpHandleAlloc(value, type);
            if (h == IntPtr.Zero)
                throw new OutOfMemoryException();
            return h;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpRegisterFrozenSegment")]
        internal static extern bool RhpRegisterFrozenSegment(IntPtr pSegmentStart, int length);

        //
        // calls to runtime for allocation
        // These calls are needed in types which cannot use "new" to allocate and need to do it manually
        //
        // calls to runtime for allocation
        //
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhNewObject")]
        internal static extern object RhNewObject(EETypePtr pEEType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhNewArray")]
        internal static extern Array RhNewArray(EETypePtr pEEType, int length);

        // @todo: Should we just have a proper export for this?
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhNewArray")]
        internal static extern String RhNewArrayAsString(EETypePtr pEEType, int length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        [RuntimeImport(RuntimeLibrary, "RhpFallbackFailFast")]
        internal extern static unsafe void RhpFallbackFailFast();

        //
        // Interlocked helpers
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpLockCmpXchg32")]
        internal extern static int InterlockedCompareExchange(ref int location1, int value, int comparand);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpMemoryBarrier")]
        internal extern static void MemoryBarrier();
    }
}
