// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReadyToRun.SuperIlc
{
    public class Application
    {
        private List<string> _compilationInputFiles;

        private string  _mainExecutable;

        private readonly List<ProcessInfo[]> _compilations;

        private readonly ProcessInfo[] _execution;

        public Application(
            List<string> compilationInputFiles, 
            string mainExecutable, 
            IEnumerable<CompilerRunner> compilerRunners, 
            string coreRunPath)
        {
            _compilationInputFiles = compilationInputFiles;
            _mainExecutable = mainExecutable;

            _compilations = new List<ProcessInfo[]>();

            foreach (string file in _compilationInputFiles)
            {
                ProcessInfo[] fileCompilations = new ProcessInfo[(int)CompilerIndex.Count];
                foreach (CompilerRunner runner in compilerRunners)
                {
                    ProcessInfo compilationProcess = runner.CompilationProcess(file);
                    fileCompilations[(int)runner.Index] = compilationProcess;
                }
                _compilations.Add(fileCompilations);
            }

            if (_mainExecutable != null && !string.IsNullOrEmpty(coreRunPath))
            {
                _execution = new ProcessInfo[(int)CompilerIndex.Count];

                foreach (CompilerRunner runner in compilerRunners)
                {
                    HashSet<string> modules = new HashSet<string>();
                    HashSet<string> folders = new HashSet<string>();

                    modules.Add(_mainExecutable);
                    modules.UnionWith(_compilationInputFiles);
                    folders.Add(Path.GetDirectoryName(_mainExecutable).ToLower());
                    folders.UnionWith(runner.ReferenceFolders.Select(folder => folder.ToLower()));

                    _execution[(int)runner.Index] = runner.ExecutionProcess(_mainExecutable, modules, folders, coreRunPath);
                }
            }
        }

        public IEnumerable<ProcessInfo[]> Compilations => _compilations;

        public ProcessInfo[] Execution => _execution;
    }
}
