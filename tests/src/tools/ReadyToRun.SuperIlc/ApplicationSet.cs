﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReadyToRun.SuperIlc
{
    public class ApplicationSet : IDisposable
    {
        private IEnumerable<Application> _applications;

        private IEnumerable<CompilerRunner> _compilerRunners;

        private string _coreRunPath;

        private string _logPath;

        private StreamWriter _logWriter;

        private long _compilationMilliseconds;

        private long _executionMilliseconds;

        private long _buildMilliseconds;

        public ApplicationSet(
            IEnumerable<Application> applications,
            IEnumerable<CompilerRunner> compilerRunners,
            string coreRunPath,
            string logPath)
        {
            _applications = applications;
            _compilerRunners = compilerRunners;
            _coreRunPath = coreRunPath;
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

            foreach (Application app in _applications)
            {
                Dictionary<string, HashSet<string>>[] appMethodsPerModulePerCompiler = new Dictionary<string, HashSet<string>>[(int)CompilerIndex.Count];

                foreach (CompilerRunner runner in _compilerRunners)
                {
                    appMethodsPerModulePerCompiler[(int)runner.Index] = new Dictionary<string, HashSet<string>>();
                    app.AddModuleToJittedMethodsMapping(allMethodsPerModulePerCompiler[(int)runner.Index], runner.Index);
                    app.AddModuleToJittedMethodsMapping(appMethodsPerModulePerCompiler[(int)runner.Index], runner.Index);
                }
                app.WriteJitStatistics(appMethodsPerModulePerCompiler, _compilerRunners);
            }

            Application.WriteJitStatistics(_logWriter, allMethodsPerModulePerCompiler, _compilerRunners);
        }

        public bool Compile()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<ProcessInfo> compilationsToRun = new List<ProcessInfo>();

            foreach (Application application in _applications)
            {
                foreach (ProcessInfo[] compilation in application.Compilations)
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

            compilationsToRun.Sort((a, b) => b.CompilationCostHeuristic.CompareTo(a.CompilationCostHeuristic));

            ParallelRunner.Run(compilationsToRun);

            bool success = true;
            List<KeyValuePair<string, string>> failedCompilationsPerBuilder = new List<KeyValuePair<string, string>>();
            int successfulCompileCount = 0;

            foreach (Application app in _applications)
            {
                foreach (ProcessInfo[] compilation in app.Compilations)
                {
                    string file = null;
                    string failedBuilders = null;
                    foreach (CompilerRunner runner in _compilerRunners)
                    {
                        ProcessInfo runnerProcess = compilation[(int)runner.Index];
                        if (runnerProcess != null && !runnerProcess.Succeeded)
                        {
                            File.Copy(runnerProcess.InputFileName, runnerProcess.OutputFileName);
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

            foreach (Application app in _applications)
            {
                AddAppExecution(executionsToRun, app, stopwatch);
            }

            ParallelRunner.Run(executionsToRun);

            _executionMilliseconds = stopwatch.ElapsedMilliseconds;

            return executionsToRun.All(processInfo => processInfo.Succeeded);
        }

        public bool Build(string coreRunPath, IEnumerable<CompilerRunner> runners, string logPath)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool success = Compile();

            if (coreRunPath != null)
            {
                success = Execute() && success;
            }

            _buildMilliseconds = stopwatch.ElapsedMilliseconds;

            WriteBuildStatistics();

            return success;
        }

        private void AddAppExecution(List<ProcessInfo> executionsToRun, Application app, Stopwatch stopwatch)
        {
            foreach (CompilerRunner runner in _compilerRunners)
            {
                bool compilationsSucceeded = app.Compilations.All(comp => comp[(int)runner.Index]?.Succeeded ?? true);
                if (compilationsSucceeded && app.Execution != null)
                {
                    ProcessInfo executionProcess = app.Execution[(int)runner.Index];
                    if (executionProcess != null)
                    {
                        executionsToRun.Add(executionProcess);
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
            EXE_FAIL = 2,

            Count
        }

        private void WriteBuildStatistics()
        {
            _logWriter.WriteLine();
            _logWriter.WriteLine($"Total apps:       {_applications.Count()}");
            _logWriter.WriteLine($"Total build time: {_buildMilliseconds} msecs");
            _logWriter.WriteLine($"Compilation time: {_compilationMilliseconds} msecs");
            _logWriter.WriteLine($"Execution time:   {_executionMilliseconds} msecs");

            // The Count'th element corresponds to totals over all compiler runners used in the run
            int[,] outcomes = new int[(int)Outcome.Count, (int)CompilerIndex.Count + 1];
            int total = 0;

            foreach (Application app in _applications)
            {
                total++;
                bool anyCompilationFailed = false;
                bool anyExecutionFailed = false;
                foreach (CompilerRunner runner in _compilerRunners)
                {
                    bool compilationFailed = app.Compilations.Any(comp => comp[(int)runner.Index] != null && !comp[(int)runner.Index].Succeeded);
                    if (compilationFailed)
                    {
                        outcomes[(int)Outcome.ILC_FAIL, (int)runner.Index]++;
                        anyCompilationFailed = true;
                    }
                    bool executionFailed = (!compilationFailed &&
                        app.Execution != null &&
                        app.Execution[(int)runner.Index] != null &&
                        !app.Execution[(int)runner.Index].Succeeded);
                    if (executionFailed)
                    {
                        outcomes[(int)Outcome.EXE_FAIL, (int)runner.Index]++;
                        anyExecutionFailed = true;
                    }
                    if (!compilationFailed && !executionFailed)
                    {
                        outcomes[(int)Outcome.PASS, (int)runner.Index]++;
                    }
                }
                if (anyCompilationFailed)
                {
                    outcomes[(int)Outcome.ILC_FAIL, (int)CompilerIndex.Count]++;
                }
                else if (anyExecutionFailed)
                {
                    outcomes[(int)Outcome.EXE_FAIL, (int)CompilerIndex.Count]++;
                }
                else
                {
                    outcomes[(int)Outcome.PASS, (int)CompilerIndex.Count]++;
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
            foreach (Application app in _applications)
            {
                foreach (ProcessInfo[] compilation in app.Compilations)
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
            foreach (Application app in _applications)
            {
                foreach (CompilerRunner runner in _compilerRunners)
                {
                    ProcessInfo executionProcess = app.Execution[(int)runner.Index];
                    if (executionProcess != null)
                    {
                        yield return executionProcess;
                    }
                }
            }
        }
    }
}
