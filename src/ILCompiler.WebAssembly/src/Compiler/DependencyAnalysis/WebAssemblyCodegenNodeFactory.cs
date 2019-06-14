// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class WebAssemblyCodegenNodeFactory : NodeFactory
    {
        private NodeCache<MethodDesc, WebAssemblyVTableSlotNode> _vTableSlotNodes;

        public WebAssemblyCodegenNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, MetadataManager metadataManager,
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
            _vTableSlotNodes = new NodeCache<MethodDesc, WebAssemblyVTableSlotNode>(methodKey =>
            {
                return new WebAssemblyVTableSlotNode(methodKey);
            });
        }

        public override bool IsCppCodegenTemporaryWorkaround => true;

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                return new WebAssemblyMethodBodyNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(this, method);
            }
        }

        public WebAssemblyVTableSlotNode VTableSlot(MethodDesc method)
        {
            return _vTableSlotNodes.GetOrAdd(method);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            return new WebAssemblyUnboxingThunkNode(TypeSystemContext.GetUnboxingThunk(method, TypeSystemContext.GeneratedAssembly));
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            throw new NotSupportedException();
        }
    }
}
