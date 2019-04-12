// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Compiles assemblies using the Cross-Platform AOT compiler
/// </summary>
class CpaotRunner : CompilerRunner
{
    public override CompilerIndex Index => CompilerIndex.CPAOT;

    protected override string CompilerFileName => "ilc.exe";

    public CpaotRunner(string compilerFolder, IEnumerable<string> referenceFolders) 
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
        yield return assemblyFileName;

        // Output
        yield return $"-o:{outputFileName}";

        // Don't forget this one.
        yield return "--readytorun";

        // Todo: Allow control of some of these
        yield return "-g";
        yield return "--runtimeopt:RH_UseServerGC=1";
        yield return "--targetarch=x64";
        yield return "--stacktracedata";

        foreach (var reference in ComputeManagedAssemblies.GetManagedAssembliesInFolder(Path.GetDirectoryName(assemblyFileName)))
        {
            yield return $"-r:{reference}";
        }

        foreach (var referenceFolder in _referenceFolders)
        {
            foreach (var reference in ComputeManagedAssemblies.GetManagedAssembliesInFolder(referenceFolder))
            {
                yield return $"-r:{reference}";
            }
        }
    }
}
