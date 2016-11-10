﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;

namespace ILCompiler
{
    public sealed class CppCodegenCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        CppCodegenConfigProvider _config = new CppCodegenConfigProvider(Array.Empty<string>());

        public CppCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(new CppCodegenNodeFactory(context, group))
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            _config = new CppCodegenConfigProvider(options);
            return this;
        }

        public override ICompilation ToCompilation()
        {
            return new CppCodegenCompilation(CreateDependencyGraph(), _nodeFactory, _compilationRoots, _logger, _config);
        }
    }

    internal class CppCodegenConfigProvider
    {
        private readonly HashSet<string> _options;
        
        public const string NoLineNumbersString = "NoLineNumbers";

        public CppCodegenConfigProvider(IEnumerable<string> options)
        {
            _options = new HashSet<string>(options, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasOption(string optionName)
        {
            return _options.Contains(optionName);
        }
    }
}
