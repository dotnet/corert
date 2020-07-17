// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Unmanaged helpers for strong name parsing.
//

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "sha1.h"

//
// Converts a public key into a public key token, by computing the SHA1 of the public key, then taking the last 8 bytes in reverse order.
//
// The only legal value for "cbPublicKeyTokenOut" is 8 - this parameter exists as defense in depth.
//

#define PUBLIC_KEY_TOKEN_LEN 8

COOP_PINVOKE_HELPER(void, RhConvertPublicKeyToPublicKeyToken, (const UInt8* pbPublicKey, int cbPublicKey, UInt8 *pbPublicKeyTokenOut, int cbPublicKeyTokenOut))
{
    ASSERT(pbPublicKey != NULL);
    ASSERT(pbPublicKeyTokenOut != NULL);

    if (cbPublicKeyTokenOut != PUBLIC_KEY_TOKEN_LEN)
    {
        RhFailFast();
    }

    SHA1Hash sha1;
    sha1.AddData(pbPublicKey, cbPublicKey);
    UInt8* pHash = sha1.GetHash();

    for (int i = 0; i < PUBLIC_KEY_TOKEN_LEN; i++)
    {
        pbPublicKeyTokenOut[i] = pHash[SHA1_HASH_SIZE - i - 1];
    }

    return;
}

