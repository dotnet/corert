// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// InteropEventProvider.cs
//
//
// Managed event source for FXCore.
// This will produce an XML file, where each event is pretty-printed with all its arguments nicely parsed.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Security;
using System.Diagnostics.Tracing;

namespace System.Runtime.InteropServices
{
    /// <summary>Provides an event source for tracing Interop information.</summary>
    [EventSource(Guid = "C4AC552A-E1EB-4FA2-A651-B200EFD7AA91", Name = "System.Runtime.InteropServices.InteropEventProvider")]
    internal sealed class InteropEventProvider : EventSource
    {
        // Defines the singleton instance for the Interop Event ETW provider
        public static readonly InteropEventProvider Log = new InteropEventProvider();

        internal new static bool IsEnabled()
        {
            // The InteropEventProvider class constructor should create an instance of InteropEventProvider and assign
            // it to the Log field so Log should never be null here. However the EventSource performs some P/Invoke
            // interop and creating a System.Type object which happens while running the EventSource ctor can perform
            // WinRT interop so it is possible that we end up calling IsEnabled before the InteropEventProvider class
            // constructor has completed so we must check that Log is not null.
            return Log != null && ((EventSource)Log).IsEnabled();
        }

        // The InteropEventSource GUID is {C4AC552A-E1EB-4FA2-A651-B200EFD7AA91}
        private InteropEventProvider() { }

        /// <summary>Keyword definitions.</summary>
        public static class Keywords
        {
            /// <summary>Interop keyword enable or disable the whole interop log events.</summary>
            public const EventKeywords Interop = (EventKeywords)0x0001; // This is bit 0.
        }

        //-----------------------------------------------------------------------------------
        //
        // Interop Event IDs (must be unique)
        //

