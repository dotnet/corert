// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.



/*============================================================
**
  Type:  AssemblyNameHelpers
**
==============================================================*/

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Internal.Runtime.Augments;
using Buffer = System.Buffer;

using SecurityException = System.Security.SecurityException;

namespace System.Reflection.Runtime.Assemblies
{
    internal static partial class AssemblyNameHelpers
    {
        internal static byte[] ComputePublicKeyToken(byte[] publicKey)
        {
            if (publicKey == null)
                return null;

            if (publicKey.Length == 0)
                return Array.Empty<byte>();

            if (!IsValidPublicKey(publicKey))
                throw new SecurityException(SR.Security_InvalidAssemblyPublicKey);

#if CORERT
            // CORERT-TODO: ComputeSHA1
            return Array.Empty<byte>();
#else
            byte[] hash = WinRTInterop.Callbacks.ComputeSHA1(publicKey);
            byte[] pkt = new byte[PUBLIC_KEY_TOKEN_LEN];
            for (int i = 0; i < PUBLIC_KEY_TOKEN_LEN; i++)
            {
                pkt[i] = hash[hash.Length - i - 1];
            }
            return pkt;
#endif
        }

        //
        // This validation logic is a manual port of StrongNameIsValidPublicKey() in the desktop CLR (see clr\src\StrongName\api\StrongNameInternal.cpp)
        //
        private static bool IsValidPublicKey(byte[] publicKey)
        {
            uint publicKeyLength = (uint)(publicKey.Length);

            // The buffer must be at least as large as the public key structure (for compat with desktop, we actually compare with the size of the header + 4).
            if (publicKeyLength < SizeOfPublicKeyBlob + 4)
                return false;


            // Poor man's reinterpret_cast into the PublicKeyBlob structure.
            uint[] publicKeyBlob = new uint[3];
            Buffer.BlockCopy(publicKey, 0, publicKeyBlob, 0, (int)SizeOfPublicKeyBlob);
            uint sigAlgID = publicKeyBlob[0];
            uint hashAlgID = publicKeyBlob[1];
            uint cbPublicKey = publicKeyBlob[2];

            // The buffer must be the same size as the structure header plus the trailing key data
            if (cbPublicKey != publicKeyLength - SizeOfPublicKeyBlob)
                return false;

            // The buffer itself looks reasonable, but the public key structure needs to be validated as well

            // The ECMA key doesn't look like a valid key so it will fail the below checks. If we were passed that
            // key, then we can skip them.
            if (ByteArrayEquals(publicKey, s_ecmaKey))
                return true;

            // If a hash algorithm is specified, it must be a sensible value
            bool fHashAlgorithmValid = GetAlgClass(hashAlgID) == ALG_CLASS_HASH && GetAlgSid(hashAlgID) == ALG_SID_SHA1;
            if (hashAlgID != 0 && !fHashAlgorithmValid)
                return false;

            // If a signature algorithm is specified, it must be a sensible value
            bool fSignatureAlgorithmValid = GetAlgClass(sigAlgID) == ALG_CLASS_SIGNATURE;
            if (sigAlgID != 0 && !fSignatureAlgorithmValid)
                return false;

            // The key blob must indicate that it is a PUBLICKEYBLOB
            if (publicKey[SizeOfPublicKeyBlob] != PUBLICKEYBLOB)
                return false;

            //@todo: Desktop also tries to import the public key blob using the Crypto api as further validation - not clear if there's any non-banned API to do this.

            return true;
        }

        private static bool ByteArrayEquals(byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length)
                return false;
            for (int i = 0; i < b1.Length; i++)
            {
                if (b1[i] != b2[i])
                    return false;
            }
            return true;
        }

        // Constants and macros copied from WinCrypt.h:

        private static uint GetAlgClass(uint x)
        {
            return (x & (7 << 13));
        }

        private static uint GetAlgSid(uint x)
        {
            return (x & (511));
        }

        private const uint ALG_CLASS_HASH = (4 << 13);
        private const uint ALG_SID_SHA1 = 4;
        private const uint ALG_CLASS_SIGNATURE = (1 << 13);
        private const uint PUBLICKEYBLOB = 0x6;

        private const uint SizeOfPublicKeyBlob = 12;

        private const int PUBLIC_KEY_TOKEN_LEN = 8;

        private static byte[] s_ecmaKey =
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
    }
}


