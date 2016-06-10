// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdarg.h>
#include <stdlib.h>
#include <stdint.h>

#include "dllexport.h"
#include "jitinterface.h"

typedef struct _GUID {
    unsigned int Data1;
    unsigned short Data2;
    unsigned short Data3;
    unsigned char Data4[8];
} GUID;

static const GUID JITEEVersionIdentifier = { /* 718c4238-2a85-45de-88ad-9b1fed806547 */
    0x718c4238,
    0x2a85,
    0x45de,
    { 0x88, 0xad, 0x9b, 0x1f, 0xed, 0x80, 0x65, 0x47 }
};

class Jit
{
public:
    virtual int __stdcall compileMethod(
        void* compHnd,
        void* methodInfo,
        unsigned flags,
        void* entryAddress,
        void* nativeSizeOfCode) = 0;

    virtual void clearCache() = 0;
    virtual unsigned isCacheCleanupRequired() = 0;
    virtual void ProcessShutdownWork(void* info) = 0;

    // The EE asks the JIT for a "version identifier". This represents the version of the JIT/EE interface.
    // If the JIT doesn't implement the same JIT/EE interface expected by the EE (because the JIT doesn't
    // return the version identifier that the EE expects), then the EE fails to load the JIT.
    // 
    virtual void getVersionIdentifier(GUID* versionIdentifier) = 0;
};

DLL_EXPORT int JitCompileMethod(
    CorInfoException **ppException,
    Jit * pJit, 
    void * thisHandle, 
    void ** callbacks,
    void* methodInfo,
    unsigned flags,
    void* entryAddress,
    void* nativeSizeOfCode)
{
    *ppException = nullptr;

    GUID versionId;
    pJit->getVersionIdentifier(&versionId);
    if (memcmp(&versionId, &JITEEVersionIdentifier, sizeof(GUID)) != 0)
        return 1;

    try
    {
        JitInterfaceWrapper jitInterfaceWrapper(thisHandle, callbacks);
        return pJit->compileMethod(&jitInterfaceWrapper, methodInfo, flags, entryAddress, nativeSizeOfCode);
    }
    catch (CorInfoException *pException)
    {
        *ppException = pException;
    }

    return 1;
}
