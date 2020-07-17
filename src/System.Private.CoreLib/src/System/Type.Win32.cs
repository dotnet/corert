// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.Runtime.Augments;

namespace System
{
    public abstract partial class Type
    {
        private static Exception GetCLSIDFromProgID(string progID, out Guid clsid)
        {
            int hr = Interop.mincore.CLSIDFromProgID(progID, out clsid);
            if (hr < 0)
                return RuntimeAugments.Callbacks.GetExceptionForHR(hr);
            return null;
        }
    }
}
