// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReadyToRun.SuperIlc
{
    public class Application
    {
        private List<string> _compilationInputFiles;

        private string  _mainExecutable;

        private readonly List<ProcessInfo[]> _compilations;

        private readonly ProcessInfo[] _execution;

        public Application(
            List<string> compilationInputFiles, 
            string mainExecutable, 
            IEnumerable<CompilerRunner> compilerRunners, 
            string coreRunPath)
        {
            _compilationInputFiles = compilationInputFiles;
            _mainExecutable = mainExecutable;

            _compilations = new List<ProcessInfo[]>();

            foreach (string file in _compilationInputFiles)
            {
                ProcessInfo[] fileCompilations = new ProcessInfo[(int)CompilerIndex.Count];
                foreach (CompilerRunner runner in compilerRunners)
                {
                    ProcessInfo compilationProcess = runner.CompilationProcess(file);
                    fileCompilations[(int)runner.Index] = compilationProcess;
                }
                _compilations.Add(fileCompilations);
            }

            if (_mainExecutable != null && !string.IsNullOrEmpty(coreRunPath))
            {
                _execution = new ProcessInfo[(int)CompilerIndex.Count];

                foreach (CompilerRunner runner in compilerRunners)
                {
                    HashSet<string> modules = new HashSet<string>();
                    HashSet<string> folders = new HashSet<string>();

                    modules.Add(_mainExecutable.ToLower());
                    modules.Add(runner.GetOutputFileName(_mainExecutable).ToLower());
                    modules.UnionWith(_compilationInputFiles.Select(file => file.ToLower()));
                    modules.UnionWith(_compilationInputFiles.Select(file => runner.GetOutputFileName(file).ToLower()));
                    folders.Add(Path.GetDirectoryName(_mainExecutable).ToLower());
                    folders.UnionWith(runner.ReferenceFolders.Select(folder => folder.ToLower()));

                    _execution[(int)runner.Index] = runner.ExecutionProcess(_mainExecutable, modules, folders, coreRunPath);
                }
            }
        }

        public void AddModuleToJittedMethodsMapping(Dictionary<string, HashSet<string>> moduleToJittedMethods, CompilerIndex compilerIndex)
        {
            ProcessInfo executionProcess = _execution[(int)compilerIndex];
            if (executionProcess != null)
            {
                foreach (KeyValuePair<string, HashSet<string>> moduleMethodKvp in executionProcess.JittedMethods)
                {
                    HashSet<string> jittedMethodsPerModule;
                    if (!moduleToJittedMethods.TryGetValue(moduleMethodKvp.Key, out jittedMethodsPerModule))
                    {
                        jittedMethodsPerModule = new HashSet<string>();
                        moduleToJittedMethods.Add(moduleMethodKvp.Key, jittedMethodsPerModule);
                    }
                    jittedMethodsPerModule.UnionWith(moduleMethodKvp.Value);
                }
            }
        }

        public static void WriteJitStatistics(TextWriter writer, Dictionary<string, HashSet<string>>[] perCompilerStatistics, IEnumerable<CompilerIndex> compilerIndices)
        {
            HashSet<string> moduleNameUnion = new HashSet<string>();
            foreach (CompilerIndex compilerIndex in compilerIndices)
            {
                moduleNameUnion.UnionWith(perCompilerStatistics[(int)compilerIndex].Keys);
                writer.Write($"{compilerIndex.ToString(),9} |");
            }
            writer.WriteLine(" Assembly Name");
            writer.WriteLine(new string('-', 11 * compilerIndices.Count() + 14));
            foreach (string moduleName in moduleNameUnion.OrderBy(modName => modName))
            {
                foreach (CompilerIndex compilerIndex in compilerIndices)
                {
                    HashSet<string> jittedMethodsPerModule;
                    perCompilerStatistics[(int)compilerIndex].TryGetValue(moduleName, out jittedMethodsPerModule);
                    writer.Write(string.Format("{0,9} |", jittedMethodsPerModule != null ? jittedMethodsPerModule.Count.ToString() : ""));
                }
                writer.Write(' ');
                writer.WriteLine(moduleName);
            }
        }

        public void WriteJitStatistics(Dictionary<string, HashSet<string>>[] perCompilerStatistics, IEnumerable<CompilerIndex> compilerIndices)
        {
            string jitStatisticsFile = Path.ChangeExtension(_mainExecutable, ".jit-statistics");
            using (StreamWriter streamWriter = new StreamWriter(jitStatisticsFile))
            {
                WriteJitStatistics(streamWriter, perCompilerStatistics, compilerIndices);
            }
        }

        public IEnumerable<ProcessInfo[]> Compilations => _compilations;

        public ProcessInfo[] Execution => _execution;
    }
}
