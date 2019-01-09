// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
        private ICollection<string> _testFolderNames;
        private List<long> _testModuleIds = new List<long>();
        private Dictionary<long, string> _testModuleIdToName = new Dictionary<long, string>();
        private Dictionary<string, HashSet<string>> _methodsJitted = new Dictionary<string, HashSet<string>>();
        private int _pid = -1;

        public ReadyToRunJittedMethods(TraceEventSession session, ICollection<string> testModuleNames, ICollection<string> testFolderNames)
        {
            _testModuleNames = testModuleNames;
            _testFolderNames = testFolderNames;

            session.Source.Clr.LoaderModuleLoad += delegate (ModuleLoadUnloadTraceData data)
            {
                if (ShouldMonitorModule(data))
                {
                    Console.WriteLine($"Tracking module {data.ModuleILFileName} with Id {data.ModuleID}");
                    _testModuleIds.Add(data.ModuleID);
                    _testModuleIdToName[data.ModuleID] = Path.GetFileNameWithoutExtension(data.ModuleILFileName);
                }
            };

            session.Source.Clr.MethodLoadVerbose += delegate (MethodLoadUnloadVerboseTraceData data)
            {
                if (data.ProcessID == _pid && _testModuleIds.Contains(data.ModuleID) && data.IsJitted)
                {
                    Console.WriteLine($"Method loaded {GetName(data)} - {data}");
                    string methodName = GetName(data);
                    string moduleName = _testModuleIdToName[data.ModuleID];
                    HashSet<string> modulesForMethodName;
                    if (!_methodsJitted.TryGetValue(methodName, out modulesForMethodName))
                    {
                        modulesForMethodName = new HashSet<string>();
                        _methodsJitted.Add(methodName, modulesForMethodName);
                    }
                    modulesForMethodName.Add(moduleName);
                }
            };
        }

        private bool ShouldMonitorModule(ModuleLoadUnloadTraceData data)
        {
            if (data.ProcessID != _pid)
                return false;

            if (File.Exists(data.ModuleILPath) && _testFolderNames.Contains(Path.GetDirectoryName(data.ModuleILPath).ToAbsoluteDirectoryPath().ToLower()))
                return true;

            if (_testModuleNames.Contains(data.ModuleILPath.ToLower()) || _testModuleNames.Contains(data.ModuleNativePath.ToLower()))
                return true;

            return false;
        }

        /// <summary>
        /// Set the process to monitor events for given its Id. This should be set immediately after
        /// calling Process.Start to ensure no module load events are missed for the runtime instance.
        /// </summary>
        public void SetProcessId(int pid)
        {
            _pid = pid;
        }

        public IReadOnlyDictionary<string, HashSet<string>> JittedMethods => _methodsJitted;

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
            var firstBox = className.IndexOf('[');
            var lastDot = className.LastIndexOf('.', firstBox >= 0 ? firstBox : className.Length - 1);
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
