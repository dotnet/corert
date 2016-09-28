// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Internal.Cryptography
{
    internal static class Sha1
    {
        public static byte[] ComputeSha1(byte[] data)
        {
            SafeBCryptAlgorithmHandle sha1AlgorithmHandle = Sha1AlgorithmHandle;  // Do not dispose - this is a shared handle.
            SafeBCryptHashHandle hHash;
            NTSTATUS nts = BCryptCreateHash(sha1AlgorithmHandle, out hHash, IntPtr.Zero, 0, null, 0, 0);
            if (nts != NTSTATUS.STATUS_SUCCESS)
                throw new InvalidOperationException(SR.Format(SR.CryptoError_Sha1, nts));
            using (hHash)
            {
                nts = BCryptHashData(hHash, data, data.Length, 0);
                if (nts != NTSTATUS.STATUS_SUCCESS)
                    throw new InvalidOperationException(SR.Format(SR.CryptoError_Sha1, nts));

                byte[] hash = new byte[Sha1HashSize];
                nts = BCryptFinishHash(hHash, hash, hash.Length, 0);
                if (nts != NTSTATUS.STATUS_SUCCESS)
                    throw new InvalidOperationException(SR.Format(SR.CryptoError_Sha1, nts));

                return hash;
            }
        }

        private static SafeBCryptAlgorithmHandle Sha1AlgorithmHandle
        {
            get
            {
                SafeBCryptAlgorithmHandle hAlgorithm = s_lazySha1AlgorithmHandle;
                if (hAlgorithm == null)
                {
                    NTSTATUS nts = BCryptOpenAlgorithmProvider(out hAlgorithm, BCRYPT_SHA1_ALGORITHM, null, 0);
                    if (nts != NTSTATUS.STATUS_SUCCESS)
                        throw new InvalidOperationException(SR.Format(SR.CryptoError_Sha1, nts));

                    s_lazySha1AlgorithmHandle = hAlgorithm;
                }
                return hAlgorithm;
            }
        }

        private static volatile SafeBCryptAlgorithmHandle s_lazySha1AlgorithmHandle;

        private const int Sha1HashSize = 20;
        private const string BCRYPT_SHA1_ALGORITHM = "SHA1";

        [DllImport(Interop.Libraries.BCrypt, CharSet = CharSet.Unicode)]
        private static extern NTSTATUS BCryptOpenAlgorithmProvider(out SafeBCryptAlgorithmHandle phAlgorithm, string pszAlgId, string pszImplementation, int dwFlags);

        [DllImport(Interop.Libraries.BCrypt, CharSet = CharSet.Unicode)]
        private static extern NTSTATUS BCryptCreateHash(SafeBCryptAlgorithmHandle hAlgorithm, out SafeBCryptHashHandle phHash, IntPtr pbHashObject, int cbHashObject, byte[] pbSecret, int cbSecret, int dwFlags);

        [DllImport(Interop.Libraries.BCrypt, CharSet = CharSet.Unicode)]
        private static extern NTSTATUS BCryptHashData(SafeBCryptHashHandle hHash, byte[] pbInput, int cbInput, int dwFlags);

        [DllImport(Interop.Libraries.BCrypt, CharSet = CharSet.Unicode)]
        private static extern NTSTATUS BCryptFinishHash(SafeBCryptHashHandle hHash, [Out] byte[] pbOutput, int cbOutput, int dwFlags);

        private enum NTSTATUS : uint
        {
            STATUS_SUCCESS = 0x0,
            STATUS_NOT_FOUND = 0xc0000225,
            STATUS_INVALID_PARAMETER = 0xc000000d,
            STATUS_NO_MEMORY = 0xc0000017,
        }

        private sealed class SafeBCryptAlgorithmHandle : SafeHandle
        {
            private SafeBCryptAlgorithmHandle() : base(IntPtr.Zero, true) {}

            public sealed override bool IsInvalid => handle == IntPtr.Zero;

            protected sealed override bool ReleaseHandle()
            {
                NTSTATUS ntStatus = BCryptCloseAlgorithmProvider(handle, 0);
                SetHandle(IntPtr.Zero);
                return ntStatus == NTSTATUS.STATUS_SUCCESS;
            }

            [DllImport(Interop.Libraries.BCrypt, CharSet = CharSet.Unicode)]
            private static extern NTSTATUS BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, int dwFlags);
        }

        private sealed class SafeBCryptHashHandle : SafeHandle
        {
            private SafeBCryptHashHandle() : base(IntPtr.Zero, true) {}

            public sealed override bool IsInvalid => handle == IntPtr.Zero;

            protected sealed override bool ReleaseHandle()
            {
                NTSTATUS ntStatus = BCryptDestroyHash(handle);
                SetHandle(IntPtr.Zero);
                return ntStatus == NTSTATUS.STATUS_SUCCESS;
            }

            [DllImport(Interop.Libraries.BCrypt, CharSet = CharSet.Unicode)]
            private static extern NTSTATUS BCryptDestroyHash(IntPtr hHash);
        }
    }
}
