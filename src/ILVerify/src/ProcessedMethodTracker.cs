// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Linq;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using static System.Console;

namespace ILVerify
{
    internal class ProcessedMethodTracker
    {
        private class MethodResult
        {
            public MethodDesc Method;
            public string Name;
            public bool Verified;
        };
        private class ModuleResult {
            public IList<MethodResult> Methods = new List<MethodResult>();
        }

        private bool _verbose;
        private bool _printStatistics;
        IDictionary<EcmaModule, ModuleResult> _statistics = new Dictionary<EcmaModule, ModuleResult>();

        public ProcessedMethodTracker(bool verbose, bool printStatistics)
        {
            _verbose = verbose;
            _printStatistics = printStatistics;
        }

        private bool IsEnabled { get { return _verbose || _printStatistics; } }

        internal void NotifyMethodProcessing(EcmaModule module, MethodDefinitionHandle methodHandle, string methodName, bool verifying)
        {
            if (!IsEnabled)
                return;

            var method = module.GetMethod(methodHandle);
            ModuleResult result = GetOrCreateModuleResult(module);
            result.Methods.Add(new MethodResult() { Method = method, Name = methodName, Verified = verifying });

            if (_verbose)
            {
                if (verifying)
                    Write($"Verifying ");
                else
                    Write($"Skipping ");

                WriteLine(methodName);
            }
        }

        private ModuleResult GetOrCreateModuleResult(EcmaModule module)
        {
            ModuleResult result;
            if (!_statistics.TryGetValue(module, out result))
            {
                result = new ModuleResult();
                _statistics.Add(module, new ModuleResult());
            }

            return result;
        }

        internal void PrintResult(EcmaModule module)
        {
            if (!IsEnabled)
                return;

            ModuleResult result = _statistics[module];
            if (_printStatistics)
            {
                WriteLine($"Methods found: {result.Methods.Count}");
                WriteLine($"Methods verified: {result.Methods.Count(m => m.Verified)}");
                WriteLine();
            }
        }
    }
}
