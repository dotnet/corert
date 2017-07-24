// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

using AssemblyName = System.Reflection.AssemblyName;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Encapsulates a list of class constructors that must be run in a prescribed order during start-up
    /// </summary>
    public sealed class LibraryInitializers
    {
        private const string ClassLibraryPlaceHolderString = "*ClassLibrary*";
        private const string LibraryInitializerContainerNamespaceName = "Internal.Runtime.CompilerHelpers";
        private const string LibraryInitializerContainerTypeName = "LibraryInitializer";
        private const string LibraryInitializerMethodName = "InitializeLibrary";

        private static readonly LibraryInitializerInfo[] s_assembliesWithLibraryInitializers =
            {
                new LibraryInitializerInfo(ClassLibraryPlaceHolderString),
                new LibraryInitializerInfo("System.Private.TypeLoader"),
                new LibraryInitializerInfo("System.Private.Reflection.Execution"),
                new LibraryInitializerInfo("System.Private.DeveloperExperience.Console"),
                new LibraryInitializerInfo("System.Private.Interop"),
            };

        private List<MethodDesc> _libraryInitializerMethods;

        private readonly TypeSystemContext _context;
        private readonly bool _isCppCodeGen;
        private readonly bool _isWasmCodeGen;

        public LibraryInitializers(TypeSystemContext context, bool isCppCodeGen, bool isWasmCodeGen)
        {
            _context = context;
            //
            // We should not care which code-gen is being used but for the time being
            // this can be useful to workaround CppCodeGen bugs.
            //
            _isCppCodeGen = isCppCodeGen;
            _isWasmCodeGen = isWasmCodeGen;
        }

        public IList<MethodDesc> LibraryInitializerMethods
        {
            get
            {
                if (_libraryInitializerMethods == null)
                    InitLibraryInitializers();

                return _libraryInitializerMethods;
            }
        }

        private void InitLibraryInitializers()
        {
            Debug.Assert(_libraryInitializerMethods == null);
            
            _libraryInitializerMethods = new List<MethodDesc>();

            foreach (var entry in s_assembliesWithLibraryInitializers)
            {
                if (_isCppCodeGen && !entry.UseWithCppCodeGen)
                    continue;

                ModuleDesc assembly = entry.Assembly == ClassLibraryPlaceHolderString
                    ? _context.SystemModule
                    : _context.ResolveAssembly(new AssemblyName(entry.Assembly), false);

                if (assembly == null)
                    continue;

                TypeDesc containingType = assembly.GetType(LibraryInitializerContainerNamespaceName, LibraryInitializerContainerTypeName, false);
                if (containingType == null)
                    continue;

                MethodDesc initializerMethod = containingType.GetMethod(LibraryInitializerMethodName, null);
                if (initializerMethod == null)
                    continue;

                _libraryInitializerMethods.Add(initializerMethod);
            }
        }

        private sealed class LibraryInitializerInfo
        {
            public string Assembly { get; }
            public bool UseWithCppCodeGen { get; }

            public LibraryInitializerInfo(string assembly, bool useWithCppCodeGen = true)
            {
                Assembly = assembly;
                UseWithCppCodeGen = useWithCppCodeGen;
            }
        }
    }
}
