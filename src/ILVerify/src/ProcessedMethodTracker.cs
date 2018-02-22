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
        private class ModuleResult
        {
            public int MethodCounter;
            public int VerifiedMethodCounter;
        }

        private bool _verbose;
        private bool _printStatistics;
        IDictionary<EcmaModule, ModuleResult> _statistics = new Dictionary<EcmaModule, ModuleResult>();

        public ProcessedMethodTracker(bool verbose, bool printStatistics)
        {
            _verbose = verbose;
            _printStatistics = printStatistics;
        }

        internal void NotifyMethodProcessing(EcmaModule module, MethodDefinitionHandle methodHandle, string methodName, bool verifying)
        {
            if (_printStatistics)
            {
                var method = module.GetMethod(methodHandle);
                ModuleResult result = GetOrCreateModuleResult(module);
                if (verifying)
                    result.VerifiedMethodCounter++;
                result.MethodCounter++;
            }

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
            if (_printStatistics)
            {
                ModuleResult result = _statistics[module];
                WriteLine($"Methods found: {result.MethodCounter}");
                WriteLine($"Methods verified: {result.VerifiedMethodCounter}");
                WriteLine();
            }
        }
    }
}
