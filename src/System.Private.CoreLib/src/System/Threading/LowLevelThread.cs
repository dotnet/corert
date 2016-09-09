// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// LowLevelThread provides low-level threading primitives, like waiting for handles or sleeping.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System.Diagnostics;
using System.Runtime;

#pragma warning disable 0420


namespace System.Threading
{
    internal static class LowLevelThread
    {
        internal const int WAIT_OBJECT_0 = (int)Interop.Constants.WaitObject0;
        internal const int WAIT_ABANDONED = (int)Interop.Constants.WaitAbandoned0;
        internal const int MAX_WAITHANDLES = 64;
        internal const int WAIT_TIMEOUT = (int)Interop.Constants.WaitTimeout;
        internal const int WAIT_FAILED = unchecked((int)Interop.Constants.WaitFailed);

        internal unsafe static int WaitForSingleObject(IntPtr handle, int millisecondsTimeout)
        {
            return WaitForMultipleObjects(&handle, 1, false, millisecondsTimeout);
        }

        internal unsafe static int WaitForMultipleObjects(IntPtr[] handles, bool waitAll, int millisecondsTimeout)
        {
            fixed (IntPtr* pHandles = handles)
            {
                return WaitForMultipleObjects(pHandles, handles.Length, waitAll, millisecondsTimeout);
            }
        }

        internal unsafe static int WaitForMultipleObjects(IntPtr* pHandles, int numHandles, bool waitAll, int millisecondsTimeout)
        {
            Debug.Assert(millisecondsTimeout >= -1);

            //
            // In the CLR, we use CoWaitForMultipleHandles to pump messages while waiting in an STA.  In that case, we cannot use WAIT_ALL.  
            // That's because the wait would only be satisfied if a message arrives while the handles are signalled.
            //
            if (waitAll)
            {
                if (numHandles == 1)
                    waitAll = false;
                else if (GetCurrentApartmentType() == ApartmentType.STA)
                    throw new NotSupportedException(SR.NotSupported_WaitAllSTAThread);
            }

            int result;
            if (ReentrantWaitsEnabled)
            {
                Debug.Assert(!waitAll);
                result = RuntimeImports.RhCompatibleReentrantWaitAny(false, millisecondsTimeout, numHandles, pHandles);
            }
            else
            {
                result = (int)Interop.mincore.WaitForMultipleObjectsEx((uint)numHandles, (IntPtr)pHandles, waitAll, (uint)millisecondsTimeout, false);
            }

            if (result == WAIT_FAILED)
            {
                int error = (int)Interop.mincore.GetLastError();
                switch (error)
                {
                    case Interop.mincore.Errors.ERROR_INVALID_PARAMETER:
                        throw new ArgumentException();

                    case Interop.mincore.Errors.ERROR_ACCESS_DENIED:
                        throw new UnauthorizedAccessException();

                    case Interop.mincore.Errors.ERROR_NOT_ENOUGH_MEMORY:
                        throw new OutOfMemoryException();

                    default:
                        Exception ex = new Exception();
                        ex.SetErrorCode(error);
                        throw ex;
                }
            }

            return result;
        }

        internal enum ApartmentType
        {
            Unknown = 0,
            None,
            STA,
            MTA
        }

        [ThreadStatic]
        private static ApartmentType t_apartmentType;

        internal static ApartmentType GetCurrentApartmentType()
        {
            ApartmentType currentThreadType = t_apartmentType;
            if (currentThreadType != ApartmentType.Unknown)
                return currentThreadType;

            Interop._APTTYPE aptType;
            Interop._APTTYPEQUALIFIER aptTypeQualifier;
            int result = Interop.mincore.CoGetApartmentType(out aptType, out aptTypeQualifier);

            ApartmentType type = ApartmentType.Unknown;

            switch ((Interop.Constants)result)
            {
                case Interop.Constants.CoENotInitialized:
                    type = ApartmentType.None;
                    break;

                case Interop.Constants.SOk:
                    switch (aptType)
                    {
                        case Interop._APTTYPE.APTTYPE_STA:
                        case Interop._APTTYPE.APTTYPE_MAINSTA:
                            type = ApartmentType.STA;
                            break;

                        case Interop._APTTYPE.APTTYPE_MTA:
                            type = ApartmentType.MTA;
                            break;

                        case Interop._APTTYPE.APTTYPE_NA:
                            switch (aptTypeQualifier)
                            {
                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MTA:
                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_IMPLICIT_MTA:
                                    type = ApartmentType.MTA;
                                    break;

                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_STA:
                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MAINSTA:
                                    type = ApartmentType.STA;
                                    break;

                                default:
                                    Debug.Assert(false, "NA apartment without NA qualifier");
                                    break;
                            }
                            break;
                    }
                    break;

                default:
                    Debug.Assert(false, "bad return from CoGetApartmentType");
                    break;
            }

            if (type != ApartmentType.Unknown)
                t_apartmentType = type;
            return type;
        }

        [ThreadStatic]
        private static int t_reentrantWaitSuppressionCount;

        //
        // Suppresses reentrant waits on the current thread, until a matching call to RestoreReentrantWaits.
        // This should be used by code that's expected to be called inside the STA message pump, so that it won't 
        // reenter itself.  In an ASTA, this should only be the CCW implementations of IUnknown and IInspectable.
        //
        internal static void SuppressReentrantWaits()
        {
            t_reentrantWaitSuppressionCount++;
        }

        internal static void RestoreReentrantWaits()
        {
            Debug.Assert(t_reentrantWaitSuppressionCount > 0);
            t_reentrantWaitSuppressionCount--;
        }

        internal static bool ReentrantWaitsEnabled
        {
            get
            {
                return GetCurrentApartmentType() == ApartmentType.STA && t_reentrantWaitSuppressionCount == 0;
            }
        }
    }
}
