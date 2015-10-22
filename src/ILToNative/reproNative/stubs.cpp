// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using namespace System_Private_CoreLib;

//
// unattributed, no body method
//
void System::Buffer::BlockCopy(class System::Array * src, int srcOfs, class System::Array * dst, int dstOfs, int count)
{
    // TODO: Argument validation
    memmove((uint8_t*)dst + 2 * sizeof(void*) + dstOfs, (uint8_t*)src + 2 * sizeof(void*) + srcOfs, count);
}
