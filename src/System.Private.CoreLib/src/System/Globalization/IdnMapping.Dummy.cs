// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Globalization
{
    public sealed partial class IdnMapping
    {
        private unsafe string GetAsciiCore(char* unicode, int count)
        {
            throw new NotImplementedException();
        }

        private unsafe string GetUnicodeCore(char* ascii, int count)
        {
            throw new NotImplementedException();
        }

        private unsafe string GetUnicodeCore(char* ascii, int count, uint flags, char* output, int outputLength, bool reattempt)
        {
            throw new NotImplementedException();
        }

        // -----------------------------
        // ---- PAL layer ends here ----
        // -----------------------------
    }
}
