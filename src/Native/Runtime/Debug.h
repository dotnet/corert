// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

enum DebuggerGcProtectionRequestKind : uint16_t
{
    EnsureConservativeReporting = 1,
    RemoveConservativeReporting = 2,
    EnsureHandle = 3,
    RemoveHandle = 4
};

/**
 * This structure represents a request from the debugger to perform a GC protection related work.
 */
struct DebuggerGcProtectionRequest
{
    DebuggerGcProtectionRequestKind kind;
    union
    {
        uint16_t size;
        uint16_t type;
    };
    uint32_t identifier;
    uint64_t address;
    uint64_t payload; /* TODO, FuncEval, what would be a better name for this? */
};

enum DebuggerGcProtectionResponseKind : uint32_t
{
    RequestBufferReady               = 2,
    ConservativeReportingBufferReady = 3,
    HandleReady                      = 4,
};

struct DebuggerGcProtectionResponse
{
    DebuggerGcProtectionResponseKind kind;
    uint32_t unused; /* To make the data structure 64 bit aligned */
    uint64_t bufferAddress;
};

struct DebuggerGcProtectionHandleReadyResponse
{
    DebuggerGcProtectionResponseKind kind;
    uint32_t unused; /* To make the data structure 64 bit aligned */
    uint64_t payload;
    uint64_t handle;
};
