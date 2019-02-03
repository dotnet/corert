// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

/// <summary>
/// Compiles assemblies using the Cross-Platform AOT compiler
/// </summary>
class CpaotRunner : CompilerRunner
{
    protected override string CompilerFileName => "ilc.exe";

    public CpaotRunner(string compilerFolder, string inputFolder, string outputFolder, IReadOnlyList<string> referenceFolders) : base(compilerFolder, inputFolder, outputFolder, referenceFolders) {}

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

        foreach (var reference in ComputeManagedAssemblies.GetManagedAssembliesInFolder(_inputPath))
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
