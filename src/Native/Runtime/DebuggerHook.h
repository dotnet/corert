// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// -----------------------------------------------------------------------------------------------------------
// Support for evaluating expression in the debuggee during debugging
// -----------------------------------------------------------------------------------------------------------

#ifndef __DEBUGGER_HOOK_H__
#define __DEBUGGER_HOOK_H__

#include "common.h"
#include "CommonTypes.h"
#ifdef DACCESS_COMPILE
#include "CommonMacros.h"
#endif
#include "daccess.h"
#include "Debug.h"

#ifndef DACCESS_COMPILE

struct DebuggerProtectedBufferListNode
{
    UInt64 address;
    UInt16 size;
    UInt32 identifier;
    struct DebuggerProtectedBufferListNode* next;
};

struct DebuggerOwnedHandleListNode
{
    void* handle;
    UInt32 identifier;
    struct DebuggerOwnedHandleListNode* next;
};

class DebuggerHook
{
public:
    static void OnBeforeGcCollection();
    static UInt32 RecordDebuggeeInitiatedHandle(void* handle);
    static DebuggerProtectedBufferListNode* s_debuggerProtectedBuffers;
    static DebuggerOwnedHandleListNode* s_debuggerOwnedHandles;
private:
    static void EnsureConservativeReporting(DebuggerGcProtectionRequest* request);
    static void RemoveConservativeReporting(DebuggerGcProtectionRequest* request);
    static void EnsureHandle(DebuggerGcProtectionRequest* request);
    static void RemoveHandle(DebuggerGcProtectionRequest* request);
    static UInt32 s_debuggeeInitiatedHandleIdentifier;
};

#endif //!DACCESS_COMPILE

#endif // __DEBUGGER_HOOK_H__
