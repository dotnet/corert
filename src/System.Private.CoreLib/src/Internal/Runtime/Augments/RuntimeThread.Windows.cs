// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Internal.Runtime.Augments
{
    public sealed partial class RuntimeThread
    {
        [ThreadStatic]
        private static int t_reentrantWaitSuppressionCount;

        [ThreadStatic]
        private static ApartmentType t_apartmentType;

        /// <summary>
        /// Used by <see cref="WaitHandle"/>'s multi-wait functions
        /// </summary>
        private WaitHandleArray<IntPtr> _waitedHandles;

        private void PlatformSpecificInitialize()
        {
            _waitedHandles = new WaitHandleArray<IntPtr>(elementInitializer: null);
        }

        internal IntPtr[] GetWaitedHandleArray(int requiredCapacity)
        {
            Debug.Assert(this == CurrentThread);

            _waitedHandles.EnsureCapacity(requiredCapacity);
            return _waitedHandles.Items;
        }

        public ApartmentState GetApartmentState() { throw null; }
        public bool TrySetApartmentState(ApartmentState state) { throw null; }
        public void DisableComObjectEagerCleanup() { throw null; }
        public void Interrupt() { throw null; }

        internal static void UninterruptibleSleep0()
        {
            global::Interop.mincore.Sleep(0);
        }

        private static void SleepCore(int millisecondsTimeout)
        {
            Debug.Assert(millisecondsTimeout >= -1);
            global::Interop.mincore.Sleep((uint)millisecondsTimeout);
        }

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

        internal static bool ReentrantWaitsEnabled =>
            GetCurrentApartmentType() == ApartmentType.STA && t_reentrantWaitSuppressionCount == 0;

        internal static ApartmentType GetCurrentApartmentType()
        {
            ApartmentType currentThreadType = t_apartmentType;
            if (currentThreadType != ApartmentType.Unknown)
                return currentThreadType;

            global::Interop._APTTYPE aptType;
            global::Interop._APTTYPEQUALIFIER aptTypeQualifier;
            int result = global::Interop.mincore.CoGetApartmentType(out aptType, out aptTypeQualifier);

            ApartmentType type = ApartmentType.Unknown;

            switch ((global::Interop.Constants)result)
            {
                case global::Interop.Constants.CoENotInitialized:
                    type = ApartmentType.None;
                    break;

                case global::Interop.Constants.SOk:
                    switch (aptType)
                    {
                        case global::Interop._APTTYPE.APTTYPE_STA:
                        case global::Interop._APTTYPE.APTTYPE_MAINSTA:
                            type = ApartmentType.STA;
                            break;

                        case global::Interop._APTTYPE.APTTYPE_MTA:
                            type = ApartmentType.MTA;
                            break;

                        case global::Interop._APTTYPE.APTTYPE_NA:
                            switch (aptTypeQualifier)
                            {
                                case global::Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MTA:
                                case global::Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_IMPLICIT_MTA:
                                    type = ApartmentType.MTA;
                                    break;

                                case global::Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_STA:
                                case global::Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MAINSTA:
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

        internal enum ApartmentType
        {
            Unknown = 0,
            None,
            STA,
            MTA
        }
    }
}
