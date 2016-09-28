// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Cryptography
{
    internal static class Sha1
    {
        public static byte[] ComputeSha1(byte[] data)
        {
            // TODO - CORERT - Add a CoreLibNative_ComputeSha1() entrypoint to System.Private.CoreLib.Native
            //  and P/Invoke to that. 
            throw new NotImplementedException();
        }
    }
}
