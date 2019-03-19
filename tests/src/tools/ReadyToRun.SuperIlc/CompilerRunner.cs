// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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

    protected abstract string CompilerFileName {get;}
    protected abstract IEnumerable<string> BuildCommandLineArguments(string assemblyFileName, string outputFileName);

    public ProcessInfo CompilationProcess(string assemblyFileName)
    {
        CreateOutputFolder();

        string outputFileName = GetOutputFileName(assemblyFileName);
        string responseFile = GetResponseFileName(assemblyFileName);
        var commandLineArgs = BuildCommandLineArguments(assemblyFileName, outputFileName);
        CreateResponseFile(responseFile, commandLineArgs);

        ProcessInfo processInfo = new ProcessInfo();
        processInfo.ProcessPath = Path.Combine(_compilerPath, CompilerFileName);
        processInfo.Arguments = $"@{responseFile}";
        processInfo.UseShellExecute = false;
        processInfo.LogPath = Path.Combine(_outputPath, Path.GetFileNameWithoutExtension(assemblyFileName) + ".log");

        return processInfo;
    }

    protected void CreateOutputFolder()
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

    public string GetOutputPath() =>
        Path.Combine(_outputPath, Path.GetFileNameWithoutExtension(CompilerFileName));

    // <input>\a.dll -> <output>\a.dll
    public string GetOutputFileName(string fileName) =>
        Path.Combine(GetOutputPath(), $"{Path.GetFileName(fileName)}"); 

    public string GetResponseFileName(string assemblyFileName) =>
        Path.Combine(GetOutputPath(), Path.GetFileNameWithoutExtension(assemblyFileName) + ".rsp");
}
