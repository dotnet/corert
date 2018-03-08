// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class CppCodegenNodeFactory : NodeFactory
    {
        public CppCodegenNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, MetadataManager metadataManager,
            InteropStubManager interopStubManager, NameMangler nameMangler, VTableSliceProvider vtableSliceProvider, DictionaryLayoutProvider dictionaryLayoutProvider)
            : base(context, 
                  compilationModuleGroup, 
                  metadataManager, 
                  interopStubManager, 
                  nameMangler, 
                  new LazyGenericsDisabledPolicy(), 
                  vtableSliceProvider, 
                  dictionaryLayoutProvider, 
                  new ImportedNodeProviderThrowing())
        {
        }

        public override bool IsCppCodegenTemporaryWorkaround => true;

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                return new CppMethodCodeNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(this, method);
            }
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            return new CppUnboxingStubNode(method);
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            // TODO: this is wrong: this returns an assembly stub node
            return new ReadyToRunHelperNode(this, helperCall.HelperId, helperCall.Target);
        }
    }
}
