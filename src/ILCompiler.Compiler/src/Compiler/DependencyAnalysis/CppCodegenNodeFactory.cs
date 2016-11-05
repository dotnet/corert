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
        public CppCodegenNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup)
            : base(context, compilationModuleGroup)
        {
        }

        public override void AttachToDependencyGraph(DependencyAnalysisFramework.DependencyAnalyzerBase<NodeFactory> graph)
        {
            AddWellKnownTypes(graph);
            base.AttachToDependencyGraph(graph);
        }
        
        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (CompilationModuleGroup.ContainsMethod(method))
            {
                return new CppMethodCodeNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(method);
            }
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            // TODO: this is wrong: this returns an assembly stub node
            return new UnboxingStubNode(method);
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(Tuple<ReadyToRunHelperId, object> helperCall)
        {
            // TODO: this is wrong: this returns an assembly stub node
            return new ReadyToRunHelperNode(this, helperCall.Item1, helperCall.Item2);
        }

        private void AddWellKnownType(WellKnownType wellKnownType, DependencyAnalysisFramework.DependencyAnalyzerBase<NodeFactory> graph)
        {
            var type = TypeSystemContext.GetWellKnownType(wellKnownType);
            var typeNode = ConstructedTypeSymbol(type);
            graph.AddRoot(typeNode, "Enables CPP codegen");
        }
        private void AddWellKnownTypes(DependencyAnalysisFramework.DependencyAnalyzerBase<NodeFactory> graph)
        {

            AddWellKnownType(WellKnownType.Void, graph);
            AddWellKnownType(WellKnownType.Boolean, graph);
            AddWellKnownType(WellKnownType.Char, graph);
            AddWellKnownType(WellKnownType.SByte, graph);
            AddWellKnownType(WellKnownType.Byte, graph);
            AddWellKnownType(WellKnownType.Int16, graph);
            AddWellKnownType(WellKnownType.UInt16, graph);
            AddWellKnownType(WellKnownType.Int32, graph);
            AddWellKnownType(WellKnownType.UInt32, graph);
            AddWellKnownType(WellKnownType.Int64, graph);
            AddWellKnownType(WellKnownType.UInt64, graph);
            AddWellKnownType(WellKnownType.IntPtr, graph);
            AddWellKnownType(WellKnownType.UIntPtr, graph);
            AddWellKnownType(WellKnownType.Single, graph);
            AddWellKnownType(WellKnownType.Double, graph);

        }

    }
}
