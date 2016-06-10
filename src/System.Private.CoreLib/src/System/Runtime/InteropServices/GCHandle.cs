// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    // This class allows you to create an opaque, GC handle to any 
    // COM+ object. A GC handle is used when an object reference must be
    // reachable from unmanaged memory.  There are 3 kinds of roots:
    // Normal - keeps the object from being collected.
    // Weak - allows object to be collected and handle contents will be zeroed.
    //          Weak references are zeroed before the finalizer runs, so if the
    //          object is resurrected in the finalizer the weak reference is
    //          still zeroed.
    // WeakTrackResurrection - Same as weak, but stays until after object is
    //          really gone.
    // Pinned - same as normal, but allows the address of the actual object
    //          to be taken.
    //

    [StructLayout(LayoutKind.Sequential)]
    public struct GCHandle
    {
        // IMPORTANT: This must be kept in sync with the GCHandleType enum.
        private const GCHandleType MaxHandleType = GCHandleType.Pinned;

        // Allocate a handle storing the object and the type.
        internal GCHandle(Object value, GCHandleType type)
        {
            // Make sure the type parameter is within the valid range for the enum.
            if ((uint)type > (uint)MaxHandleType)
            {
                throw new ArgumentOutOfRangeException(); // "type", SR.ArgumentOutOfRange_Enum;
            }

            if (type == GCHandleType.Pinned)
                GCHandleValidatePinnedObject(value);

            _handle = RuntimeImports.RhHandleAlloc(value, type);

            // Record if the handle is pinned.
            if (type == GCHandleType.Pinned)
                SetIsPinned();
        }

        // Used in the conversion functions below.
        internal GCHandle(IntPtr handle)
        {
            _handle = handle;
        }

        // Creates a new GC handle for an object.
        //
        // value - The object that the GC handle is created for.
        // type - The type of GC handle to create.
        // 
        // returns a new GC handle that protects the object.
        public static GCHandle Alloc(Object value)
        {
            return new GCHandle(value, GCHandleType.Normal);
        }

        public static GCHandle Alloc(Object value, GCHandleType type)
        {
            return new GCHandle(value, type);
        }

        // Frees a GC handle.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public void Free()
        {
            // Copy the handle instance member to a local variable. This is required to prevent
            // race conditions releasing the handle.
            IntPtr handle = _handle;

            // Free the handle if it hasn't already been freed.
            if (handle != default(IntPtr) && Interlocked.CompareExchange(ref _handle, default(IntPtr), handle) == handle)
            {
#if BIT64
                RuntimeImports.RhHandleFree((IntPtr)(((long)handle) & ~1L));
#else
                RuntimeImports.RhHandleFree((IntPtr)(((int)handle) & ~1));
#endif
            }
            else
            {
                throw new InvalidOperationException(); // SR.InvalidOperation_HandleIsNotInitialized);
            }
        }

        // Target property - allows getting / updating of the handle's referent.
        public Object Target
        {
            get
            {
                // Check if the handle was never initialized or was freed.
                if (_handle == default(IntPtr))
                {
                    throw new InvalidOperationException(); // SR.InvalidOperation_HandleIsNotInitialized);
                }

                return RuntimeImports.RhHandleGet(GetHandleValue());
            }

            set
            {
                // Check if the handle was never initialized or was freed.
                if (_handle == default(IntPtr))
                {
                    throw new InvalidOperationException(); // SR.InvalidOperation_HandleIsNotInitialized);
                }

#if CORERT // TODO: Higher level ProjectN frameworks took dependency on this validation missing
                if (IsPinned())
                    GCHandleValidatePinnedObject(value);
#endif

                RuntimeImports.RhHandleSet(GetHandleValue(), value);
            }
        }

        // Retrieve the address of an object in a Pinned handle.  This throws
        // an exception if the handle is any type other than Pinned.
        public IntPtr AddrOfPinnedObject()
        {
            // Check if the handle was not a pinned handle.
            if (!IsPinned())
            {
                // Check if the handle was never initialized for was freed.
                if (_handle == default(IntPtr))
                {
                    throw new InvalidOperationException(); // SR.InvalidOperation_HandleIsNotInitialized);
                }

                // You can only get the address of pinned handles.
                throw new InvalidOperationException(); // SR.InvalidOperation_HandleIsNotPinned);
            }

            unsafe
            {
                // Get the address of the pinned object.
                // The layout of String and Array is different from Object

                Object target = this.Target;

                if (target == null)
                    return default(IntPtr);

                String targetAsString = target as String;
                if (targetAsString != null)
                {
                    fixed (char* ptr = targetAsString)
                    {
                        return (IntPtr)ptr;
                    }
                }

                Array targetAsArray = target as Array;
                if (targetAsArray != null)
                {
                    fixed (IntPtr* pTargetEEType = &targetAsArray.m_pEEType)
                    {
                        return (IntPtr)Array.GetAddrOfPinnedArrayFromEETypeField(pTargetEEType);
                    }
                }
                else
                {
                    fixed (IntPtr* pTargetEEType = &target.m_pEEType)
                    {
                        return (IntPtr)Object.GetAddrOfPinnedObjectFromEETypeField(pTargetEEType);
                    }
                }
            }
        }

        // Determine whether this handle has been allocated or not.
        public bool IsAllocated
        {
            get
            {
                return _handle != default(IntPtr);
            }
        }

        // Used to create a GCHandle from an int.  This is intended to
        // be used with the reverse conversion.
        public static explicit operator GCHandle(IntPtr value)
        {
            return FromIntPtr(value);
        }

        public static GCHandle FromIntPtr(IntPtr value)
        {
            if (value == default(IntPtr))
            {
                throw new InvalidOperationException(); // SR.InvalidOperation_HandleIsNotInitialized);
            }

            return new GCHandle(value);
        }

        // Used to get the internal integer representation of the handle out.
        public static explicit operator IntPtr(GCHandle value)
        {
            return ToIntPtr(value);
        }

        public static IntPtr ToIntPtr(GCHandle value)
        {
            return value._handle;
        }

        public override int GetHashCode()
        {
            return _handle.GetHashCode();
        }

        public override bool Equals(Object o)
        {
            GCHandle hnd;

            // Check that o is a GCHandle first
            if (o == null || !(o is GCHandle))
                return false;
            else
                hnd = (GCHandle)o;

            return _handle == hnd._handle;
        }

        public static bool operator ==(GCHandle a, GCHandle b)
        {
            return a._handle == b._handle;
        }

        public static bool operator !=(GCHandle a, GCHandle b)
        {
            return a._handle != b._handle;
        }

        internal IntPtr GetHandleValue()
        {
#if BIT64
            return new IntPtr(((long)_handle) & ~1L);
#else
            return new IntPtr(((int)_handle) & ~1);
#endif
        }

        internal bool IsPinned()
        {
#if BIT64
            return (((long)_handle) & 1) != 0;
#else
            return (((int)_handle) & 1) != 0;
#endif
        }

        internal void SetIsPinned()
        {
#if BIT64
            _handle = new IntPtr(((long)_handle) | 1L);
#else
            _handle = new IntPtr(((int)_handle) | 1);
#endif
        }

        //
        // C# port of GCHandleValidatePinnedObject(OBJECTREF) in MarshalNative.cpp.
        //
        private static void GCHandleValidatePinnedObject(Object obj)
        {
            if (obj == null)
                return;
            if (obj is String)
                return;
            EETypePtr eeType = obj.EETypePtr;
            if (eeType.IsArray)
            {
                EETypePtr elementEEType = eeType.ArrayElementType;
                if (elementEEType.IsPrimitive)
                    return;
                if (elementEEType.IsValueType && elementEEType.MightBeBlittable())
                    return;
            }
            else if (eeType.MightBeBlittable())
            {
                return;
            }
            throw new ArgumentException(SR.Argument_NotIsomorphic);
        }

        // The actual integer handle value that the EE uses internally.
        private IntPtr _handle;
    }
}
