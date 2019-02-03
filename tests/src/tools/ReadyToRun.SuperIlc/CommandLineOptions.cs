// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;

namespace ReadyToRun.SuperIlc
{
    internal enum ReadyToRunTool
    {
        Crossgen,
        Cpaot
    }

    internal class CommandLineOptions
    {
        private string _inputPath;
        private string _outputPath;
        private bool _crossgen;
        private bool _cpaot;
        private string _toolPath;
        private IReadOnlyList<string> _references = Array.Empty<string>();
        private bool _helpRequested;
        public ReadyToRunTool OptimizingTool => _crossgen ? ReadyToRunTool.Crossgen : ReadyToRunTool.Cpaot;
        public string InputPath => _inputPath;
        public string OutputPath => _outputPath;
        public string ToolPath => _toolPath;
        public IReadOnlyList<string> ReferenceFolders => _references;
        public bool Help => _helpRequested;

        public ArgumentSyntax ParseCommandLine(string[] args)
        {
            ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.ApplicationName = "ReadyToRun.SuperIlc";
                syntax.HandleHelp = false;
                syntax.HandleErrors = false;
                syntax.HandleResponseFiles = true;
                
                syntax.DefineOption("h|help", ref _helpRequested, "");
                syntax.DefineOption("crossgen", ref _crossgen, "Compile with Crossgen");
                syntax.DefineOption("cpaot", ref _cpaot, "Compile with CPAOT");
                syntax.DefineOptionList("r|ref", ref _references, "Folder containing assemblies to reference");
                syntax.DefineOption("in", ref _inputPath, "Input folder of assemblies to optimize");
                syntax.DefineOption("out", ref _outputPath, "Output folder for optimized assemblies");
                syntax.DefineOption("toolpath", ref _toolPath, "Path to optimizing tool");
            });

            return argSyntax;
        }

        public bool Validate()
        {
            if (_crossgen && _cpaot)
            {
                Console.WriteLine($"Error: --crossgen and --cpaot both specified");
            }

            if (!Directory.Exists(InputPath))
            {
                Console.WriteLine($"Error: Input folder {InputPath} does not exist");
                return false;
            }

            if (!Directory.Exists(ToolPath))
            {
                Console.WriteLine($"Error: Tool folder {ToolPath} does not exist");
                return false;
            }

            return true;
        }
    }
}
