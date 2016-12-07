// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

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
            if (method.IsInternalCall)
            {
                // The only way to locate the entrypoint for an internal call is through the RuntimeImportAttribute.
                if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
                {
                    return new RuntimeImportMethodNode(method);
                }

                // On CLR this would throw a SecurityException with "ECall methods must be packaged into a system module."
                // This is a corner case that nobody is likely to care about.
                throw new TypeSystemException.InvalidProgramException(ExceptionStringID.InvalidProgramSpecific, method);
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
            return new ReadyToRunHelperNode(this, helperCall.Item1, helperCall.Item2);
        }
    }
}
