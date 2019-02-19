// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler
{
    // TODO: make this actually configurable
    class ConfigurablePInvokePolicy : PInvokeILEmitterConfiguration
    {
        private readonly TargetDetails _target;

        public ConfigurablePInvokePolicy(TargetDetails target)
        {
            _target = target;
        }

        public override bool GenerateDirectCall(string importModule, string methodName)
        {
            // Determine whether this call should be made through a lazy resolution or a static reference
            // Eventually, this should be controlled by a custom attribute (or an extension to the metadata format).
            if (importModule == "[MRT]" || importModule == "*")
                return true;

            // Force link time symbol resolution for "__Internal" module for compatibility with Mono
            if (importModule == "__Internal")
                return true;

            if (_target.IsWindows)
            {
                // Force link time symbol resolution for PInvokes used on CoreLib startup path

                if (importModule.StartsWith("api-ms-win-"))
                    return true;

                if (importModule == "BCrypt.dll")
                {
                    if (methodName == "BCryptGenRandom")
                        return true;
                }

                return false;
            }
            else
            {
                // Account for System.Private.CoreLib.Native / System.Globalization.Native / System.Native / etc
                return importModule.StartsWith("System.");
            }
        }
    }
}
