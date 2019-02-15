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

    public bool CompileAssembly(string assemblyFileName)
    {
        CreateOutputFolder();
        string outputFileName = GetOutputFileName(assemblyFileName);
        string responseFile = GetResponseFileName(assemblyFileName);
        var commandLineArgs = BuildCommandLineArguments(assemblyFileName, outputFileName);
        CreateResponseFile(responseFile, commandLineArgs);

        using (var process = new Process())
        {
            process.StartInfo.FileName = Path.Combine(_compilerPath, CompilerFileName);
            process.StartInfo.Arguments = $"@{responseFile}";
            process.StartInfo.UseShellExecute = false;

            process.Start();

            process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args)
            {
                Console.WriteLine(args.Data);
            };

            process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs args)
            {
                Console.WriteLine(args.Data);
            };

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Compilation of {Path.GetFileName(assemblyFileName)} failed with exit code {process.ExitCode}");
                return false;
            }
        }

        return true;
    }

    protected void CreateOutputFolder()
    {
        if (!Directory.Exists(_outputPath))
        {
            Directory.CreateDirectory(_outputPath);
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

    // <input>\a.dll -> <output>\a.ni.dll
    protected string GetOutputFileName(string assemblyFileName) =>
        Path.Combine(_outputPath, $"{Path.GetFileNameWithoutExtension(assemblyFileName)}.ni{Path.GetExtension(assemblyFileName)}"); 
    protected string GetResponseFileName(string assemblyFileName) =>
        Path.Combine(_outputPath, $"{Path.GetFileNameWithoutExtension(assemblyFileName)}.{Path.GetFileNameWithoutExtension(CompilerFileName)}.rsp");
}
