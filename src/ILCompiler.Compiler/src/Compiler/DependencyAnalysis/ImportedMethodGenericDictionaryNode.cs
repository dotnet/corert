// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class ImportedMethodGenericDictionaryNode : ExternSymbolNode
    {
        private MethodDesc _owningMethod;

        public ImportedMethodGenericDictionaryNode(NodeFactory factory, MethodDesc owningMethod)
            : base("__imp_" + MethodGenericDictionaryNode.GetMangledName(factory.NameMangler, owningMethod))
        {
            _owningMethod = owningMethod;
        }

        public override bool RepresentsIndirectionCell => true;
    }
}