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

        private void AddReflectionInitializationCode(IRootingServiceProvider rootProvider)
        {
            // System.Private.Reflection.Execution needs to establish a communication channel with System.Private.CoreLib
            // at process startup. This is done through an eager constructor that calls into CoreLib and passes it
            // a callback object.
            //
            // Since CoreLib cannot reference anything, the type and it's eager constructor won't be added to the compilation
            // unless we explictly add it.

            var refExec = _typeSystemContext.GetModuleForSimpleName("System.Private.Reflection.Execution", false);
            if (refExec != null)
            {
                var exec = refExec.GetKnownType("Internal.Reflection.Execution", "ReflectionExecution");
                if (ContainsType(exec))
                {
                    rootProvider.AddCompilationRoot(exec.GetStaticConstructor(), "Reflection execution");
                }
            }
            else
            {
                // If we can't find Reflection.Execution, we better be compiling a nonstandard thing (managed
                // portion of the runtime maybe?).
                Debug.Assert(_typeSystemContext.GetModuleForSimpleName("System.Private.CoreLib", false) == null);
            }
        }

        
        
    }
}