        #region RCWProvider
        #region TaskID
        /// <summary>A new RCW was created. Details at TaskRCWCreation.</summary>
        private const int TASKRCWCREATION_ID = 10;
        /// <summary>A RCW was finalized. Details at TaskRCWFinalization.</summary>
        private const int TASKRCWFINALIZATION_ID = 11;
        /// <summary>The RCW reference counter was incremented. Details at TaskRCWRefCountInc.</summary>
        private const int TASKRCWREFCOUNTINC_ID = 12;
        /// <summary>The RCW reference counter was decremented. Details at TaskRCWRefCountDec.</summary>
        private const int TASKRCWREFCOUNTDEC_ID = 13;
        /// <summary>The query interface failure. Details at TaskRCWQueryInterfaceFailure.</summary>
        private const int TASKRCWQUERYINTERFACEFAILURE_ID = 14;
        #endregion TaskID
        #region TaskRCWCreation
        /// <summary>
        /// Fired when a new RCW was created.
        /// </summary>
        /// <scenarios>
        /// - Pair with RCW finalization to understand RCW lifetime and analyze leaks
        /// - Reference with other RCW events to understand basic properties of RCW (without using tool to inspect RCWs at runtime)
        /// - Understanding why weakly typed RCW are created or strongly-typed RCW are created
        /// </scenarios>
        /// <param name="comObject">Base address that unique identify the RCW.</param>
        /// <param name="typeRawValue">RCW type identification.</param>
        /// <param name="runtimeClassName">RCW runtime class name.</param>
        /// <param name="context">RCW context.</param>
        /// <param name="flags">RCW control flags.</param>
        [Event(TASKRCWCREATION_ID, Message = "New RCW created", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskRCWCreation(long objectID, long typeRawValue, string runtimeClassName, long context, long flags)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[5];

                    int runtimeClassNameLength = (runtimeClassName.Length + 1) * 2;
                    fixed (char* StringAux = runtimeClassName)
                    {
                        eventPayload[0].Size = sizeof(long);
                        eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                        eventPayload[1].Size = sizeof(long);
                        eventPayload[1].DataPointer = ((IntPtr)(&typeRawValue));
                        eventPayload[2].Size = runtimeClassNameLength;
                        eventPayload[2].DataPointer = ((IntPtr)(StringAux));
                        eventPayload[3].Size = sizeof(long);
                        eventPayload[3].DataPointer = ((IntPtr)(&context));
                        eventPayload[4].Size = sizeof(long);
                        eventPayload[4].DataPointer = ((IntPtr)(&flags));

                        WriteEventCore(TASKRCWCREATION_ID, 5, eventPayload);
                    }
                }
            }
        }
        #endregion TaskRCWCreation
        #region TaskRCWRefCountInc
        /// <summary>
        /// Fired when a reference counter is incremented in RCW.
        /// </summary>
        /// <scenarios>
        /// - Diagnosing Marshal.ReleaseCOmObject/FInalReleaseComObject errors
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the RCW.</param>
        /// <param name="refCount">New reference counter value.</param>
        [Event(TASKRCWREFCOUNTINC_ID, Message = "RCW refCount incremented", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskRCWRefCountInc(long objectID, int refCount)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(int);
                    eventPayload[1].DataPointer = ((IntPtr)(&refCount));

                    WriteEventCore(TASKRCWREFCOUNTINC_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskRCWRefCountInc
        #region TaskRCWRefCountDec
        /// <summary>
        /// Fired when a reference counter is decremented in RCW.
        /// </summary>
        /// <scenarios>
        /// - Diagnosing Marshal.ReleaseCOmObject/FInalReleaseComObject errors
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the RCW.</param>
        /// <param name="refCount">New reference counter value.</param>
        [Event(TASKRCWREFCOUNTDEC_ID, Message = "RCW refCount decremented", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskRCWRefCountDec(long objectID, int refCount)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(int);
                    eventPayload[1].DataPointer = ((IntPtr)(&refCount));

                    WriteEventCore(TASKRCWREFCOUNTDEC_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskRCWRefCountDec
        #region TaskRCWFinalization
        /// <summary>
        /// Fired when a new RCW was finalized.
        /// </summary>
        /// <scenarios>
        /// - Pair with RCW finalization to understand RCW lifetime and analyze leaks
        /// - See if certain COM objects are finalized or not
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the RCW.</param>
        /// <param name="refCount">RCW reference counter.</param>
        [Event(TASKRCWFINALIZATION_ID, Message = "RCW Finalized", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskRCWFinalization(long objectID, int refCount)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(int);
                    eventPayload[1].DataPointer = ((IntPtr)(&refCount));

                    WriteEventCore(TASKRCWFINALIZATION_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskRCWFinalization
        #region TaskRCWQueryInterfaceFailure
        /// <summary>
        /// Fired when a RCW Interface address is queried and failure.
        /// </summary>
        /// <scenarios>
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the RCW.</param>
        /// <param name="context">RCW context.</param>
        /// <param name="interfaceIId">Queried interface IID.</param>
        /// <param name="reason">Failure reason.</param>
        /// <remarks>Not used</remarks>
        [Event(TASKRCWQUERYINTERFACEFAILURE_ID, Message = "RCW Queried Interface Failure", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskRCWQueryInterfaceFailure(long objectID, long context, Guid interfaceIId, int reason)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[4];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(long);
                    eventPayload[1].DataPointer = ((IntPtr)(&context));
                    eventPayload[2].Size = sizeof(Guid);
                    eventPayload[2].DataPointer = ((IntPtr)(&interfaceIId));
                    eventPayload[3].Size = sizeof(int);
                    eventPayload[3].DataPointer = ((IntPtr)(&reason));

                    WriteEventCore(TASKRCWQUERYINTERFACEFAILURE_ID, 4, eventPayload);
                }
            }
        }
        #endregion TaskRCWQueryInterfaceFailure
        #endregion RCWProvider

        #region CCWProvider
        #region TaskID
        /// <summary>A new CCW was created. Details at TaskCCWCreation. Details at TaskCCWCreation.</summary>
        private const int TASKCCWCREATION_ID = 20;
        /// <summary>A CCW was finalized. Details at TaskCCWFinalization. Details at TaskCCWFinalization.</summary>
        private const int TASKCCWFINALIZATION_ID = 21;
        /// <summary>The CCW reference counter was incremented. Details at TaskCCWRefCountInc.</summary>
        private const int TASKCCWREFCOUNTINC_ID = 22;
        /// <summary>The CCW reference counter was decremented. Details at TaskCCWRefCountDec.</summary>
        private const int TASKCCWREFCOUNTDEC_ID = 23;
        /// <summary>The Runtime class name was queried. Details at TaskCCWQueryRuntimeClassName.</summary>
        private const int TASKCCWQUERYRUNTIMECLASSNAME_ID = 24;
        /// <summary>An interface was queried whit error. Details at TaskCCWQueryInterfaceFailure.</summary>
        private const int TASKCCWQUERYINTERFACEFAILURE_ID = 30;
        /// <summary>Resolve was queried with error. Details at TaskCCWResolveFailure.</summary>
        private const int TASKCCWRESOLVEFAILURE_ID = 33;
        #endregion TaskID
        #region TaskCCWCreation
        /// <summary>
        /// Fired when a new CCW was created.
        /// </summary>
        /// <scenarios>
        /// - Understand lifetime of CCWs
        /// - Reference with other CCW events
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the CCW.</param>
        /// <param name="targetObjectID">Base address that unique identify the target object in CCW.</param>
        /// <param name="targetObjectIDType">Raw value for the type of the target Object.</param>
        [Event(TASKCCWCREATION_ID, Message = "New CCW created", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskCCWCreation(long objectID, long targetObjectID, long targetObjectIDType)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[3];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(long);
                    eventPayload[1].DataPointer = ((IntPtr)(&targetObjectID));
                    eventPayload[2].Size = sizeof(long);
                    eventPayload[2].DataPointer = ((IntPtr)(&targetObjectIDType));

                    WriteEventCore(TASKCCWCREATION_ID, 3, eventPayload);
                }
            }
        }
        #endregion TaskCCWCreation
        #region TaskCCWFinalization
        /// <summary>
        /// Fired when a new CCW was finalized.
        /// </summary>
        /// <scenarios>
        /// - Understand lifetime of CCWs and help track addref/release problems.
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the CCW.</param>
        /// <param name="refCount">The reference counter value at the ending time.</param>
        [Event(TASKCCWFINALIZATION_ID, Message = "CCW Finalized", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskCCWFinalization(long objectID, long refCount)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(long);
                    eventPayload[1].DataPointer = ((IntPtr)(&refCount));

                    WriteEventCore(TASKCCWFINALIZATION_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskCCWFinalization
        #region TaskCCWRefCountInc
        /// <summary>
        /// Fired when a reference counter is incremented in CCW.
        /// </summary>
        /// <scenarios>
        /// - Tracking addref/release problems
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the CCW.</param>
        /// <param name="refCount">New reference counter value.</param>
        [Event(TASKCCWREFCOUNTINC_ID, Message = "CCW refCount incremented", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskCCWRefCountInc(long objectID, long refCount)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(long);
                    eventPayload[1].DataPointer = ((IntPtr)(&refCount));

                    WriteEventCore(TASKCCWREFCOUNTINC_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskCCWRefCountInc
        #region TaskCCWRefCountDec
        /// <summary>
        /// Fired when a reference counter is decremented in CCW.
        /// </summary>
        /// <scenarios>
        /// - Tracking addref/release problems
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the CCW.</param>
        /// <param name="refCount">New reference counter value.</param>
        [Event(TASKCCWREFCOUNTDEC_ID, Message = "CCW refCount decremented", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskCCWRefCountDec(long objectID, long refCount)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(long);
                    eventPayload[1].DataPointer = ((IntPtr)(&refCount));

                    WriteEventCore(TASKCCWREFCOUNTDEC_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskCCWRefCountDec
        #region TaskCCWQueryRuntimeClassName
        /// <summary>
        /// Fired when a runtime class name was queried.
        /// </summary>
        /// <scenarios>
        /// - Diagnosing bugs in JavaScript/.NET interaction, such as why JavaSCript refuse to call a function on a managed WinMD type
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the CCW.</param>
        /// <param name="runtimeClassName">Required runtime class name.</param>
        [Event(TASKCCWQUERYRUNTIMECLASSNAME_ID, Message = "CCW runtime class name required", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskCCWQueryRuntimeClassName(long objectID, string runtimeClassName)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    int runtimeClassNameLength = (runtimeClassName.Length + 1) * 2;
                    fixed (char* StringAux = runtimeClassName)
                    {
                        eventPayload[0].Size = sizeof(long);
                        eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                        eventPayload[1].Size = runtimeClassNameLength;
                        eventPayload[1].DataPointer = ((IntPtr)(StringAux));

                        WriteEventCore(TASKCCWQUERYRUNTIMECLASSNAME_ID, 2, eventPayload);
                    }
                }
            }
        }
        #endregion TaskCCWQueryRuntimeClassName
        #region TaskCCWQueryInterfaceFailure
        /// <summary>
        /// Fired when a CCW Interface address is queried and for any reason it was rejected.
        /// </summary>
        /// <scenarios>
        /// - Diagnosing interop bugs where Qis are rejected for no apparent reason and causing Jupiter to fail in strange ways.
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the CCW.</param>
        /// <param name="interfaceIId">Guid of the queried interface.</param>
        [Event(TASKCCWQUERYINTERFACEFAILURE_ID, Message = "CCW queried interface failure", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskCCWQueryInterfaceFailure(long objectID, Guid interfaceIId)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(Guid);
                    eventPayload[1].DataPointer = ((IntPtr)(&interfaceIId));

                    WriteEventCore(TASKCCWQUERYINTERFACEFAILURE_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskCCWQueryInterfaceFailure
        #region TaskCCWResolveFailure
        /// <summary>
        /// Fired when a CCW interface resolve is queried and for any reason it was rejected.
        /// </summary>
        /// <scenarios>
        /// - Diagnosing interop bugs where Resolves are rejected for no apparent reason and causing to fail.
        /// </scenarios>
        /// <param name="objectID">Base address that unique identify the CCW.</param>
        /// <param name="interfaceAddress">Address of the interface that must be resolved</param>
        /// <param name="interfaceIId">Guid of the queried interface.</param>
        /// <param name="rejectedReason">Rejected reason.</param>
        [Event(TASKCCWRESOLVEFAILURE_ID, Message = "CCW resolve failure", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskCCWResolveFailure(long objectID, long interfaceAddress, Guid interfaceIId, int rejectedReason)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[4];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&objectID));
                    eventPayload[1].Size = sizeof(long);
                    eventPayload[1].DataPointer = ((IntPtr)(&interfaceAddress));
                    eventPayload[2].Size = sizeof(Guid);
                    eventPayload[2].DataPointer = ((IntPtr)(&interfaceIId));
                    eventPayload[3].Size = sizeof(int);
                    eventPayload[3].DataPointer = ((IntPtr)(&rejectedReason));

                    WriteEventCore(TASKCCWRESOLVEFAILURE_ID, 4, eventPayload);
                }
            }
        }
        #endregion TaskCCWResolveFailure
        #endregion CCWProvider

        #region JupiterProvider
        #region TaskID
        /// <summary>Jupter Garbage Collector was invoked via Callback. Details at TaskJupiterGarbageCollect.</summary>
        private const int TASKJUPITERGARBAGECOLLECT_ID = 40;
        /// <summary>Jupiter disconnect RCWs in current apartment. Details at TaskJupiterDisconnectRCWsInCurrentApartment.</summary>
        private const int TASKJUPITERDISCONNECTERCWSINCURRENTAPARTMENT_ID = 41;
        /// <summary>Jupiter add memory pressure callback. Details at TaskJupiterAddMemoryPressure.</summary>
        private const int TASKJUPITERADDMEMORYPRESSURE_ID = 42;
        /// <summary>Jupiter renove memory pressure callback. Details at TaskJupiterRemoveMemoryPressure.</summary>
        private const int TASKJUPITERREMOVEMEMORYPRESSURE_ID = 43;
        /// <summary>Jupiter create managed reference callback. Details at TaskJupiterCreateManagedReference.</summary>
        private const int TASKJUPITERCREATEMANAGEDREFERENCE_ID = 44;
        #endregion TaskID
        #region TaskJupiterGarbageCollect
        /// <summary>
        /// Fired when Jupiter garbage collector callback is called.
        /// </summary>
        /// <scenarios>
        /// - Monitoring the frequency of GarbageCollect is being triggered by Jupiter
        /// </scenarios>
        [Event(TASKJUPITERGARBAGECOLLECT_ID, Message = "Garbage Collect", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskJupiterGarbageCollect()
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    WriteEventCore(TASKJUPITERGARBAGECOLLECT_ID, 0, null);
                }
            }
        }
        #endregion TaskJupiterGarbageCollect
        #region TaskJupiterDisconnectRCWsInCurrentApartment
        /// <summary>
        /// Fired when Jupiter disconnect RCWs in current apartment.
        /// </summary>
        /// <scenarios>
        /// - Monitoring the frequency of wait for pending finalizer callback is being triggered by Jupiter
        /// </scenarios>
        [Event(TASKJUPITERDISCONNECTERCWSINCURRENTAPARTMENT_ID, Message = "Jupiter disconnect RCWs in current apartment", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskJupiterDisconnectRCWsInCurrentApartment()
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    WriteEventCore(TASKJUPITERDISCONNECTERCWSINCURRENTAPARTMENT_ID, 0, null);
                }
            }
        }
        #endregion TaskJupiterDisconnectRCWsInCurrentApartment
        #region TaskJupiterAddMemoryPressure
        /// <summary>
        /// Fired when a Jupiter add memory pressure callback is called.
        /// </summary>
        /// <scenarios>
        /// - Monitoring memory pressure added by Jupiter
        /// </scenarios>
        /// <param name="memorySize">Number of bytes in the added memory.</param>
        [Event(TASKJUPITERADDMEMORYPRESSURE_ID, Message = "Jupiter add memory pressure", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskJupiterAddMemoryPressure(long memorySize)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[1];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&memorySize));

                    WriteEventCore(TASKJUPITERADDMEMORYPRESSURE_ID, 1, eventPayload);
                }
            }
        }
        #endregion TaskJupiterAddMemoryPressure
        #region TaskJupiterRemoveMemoryPressure
        /// <summary>
        /// Fired when a Jupiter Remove memory pressure callback is called.
        /// </summary>
        /// <scenarios>
        /// - Monitoring memory pressure Removeed by Jupiter
        /// </scenarios>
        /// <param name="memorySize">Number of bytes in the memory removed.</param>
        [Event(TASKJUPITERREMOVEMEMORYPRESSURE_ID, Message = "Jupiter Remove memory pressure", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskJupiterRemoveMemoryPressure(long memorySize)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[1];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&memorySize));

                    WriteEventCore(TASKJUPITERREMOVEMEMORYPRESSURE_ID, 1, eventPayload);
                }
            }
        }
        #endregion TaskJupiterRemoveMemoryPressure
        #region TaskJupiterCreateManagedReference
        /// <summary>
        /// Fired when a new managed reference is created in Jupiter.
        /// </summary>
        /// <scenarios>
        /// - Monitoring the frequency of managed 'proxies' being created/used.
        /// </scenarios>
        /// <param name="IUnknown">Base address that unique identify the Jupiter.</param>
        /// <param name="objectType">Jupiter type.</param>
        [Event(TASKJUPITERCREATEMANAGEDREFERENCE_ID, Message = "Jupiter create managed reference", Level = EventLevel.Verbose, Keywords = Keywords.Interop)]
        public void TaskJupiterCreateManagedReference(long IUnknown, long objectType)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Interop))
            {
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[2];

                    eventPayload[0].Size = sizeof(long);
                    eventPayload[0].DataPointer = ((IntPtr)(&IUnknown));
                    eventPayload[1].Size = sizeof(long);
                    eventPayload[1].DataPointer = ((IntPtr)(&objectType));

                    WriteEventCore(TASKJUPITERCREATEMANAGEDREFERENCE_ID, 2, eventPayload);
                }
            }
        }
        #endregion TaskJupiterCreateManagedReference
        #endregion JupiterProvider

    }   //Class InteropEventProvider
}
