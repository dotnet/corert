// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    class SingleFileCompilationModuleGroup : CompilationModuleGroup
    {
        public SingleFileCompilationModuleGroup(CompilerTypeSystemContext typeSystemContext, ICompilationRootProvider rootProvider) : base(typeSystemContext, rootProvider)
        { }

        public override bool IsTypeInCompilationGroup(TypeDesc type)
        {
            return true;
        }

        public override bool IsMethodInCompilationGroup(MethodDesc method)
        {
            return true;
        }

        public override void AddCompilationRoots()
        {
            base.AddCompilationRoots();

            AddCompilationRootsForRuntimeExports((EcmaModule)_typeSystemContext.SystemModule);
        }
    }
}
