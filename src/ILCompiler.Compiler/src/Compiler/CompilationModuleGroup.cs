// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.IL;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public abstract class CompilationModuleGroup
    {
        protected CompilerTypeSystemContext _typeSystemContext;

        protected CompilationModuleGroup(CompilerTypeSystemContext typeSystemContext)
        {
            _typeSystemContext = typeSystemContext;
        }

        /// <summary>
        /// If true, "type" is in the set of input assemblies being compiled
        /// </summary>
        public abstract bool ContainsType(TypeDesc type);
        /// <summary>
        /// If true, "method" is in the set of input assemblies being compiled
        /// </summary>
        public abstract bool ContainsMethod(MethodDesc method);
        /// <summary>
        /// If true, it's possible for "type" to be generated in multiple modules independently and should be shared
        /// TODO: This API is in flux. Please do not add a dependency on it.
        /// </summary>
        public abstract bool ShouldShareAcrossModules(TypeDesc type);
        /// <summary>
        /// If true, it's possible for "method" to be generated in multiple modules independently and should be shared
        /// TODO: This API is in flux. Please do not add a dependency on it.
        /// </summary>
        public abstract bool ShouldShareAcrossModules(MethodDesc method);
        /// <summary>
        /// If true, all code is compiled into a single module
        /// </summary>
        public abstract bool IsSingleFileCompilation { get; }
        /// <summary>
        /// If true, the full type should be generated. This occurs in situations where the type is 
        /// shared between modules (generics, parameterized types), or the type lives in a different module
        /// and therefore needs a full VTable
        /// </summary>
        public abstract bool ShouldProduceFullType(TypeDesc type);
        /// <summary>
        /// If true, the type will not be linked into the same module as the current compilation and therefore
        /// accessed through the target platform's import mechanism (ie, Import Address Table on Windows)
        /// </summary>
        public abstract bool ShouldReferenceThroughImportTable(TypeDesc type);
    }
}
