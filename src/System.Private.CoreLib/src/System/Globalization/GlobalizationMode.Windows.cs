// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Globalization
{
    internal static partial class GlobalizationMode
    {
        private static bool GetGlobalizationInvariantMode()
        {
            // CORERT-TODO: Enable System.Globalization.Invariant switch
            // return CLRConfig.GetBoolValue(c_InvariantModeConfigSwitch);
            return false;
        }
    }
}
