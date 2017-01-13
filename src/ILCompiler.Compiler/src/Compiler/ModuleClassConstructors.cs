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
    public sealed class ModuleClassConstructors
    {
        private const string ClassLibraryPlaceHolderString = "*ClassLibrary*";
        private const string ModuleCctorContainerTypeName = "Internal.Runtime.CompilerHelpers.ILT_ModuleCctorContainer";
        private const string ModuleCctorMethodName = "ILT_cctor";

        private static readonly ModuleClassConstructorInfo[] s_assembliesWithModuleCctors =
            {
                new ModuleClassConstructorInfo(ClassLibraryPlaceHolderString),
                new ModuleClassConstructorInfo("System.Private.TypeLoader", false),
                new ModuleClassConstructorInfo("System.Private.Reflection.Execution", false)
            };

        private List<MethodDesc> _moduleCctorMethods;

        private readonly TypeSystemContext _context;
        private readonly bool _isCppCodeGen;

        public ModuleClassConstructors(TypeSystemContext context, bool isCppCodeGen)
        {
            _context = context;
            //
            // We should not care which code-gen is being used, however currently CppCodeGen cannot
            // handle code pulled in by all explicit cctors.
            //
            _isCppCodeGen = isCppCodeGen;
        }
        
        public IList<MethodDesc> ModuleCctorMethods
        {
            get
            {
                if (_moduleCctorMethods == null)
                    InitExplicitCctors();

                return _moduleCctorMethods;
            }
        }

        private void InitExplicitCctors()
        {
            Debug.Assert(_moduleCctorMethods == null);
            
            _moduleCctorMethods = new List<MethodDesc>();

            foreach (var entry in s_assembliesWithModuleCctors)
            {
                if (_isCppCodeGen && !entry.UseWithCppCodeGen)
                    continue;

                ModuleDesc assembly = entry.Assembly == ClassLibraryPlaceHolderString
                    ? _context.SystemModule
                    : _context.ResolveAssembly(new AssemblyName(entry.Assembly), false);

                if (assembly == null)
                    continue;

                TypeDesc containingType = assembly.GetTypeByCustomAttributeTypeName(ModuleCctorContainerTypeName);
                if (containingType == null)
                    continue;

                MethodDesc cctor = containingType.GetMethod(ModuleCctorMethodName, null);
                if (cctor == null)
                    continue;

                _moduleCctorMethods.Add(cctor);
            }
        }

        private sealed class ModuleClassConstructorInfo
        {
            public string Assembly { get; }
            public bool UseWithCppCodeGen { get; }

            public ModuleClassConstructorInfo(string assembly, bool useWithCppCodeGen = true)
            {
                Assembly = assembly;
                UseWithCppCodeGen = useWithCppCodeGen;
            }
        }
    }
}
