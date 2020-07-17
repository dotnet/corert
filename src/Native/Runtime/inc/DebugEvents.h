// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// -----------------------------------------------------------------------------------------------------------
// This defines the payload of debug events that are emited by Redhawk runtime and
// received by the debugger. These payloads are referenced by 1st chance SEH exceptions


// -----------------------------------------------------------------------------------------------------------
// This version of holder does not have a default constructor.
#ifndef __DEBUG_EVENTS_H_
#define __DEBUG_EVENTS_H_

// Special Exception code for RH to communicate to debugger
// RH will raise this exception to communicate managed debug events.
// Exception codes can't use bit 0x10000000, that's reserved by OS.
// NOTE: This is intentionally different than CLR's exception code (0x04242420)
// Perhaps it is because now we are in building 40? Who would know
#define CLRDBG_NOTIFICATION_EXCEPTION_CODE  ((int) 0x04040400)

// This is exception argument 0 included in debugger notification events. 
// The debugger uses this as a sanity check.
// This could be very volatile data that changes between builds.
// NOTE: Again intentionally different than CLR's checksum (0x31415927)
//       It doesn't have to be, but if anyone is manually looking at these
//       exception payloads I am trying to make it obvious that they aren't
//       the same.
#define CLRDBG_EXCEPTION_DATA_CHECKSUM ((int) 0x27182818)

typedef enum 
{
    DEBUG_EVENT_TYPE_INVALID = 0,
    DEBUG_EVENT_TYPE_LOAD_MODULE = 1,
    DEBUG_EVENT_TYPE_UNLOAD_MODULE = 2,
    DEBUG_EVENT_TYPE_EXCEPTION_THROWN = 3,
    DEBUG_EVENT_TYPE_EXCEPTION_FIRST_PASS_FRAME_ENTER = 4,
    DEBUG_EVENT_TYPE_EXCEPTION_CATCH_HANDLER_FOUND = 5,
    DEBUG_EVENT_TYPE_EXCEPTION_UNHANDLED = 6,
    DEBUG_EVENT_TYPE_CUSTOM = 7,
    DEBUG_EVENT_TYPE_MAX = 8
} DebugEventType;

typedef unsigned int ULONG32;

struct DebugEventPayload
{
    DebugEventType type;
    union
    {
        struct 
        {
            CORDB_ADDRESS pModuleHeader; //ModuleHeader*
        } ModuleLoadUnload;
        struct
        {
            CORDB_ADDRESS ip;
            CORDB_ADDRESS sp;
        } Exception;
        struct
        {
            CORDB_ADDRESS payload;
            ULONG32 length;
        } Custom;
    };
};


#endif // __DEBUG_EVENTS_H_
