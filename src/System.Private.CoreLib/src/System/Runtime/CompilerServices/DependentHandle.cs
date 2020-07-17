// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Internal.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    //=========================================================================================
    // This struct collects all operations on native DependentHandles. The DependentHandle
    // merely wraps an IntPtr so this struct serves mainly as a "managed typedef."
    //
    // DependentHandles exist in one of two states:
    //
    //    IsAllocated == false
    //        No actual handle is allocated underneath. Illegal to call GetPrimary
    //        or GetPrimaryAndSecondary(). Ok to call Free().
    //
    //        Initializing a DependentHandle using the nullary ctor creates a DependentHandle
    //        that's in the !IsAllocated state.
    //        (! Right now, we get this guarantee for free because (IntPtr)0 == NULL unmanaged handle.
    //         ! If that assertion ever becomes false, we'll have to add an _isAllocated field
    //         ! to compensate.)
    //        
    //
    //    IsAllocated == true
    //        There's a handle allocated underneath. You must call Free() on this eventually
    //        or you cause a native handle table leak.
    //
    // This struct intentionally does no self-synchronization. It's up to the caller to
    // to use DependentHandles in a thread-safe way.
    //=========================================================================================
    internal struct DependentHandle
    {
        private IntPtr _handle;

        public DependentHandle(object primary, object secondary) =>
            _handle = RuntimeImports.RhHandleAllocDependent(primary, secondary);

        public bool IsAllocated => _handle != IntPtr.Zero;

        // Getting the secondary object is more expensive than getting the first so
        // we provide a separate primary-only accessor for those times we only want the
        // primary.
        public object GetPrimary() =>
            RuntimeImports.RhHandleGet(_handle);

        public object GetPrimaryAndSecondary(out object secondary) =>
            RuntimeImports.RhHandleGetDependent(_handle, out secondary);

        public void SetPrimary(object primary) =>
            RuntimeImports.RhHandleSet(_handle, primary);

        public void SetSecondary(object secondary) =>
            RuntimeImports.RhHandleSetDependentSecondary(_handle, secondary);

        // Forces dependentHandle back to non-allocated state (if not already there)
        // and frees the handle if needed.
        public void Free()
        {
            if (_handle != IntPtr.Zero)
            {
                IntPtr handle = _handle;
                _handle = IntPtr.Zero;
                RuntimeImports.RhHandleFree(handle);
            }
        }
    }
}
