// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "CommonTypes.h"
#include "DebuggerHook.h"
#include "DebugEventSource.h"

GVAL_IMPL_INIT(UInt32, g_numGcProtectionRequests, 0);

#ifndef DACCESS_COMPILE

// TODO: Tab to space, overall, just to make sure I will actually do it :)
// TODO: This structure needs to match with DBI
struct FuncEvalParameterCommand
{
    // TODO: Consider giving these command code a good enumeration to define what they really are
    UInt32 commandCode;
    UInt32 unused; /* To make the data structure 64 bit aligned */
    UInt64 bufferAddress;
};

// TODO: This structure needs to match with DBI
struct GcProtectionRequest
{
    UInt16 type;
    UInt16 size;
    UInt64 address;
};

/* static */ void DebuggerHook::OnBeforeGcCollection()
{
    if (g_numGcProtectionRequests > 0)
    {
        // The debugger has some request with respect to GC protection.
        // Here we are allocating a buffer to store them
        GcProtectionRequest* requests = new (nothrow) GcProtectionRequest[g_numGcProtectionRequests];

        // TODO: We need to figure out how to communicate this broken promise to the debugger

        // Notifying the debugger the buffer is ready to use
        FuncEvalParameterCommand command;
        command.commandCode = 2;
        command.bufferAddress = (uint64_t)requests;
        DebugEventSource::SendCustomEvent((void*)&command, sizeof(command));

        // ... debugger magic happen here ...

        // The debugger has filled the requests array
        for (uint32_t i = 0; i < g_numGcProtectionRequests; i++)
        {
            if (requests[i].type == 1)
            {
                // If the request requires extra memory, allocate for it
                requests[i].address = (uint64_t)new (nothrow) uint8_t[requests[i].size];
            }
        }

        command.commandCode = 3;
        DebugEventSource::SendCustomEvent((void*)&command, sizeof(command));

        // ... debugger magic happen here again ...

        for (uint32_t i = 0; i < g_numGcProtectionRequests; i++)
        {
            if (requests[i].type == 1)
            {
                // What shall I do?
            }
        }

        g_numGcProtectionRequests = 0;
    }
}

#endif // !DACCESS_COMPILE