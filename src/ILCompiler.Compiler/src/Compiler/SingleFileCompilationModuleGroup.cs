// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    class SingleFileCompilationModuleGroup : CompilationModuleGroup
    {
        public SingleFileCompilationModuleGroup(CompilerTypeSystemContext typeSystemContext, ICompilationRootProvider rootProvider) : base(typeSystemContext, rootProvider)
        { }

        public override bool ContainsType(TypeDesc type)
        {
            return true;
        }

        public override bool ContainsMethod(MethodDesc method)
        {
            return true;
        }

        public override void AddCompilationRoots()
        {
            base.AddCompilationRoots();

            AddCompilationRootsForExports((EcmaModule)_typeSystemContext.SystemModule);
        }

        public override bool IsSingleFileCompilation
        {
            get
            {
                return true;
            }
        }

        public override bool ShouldShareAcrossModules(MethodDesc method)
        {
            return false;
        }

        public override bool ShouldShareAcrossModules(TypeDesc type)
        {
            return false;
        }

        public override bool ShouldProduceFullType(TypeDesc type)
        {
            return false;
        }
    }
}
