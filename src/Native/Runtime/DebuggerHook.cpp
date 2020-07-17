// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "gcrhinterface.h"
#include "DebuggerHook.h"
#include "DebugEventSource.h"

GVAL_IMPL_INIT(UInt32, g_numGcProtectionRequests, 0);

#ifndef DACCESS_COMPILE

/* static */ DebuggerProtectedBufferListNode* DebuggerHook::s_debuggerProtectedBuffers = nullptr;

/* static */ DebuggerOwnedHandleListNode* DebuggerHook::s_debuggerOwnedHandles = nullptr;

/* static */ UInt32 DebuggerHook::s_debuggeeInitiatedHandleIdentifier = 2;

/* static */ void DebuggerHook::OnBeforeGcCollection()
{
    if (g_numGcProtectionRequests > 0)
    {
        // The debugger has some requests with respect to GC protection.
        // Here we are allocating a buffer to store them
        DebuggerGcProtectionRequest* requests = new (nothrow) DebuggerGcProtectionRequest[g_numGcProtectionRequests];

        // Notifying the debugger the buffer is ready to use
        DebuggerGcProtectionResponse response;
        response.kind = DebuggerResponseKind::RequestBufferReady;
        response.bufferAddress = (uint64_t)requests;
        DebugEventSource::SendCustomEvent((void*)&response, sizeof(response));

        // ... debugger magic happen here ...

        // The debugger has filled the requests array
        for (uint32_t i = 0; i < g_numGcProtectionRequests; i++)
        {
            if (requests[i].kind == DebuggerGcProtectionRequestKind::EnsureConservativeReporting)
            {
                // If the request requires extra memory, allocate for it
                requests[i].address = (uint64_t)new (nothrow) uint8_t[requests[i].size];

                // The debugger will handle the case when address is nullptr (we have to break our promise)
            }
        }

        // TODO, FuncEval, consider an optimization to eliminate this message when they is nothing required from the
        // debugger side to fill

        response.kind = DebuggerResponseKind::ConservativeReportingBufferReady;
        DebugEventSource::SendCustomEvent((void*)&response, sizeof(response));

        // ... debugger magic happen here again ...

        for (uint32_t i = 0; i < g_numGcProtectionRequests; i++)
        {
            DebuggerGcProtectionRequest* request = requests + i;
            switch(request->kind)
            {
            case DebuggerGcProtectionRequestKind::EnsureConservativeReporting: 
                EnsureConservativeReporting(request); 
                break;

            case DebuggerGcProtectionRequestKind::RemoveConservativeReporting:
                RemoveConservativeReporting(request);
                break;

            case DebuggerGcProtectionRequestKind::EnsureHandle:
                EnsureHandle(request);
                break;

            case DebuggerGcProtectionRequestKind::RemoveHandle:
                RemoveHandle(request);
                break;

            default:
                assert("Debugger is providing an invalid request kind." && false);
            }
        }

        g_numGcProtectionRequests = 0;
    }
}

/* static */ UInt32 DebuggerHook::RecordDebuggeeInitiatedHandle(void* objectHandle)
{
    DebuggerOwnedHandleListNode* head = new (nothrow) DebuggerOwnedHandleListNode();
    if (head == nullptr)
    {
        return 0;
    }

    head->handle = objectHandle;
    head->identifier = DebuggerHook::s_debuggeeInitiatedHandleIdentifier;
    head->next = s_debuggerOwnedHandles;
    s_debuggerOwnedHandles = head;

    s_debuggeeInitiatedHandleIdentifier += 2;

    return head->identifier;
}

/* static */ void DebuggerHook::EnsureConservativeReporting(DebuggerGcProtectionRequest* request)
{
    DebuggerProtectedBufferListNode* tail = DebuggerHook::s_debuggerProtectedBuffers;
    s_debuggerProtectedBuffers = new (std::nothrow) DebuggerProtectedBufferListNode();
    if (s_debuggerProtectedBuffers == nullptr)
    {
        s_debuggerProtectedBuffers = tail;
        // TODO, FuncEval, we cannot handle the debugger request to protect a buffer (we have to break our promise)
        // TODO, FuncEval, we need to figure out how to communicate this broken promise to the debugger
    }
    else
    {
        s_debuggerProtectedBuffers->address = request->address;
        s_debuggerProtectedBuffers->size = request->size;
        s_debuggerProtectedBuffers->identifier = request->identifier;
        s_debuggerProtectedBuffers->next = tail;
    }
}

