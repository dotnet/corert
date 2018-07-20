
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.JitInterface;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ExternalMethodImport : Import, IMethodNode
    {
        private readonly MethodDesc _methodDesc;

        private readonly mdToken _token;

        private readonly MethodWithGCInfo _localMethod;

        public ExternalMethodImport(
            ReadyToRunCodegenNodeFactory factory,
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodDesc,
            mdToken token,
            MethodWithGCInfo localMethod,
            MethodFixupSignature.SignatureKind signatureKind)
            : base(factory.MethodImports, factory.GetOrAddMethodSignature(fixupKind, methodDesc, token, signatureKind))
        {
            factory.MethodImports.AddImport(factory, this);
            _methodDesc = methodDesc;
            _token = token;
            _localMethod = localMethod;
        }

        public MethodDesc Method => _methodDesc;

        int ISortableSymbolNode.ClassCode => 458823351;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            if (_localMethod == null)
            {
                return base.GetStaticDependencies(factory);
            }
            return new DependencyListEntry[] 
            {
                new DependencyListEntry(_localMethod, "Local method import"),
                new DependencyListEntry(ImportSignature, "Method fixup signature"),
            };
        }
    }
}
