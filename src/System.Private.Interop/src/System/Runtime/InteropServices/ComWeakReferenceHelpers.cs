// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Runtime.InteropServices
{
#if ENABLE_WINRT
    /// <summary>
    /// This class stores the weak references to the native COM Objects to ensure a way to map the weak 
    /// reference to the native ComObject target and keep the mapping alive until the native object is alive
    /// allowing the connection to remain alive even though the managed wrapper might die.
    /// </summary>
    public static class COMWeakReferenceHelpers
    {
        // Holds the mapping from the weak reference to the COMWeakReference which is a thin wrapper for native WeakReference.
        static ConditionalWeakTable<object, ComWeakReference> s_COMWeakReferenceTable = new ConditionalWeakTable<object, ComWeakReference>();

        /// <summary>
        /// This class is a thin wrapper that holds the native IWeakReference.
        /// </summary>
        internal class ComWeakReference
        {
            private IntPtr m_pComWeakRef;

            internal ComWeakReference(ref IntPtr pUnk)
            {
                Debug.Assert(pUnk != IntPtr.Zero);

                m_pComWeakRef = pUnk;
                pUnk = IntPtr.Zero;
            }

            /// <summary>
            /// This method validates that the given weak reference is alive.
            /// 1. IWeakReference->Resolve method returns the target's interface for the mapping IID passed to it.
            /// 2. If the object is not alive it returns null.
            /// 2. From the returned interface we get or create a new RCW and return it.
            /// </summary>
            /// <returns></returns>
            internal unsafe object Resolve()
            {
                IntPtr pInspectable;
                __com_IWeakReference* pComIWeakReference = (__com_IWeakReference*)m_pComWeakRef;
                Guid inspectableIID = Interop.COM.IID_IInspectable;
                int result = CalliIntrinsics.StdCall__int(
                   pComIWeakReference->pVtable->pfnResolve,
                   m_pComWeakRef,
                   &inspectableIID,
                   &pInspectable);

                if (result >= 0 && pInspectable != IntPtr.Zero)
                {
                    try
                    {
                        return McgMarshal.IInspectableToObject(pInspectable);
                    }
                    finally
                    {
                        // Release the pInspectable.
                        McgMarshal.ComRelease(pInspectable);
                    }
                }

                return null;
            }

            ~ComWeakReference()
            {
#pragma warning disable 420  // FYI - ref m_pComWeakRef causes this.
                IntPtr handle = Interlocked.Exchange(ref m_pComWeakRef, IntPtr.Zero);
#pragma warning restore 420
                McgMarshal.ComSafeRelease(handle);
            }
        }
#pragma warning disable 649, 169  // Field 'blah' is never assigned to/Field 'blah' is never used
        private unsafe struct __com_IWeakReferenceSource
        {
            internal __vtable_IWeakReferenceSource* pVtable;
        }
#pragma warning restore 649, 169

        /// <summary>
        /// This method gets called every time the WeakReference or WeakReference'1 set the Target.
        /// We can have 4 possible combination here.
        /// a. Target is a GC object and it is either getting set for the first time or previous object is also GC object.
        ///     In this case we do not need to do anything.
        ///
        /// b. Target is a GC object and previous target was __ComObject
        ///    i. We remove the element from ConditionalWeakTable.
        ///   ii. When the GC collects this ComWeakReference the finalizer will ensure that the native object is released.
        ///
        /// c. Target is a __ComObject and the previous target was null or GC object.
        ///    We simply add the new target to the dictionary.
        ///
        /// d. Target is a __COmObject and the previous object was __COmObject.
        ///    i. We first remove the element from the ConditionalWeakTable.
        ///   ii. When the GC collects the previous ComWeakReference the finalizer will ensure that the native object is released.
        ///  iii. We add the new ComWeakReference to the conditionalWeakTable.
        /// </summary>
        /// <param name="weakReference"></param>
        /// <param name="target"></param>
        public static unsafe void SetTarget(object weakReference, object target)
        {
            Debug.Assert(weakReference != null);

            // Check if this weakReference is already associated with a native target.
            ComWeakReference pOldComWeakReference;
            if (s_COMWeakReferenceTable.TryGetValue(weakReference, out pOldComWeakReference))
            {
                // Remove the previous target.
                // We do not have to release the native ComWeakReference since it will be done as part of the finalizer.
                s_COMWeakReferenceTable.Remove(weakReference);
            }

            // Now check whether the current target is __ComObject.
            // However, we don't want to pass the QI to a managed object deriving from native object - we
            // would end up passing the QI to back the CCW and end up stack overflow
            __ComObject comObject = target as __ComObject;
            if (comObject != null && !comObject.ExtendsComObject)
            {
                // Get the IWeakReferenceSource for the given object
                IntPtr pWeakRefSource = McgMarshal.ObjectToComInterface(comObject, InternalTypes.IWeakReferenceSource);

                if (pWeakRefSource != IntPtr.Zero)
                {
                    IntPtr pWeakRef = IntPtr.Zero;
                    try
                    {
                        // Now that we have the IWeakReferenceSource , we need to call the GetWeakReference method to get the corresponding IWeakReference
                        __com_IWeakReferenceSource* pComWeakRefSource = (__com_IWeakReferenceSource*)pWeakRefSource;
                        int result = CalliIntrinsics.StdCall<int>(
                           pComWeakRefSource->pVtable->pfnGetWeakReference,
                           pWeakRefSource,
                           out pWeakRef);
                        if (result >= 0 && pWeakRef != IntPtr.Zero)
                        {
                            // Since we have already checked s_COMWeakReferenceTable for the weak reference, we can simply add the entry w/o checking.
                            // PS - We do not release the pWeakRef as it should be alive until the ComWeakReference is alive.
                            s_COMWeakReferenceTable.Add(weakReference, new ComWeakReference(ref pWeakRef));
                        }
                    }
                    finally
                    {
                        McgMarshal.ComSafeRelease(pWeakRef);
                        McgMarshal.ComRelease(pWeakRefSource);
                    }
                }
            }
        }

        /// <summary>
        /// This method gets the native target for the current weak reference if present.
        /// This is done as a 2 step process.
        /// a. Fetch the native target for the weakreference through conditionalweaktable lookup.
        /// b. Checking whether the native object is alive by calling the IWeakReference->Resolve method.
        /// </summary>
        /// <param name="weakReference"></param>
        /// <returns></returns>
        public static object GetTarget(object weakReference)
        {
            Debug.Assert(weakReference != null);

            ComWeakReference comWeakRef;
            if (s_COMWeakReferenceTable.TryGetValue(weakReference, out comWeakRef))
            {
                return comWeakRef.Resolve();
            }

            return null;
        }
    }
#endif
}