/* static */ void DebuggerHook::RemoveConservativeReporting(DebuggerGcProtectionRequest* request)
{
    DebuggerProtectedBufferListNode* prev = nullptr;
    DebuggerProtectedBufferListNode* curr = DebuggerHook::s_debuggerProtectedBuffers;
    while (true)
    {
        if (curr == nullptr)
        {
            assert("Debugger is trying to remove a conservative reporting entry which is no longer exist." && false);
            break;
        }
        if (curr->identifier == request->identifier)
        {
            DebuggerProtectedBufferListNode* toDelete = curr;
            if (prev == nullptr)
            {
                // We are trying to remove the head of the linked list
                DebuggerHook::s_debuggerProtectedBuffers = curr->next;
            }
            else
            {
                prev->next = curr->next;
            }

            delete toDelete;
            break;
        }
        else
        {
            prev = curr;
            curr = curr->next;
        }
    }
}

/* static */ void DebuggerHook::EnsureHandle(DebuggerGcProtectionRequest* request)
{
    DebuggerOwnedHandleListNode* tail = DebuggerHook::s_debuggerOwnedHandles;
    s_debuggerOwnedHandles = new (std::nothrow) DebuggerOwnedHandleListNode();
    if (s_debuggerOwnedHandles == nullptr)
    {
        s_debuggerOwnedHandles = tail;
        // TODO, FuncEval, we cannot handle the debugger request to protect a buffer (we have to break our promise)
        // TODO, FuncEval, we need to figure out how to communicate this broken promise to the debugger
    }
    else
    {
        int handleType = (int)request->type;
        void* handle = RedhawkGCInterface::CreateTypedHandle((void*)request->address, handleType);

        DebuggerGcProtectionHandleReadyResponse response;
        response.kind = DebuggerResponseKind::HandleReady;
        response.payload = request->payload;
        response.handle = (uint64_t)handle;
        DebugEventSource::SendCustomEvent((void*)&response, sizeof(response));

        s_debuggerOwnedHandles->handle = handle;
        s_debuggerOwnedHandles->identifier = request->identifier;
        s_debuggerOwnedHandles->next = tail;
    }
}

/* static */ void DebuggerHook::RemoveHandle(DebuggerGcProtectionRequest* request)
{
    DebuggerOwnedHandleListNode* prev = nullptr;
    DebuggerOwnedHandleListNode* curr = DebuggerHook::s_debuggerOwnedHandles;
    while (true)
    {
        if (curr == nullptr)
        {
            assert("Debugger is trying to remove a gc handle entry which is no longer exist." && false);
            break;
        }
        if (curr->identifier == request->identifier)
        {
            DebuggerOwnedHandleListNode* toDelete = curr;
            RedhawkGCInterface::DestroyTypedHandle(toDelete->handle);

            if (prev == nullptr)
            {
                // We are trying to remove the head of the linked list
                DebuggerHook::s_debuggerOwnedHandles = curr->next;
            }
            else
            {
                prev->next = curr->next;
            }

            delete toDelete;
            break;
        }
        else
        {
            prev = curr;
            curr = curr->next;
        }
    }
}

EXTERN_C REDHAWK_API UInt32 __cdecl RhpRecordDebuggeeInitiatedHandle(void* objectHandle)
{
    return DebuggerHook::RecordDebuggeeInitiatedHandle(objectHandle);
}

EXTERN_C REDHAWK_API void __cdecl RhpVerifyDebuggerCleanup()
{
    assert(DebuggerHook::s_debuggerOwnedHandles == nullptr);
    assert(DebuggerHook::s_debuggerProtectedBuffers == nullptr);
}

#endif // !DACCESS_COMPILE
