// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace ReadyToRun.TestHarness
{
    //
    // Intercept module loads for assemblies we want to collect method Jit info for.
    // Each Method that gets Jitted from a ready-to-run assembly is interesting to look at.
    // For a fully r2r'd assembly, there should be no such methods, so that would be a test failure.
    //
    class ReadyToRunJittedMethods
    {
        private ICollection<string> _testModuleNames;
        private List<long> _testModuleIds = new List<long>();
        private List<(string, bool)> _methodsJitted = new List<(string name, bool readyToRunRejected)>();

        public ReadyToRunJittedMethods(TraceEventSession session, ICollection<string> testModuleNames)
        {
            _testModuleNames = testModuleNames;
            
            session.Source.Clr.LoaderModuleLoad += delegate(ModuleLoadUnloadTraceData data)
            {
                if (_testModuleNames.Contains(data.ModuleILPath) || _testModuleNames.Contains(data.ModuleNativePath))
                {
                    Console.WriteLine($"Tracking module {data.ModuleILFileName} with Id {data.ModuleID}");
                    _testModuleIds.Add(data.ModuleID);
                }
            };
            
            session.Source.Clr.MethodLoadVerbose += delegate (MethodLoadUnloadVerboseTraceData data)
            {
                if (_testModuleIds.Contains(data.ModuleID) && data.IsJitted)
                {
                    Console.WriteLine($"Method loaded {GetName(data)} - {data}");
                    _methodsJitted.Add((GetName(data), ((int)data.MethodFlags & 0x40) != 0));
                }
            };
        }

        public IEnumerable<(string MethodName, bool ReadyToRunRejected)> JittedMethods => _methodsJitted;

        /// <summary>
        /// Returns the number of test assemblies that were loaded by the runtime
        /// </summary>
        public int AssembliesWithEventsCount => _testModuleIds.Count;

        //
        // Builds a method name from event data of the form Class.Method(arg1, arg2)
        //
        private static string GetName(MethodLoadUnloadVerboseTraceData data)
        {
            var signature = "";
            var signatureWithReturnType = data.MethodSignature;
            var openParenIndex = signatureWithReturnType.IndexOf('(');
            
            if (0 <= openParenIndex)
            {
                signature = signatureWithReturnType.Substring(openParenIndex);
            }

            var className = data.MethodNamespace;
            var lastDot = className.LastIndexOf('.');
            if (0 <= lastDot)
            {
                className = className.Substring(lastDot + 1);
            }

            var optionalSeparator = ".";
            if (className.Length == 0)
            {
                optionalSeparator = "";
            }

            return className + optionalSeparator + data.MethodName + signature;
        }
    }
}