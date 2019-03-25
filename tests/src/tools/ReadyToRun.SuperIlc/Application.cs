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

        private string _outputFolder;

        private readonly ProcessInfo[] _execution;

        public string MainExecutable => _mainExecutable;

        public Application(
            List<string> compilationInputFiles, 
            string mainExecutable, 
            IEnumerable<CompilerRunner> compilerRunners,
            string outputFolder,
            string coreRunPath,
            bool noEtw)
        {
            _compilationInputFiles = compilationInputFiles;
            _mainExecutable = mainExecutable;
            _outputFolder = outputFolder;

            _compilations = new List<ProcessInfo[]>();

            foreach (string file in _compilationInputFiles)
            {
                ProcessInfo[] fileCompilations = new ProcessInfo[(int)CompilerIndex.Count];
                foreach (CompilerRunner runner in compilerRunners)
                {
                    ProcessInfo compilationProcess = runner.CompilationProcess(_outputFolder, file);
                    fileCompilations[(int)runner.Index] = compilationProcess;
                }
                _compilations.Add(fileCompilations);
            }

            if (_mainExecutable != null && !string.IsNullOrEmpty(coreRunPath))
            {
                _execution = new ProcessInfo[(int)CompilerIndex.Count];

                foreach (CompilerRunner runner in compilerRunners)
                {
                    HashSet<string> modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    modules.Add(_mainExecutable);
                    modules.Add(runner.GetOutputFileName(_outputFolder, _mainExecutable));
                    modules.UnionWith(_compilationInputFiles);
                    modules.UnionWith(_compilationInputFiles.Select(file => runner.GetOutputFileName(_outputFolder, file)));
                    folders.Add(Path.GetDirectoryName(_mainExecutable));
                    folders.UnionWith(runner.ReferenceFolders);

                    _execution[(int)runner.Index] = runner.ExecutionProcess(_outputFolder, _mainExecutable, modules, folders, coreRunPath, noEtw);
                }
            }
        }

        public static Application FromDirectory(string inputDirectory, IEnumerable<CompilerRunner> compilerRunners, string outputRoot, bool noEtw, string coreRunPath)
        {
            List<string> compilationInputFiles = new List<string>();
            List<string> passThroughFiles = new List<string>();
            string mainExecutable = null;

            // Copy unmanaged files (runtime, native dependencies, resources, etc)
            foreach (string file in Directory.EnumerateFiles(inputDirectory))
            {
                bool isManagedAssembly = ComputeManagedAssemblies.IsManaged(file);
                if (isManagedAssembly)
                {
                    compilationInputFiles.Add(file);
                }
                else
                {
                    passThroughFiles.Add(file);
                }
                if (Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    mainExecutable = file;
                }
            }

            if (compilationInputFiles.Count == 0)
            {
                return null;
            }

            foreach (CompilerRunner runner in compilerRunners)
            {
                string runnerOutputPath = runner.GetOutputPath(outputRoot);
                runnerOutputPath.RecreateDirectory();
                foreach (string file in passThroughFiles)
                {
                    File.Copy(file, Path.Combine(runnerOutputPath, Path.GetFileName(file)));
                }
            }

            return new Application(compilationInputFiles, mainExecutable, compilerRunners, outputRoot, coreRunPath, noEtw);
        }

        public void AddModuleToJittedMethodsMapping(Dictionary<string, HashSet<string>> moduleToJittedMethods, CompilerIndex compilerIndex)
        {
            ProcessInfo executionProcess = (_execution != null ? _execution[(int)compilerIndex] : null);
            if (executionProcess != null && executionProcess.JittedMethods != null)
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

        public static void WriteJitStatistics(TextWriter writer, Dictionary<string, HashSet<string>>[] perCompilerStatistics, IEnumerable<CompilerRunner> compilerRunners)
        {
            Dictionary<string, int> moduleNameUnion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (CompilerRunner compilerRunner in compilerRunners)
            {
                foreach (KeyValuePair<string, HashSet<string>> kvp in perCompilerStatistics[(int)compilerRunner.Index])
                {
                    int methodCount;
                    moduleNameUnion.TryGetValue(kvp.Key, out methodCount);
                    moduleNameUnion[kvp.Key] = Math.Max(methodCount, kvp.Value.Count);
                }
            }

            if (moduleNameUnion.Count == 0)
            {
                // No JIT statistics available
                return;
            }

            writer.WriteLine();
            writer.WriteLine("Jitted method statistics:");

            foreach (CompilerRunner compilerRunner in compilerRunners)
            {
                writer.Write($"{compilerRunner.Index.ToString(),9} |");
            }
            writer.WriteLine(" Assembly Name");
            writer.WriteLine(new string('-', 11 * compilerRunners.Count() + 14));
            foreach (string moduleName in moduleNameUnion.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key))
            {
                foreach (CompilerRunner compilerRunner in compilerRunners)
                {
                    HashSet<string> jittedMethodsPerModule;
                    perCompilerStatistics[(int)compilerRunner.Index].TryGetValue(moduleName, out jittedMethodsPerModule);
                    writer.Write(string.Format("{0,9} |", jittedMethodsPerModule != null ? jittedMethodsPerModule.Count.ToString() : ""));
                }
                writer.Write(' ');
                writer.WriteLine(moduleName);
            }
        }

        public void WriteJitStatistics(Dictionary<string, HashSet<string>>[] perCompilerStatistics, IEnumerable<CompilerRunner> compilerRunners)
        {
            string jitStatisticsFile = Path.ChangeExtension(_mainExecutable, ".jit-statistics");
            using (StreamWriter streamWriter = new StreamWriter(jitStatisticsFile))
            {
                WriteJitStatistics(streamWriter, perCompilerStatistics, compilerRunners);
            }
        }

        public IEnumerable<ProcessInfo[]> Compilations => _compilations;

        public ProcessInfo[] Execution => _execution;
    }
}
