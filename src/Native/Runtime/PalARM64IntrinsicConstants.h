// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef PAL_ARM64INTRINSICCONSTANTS_INCLUDED
#define PAL_ARM64INTRINSICCONSTANTS_INCLUDED

#if defined(HOST_ARM64)
// Should match the constants defined in the compiler in HardwareIntrinsicHelpers.Aot.cs
enum ARM64IntrinsicConstants
{
    ARM64IntrinsicConstants_ArmBase = 0x0001,
    ARM64IntrinsicConstants_ArmBase_Arm64 = 0x0002,
    ARM64IntrinsicConstants_AdvSimd = 0x0004,
    ARM64IntrinsicConstants_AdvSimd_Arm64 = 0x0008,
    ARM64IntrinsicConstants_Aes = 0x0010,
    ARM64IntrinsicConstants_Crc32 = 0x0020,
    ARM64IntrinsicConstants_Crc32_Arm64 = 0x0040,
    ARM64IntrinsicConstants_Sha1 = 0x0080,
    ARM64IntrinsicConstants_Sha256 = 0x0100,
    ARM64IntrinsicConstants_Atomics = 0x0200,
    ARM64IntrinsicConstants_Vector64 = 0x0400,
    ARM64IntrinsicConstants_Vector128 = 0x0800
};

#endif //defined(HOST_ARM64)

#endif //!PAL_ARM64INTRINSICCONSTANTS_INCLUDED
