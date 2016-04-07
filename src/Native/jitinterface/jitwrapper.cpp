// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "corinfoexception.h"
#include "dllexport.h"

class Jit
{
public:
    virtual int __stdcall compileMethod(
        void* compHnd,
        void* methodInfo,
        unsigned flags,
        void* entryAddress,
        void* nativeSizeOfCode) = 0;
};

DLL_EXPORT int JitWrapper(
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
