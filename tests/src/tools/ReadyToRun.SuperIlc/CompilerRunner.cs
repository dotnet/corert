// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public enum CompilerIndex
{
    CPAOT,
    Crossgen,
    Jit,

    Count
}

public abstract class CompilerRunner
{
    protected string _compilerPath;
    protected string _inputPath;
    protected string _outputPath;
    protected IReadOnlyList<string> _referenceFolders;

    public CompilerRunner(string compilerFolder, string inputFolder, string outputFolder, IReadOnlyList<string> referenceFolders)
    {
        _compilerPath = compilerFolder;
        _inputPath = inputFolder;
        _outputPath = outputFolder;
        _referenceFolders = referenceFolders ?? new List<string>();
    }

    public IReadOnlyList<string> ReferenceFolders => _referenceFolders;

    public abstract CompilerIndex Index { get;  }

    public string CompilerName => Index.ToString();

    protected abstract string CompilerFileName {get;}
    protected abstract IEnumerable<string> BuildCommandLineArguments(string assemblyFileName, string outputFileName);

    public virtual ProcessInfo CompilationProcess(string assemblyFileName)
    {
        CreateOutputFolder();

        string outputFileName = GetOutputFileName(assemblyFileName);
        string responseFile = GetResponseFileName(assemblyFileName);
        var commandLineArgs = BuildCommandLineArguments(assemblyFileName, outputFileName);
        CreateResponseFile(responseFile, commandLineArgs);

        ProcessInfo processInfo = new ProcessInfo();
        processInfo.ProcessPath = Path.Combine(_compilerPath, CompilerFileName);
        processInfo.Arguments = $"@{responseFile}";
        processInfo.TimeoutMilliseconds = ProcessInfo.DefaultIlcTimeout;
        processInfo.UseShellExecute = false;
        processInfo.LogPath = Path.ChangeExtension(outputFileName, ".ilc.log");
        processInfo.InputFileName = assemblyFileName;
        processInfo.OutputFileName = outputFileName;
        processInfo.CompilationCostHeuristic = new FileInfo(assemblyFileName).Length;

        return processInfo;
    }

    public virtual ProcessInfo ExecutionProcess(string appPath, IEnumerable<string> modules, IEnumerable<string> folders, string coreRunPath)
    {
        string exeToRun = GetOutputFileName(appPath);
        ProcessInfo processInfo = new ProcessInfo();
        processInfo.ProcessPath = coreRunPath;
        processInfo.Arguments = exeToRun;

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

        processInfo.UseShellExecute = false;
        processInfo.LogPath = Path.ChangeExtension(exeToRun, ".exe.log");
        processInfo.ExpectedExitCode = 100;
        processInfo.CollectJittedMethods = true;
        processInfo.MonitorModules = modules;
        processInfo.MonitorFolders = folders;

        return processInfo;
    }

    public void CreateOutputFolder()
    {
        string outputPath = GetOutputPath();
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

    public string GetOutputPath() => Path.Combine(_outputPath, CompilerName);

    // <input>\a.dll -> <output>\a.dll
    public string GetOutputFileName(string fileName) =>
        Path.Combine(GetOutputPath(), $"{Path.GetFileName(fileName)}"); 

    public string GetResponseFileName(string assemblyFileName) =>
        Path.Combine(GetOutputPath(), Path.GetFileNameWithoutExtension(assemblyFileName) + ".rsp");
}
