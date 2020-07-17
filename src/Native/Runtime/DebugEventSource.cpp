// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "type_traits.hpp"
#include "slist.h"
#include "holder.h"
#include "Crst.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "gcrhinterface.h"
#include "shash.h"
#include "DebugEventSource.h"

#include "slist.inl"

#include "DebugEvents.h"

GVAL_IMPL_INIT(UInt32, g_DebuggerEventsFilter, 0);

#ifndef DACCESS_COMPILE

bool EventEnabled(DebugEventType eventType)
{
    return ((int)eventType > 0) && 
           ((g_DebuggerEventsFilter & (1 << ((int)eventType-1))) != 0);
}

void DebugEventSource::SendModuleLoadEvent(void* pAddressInModule)
{
    if(!EventEnabled(DEBUG_EVENT_TYPE_LOAD_MODULE))
        return;
    DebugEventPayload payload;
    payload.type = DEBUG_EVENT_TYPE_LOAD_MODULE;
    payload.ModuleLoadUnload.pModuleHeader = (CORDB_ADDRESS)pAddressInModule;
    SendRawEvent(&payload);
}

void DebugEventSource::SendExceptionThrownEvent(CORDB_ADDRESS faultingIP, CORDB_ADDRESS faultingFrameSP)
{
    if(!EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_THROWN))
        return;
    DebugEventPayload payload;
    payload.type = DEBUG_EVENT_TYPE_EXCEPTION_THROWN;
    payload.Exception.ip = faultingIP;
    payload.Exception.sp = faultingFrameSP;
    SendRawEvent(&payload);
}

void DebugEventSource::SendExceptionCatchHandlerFoundEvent(CORDB_ADDRESS handlerIP, CORDB_ADDRESS HandlerFrameSP)
{
    if(!EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_CATCH_HANDLER_FOUND))
        return;
    DebugEventPayload payload;
    payload.type = DEBUG_EVENT_TYPE_EXCEPTION_CATCH_HANDLER_FOUND;
    payload.Exception.ip = handlerIP;
    payload.Exception.sp = HandlerFrameSP;
    SendRawEvent(&payload);
}

void DebugEventSource::SendExceptionUnhandledEvent()
{
    if(!EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_UNHANDLED))
        return;
    DebugEventPayload payload;
    payload.type = DEBUG_EVENT_TYPE_EXCEPTION_UNHANDLED;
    payload.Exception.ip = (CORDB_ADDRESS)0;
    payload.Exception.sp = (CORDB_ADDRESS)0;
    SendRawEvent(&payload);
}

void DebugEventSource::SendExceptionFirstPassFrameEnteredEvent(CORDB_ADDRESS ipInFrame, CORDB_ADDRESS frameSP)
{
    if(!EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_FIRST_PASS_FRAME_ENTER))
        return;
    DebugEventPayload payload;
    payload.type = DEBUG_EVENT_TYPE_EXCEPTION_FIRST_PASS_FRAME_ENTER;
    payload.Exception.ip = ipInFrame;
    payload.Exception.sp = frameSP;
    SendRawEvent(&payload);
}

void DebugEventSource::SendCustomEvent(void* payload, int length)
{
    if (!EventEnabled(DEBUG_EVENT_TYPE_CUSTOM))
        return;
    DebugEventPayload rawPayload;
    rawPayload.type = DEBUG_EVENT_TYPE_CUSTOM;
    rawPayload.Custom.payload = (CORDB_ADDRESS)payload;
    rawPayload.Custom.length = length;
    SendRawEvent(&rawPayload);
}

