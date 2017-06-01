// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
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

#ifndef DACCESS_COMPILE

struct DebuggerProtectedBufferList
{
    UInt64 address;
    UInt16 size;
    UInt32 identifier;
    struct DebuggerProtectedBufferList* next;
};

struct DebuggerOwnedHandleList
{
    void* handle;
    UInt32 identifier;
    struct DebuggerOwnedHandleList* next;
};

class DebuggerHook
{
public:
    static void OnBeforeGcCollection();
    static UInt32 RecordDebuggeeInitiatedHandle(void* handle);
    static DebuggerProtectedBufferList* s_debuggerProtectedBuffers;
    static DebuggerOwnedHandleList* s_debuggerOwnedHandleList;
    static UInt32 s_debuggeeInitiatedHandleIdentifier;
};

#endif //!DACCESS_COMPILE

#endif // __DEBUGGER_HOOK_H__