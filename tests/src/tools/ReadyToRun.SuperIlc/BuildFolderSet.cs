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
    public class BuildFolderSet : IDisposable
    {
        private IEnumerable<BuildFolder> _buildFolders;

        private IEnumerable<CompilerRunner> _compilerRunners;

        private BuildOptions _options;

        private string _logPath;

        private StreamWriter _logWriter;

        private long _compilationMilliseconds;

        private long _executionMilliseconds;

        private long _buildMilliseconds;

        public IEnumerable<BuildFolder> BuildFolders => _buildFolders;

        public BuildFolderSet(
            IEnumerable<BuildFolder> buildFolders,
            IEnumerable<CompilerRunner> compilerRunners,
            BuildOptions options,
            string logPath)
        {
            _buildFolders = buildFolders;
            _compilerRunners = compilerRunners;
            _options = options;
            _logPath = logPath;

            _logWriter = new StreamWriter(_logPath);
        }

        public void Dispose()
        {
            _logWriter?.Dispose();
        }

        private void WriteJittedMethodSummary()
        {
            Dictionary<string, HashSet<string>>[] allMethodsPerModulePerCompiler = new Dictionary<string, HashSet<string>>[(int)CompilerIndex.Count];

            foreach (CompilerRunner runner in _compilerRunners)
            {
                allMethodsPerModulePerCompiler[(int)runner.Index] = new Dictionary<string, HashSet<string>>();
            }

            foreach (BuildFolder folder in _buildFolders)
            {
                for (int exeIndex = 0; exeIndex < folder.Executions.Count; exeIndex++)
                {
                    Dictionary<string, HashSet<string>>[] appMethodsPerModulePerCompiler = new Dictionary<string, HashSet<string>>[(int)CompilerIndex.Count];
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        appMethodsPerModulePerCompiler[(int)runner.Index] = new Dictionary<string, HashSet<string>>();
                        folder.AddModuleToJittedMethodsMapping(allMethodsPerModulePerCompiler[(int)runner.Index], exeIndex, runner.Index);
                        folder.AddModuleToJittedMethodsMapping(appMethodsPerModulePerCompiler[(int)runner.Index], exeIndex, runner.Index);
                    }
                    folder.WriteJitStatistics(appMethodsPerModulePerCompiler, _compilerRunners);
                }

            }

            BuildFolder.WriteJitStatistics(_logWriter, allMethodsPerModulePerCompiler, _compilerRunners);
        }

        public bool Compile()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<ProcessInfo> compilationsToRun = new List<ProcessInfo>();

            foreach (BuildFolder folder in _buildFolders)
            {
                foreach (ProcessInfo[] compilation in folder.Compilations)
                {
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        ProcessInfo compilationProcess = compilation[(int)runner.Index];
                        if (compilationProcess != null)
                        {
                            compilationsToRun.Add(compilationProcess);
                        }
                    }
                }
            }

            _logWriter.WriteLine();
            _logWriter.WriteLine($"Building {_buildFolders.Count()} folders ({compilationsToRun.Count} compilations total)");
            compilationsToRun.Sort((a, b) => b.CompilationCostHeuristic.CompareTo(a.CompilationCostHeuristic));

            ParallelRunner.Run(startIndex: 0, compilationsToRun, _logWriter);
            
            bool success = true;
            List<KeyValuePair<string, string>> failedCompilationsPerBuilder = new List<KeyValuePair<string, string>>();
            int successfulCompileCount = 0;

            foreach (BuildFolder folder in _buildFolders)
            {
                foreach (ProcessInfo[] compilation in folder.Compilations)
                {
                    string file = null;
                    string failedBuilders = null;
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        ProcessInfo runnerProcess = compilation[(int)runner.Index];
                        if (runnerProcess != null && !runnerProcess.Succeeded)
                        {
                            try
                            {
                                File.Copy(runnerProcess.InputFileName, runnerProcess.OutputFileName);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine("Error copying {0} to {1}: {2}", runnerProcess.InputFileName, runnerProcess.OutputFileName, ex.Message);
                            }
                            if (file == null)
                            {
                                file = runnerProcess.InputFileName;
                                failedBuilders = runner.CompilerName;
                            }
                            else
                            {
                                failedBuilders += "; " + runner.CompilerName;
                            }
                        }
                    }
                    if (file != null)
                    {
                        failedCompilationsPerBuilder.Add(new KeyValuePair<string, string>(file, failedBuilders));
                        success = false;
                    }
                    else
                    {
                        successfulCompileCount++;
                    }
                }
            }

            _logWriter.WriteLine($"Compiled {successfulCompileCount} / {successfulCompileCount + failedCompilationsPerBuilder.Count} assemblies in {stopwatch.ElapsedMilliseconds} msecs.");

            if (failedCompilationsPerBuilder.Count > 0)
            {
                int compilerRunnerCount = _compilerRunners.Count();
                _logWriter.WriteLine($"Failed to compile {failedCompilationsPerBuilder.Count} assemblies:");
                foreach (KeyValuePair<string, string> assemblyBuilders in failedCompilationsPerBuilder)
                {
                    string assemblySpec = assemblyBuilders.Key;
                    if (compilerRunnerCount > 1)
                    {
                        assemblySpec += " (" + assemblyBuilders.Value + ")";
                    }
                    _logWriter.WriteLine(assemblySpec);
                }
            }

            _compilationMilliseconds = stopwatch.ElapsedMilliseconds;

            return success;
        }

        public bool Execute()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            List<ProcessInfo> executionsToRun = new List<ProcessInfo>();

            foreach (BuildFolder folder in _buildFolders)
            {
                AddBuildFolderExecutions(executionsToRun, folder, stopwatch);
            }

            ParallelRunner.Run(startIndex: 0, executionsToRun, _logWriter);

            List<KeyValuePair<string, string>> failedExecutionsPerBuilder = new List<KeyValuePair<string, string>>();

            int successfulExecuteCount = 0;

            bool success = true;
            foreach (BuildFolder folder in _buildFolders)
            {
                foreach (ProcessInfo[] execution in folder.Executions)
                {
                    string file = null;
                    string failedBuilders = null;
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        ProcessInfo runnerProcess = execution[(int)runner.Index];
                        if (runnerProcess != null && !runnerProcess.Succeeded)
                        {
                            if (file == null)
                            {
                                file = runnerProcess.InputFileName;
                                failedBuilders = runner.CompilerName;
                            }
                            else
                            {
                                failedBuilders += "; " + runner.CompilerName;
                            }
                        }
                    }
                    if (file != null)
                    {
                        failedExecutionsPerBuilder.Add(new KeyValuePair<string, string>(file, failedBuilders));
                        success = false;
                    }
                    else
                    {
                        successfulExecuteCount++;
                    }
                }
            }

            _logWriter.WriteLine($"Successfully executed {successfulExecuteCount} / {successfulExecuteCount + failedExecutionsPerBuilder.Count} apps in {stopwatch.ElapsedMilliseconds} msecs.");

            if (failedExecutionsPerBuilder.Count > 0)
            {
                int compilerRunnerCount = _compilerRunners.Count();
                _logWriter.WriteLine($"Failed to execute {failedExecutionsPerBuilder.Count} apps:");
                foreach (KeyValuePair<string, string> assemblyBuilders in failedExecutionsPerBuilder)
                {
                    string assemblySpec = assemblyBuilders.Key;
                    if (compilerRunnerCount > 1)
                    {
                        assemblySpec += " (" + assemblyBuilders.Value + ")";
                    }
                    _logWriter.WriteLine(assemblySpec);
                }
            }

            _executionMilliseconds = stopwatch.ElapsedMilliseconds;

            return success;
        }

        public bool Build(IEnumerable<CompilerRunner> runners, string logPath)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool success = Compile();

            if (!_options.NoExe)
            {
                success = Execute() && success;
            }

            _buildMilliseconds = stopwatch.ElapsedMilliseconds;

            WriteBuildStatistics();

            return success;
        }

        private void AddBuildFolderExecutions(List<ProcessInfo> executionsToRun, BuildFolder folder, Stopwatch stopwatch)
        {
            foreach (ProcessInfo[] execution in folder.Executions)
            {
                foreach (CompilerRunner runner in _compilerRunners)
                {
                    ProcessInfo executionProcess = execution[(int)runner.Index];
                    if (executionProcess != null)
                    {
                        bool compilationsSucceeded = folder.Compilations.All(comp => comp[(int)runner.Index]?.Succeeded ?? true);
                        if (compilationsSucceeded)
                        {
                            executionsToRun.Add(executionProcess);
                        }
                    }
                }
            }
        }

        void WriteTopRankingProcesses(string metric, IEnumerable<ProcessInfo> processes)
        {
            const int TopAppCount = 10;

            _logWriter.WriteLine();

            string headerLine = $"Top {TopAppCount} top ranking {metric}";
            _logWriter.WriteLine(headerLine);
            _logWriter.WriteLine(new string('-', headerLine.Length));

            foreach (ProcessInfo processInfo in processes.OrderByDescending(process => process.DurationMilliseconds).Take(TopAppCount))
            {
                _logWriter.WriteLine($"{processInfo.DurationMilliseconds,10} | {processInfo.InputFileName}");
            }
        }

        enum Outcome
        {
            PASS = 0,
            ILC_FAIL = 1,
            WRONG_EXIT_CODE = 2,
            CRASHED = 3,
            TIMED_OUT = 4,

            Count
        }

        private void WriteBuildStatistics()
        {
            _logWriter.WriteLine();
            _logWriter.WriteLine($"Total folders:    {_buildFolders.Count()}");
            _logWriter.WriteLine($"Total build time: {_buildMilliseconds} msecs");
            _logWriter.WriteLine($"Compilation time: {_compilationMilliseconds} msecs");
            _logWriter.WriteLine($"Execution time:   {_executionMilliseconds} msecs");

            // The Count'th element corresponds to totals over all compiler runners used in the run
            int[,] outcomes = new int[(int)Outcome.Count, (int)CompilerIndex.Count + 1];
            int total = 0;

            foreach (BuildFolder folder in _buildFolders)
            {
                total++;
                bool[] compilationFailedPerRunner = new bool[(int)CompilerIndex.Count];
                foreach (ProcessInfo[] compilation in folder.Compilations)
                {
                    bool anyCompilationFailed = false;
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        bool compilationFailed = compilation[(int)runner.Index] != null && !compilation[(int)runner.Index].Succeeded;
                        if (compilationFailed)
                        {
                            outcomes[(int)Outcome.ILC_FAIL, (int)runner.Index]++;
                            anyCompilationFailed = true;
                            compilationFailedPerRunner[(int)runner.Index] = true;
                        }
                    }
                    if (anyCompilationFailed)
                    {
                        outcomes[(int)Outcome.ILC_FAIL, (int)CompilerIndex.Count]++;
                    }
                }
                foreach (ProcessInfo[] execution in folder.Executions)
                {
                    bool anyCompilationFailed = false;
                    int executionFailureOutcomeMask = 0;
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        ProcessInfo execProcess = execution[(int)runner.Index];
                        bool compilationFailed = compilationFailedPerRunner[(int)runner.Index];
                        anyCompilationFailed |= compilationFailed;
                        bool executionFailed = !compilationFailed && (execProcess != null && !execProcess.Succeeded);
                        if (executionFailed)
                        {
                            Outcome outcome = (execProcess.TimedOut ? Outcome.TIMED_OUT :
                                execProcess.ExitCode < -1000 * 1000 ? Outcome.CRASHED :
                                Outcome.WRONG_EXIT_CODE);
                            outcomes[(int)outcome, (int)runner.Index]++;
                            executionFailureOutcomeMask |= 1 << (int)outcome;
                        }
                        if (!compilationFailed && !executionFailed)
                        {
                            outcomes[(int)Outcome.PASS, (int)runner.Index]++;
                        }
                    }
                    if (executionFailureOutcomeMask != 0)
                    {
                        for (int outcomeIndex = 0; outcomeIndex < (int)Outcome.Count; outcomeIndex++)
                        {
                            if ((executionFailureOutcomeMask & (1 << outcomeIndex)) != 0)
                            {
                                outcomes[outcomeIndex, (int)CompilerIndex.Count]++;
                            }
                        }
                    }
                    else if (!anyCompilationFailed)
                    {
                        outcomes[(int)Outcome.PASS, (int)CompilerIndex.Count]++;
                    }
                }
            }

            _logWriter.WriteLine();
            _logWriter.Write($"{total,5} TOTAL |");
            foreach (CompilerRunner runner in _compilerRunners)
            {
                _logWriter.Write($"{runner.CompilerName,8} |");
            }
            _logWriter.WriteLine(" Overall");
            int lineSize = 10 * _compilerRunners.Count() + 13 + 8;
            string separator = new string('-', lineSize);
            _logWriter.WriteLine(separator);
            for (int outcomeIndex = 0; outcomeIndex < (int)Outcome.Count; outcomeIndex++)
            {
                _logWriter.Write($"{((Outcome)outcomeIndex).ToString(),11} |");
                foreach (CompilerRunner runner in _compilerRunners)
                {
                    _logWriter.Write($"{outcomes[outcomeIndex, (int)runner.Index],8} |");
                }
                _logWriter.WriteLine($"{outcomes[outcomeIndex, (int)CompilerIndex.Count],8}");
            }

            WriteJittedMethodSummary();

            WriteTopRankingProcesses("compilations by duration", EnumerateCompilations());
            WriteTopRankingProcesses("executions by duration", EnumerateExecutions());
        }

        private IEnumerable<ProcessInfo> EnumerateCompilations()
        {
            foreach (BuildFolder folder in _buildFolders)
            {
                foreach (ProcessInfo[] compilation in folder.Compilations)
                {
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        ProcessInfo compilationProcess = compilation[(int)runner.Index];
                        if (compilationProcess != null)
                        {
                            yield return compilationProcess;
                        }
                    }
                }
            }
        }

        private IEnumerable<ProcessInfo> EnumerateExecutions()
        {
            foreach (BuildFolder folder in _buildFolders)
            {
                foreach (ProcessInfo[] execution in folder.Executions)
                {
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        ProcessInfo executionProcess = execution[(int)runner.Index];
                        if (executionProcess != null)
                        {
                            yield return executionProcess;
                        }
                    }
                }
            }
        }
    }
}
