// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using Internal.Runtime.TypeLoader;

namespace Internal.Runtime.JitSupport
{
    public class JitGenericMethodDictionaryNode : ExternObjectSymbolNode
    {
        public JitGenericMethodDictionaryNode(InstantiatedMethod method)
        {
            Method = method;
        }

        public InstantiatedMethod Method { get; }

        public override GenericDictionaryCell GetDictionaryCell()
        {
            return GenericDictionaryCell.CreateMethodDictionaryCell(Method);
        }
    }
}
