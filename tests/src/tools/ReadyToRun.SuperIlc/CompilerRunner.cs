// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ReadyToRun.SuperIlc
{
    public enum CompilerIndex
    {
        CPAOT,
        Crossgen,
        Jit,

        Count
    }

    internal static class Linux
    {
        [Flags]
        private enum Permissions : byte
        {
            Read = 1,
            Write = 2,
            Execute = 4,

            ReadExecute = Read | Execute,

            ReadWriteExecute = Read | Write | Execute,
        }

        private enum PermissionGroupShift : int
        {
            Owner = 6,
            Group = 3,
            Other = 0,
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string path, int flags);

        public static void MakeExecutable(string path)
        {
            int errno = chmod(path,
                ((byte)Permissions.ReadWriteExecute << (int)PermissionGroupShift.Owner) |
                ((byte)Permissions.ReadExecute << (int)PermissionGroupShift.Owner) |
                ((byte)Permissions.ReadExecute << (int)PermissionGroupShift.Owner));

            if (errno != 0)
            {
                throw new Exception($@"Failed to set permissions on {path}: error code {errno}");
            }
        }
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

        public virtual ProcessParameters CompilationProcess(string outputRoot, string assemblyFileName)
        {
            CreateOutputFolder(outputRoot);

            string outputFileName = GetOutputFileName(outputRoot, assemblyFileName);
            string responseFile = GetResponseFileName(outputRoot, assemblyFileName);
            var commandLineArgs = BuildCommandLineArguments(assemblyFileName, outputFileName);
            CreateResponseFile(responseFile, commandLineArgs);

            ProcessParameters processParameters = new ProcessParameters();
            processParameters.ProcessPath = Path.Combine(_compilerPath, CompilerFileName);
            processParameters.Arguments = $"@{responseFile}";
            if (_options.CompilationTimeoutMinutes != 0)
            {
                processParameters.TimeoutMilliseconds = _options.CompilationTimeoutMinutes * 60 * 1000;
            }
            else
            {
                processParameters.TimeoutMilliseconds = ProcessParameters.DefaultIlcTimeout;
            }
            processParameters.LogPath = Path.ChangeExtension(outputFileName, ".ilc.log");
            processParameters.InputFileName = assemblyFileName;
            processParameters.OutputFileName = outputFileName;
            processParameters.CompilationCostHeuristic = new FileInfo(assemblyFileName).Length;

            return processParameters;
        }

        protected virtual ProcessParameters ExecutionProcess(IEnumerable<string> modules, IEnumerable<string> folders, bool noEtw)
        {
            ProcessParameters processParameters = new ProcessParameters();

            if (_options.ExecutionTimeoutMinutes != 0)
            {
                processParameters.TimeoutMilliseconds = _options.ExecutionTimeoutMinutes * 60 * 1000;
            }
            else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("__GCSTRESSLEVEL")))
            {
                processParameters.TimeoutMilliseconds = ProcessParameters.DefaultExeTimeout;
            }
            else
            {
                processParameters.TimeoutMilliseconds = ProcessParameters.DefaultExeTimeoutGCStress;
            }

            // TODO: support for tier jitting - for now we just turn it off as it may distort the JIT statistics 
            processParameters.EnvironmentOverrides["COMPLUS_TieredCompilation"] = "0";

            processParameters.CollectJittedMethods = !noEtw;
            if (!noEtw)
            {
                processParameters.MonitorModules = modules;
                processParameters.MonitorFolders = folders;
            }

            return processParameters;
        }

        public virtual ProcessParameters ScriptExecutionProcess(string outputRoot, string scriptPath, IEnumerable<string> modules, IEnumerable<string> folders)
        {
            string scriptToRun = GetOutputFileName(outputRoot, scriptPath);
            ProcessParameters processParameters = ExecutionProcess(modules, folders, _options.NoEtw);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                processParameters.ProcessPath = scriptToRun;
                processParameters.Arguments = null;
            }
            else
            {
                Linux.MakeExecutable(scriptToRun);
                processParameters.ProcessPath = "bash";
                processParameters.Arguments = "-c " + scriptToRun;
            }

            processParameters.InputFileName = scriptToRun;
            processParameters.LogPath = scriptToRun + ".log";
            processParameters.EnvironmentOverrides["CORE_ROOT"] = _options.CoreRootOutputPath(Index, isFramework: false);
            return processParameters;
        }

        public virtual ProcessParameters AppExecutionProcess(string outputRoot, string appPath, IEnumerable<string> modules, IEnumerable<string> folders)
        {
            string exeToRun = GetOutputFileName(outputRoot, appPath);
            ProcessParameters processParameters = ExecutionProcess(modules, folders, _options.NoEtw);
            processParameters.ProcessPath = _options.CoreRunPath(Index, isFramework: false);
            processParameters.Arguments = exeToRun;
            processParameters.InputFileName = exeToRun;
            processParameters.LogPath = exeToRun + ".log";
            processParameters.ExpectedExitCode = 100;
            return processParameters;
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

    public abstract class CompilerRunnerProcessConstructor : ProcessConstructor
    {
        protected readonly CompilerRunner _runner;

        public CompilerRunnerProcessConstructor(CompilerRunner runner)
        {
            _runner = runner;
        }
    }

    public class CompilationProcessConstructor : CompilerRunnerProcessConstructor
    {
        private readonly string _outputRoot;
        private readonly string _assemblyFileName;

        public CompilationProcessConstructor(CompilerRunner runner, string outputRoot, string assemblyFileName)
            : base(runner)
        {
            _outputRoot = outputRoot;
            _assemblyFileName = assemblyFileName;
        }

        public override ProcessParameters Construct()
        {
            return _runner.CompilationProcess(_outputRoot, _assemblyFileName);
        }
    }

    public sealed class ScriptExecutionProcessConstructor : CompilerRunnerProcessConstructor
    {
        private readonly string _outputRoot;
        private readonly string _scriptPath;
        private readonly IEnumerable<string> _modules;
        private readonly IEnumerable<string> _folders;

        public ScriptExecutionProcessConstructor(CompilerRunner runner, string outputRoot, string scriptPath, IEnumerable<string> modules, IEnumerable<string> folders)
            : base(runner)
        {
            _outputRoot = outputRoot;
            _scriptPath = scriptPath;
            _modules = modules;
            _folders = folders;
        }

        public override ProcessParameters Construct()
        {
            return _runner.ScriptExecutionProcess(_outputRoot, _scriptPath, _modules, _folders);
        }
    }

    public sealed class AppExecutionProcessConstructor : CompilerRunnerProcessConstructor
    {
        private readonly string _outputRoot;
        private readonly string _appPath;
        private readonly IEnumerable<string> _modules;
        private readonly IEnumerable<string> _folders;

        public AppExecutionProcessConstructor(CompilerRunner runner, string outputRoot, string appPath, IEnumerable<string> modules, IEnumerable<string> folders)
            : base(runner)
        {
            _outputRoot = outputRoot;
            _appPath = appPath;
            _modules = modules;
            _folders = folders;
        }

        public override ProcessParameters Construct()
        {
            return _runner.AppExecutionProcess(_outputRoot, _appPath, _modules, _folders);
        }
    }
}
