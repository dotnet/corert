// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Unmanaged helpers for strong name parsing.
//

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "PalRedhawkCommon.h"

//
// Converts a public key into a public key token, by computing the SHA1 of the public key, then taking the last 8 bytes in reverse order.
//
// The only legal value for "cbPublicKeyTokenOut" is 8 - this parameter exists as defense in depth.
//
COOP_PINVOKE_HELPER(void, RhConvertPublicKeyToPublicKeyToken, (const char* pbPublicKey, int cbPublicKey, char *pbPublicKeyTokenOut, int cbPublicKeyTokenOut))
{
    ASSERT(pbPublicKey != NULL);
    ASSERT(pbPublicKeyTokenOut != NULL);
    ASSERT(!"RhConvertPublicKeyToPublicKeyToken not yet implemented.");

    // Nonsense code to keep C++ from complaining about unused parameters.
    pbPublicKeyTokenOut[cbPublicKeyTokenOut - 1] = pbPublicKey[cbPublicKey - 1];

    return;
}

