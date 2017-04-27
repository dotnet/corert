// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using Internal.Runtime.TypeLoader;

namespace Internal.Runtime.JitSupport
{
    internal class JitMethodEntrypointNode : ExternObjectSymbolNode, IMethodNode
    {
        public JitMethodEntrypointNode(MethodDesc m)
        {
            Method = m;
        }

        public MethodDesc Method { get; }

        public override GenericDictionaryCell GetDictionaryCell()
        {
            return GenericDictionaryCell.CreateExactCallableMethodCell(Method);
        }
    }
}
