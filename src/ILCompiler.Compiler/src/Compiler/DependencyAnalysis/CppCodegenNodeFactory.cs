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
            return new ReadyToRunHelperNode(helperCall.Item1, helperCall.Item2);
        }

        public override void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            base.AttachToDependencyGraph(graph);

            var rooting = new RootingServiceProvider(graph, this);

            foreach (var wkt in Enum.GetValues(typeof(WellKnownType)))
            {
                if ((WellKnownType)wkt == WellKnownType.Unknown)
                    continue;

                var wellKnownType = TypeSystemContext.GetWellKnownType((WellKnownType)wkt);
                
                if (wellKnownType.IsPrimitive)
                {
                    rooting.AddCompilationRoot(wellKnownType, "Primitive well-known types are rooted");
                    System.Diagnostics.Debug.WriteLine("Rooting {0}", wellKnownType.ToString());
                }
            }
        }
    }
}