//---------------------------------------------------------------------------------------
//
// Sends a raw managed debug event to the debugger.
//
// Arguments:
//      pPayload - managed debug event data
//
//
// Notes:
//    The entire process will get frozen by the debugger once we send.  The debugger
//    needs to resume the process. It may detach as well.
//    See CordbProcess::DecodeEvent in mscordbi for decoding this event. These methods must stay in sync.
//
//---------------------------------------------------------------------------------------
void DebugEventSource::SendRawEvent(DebugEventPayload* pPayload)
{
#ifdef _MSC_VER
    // We get to send an array of void* as data with the notification.
    // The debugger can then use ReadProcessMemory to read through this array.
    UInt64 rgData [] = {
        (UInt64) CLRDBG_EXCEPTION_DATA_CHECKSUM, 
        (UInt64) GetRuntimeInstance()->GetPalInstance(), 
        (UInt64) pPayload
    };

    //
    // Physically send the event via an OS Exception. We're using exceptions as a notification
    // mechanism on top of the OS native debugging pipeline.
    //
    __try
    {
        const UInt32 dwFlags = 0; // continuable (eg, Debugger can continue GH)
        // RaiseException treats arguments as pointer sized values, but we encoded 3 QWORDS.
        // On 32 bit platforms we have 6 elements, on 64 bit platforms we have 3 elements
        RaiseException(CLRDBG_NOTIFICATION_EXCEPTION_CODE, dwFlags, 3*sizeof(UInt64)/sizeof(UInt32*), (UInt32*)rgData);

        // If debugger continues "GH" (DBG_CONTINUE), then we land here. 
        // This is the expected path for a well-behaved ICorDebug debugger.
    }
    __except(1)
    {
        // We can get here if:
        // An ICorDebug aware debugger enabled the debug events AND
        // a) the debugger detached during the event OR
        // b) the debugger continues "GN" (DBG_EXCEPTION_NOT_HANDLED) - this would be considered a badly written debugger
        //
        // there is no great harm in reaching here but it is a needless perf-cost
    }
#endif // _MSC_VER
}

//keep these synced with the enumeration in exceptionhandling.cs
enum ExceptionEventKind
{
    EEK_Thrown=1,
    EEK_CatchHandlerFound=2,
    EEK_Unhandled=4,
    EEK_FirstPassFrameEntered=8
};

//Called by the C# exception dispatch code with events to send to the debugger
EXTERN_C REDHAWK_API void __cdecl RhpSendExceptionEventToDebugger(ExceptionEventKind eventKind, void* ip, void* sp)
{
    CORDB_ADDRESS cordbIP = (CORDB_ADDRESS)ip;
    CORDB_ADDRESS cordbSP = (CORDB_ADDRESS)sp;
#if HOST_ARM
    // clear the THUMB-bit from IP
    cordbIP &= ~1;
#endif

    if(eventKind == EEK_Thrown)
    {
        DebugEventSource::SendExceptionThrownEvent(cordbIP, cordbSP);
    }
    else if(eventKind == EEK_CatchHandlerFound)
    {
        DebugEventSource::SendExceptionCatchHandlerFoundEvent(cordbIP, cordbSP);
    }
    else if(eventKind == EEK_Unhandled)
    {
        DebugEventSource::SendExceptionUnhandledEvent();
    }
    else if(eventKind == EEK_FirstPassFrameEntered)
    {
        DebugEventSource::SendExceptionFirstPassFrameEnteredEvent(cordbIP, cordbSP);
    }
}

// Called to cache the current events the debugger is listening for in the C# implemented exception layer
// Filtering in managed code prevents making unneeded p/invokes
COOP_PINVOKE_HELPER(ExceptionEventKind, RhpGetRequestedExceptionEvents, ())
{
    int mask = 0;
    if(EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_THROWN))
        mask |= EEK_Thrown;
    if(EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_CATCH_HANDLER_FOUND))
        mask |= EEK_CatchHandlerFound;
    if(EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_UNHANDLED))
        mask |= EEK_Unhandled;
    if(EventEnabled(DEBUG_EVENT_TYPE_EXCEPTION_FIRST_PASS_FRAME_ENTER))
        mask |= EEK_FirstPassFrameEntered;
    return (ExceptionEventKind)mask;
}

//Called by the C# func eval code to hand shake with the debugger
COOP_PINVOKE_HELPER(void, RhpSendCustomEventToDebugger, (void* payload, int length))
{
    DebugEventSource::SendCustomEvent(payload, length);
}

#endif //!DACCESS_COMPILE
