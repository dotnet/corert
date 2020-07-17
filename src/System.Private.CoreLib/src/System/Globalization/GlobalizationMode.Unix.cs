// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Globalization
{
    internal static partial class GlobalizationMode
    {
        private static bool GetGlobalizationInvariantMode()
        {
            // CORERT-TODO: Enable System.Globalization.Invariant switch
            // bool invariantEnabled = CLRConfig.GetBoolValue(c_InvariantModeConfigSwitch);
            bool invariantEnabled = false;
            if (!invariantEnabled)
            {
                // TARGET_WASM TODO: There's no TARGET_WASM build of LibICU. We may be able to cross-compile it ourselves.
#if TARGET_WASM
                return true;
#else
                if (Interop.Globalization.LoadICU() == 0)
                {
                    string message = "Couldn't find a valid ICU package installed on the system. " + 
                                    "Set the configuration flag System.Globalization.Invariant to true if you want to run with no globalization support.";
                    Environment.FailFast(message);
                }
#endif // !TARGET_WASM
            }
            return invariantEnabled;
        }

        internal static bool UseNls => false;
    }
}
