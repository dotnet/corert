//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#include "corinfoexception.h"

class Jit
{
public:
    virtual int compileMethod(
        void* compHnd,
        void* methodInfo,
        unsigned flags,
        void* entryAddress,
        void* nativeSizeOfCode) = 0;
};

extern "C" int JitWrapper(
    CorInfoException **ppException,
    Jit* pJit,
    void* compHnd,
    void* methodInfo,
    unsigned flags,
    void* entryAddress,
    void* nativeSizeOfCode)
{
    *ppException = nullptr;

    try
    {
        return pJit->compileMethod(compHnd, methodInfo, flags, entryAddress, nativeSizeOfCode);
    }
    catch (CorInfoException *pException)
    {
        *ppException = pException;
    }

    return 1;
}
