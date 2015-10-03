// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//
// BoundsChecking
//
void ThrowRangeOverflowException();
unsigned short System::String::get_Chars(class System::String *pString, int index)
{
    if ((uint32_t)index >= (uint32_t)pString->m_stringLength)
        ThrowRangeOverflowException();
    return *(&pString->m_firstChar + index);
}

//
// unattributed, no body method
//
void System::Buffer::BlockCopy(class System::Array * src, int srcOfs, class System::Array * dst, int dstOfs, int count)
{
    // TODO: Argument validation
    memmove((uint8_t*)dst + 2 * sizeof(void*) + dstOfs, (uint8_t*)src + 2 * sizeof(void*) + srcOfs, count);
}
