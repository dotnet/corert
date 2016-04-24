// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "corinfoexception.h"
#include "dllexport.h"

typedef struct _GUID {
    unsigned int Data1;
    unsigned short Data2;
    unsigned short Data3;
    unsigned char Data4[8];
} GUID;

static const GUID JITEEVersionIdentifier = { /* 27626524-7315-4ed0-b74e-a0e4579883bb */
    0x27626524, 
    0x7315, 
    0x4ed0, 
    { 0xb7, 0x4e, 0xa0, 0xe4, 0x57, 0x98, 0x83, 0xbb }
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

    GUID versionId;
    pJit->getVersionIdentifier(&versionId);
    if (memcmp(&versionId, &JITEEVersionIdentifier, sizeof(GUID)) != 0)
        return 1;

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
