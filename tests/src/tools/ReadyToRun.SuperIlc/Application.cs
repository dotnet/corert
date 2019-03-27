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

        private List<string> _mainExecutables;

        private readonly List<ProcessInfo[]> _compilations;

        private string _outputFolder;

        private readonly List<ProcessInfo[]> _executions;

        public IList<string> MainExecutables => _mainExecutables;

        public Application(
            List<string> compilationInputFiles, 
            List<string> mainExecutables,
            IEnumerable<CompilerRunner> compilerRunners,
            string outputFolder,
            string coreRunPath,
            bool noExe,
            bool noEtw)
        {
            _compilationInputFiles = compilationInputFiles;
            _mainExecutables = mainExecutables;
            _outputFolder = outputFolder;

            _compilations = new List<ProcessInfo[]>();
            _executions = new List<ProcessInfo[]>();

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

            if (!noExe && !string.IsNullOrEmpty(coreRunPath))
            {
                for (int exeIndex = 0; exeIndex < _mainExecutables.Count; exeIndex++)
                {
                    string mainExe = _mainExecutables[exeIndex];
                    ProcessInfo[] mainAppExecutions = new ProcessInfo[(int)CompilerIndex.Count];
                    _executions.Add(mainAppExecutions);
                    foreach (CompilerRunner runner in compilerRunners)
                    {
                        HashSet<string> modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        modules.Add(mainExe);
                        modules.Add(runner.GetOutputFileName(_outputFolder, mainExe));
                        modules.UnionWith(_compilationInputFiles);
                        modules.UnionWith(_compilationInputFiles.Select(file => runner.GetOutputFileName(_outputFolder, file)));
                        folders.Add(Path.GetDirectoryName(mainExe));
                        folders.UnionWith(runner.ReferenceFolders);

                        mainAppExecutions[(int)runner.Index] = runner.ExecutionProcess(_outputFolder, mainExe, modules, folders, coreRunPath, noEtw);
                    }
                }
            }
        }

        public static Application FromDirectory(string inputDirectory, IEnumerable<CompilerRunner> compilerRunners, string outputRoot, bool noExe, bool noEtw, string coreRunPath)
        {
            List<string> compilationInputFiles = new List<string>();
            List<string> passThroughFiles = new List<string>();
            List<string> mainExecutables = new List<string>();

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
                    mainExecutables.Add(file);
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

            return new Application(compilationInputFiles, mainExecutables, compilerRunners, outputRoot, coreRunPath, noExe, noEtw);
        }

        public void AddModuleToJittedMethodsMapping(Dictionary<string, HashSet<string>> moduleToJittedMethods, int executionIndex, CompilerIndex compilerIndex)
        {
            ProcessInfo executionProcess = _executions[executionIndex][(int)compilerIndex];
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
            for (int exeIndex = 0; exeIndex < _mainExecutables.Count; exeIndex++)
            {
                string jitStatisticsFile = Path.ChangeExtension(_mainExecutables[exeIndex], ".jit-statistics");
                using (StreamWriter streamWriter = new StreamWriter(jitStatisticsFile))
                {
                    WriteJitStatistics(streamWriter, perCompilerStatistics, compilerRunners);
                }
            }
        }

        public IEnumerable<ProcessInfo[]> Compilations => _compilations;

        public IEnumerable<ProcessInfo[]> Executions => _executions ?? Enumerable.Empty<ProcessInfo[]>();
    }
}
