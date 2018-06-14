// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class ReadyToRunCodegenNodeFactory : NodeFactory
    {
        
        public ReadyToRunCodegenNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, MetadataManager metadataManager,
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

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            throw new NotImplementedException();
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            throw new NotImplementedException();
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            throw new NotImplementedException();
        }

        public override void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {

        }
    }
}
