// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//
// Intrinsic
//

void System::Runtime::RuntimeImports::memmove_0(unsigned char *, unsigned char *, int)
{
    throw 42;
}

double System::Runtime::RuntimeImports::sqrt(double value)
{
    return sqrt(value);
}

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


#if 1
//
// unattributed, no body method
//
void System::Buffer::BlockCopy(class System::Array * src, int srcOfs, class System::Array * dst, int dstOfs, int count)
{
    // TODO: Argument validation
    memmove((uint8_t*)dst + 2 * sizeof(void*) + dstOfs, (uint8_t*)src + 2 * sizeof(void*) + srcOfs, count);
}
#endif

#if 0
typedef int (*pfnMain)(System::String__Array * args);

int Internal::Runtime::Loader::LoaderImage::Call(__int64 p, class System::String__Array * args)
{
    return ((pfnMain)p)(args);
}
#endif
