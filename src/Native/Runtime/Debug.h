// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

// The following definitions are required for interop with the VS Debugger
// Prior to making any changes to these, please reach out to the VS Debugger 
// team to make sure that your changes are not going to prevent the debugger
// from working.

enum FuncEvalMode : uint32_t
{
    RegularFuncEval = 1,
    NewStringWithLength = 2,
};

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

struct DebuggerResponse
{
    int kind;
};

enum DebuggerResponseKind : uint32_t
{
    FuncEvalCompleteWithReturn       = 0,
    FuncEvalParameterBufferReady     = 1,
    RequestBufferReady               = 2,
    ConservativeReportingBufferReady = 3,
    HandleReady                      = 4,
};

struct DebuggerGcProtectionResponse
{
    DebuggerResponseKind kind;
    uint32_t padding;
    uint64_t bufferAddress;
};

struct DebuggerGcProtectionHandleReadyResponse
{
    DebuggerResponseKind kind;
    uint32_t padding;
    uint64_t payload;
    uint64_t handle;
};

struct DebuggerFuncEvalCompleteWithReturnResponse
{
    DebuggerResponseKind kind;
    uint32_t returnHandleIdentifier;
    uint64_t returnAddress;
};

struct DebuggerFuncEvalParameterBufferReadyResponse
{
    DebuggerResponseKind kind;
    uint32_t padding;
    uint64_t bufferAddress;
};
