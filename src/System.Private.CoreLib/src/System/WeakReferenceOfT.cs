// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
** Purpose: A wrapper for establishing a WeakReference to a generic type.
**
===========================================================*/

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Diagnostics;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

namespace System
{
    // This class is sealed to mitigate security issues caused by Object::MemberwiseClone.
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class WeakReference<T> : ISerializable where T : class
    {
        // If you fix bugs here, please fix them in WeakReference at the same time.

        internal volatile IntPtr m_handle;
        private bool m_trackResurrection;

        // Creates a new WeakReference that keeps track of target.
        // Assumes a Short Weak Reference (ie TrackResurrection is false.)
        //
        public WeakReference(T target)
            : this(target, false)
        {
        }

        //Creates a new WeakReference that keeps track of target.
        //
        public WeakReference(T target, bool trackResurrection)
        {
            m_handle = (IntPtr)GCHandle.Alloc(target, trackResurrection ? GCHandleType.WeakTrackResurrection : GCHandleType.Weak);
            m_trackResurrection = trackResurrection;

            if (target != null)
            {
                // Set the conditional weak table if the target is a __ComObject.
                TrySetComTarget(target);
            }
        }

        internal WeakReference(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            T target = (T)info.GetValue("TrackedObject", typeof(T)); // Do not rename (binary serialization)
            bool trackResurrection = info.GetBoolean("TrackResurrection"); // Do not rename (binary serialization)

            m_handle = (IntPtr)GCHandle.Alloc(target, trackResurrection ? GCHandleType.WeakTrackResurrection : GCHandleType.Weak);

            if (target != null)
            {
                // Set the conditional weak table if the target is a __ComObject.
                TrySetComTarget(target);
            }
        }

        //
        // We are exposing TryGetTarget instead of a simple getter to avoid a common problem where people write incorrect code like:
        //
        //      WeakReference ref = ...;
        //      if (ref.Target != null)
        //          DoSomething(ref.Target)
        //
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetTarget(out T target)
        {
            // Call the worker method that has more performant but less user friendly signature.
            T o = GetTarget();
            target = o;
            return o != null;
        }

        public void SetTarget(T target)
        {
            if (m_handle == default(IntPtr))
                throw new InvalidOperationException(SR.InvalidOperation_HandleIsNotInitialized);

            // Update the conditionalweakTable in case the target is __ComObject.
            TrySetComTarget(target);

            RuntimeImports.RhHandleSet(m_handle, target);
            GC.KeepAlive(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T GetTarget()
        {
            IntPtr h = m_handle;

            // Should only happen for corner cases, like using a
            // WeakReference from a finalizer.
            if (default(IntPtr) == h)
                return null;

            T target = Unsafe.As<T>(RuntimeImports.RhHandleGet(h));

            if (target == null)
            {
                target = TryGetComTarget() as T;
            }

            // We want to ensure that the handle was still alive when we fetched the target,
            // so we double check m_handle here. Note that the reading of the handle
            // value has to be volatile for this to work, but reading of m_handle does not.

            if (default(IntPtr) == m_handle)
                return null;

            return target;
        }

        /// <summary>
        /// This method checks whether the target to the weakreference is a native COMObject in which case the native object might still be alive although the RuntimeHandle could be null.
        /// Hence we check in the conditionalweaktable maintained by the System.Private.Interop.dll that maps weakreferenceInstance->nativeComObject to check whether the native COMObject is alive or not.
        /// and gets\create a new RCW in case it is alive.
        /// </summary>
        /// <returns></returns>
        private object TryGetComTarget()
        {
#if ENABLE_WINRT
            WinRTInteropCallbacks callbacks = WinRTInterop.UnsafeCallbacks;
            if (callbacks != null)
            {
                return callbacks.GetCOMWeakReferenceTarget(this);
            }
            else
            {
                Debug.Fail("WinRTInteropCallback is null");
            }
#endif // ENABLE_WINRT
            return null;
        }

        /// <summary>
        /// This method notifies the System.Private.Interop.dll to update the conditionalweaktable for weakreferenceInstance->target in case the target is __ComObject. This ensures that we have a means to 
        /// go from the managed weak reference to the actual native object even though the managed counterpart might have been collected.
        /// </summary>
        /// <param name="target"></param>
        private void TrySetComTarget(object target)
        {
#if ENABLE_WINRT
            WinRTInteropCallbacks callbacks = WinRTInterop.UnsafeCallbacks;
            if (callbacks != null)
                callbacks.SetCOMWeakReferenceTarget(this, target);
            else
            {
                Debug.Fail("WinRTInteropCallback is null");
            }
#endif // ENABLE_WINRT
        }

        // Free all system resources associated with this reference.
        //
        // Note: The WeakReference<T> finalizer is not usually run, but
        // treated specially in gc.cpp's ScanForFinalization
        // This is needed for subclasses deriving from WeakReference<T>, however.
        // Additionally, there may be some cases during shutdown when we run this finalizer.
        ~WeakReference()
        {
            IntPtr old_handle = m_handle;
            if (old_handle != default(IntPtr))
            {
                if (old_handle == Interlocked.CompareExchange(ref m_handle, default(IntPtr), old_handle))
                    ((GCHandle)old_handle).Free();
            }
        }


        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue("TrackedObject", this.GetTarget(), typeof(T)); // Do not rename (binary serialization)
            info.AddValue("TrackResurrection", m_trackResurrection); // Do not rename (binary serialization)
        }
    }
}
