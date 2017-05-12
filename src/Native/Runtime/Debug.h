// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

enum DebuggerGcProtectionMessage : uint32_t
{
    RequestBufferReady               = 2,
    ConservativeReportingBufferReady = 3,
};

enum DebuggerGcProtectionRequestKind : uint16_t
{
	ConservativeReporting = 1,
};

struct GcProtectionMessage
{
    DebuggerGcProtectionMessage commandCode;
    uint32_t unused; /* To make the data structure 64 bit aligned */
    uint64_t bufferAddress;
};

struct GcProtectionRequest
{
    DebuggerGcProtectionRequestKind type;
    uint16_t size;
    uint64_t address;
};
