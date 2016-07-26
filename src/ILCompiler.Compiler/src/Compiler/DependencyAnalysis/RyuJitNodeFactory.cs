// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class RyuJitNodeFactory : NodeFactory
    {
        public RyuJitNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup)
            : base(context, compilationModuleGroup)
        {
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                return new RuntimeImportMethodNode(method);
            }

            if (CompilationModuleGroup.ContainsMethod(method))
            {
                return new MethodCodeNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(method);
            }
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            return new UnboxingStubNode(method);
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(Tuple<ReadyToRunHelperId, object> helperCall)
        {
            return new ReadyToRunHelperNode(helperCall.Item1, helperCall.Item2);
        }
    }
}
