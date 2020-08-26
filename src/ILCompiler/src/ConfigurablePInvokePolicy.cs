// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

                if (importModule == "kernel32.dll")
                {
                    if (methodName == "WaitForMultipleObjectsEx" ||
                        methodName == "WaitForSingleObject" ||
                        methodName == "Sleep" ||
                        methodName == "CloseHandle" ||
                        methodName == "CreateEventW" ||
                        methodName == "CreateEventExW" ||
                        methodName == "SetEvent" ||
                        methodName == "ResetEvent")
                    {
                        return true;
                    }
                }

                if (importModule == "ole32.dll")
                {
                    if (methodName == "CoGetApartmentType" ||
                        methodName == "CoInitializeEx" ||
                        methodName == "CoUninitialize")
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                // Account for System.Private.CoreLib.Native / System.Globalization.Native / System.Native / etc
                // TODO: Remove "System." prefix - temporary workaround for https://github.com/dotnet/corert/issues/8241
                return importModule.StartsWith("libSystem.") || importModule.StartsWith("System.");
            }
        }
    }
}
