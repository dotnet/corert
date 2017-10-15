// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
 * This files defines the following types:
 *  - _IOCompletionCallback
 *  - OverlappedData
 *  - Overlapped
 */

/*=============================================================================
**
**
**
** Purpose: Class for converting information to and from the native 
**          overlapped structure used in asynchronous file i/o
**
**
=============================================================================*/

using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Runtime.ConstrainedExecution;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace System.Threading
{
    #region class _IOCompletionCallback

    internal class _IOCompletionCallback
    {
        private IOCompletionCallback _ioCompletionCallback;
        private ExecutionContext _executionContext;
        private uint _errorCode; // Error code
        private uint _numBytes; // No. of bytes transferred 
        private unsafe NativeOverlapped* _pOVERLAP;

        internal _IOCompletionCallback(IOCompletionCallback ioCompletionCallback)
        {
            _ioCompletionCallback = ioCompletionCallback;
            _executionContext = ExecutionContext.Capture();
        }

        private static ContextCallback s_ccb = new ContextCallback(IOCompletionCallback_Context);

        private static unsafe void IOCompletionCallback_Context(Object state)
        {
            _IOCompletionCallback helper = (_IOCompletionCallback)state;
            Debug.Assert(helper != null, "_IOCompletionCallback cannot be null");
            helper._ioCompletionCallback(helper._errorCode, helper._numBytes, helper._pOVERLAP);
        }

        internal static unsafe void PerformIOCompletionCallback(uint errorCode, uint numBytes, NativeOverlapped* pOVERLAP)
        {
            Overlapped overlapped;
            _IOCompletionCallback helper;

            do
            {
                overlapped = OverlappedData.GetOverlappedFromNative(pOVERLAP).m_overlapped;
                helper = overlapped.iocbHelper;

                if (helper == null || helper._executionContext == null || helper._executionContext == ExecutionContext.Default)
                {
                    // We got here because of UnsafePack (or) Pack with EC flow supressed
                    IOCompletionCallback callback = overlapped.UserCallback;
                    callback(errorCode, numBytes, pOVERLAP);
                }
                else
                {
                    // We got here because of Pack
                    helper._errorCode = errorCode;
                    helper._numBytes = numBytes;
                    helper._pOVERLAP = pOVERLAP;
                    ExecutionContext.Run(helper._executionContext, s_ccb, helper);
                }

                //Quickly check the VM again, to see if a packet has arrived.
                //OverlappedData.CheckVMForIOPacket(out pOVERLAP, out errorCode, out numBytes);
                pOVERLAP = null;
            } while (pOVERLAP != null);
        }
    }

    #endregion class _IOCompletionCallback

    #region class OverlappedData

    internal sealed class OverlappedData
    {
        // The offset of m_nativeOverlapped field from m_pEEType
        private static int s_nativeOverlappedOffset;

        internal IAsyncResult m_asyncResult;
        internal IOCompletionCallback m_iocb;
        internal _IOCompletionCallback m_iocbHelper;
        internal Overlapped m_overlapped;
        private Object m_userObject;
        private IntPtr m_pinSelf;
        private GCHandle[] m_pinnedData;
        internal NativeOverlapped m_nativeOverlapped;

        // Adding an empty default ctor for annotation purposes
        internal OverlappedData() { }

        ~OverlappedData()
        {
            if (m_pinnedData != null)
            {
                for (int i = 0; i < m_pinnedData.Length; i++)
                {
                    if (m_pinnedData[i].IsAllocated)
                    {
                        m_pinnedData[i].Free();
                    }
                }
            }
        }

        internal void ReInitialize()
        {
            m_asyncResult = null;
            m_iocb = null;
            m_iocbHelper = null;
            m_overlapped = null;
            m_userObject = null;
            Debug.Assert(m_pinSelf.IsNull(), "OverlappedData has not been freed: m_pinSelf");
            m_pinSelf = IntPtr.Zero;
            // Reuse m_pinnedData array
            m_nativeOverlapped = default(NativeOverlapped);
        }

        internal unsafe NativeOverlapped* Pack(IOCompletionCallback iocb, Object userData)
        {
            if (!m_pinSelf.IsNull())
            {
                throw new InvalidOperationException(SR.InvalidOperation_Overlapped_Pack);
            }
            m_iocb = iocb;
            m_iocbHelper = (iocb != null) ? new _IOCompletionCallback(iocb) : null;
            m_userObject = userData;
            return AllocateNativeOverlapped();
        }

        internal unsafe NativeOverlapped* UnsafePack(IOCompletionCallback iocb, Object userData)
        {
            if (!m_pinSelf.IsNull())
            {
                throw new InvalidOperationException(SR.InvalidOperation_Overlapped_Pack);
            }
            m_iocb = iocb;
            m_iocbHelper = null;
            m_userObject = userData;
            return AllocateNativeOverlapped();
        }

        internal IntPtr UserHandle
        {
            get { return m_nativeOverlapped.EventHandle; }
            set { m_nativeOverlapped.EventHandle = value; }
        }

        private unsafe NativeOverlapped* AllocateNativeOverlapped()
        {
            if (m_userObject != null)
            {
                if (m_userObject.GetType() == typeof(Object[]))
                {
                    Object[] objArray = (Object[])m_userObject;
                    if (m_pinnedData == null || m_pinnedData.Length < objArray.Length)
                        Array.Resize(ref m_pinnedData, objArray.Length);

                    for (int i = 0; i < objArray.Length; i++)
                    {
                        if (!m_pinnedData[i].IsAllocated)
                            m_pinnedData[i] = GCHandle.Alloc(objArray[i], GCHandleType.Pinned);
                        else
                            m_pinnedData[i].Target = objArray[i];
                    }
                }
                else
                {
                    if (m_pinnedData == null || m_pinnedData.Length < 1)
                        m_pinnedData = new GCHandle[1];

                    if (!m_pinnedData[0].IsAllocated)
                        m_pinnedData[0] = GCHandle.Alloc(m_userObject, GCHandleType.Pinned);
                    else
                        m_pinnedData[0].Target = m_userObject;
                }
            }

            m_pinSelf = RuntimeImports.RhHandleAlloc(this, GCHandleType.Pinned);

            fixed (NativeOverlapped* pNativeOverlapped = &m_nativeOverlapped)
            {
                return pNativeOverlapped;
            }
        }

        internal static unsafe void FreeNativeOverlapped(NativeOverlapped* nativeOverlappedPtr)
        {
            OverlappedData overlappedData = OverlappedData.GetOverlappedFromNative(nativeOverlappedPtr);
            overlappedData.FreeNativeOverlapped();
        }

        private void FreeNativeOverlapped()
        {
            IntPtr pinSelf = m_pinSelf;
            if (!pinSelf.IsNull())
            {
                if (Interlocked.CompareExchange(ref m_pinSelf, IntPtr.Zero, pinSelf) == pinSelf)
                {
                    if (m_pinnedData != null)
                    {
                        for (int i = 0; i < m_pinnedData.Length; i++)
                        {
                            if (m_pinnedData[i].IsAllocated && (m_pinnedData[i].Target != null))
                            {
                                m_pinnedData[i].Target = null;
                            }
                        }
                    }

                    RuntimeImports.RhHandleFree(pinSelf);
                }
            }
        }

        internal static unsafe OverlappedData GetOverlappedFromNative(NativeOverlapped* nativeOverlappedPtr)
        {
            if (s_nativeOverlappedOffset == 0)
            {
                CalculateNativeOverlappedOffset();
            }

            void* pOverlappedData = (byte*)nativeOverlappedPtr - s_nativeOverlappedOffset;
            return Unsafe.Read<OverlappedData>(&pOverlappedData);
        }

        private static unsafe void CalculateNativeOverlappedOffset()
        {
            OverlappedData overlappedData = new OverlappedData();

            fixed (IntPtr* pEETypePtr = &overlappedData.m_pEEType)
            fixed (NativeOverlapped* pNativeOverlapped = &overlappedData.m_nativeOverlapped)
            {
                s_nativeOverlappedOffset = (int)((byte*)pNativeOverlapped - (byte*)pEETypePtr);
            }
        }
    }

    #endregion class OverlappedData

    #region class Overlapped

    public class Overlapped
    {
        private static PinnableBufferCache s_overlappedDataCache = new PinnableBufferCache("System.Threading.OverlappedData", () => new OverlappedData());

        private OverlappedData m_overlappedData;

        public Overlapped()
        {
            m_overlappedData = (OverlappedData)s_overlappedDataCache.Allocate();
            m_overlappedData.m_overlapped = this;
        }

        public Overlapped(int offsetLo, int offsetHi, IntPtr hEvent, IAsyncResult ar)
        {
            m_overlappedData = (OverlappedData)s_overlappedDataCache.Allocate();
            m_overlappedData.m_overlapped = this;
            m_overlappedData.m_nativeOverlapped.OffsetLow = offsetLo;
            m_overlappedData.m_nativeOverlapped.OffsetHigh = offsetHi;
            m_overlappedData.UserHandle = hEvent;
            m_overlappedData.m_asyncResult = ar;
        }

        [Obsolete("This constructor is not 64-bit compatible.  Use the constructor that takes an IntPtr for the event handle.  http://go.microsoft.com/fwlink/?linkid=14202")]
        public Overlapped(int offsetLo, int offsetHi, int hEvent, IAsyncResult ar) : this(offsetLo, offsetHi, new IntPtr(hEvent), ar)
        {
        }

        public IAsyncResult AsyncResult
        {
            get { return m_overlappedData.m_asyncResult; }
            set { m_overlappedData.m_asyncResult = value; }
        }

        public int OffsetLow
        {
            get { return m_overlappedData.m_nativeOverlapped.OffsetLow; }
            set { m_overlappedData.m_nativeOverlapped.OffsetLow = value; }
        }

        public int OffsetHigh
        {
            get { return m_overlappedData.m_nativeOverlapped.OffsetHigh; }
            set { m_overlappedData.m_nativeOverlapped.OffsetHigh = value; }
        }

        [Obsolete("This property is not 64-bit compatible.  Use EventHandleIntPtr instead.  http://go.microsoft.com/fwlink/?linkid=14202")]
        public int EventHandle
        {
            get { return m_overlappedData.UserHandle.ToInt32(); }
            set { m_overlappedData.UserHandle = new IntPtr(value); }
        }

        public IntPtr EventHandleIntPtr
        {
            get { return m_overlappedData.UserHandle; }
            set { m_overlappedData.UserHandle = value; }
        }

        internal _IOCompletionCallback iocbHelper
        {
            get { return m_overlappedData.m_iocbHelper; }
        }

        internal IOCompletionCallback UserCallback
        {
            get { return m_overlappedData.m_iocb; }
        }

        /*====================================================================
        *  Packs a managed overlapped class into native Overlapped struct.
        *  Roots the iocb and stores it in the ReservedCOR field of native Overlapped 
        *  Pins the native Overlapped struct and returns the pinned index. 
        ====================================================================*/
        [Obsolete("This method is not safe.  Use Pack (iocb, userData) instead.  http://go.microsoft.com/fwlink/?linkid=14202")]
        [CLSCompliant(false)]
        public unsafe NativeOverlapped* Pack(IOCompletionCallback iocb)
        {
            return Pack(iocb, null);
        }

        [CLSCompliant(false)]
        public unsafe NativeOverlapped* Pack(IOCompletionCallback iocb, Object userData)
        {
            return m_overlappedData.Pack(iocb, userData);
        }

        [Obsolete("This method is not safe.  Use UnsafePack (iocb, userData) instead.  http://go.microsoft.com/fwlink/?linkid=14202")]
        [CLSCompliant(false)]
        public unsafe NativeOverlapped* UnsafePack(IOCompletionCallback iocb)
        {
            return UnsafePack(iocb, null);
        }

        [CLSCompliant(false)]
        public unsafe NativeOverlapped* UnsafePack(IOCompletionCallback iocb, Object userData)
        {
            return m_overlappedData.UnsafePack(iocb, userData);
        }

        /*====================================================================
        *  Unpacks an unmanaged native Overlapped struct. 
        *  Unpins the native Overlapped struct
        ====================================================================*/
        [CLSCompliant(false)]
        public static unsafe Overlapped Unpack(NativeOverlapped* nativeOverlappedPtr)
        {
            if (nativeOverlappedPtr == null)
                throw new ArgumentNullException(nameof(nativeOverlappedPtr));

            Overlapped overlapped = OverlappedData.GetOverlappedFromNative(nativeOverlappedPtr).m_overlapped;

            return overlapped;
        }

        [CLSCompliant(false)]
        public static unsafe void Free(NativeOverlapped* nativeOverlappedPtr)
        {
            if (nativeOverlappedPtr == null)
                throw new ArgumentNullException(nameof(nativeOverlappedPtr));

            Overlapped overlapped = OverlappedData.GetOverlappedFromNative(nativeOverlappedPtr).m_overlapped;
            OverlappedData.FreeNativeOverlapped(nativeOverlappedPtr);
            OverlappedData overlappedData = overlapped.m_overlappedData;
            overlapped.m_overlappedData = null;
            overlappedData.ReInitialize();
            s_overlappedDataCache.Free(overlappedData);
        }
    }

    #endregion class Overlapped
}  // namespace
