// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security;

namespace System.Runtime.InteropServices
{
    internal static class GCEventProvider
    {
        #region TaskLogLiveCCW

        public static bool IsETWHeapCollectionEnabled()
        {
#if !RHTESTCL
            return InteropExtensions.RhpETWShouldWalkCom();
#else
            return false;
#endif // !RHTESTCL
        }

        enum EVENT_TYPE
        {
            EVENT_LOG_CCW = 1,
            EVENT_LOG_RCW,
            EVENT_FLUSH_COM
        };

        // WARNING: If you want to pass new flags to native runtime
        // you must make sure you update the ETW manifest to include the new flags.
        // The description is located at:rh\src\rtetw\ClrEtwAll.man
        // After adding the flags you need to make sure you update the ETW headers by running:
        // 'perl  EtwImportClrEvents.pl'
        enum RCW_ETW_FLAGS
        {
            Duplicate = 0x1,
            JupiterObject = 0x2,
            ExtendsComObject = 0x4
        };

        enum CCW_ETW_FLAGS
        {
            Strong = 0x1,
            Pegged = 0x2,
            AggregatesRCW = 0x4
        };

        /// <summary>
        /// Fired when a CCW is being found at GC time.
        /// </summary>
        /// <param name="CCWGCHandle">GC Root handle that keep this CCW alive.</param>
        /// <param name="pCCW">CCW managed object base address.</param>
        /// <param name="typeRawValue">Pointer to the type address.</param>
        /// <param name="IUnknown">Address of the IUnknown interface.</param>
        /// <param name="ComRefCount">CCW reference count.</param>
        /// <param name="JupiterRefCount">CCW Jupiter reference count.</param>
        /// <param name="flags">CCW's flags.</param>
        [GCCallback]
        public static void TaskLogLiveCCW(IntPtr CCWGCHandle, IntPtr pCCW, IntPtr typeRawValue, IntPtr IUnknown, int ComRefCount, int JupiterRefCount, int flags)
        {
            long ccwEtwFlags = 0;

            if ((flags & (long)ComCallableObjectFlags.IsPegged) != 0)
                ccwEtwFlags &= (long)CCW_ETW_FLAGS.Pegged;

            if ((flags & (long)ComCallableObjectFlags.IsAggregatingRCW) != 0)
                ccwEtwFlags &= (long)CCW_ETW_FLAGS.AggregatesRCW;
#if !RHTESTCL
            InteropExtensions.RhpETWLogLiveCom((int)EVENT_TYPE.EVENT_LOG_CCW, CCWGCHandle, pCCW, typeRawValue, IUnknown, (IntPtr)0, ComRefCount, JupiterRefCount, flags);
#endif // !RHTESTCL
        }

        /// <summary>
        /// Fired when a RCW is being found at GC time.
        /// </summary>
        /// <param name="pRCW">RCW managed object base address.</param>
        /// <param name="typeRawValue">Pointer to the type address.</param>
        /// <param name="IUnknown">Address of the IUnknown interface.</param>
        /// <param name="VTable">Address of the VTable.</param>
        /// <param name="refCount">RCW reference count.</param>
        /// <param name="flags">RCW's flags.</param>
        [GCCallback]
        public static void TaskLogLiveRCW(IntPtr pRCW, IntPtr typeRawValue, IntPtr IUnknown, IntPtr VTable, int refCount, ComObjectFlags flags)
        {
            int rcwEtwFlags = 0;

            if ((flags & ComObjectFlags.IsDuplicate) != 0)
                rcwEtwFlags &= (int)RCW_ETW_FLAGS.Duplicate;
            if ((flags & ComObjectFlags.IsJupiterObject) != 0)
                rcwEtwFlags &= (int)RCW_ETW_FLAGS.JupiterObject;
            if ((flags & ComObjectFlags.ExtendsComObject) != 0)
                rcwEtwFlags &= (int)RCW_ETW_FLAGS.ExtendsComObject;
#if !RHTESTCL
            InteropExtensions.RhpETWLogLiveCom((int)EVENT_TYPE.EVENT_LOG_RCW, (IntPtr)0, pRCW, typeRawValue, IUnknown, VTable, refCount, 0, rcwEtwFlags);
#endif // !RHTESTCL
        }

        /// <summary>
        /// Fired when a CCW and RCW logging finished, allowing buffered events to be flushed out.
        /// </summary>
        [GCCallback]
        public static void FlushComETW()
        {
#if !RHTESTCL
            InteropExtensions.RhpETWLogLiveCom((int)EVENT_TYPE.EVENT_FLUSH_COM, (IntPtr)0, (IntPtr)0, (IntPtr)0, (IntPtr)0, (IntPtr)0, 0, 0, 0);
#endif // !RHTESTCL
        }
    }
    #endregion // TaskLogLiveCCW
}
