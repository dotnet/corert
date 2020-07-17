// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System
{
    public abstract partial class Type
    {
        private static Exception GetCLSIDFromProgID(string progID, out Guid clsid)
        {
            throw new PlatformNotSupportedException();
        }
    }
}

