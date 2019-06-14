// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ReadyToRun.SuperIlc
{
    public enum CompilerIndex
    {
        CPAOT,
        Crossgen,
        Jit,

        Count
    }

    public abstract class CompilerRunner
    {
        protected readonly BuildOptions _options;
        protected readonly string _compilerPath;
        protected readonly IEnumerable<string> _referenceFolders;

        public CompilerRunner(BuildOptions options, string compilerFolder, IEnumerable<string> referenceFolders)
        {
            _options = options;
            _compilerPath = compilerFolder;
            _referenceFolders = referenceFolders;
        }

        public IEnumerable<string> ReferenceFolders => _referenceFolders;

        public abstract CompilerIndex Index { get; }

        public string CompilerName => Index.ToString();

        protected abstract string CompilerFileName { get; }
        protected abstract IEnumerable<string> BuildCommandLineArguments(string assemblyFileName, string outputFileName);

        public virtual ProcessInfo CompilationProcess(string outputRoot, string assemblyFileName)
        {
            CreateOutputFolder(outputRoot);

            string outputFileName = GetOutputFileName(outputRoot, assemblyFileName);
            string responseFile = GetResponseFileName(outputRoot, assemblyFileName);
            var commandLineArgs = BuildCommandLineArguments(assemblyFileName, outputFileName);
            CreateResponseFile(responseFile, commandLineArgs);

            ProcessInfo processInfo = new ProcessInfo();
            processInfo.ProcessPath = Path.Combine(_compilerPath, CompilerFileName);
            processInfo.Arguments = $"@{responseFile}";
            processInfo.TimeoutMilliseconds = ProcessInfo.DefaultIlcTimeout;
            processInfo.LogPath = Path.ChangeExtension(outputFileName, ".ilc.log");
            processInfo.InputFileName = assemblyFileName;
            processInfo.OutputFileName = outputFileName;
            processInfo.CompilationCostHeuristic = new FileInfo(assemblyFileName).Length;

            return processInfo;
        }

        protected virtual ProcessInfo ExecutionProcess(IEnumerable<string> modules, IEnumerable<string> folders, bool noEtw)
        {
            ProcessInfo processInfo = new ProcessInfo();

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("__GCSTRESSLEVEL")))
            {
                processInfo.TimeoutMilliseconds = ProcessInfo.DefaultExeTimeout;
            }
            else
            {
                processInfo.TimeoutMilliseconds = ProcessInfo.DefaultExeTimeoutGCStress;
            }

            // TODO: support for tier jitting - for now we just turn it off as it may distort the JIT statistics 
            processInfo.EnvironmentOverrides["COMPLUS_TieredCompilation"] = "0";

            processInfo.CollectJittedMethods = !noEtw;
            if (!noEtw)
            {
                processInfo.MonitorModules = modules;
                processInfo.MonitorFolders = folders;
            }

            return processInfo;
        }

        public virtual ProcessInfo ScriptExecutionProcess(string outputRoot, string scriptPath, IEnumerable<string> modules, IEnumerable<string> folders)
        {
            string scriptToRun = GetOutputFileName(outputRoot, scriptPath);
            ProcessInfo processInfo = ExecutionProcess(modules, folders, _options.NoEtw);
            processInfo.ProcessPath = scriptToRun;
            processInfo.Arguments = null;
            processInfo.InputFileName = scriptToRun;
            processInfo.LogPath = scriptToRun + ".log";
            processInfo.EnvironmentOverrides["CORE_ROOT"] = _options.CoreRootOutputPath(Index, isFramework: false);
            return processInfo;
        }

        public virtual ProcessInfo AppExecutionProcess(string outputRoot, string appPath, IEnumerable<string> modules, IEnumerable<string> folders)
        {
            string exeToRun = GetOutputFileName(outputRoot, appPath);
            ProcessInfo processInfo = ExecutionProcess(modules, folders, _options.NoEtw);
            processInfo.ProcessPath = _options.CoreRunPath(Index, isFramework: false);
            processInfo.Arguments = exeToRun;
            processInfo.InputFileName = exeToRun;
            processInfo.LogPath = exeToRun + ".log";
            processInfo.ExpectedExitCode = 100;
            return processInfo;
        }

        public void CreateOutputFolder(string outputRoot)
        {
            string outputPath = GetOutputPath(outputRoot);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
        }

        protected void CreateResponseFile(string responseFile, IEnumerable<string> commandLineArguments)
        {
            using (TextWriter tw = File.CreateText(responseFile))
            {
                foreach (var arg in commandLineArguments)
                {
                    tw.WriteLine(arg);
                }
            }
        }

        public string GetOutputPath(string outputRoot) => Path.Combine(outputRoot, CompilerName + _options.ConfigurationSuffix);

        // <input>\a.dll -> <output>\a.dll
        public string GetOutputFileName(string outputRoot, string fileName) =>
            Path.Combine(GetOutputPath(outputRoot), $"{Path.GetFileName(fileName)}");

        public string GetResponseFileName(string outputRoot, string assemblyFileName) =>
            Path.Combine(GetOutputPath(outputRoot), Path.GetFileNameWithoutExtension(assemblyFileName) + ".rsp");
    }
}
