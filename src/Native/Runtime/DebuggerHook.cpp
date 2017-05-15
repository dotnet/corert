// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "CommonTypes.h"
#include "DebuggerHook.h"
#include "DebugEventSource.h"
#include "Debug.h"

GVAL_IMPL_INIT(UInt32, g_numGcProtectionRequests, 0);

#ifndef DACCESS_COMPILE

/* static */ DebuggerProtectedBufferList* DebuggerHook::s_debuggerProtectedBuffers = nullptr;

/* static */ void DebuggerHook::OnBeforeGcCollection()
{
    if (g_numGcProtectionRequests > 0)
    {
        // The debugger has some request with respect to GC protection.
        // Here we are allocating a buffer to store them
        GcProtectionRequest* requests = new (nothrow) GcProtectionRequest[g_numGcProtectionRequests];

        // TODO: We need to figure out how to communicate this broken promise to the debugger

        // Notifying the debugger the buffer is ready to use
        GcProtectionMessage command;
        command.commandCode = DebuggerGcProtectionMessage::RequestBufferReady;
        command.bufferAddress = (uint64_t)requests;
        DebugEventSource::SendCustomEvent((void*)&command, sizeof(command));

        // ... debugger magic happen here ...

        // The debugger has filled the requests array
        for (uint32_t i = 0; i < g_numGcProtectionRequests; i++)
        {
            if (requests[i].type == DebuggerGcProtectionRequestKind::ConservativeReporting)
            {
                // If the request requires extra memory, allocate for it
                requests[i].address = (uint64_t)new (nothrow) uint8_t[requests[i].size];

                // The debugger will handle the case when address is nullptr (we have to break our promise)
            }
        }

        command.commandCode = DebuggerGcProtectionMessage::ConservativeReportingBufferReady;
        DebugEventSource::SendCustomEvent((void*)&command, sizeof(command));

        // ... debugger magic happen here again ...

        for (uint32_t i = 0; i < g_numGcProtectionRequests; i++)
        {
            if (requests[i].type == DebuggerGcProtectionRequestKind::ConservativeReporting)
            {
                // TODO: Release them when there should be gone
                DebuggerProtectedBufferList* tail = DebuggerHook::s_debuggerProtectedBuffers;
                s_debuggerProtectedBuffers = new (std::nothrow) DebuggerProtectedBufferList();
                if (s_debuggerProtectedBuffers == nullptr)
                {
                    // TODO: We cannot handle the debugger request to protect a buffer (we have to break our promise)
                }
                else
                {
                    s_debuggerProtectedBuffers->address = requests[i].address;
                    s_debuggerProtectedBuffers->size = requests[i].size;
                    s_debuggerProtectedBuffers->next = tail;
                }
            }
        }

        g_numGcProtectionRequests = 0;
    }
}

#endif // !DACCESS_COMPILE