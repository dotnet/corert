// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Compiles assemblies using the Cross-Platform AOT compiler
/// </summary>
class CrossgenRunner : CompilerRunner
{
    public override CompilerIndex Index => CompilerIndex.Crossgen;

    protected override string CompilerFileName => "crossgen.exe";

    public CrossgenRunner(string compilerFolder, IEnumerable<string> referenceFolders) 
        : base(compilerFolder, referenceFolders) {}

    protected override ProcessInfo ExecutionProcess(IEnumerable<string> modules, IEnumerable<string> folders, bool noEtw)
    {
        ProcessInfo processInfo = base.ExecutionProcess(modules, folders, noEtw);
        processInfo.EnvironmentOverrides["COMPLUS_ReadyToRun"] = "1";
        return processInfo;
    }

    protected override IEnumerable<string> BuildCommandLineArguments(string assemblyFileName, string outputFileName)
    {
        // The file to compile
        yield return "/in";
        yield return assemblyFileName;

        // Output
        yield return "/out";
        yield return outputFileName;

        yield return "/platform_assemblies_paths";
        
        StringBuilder sb = new StringBuilder();
        sb.Append(Path.GetDirectoryName(assemblyFileName) + (_referenceFolders.Any() ? ";" : ""));
        sb.AppendJoin(';', _referenceFolders);
        yield return sb.ToString();
    }
}
