// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.JitInterface;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// This class represents a single method indirection cell resolved using fixup table
    /// at function startup; in addition to PrecodeHelperImport instances of this import type
    /// emit GC ref map entries into the R2R executable.
    /// </summary>
    public class PrecodeHelperMethodImport : PrecodeHelperImport, IMethodNode
    {
        private readonly MethodWithToken _methodWithToken;
        private readonly bool _useInstantiatingStub;
        private readonly SignatureContext _signatureContext;

        public PrecodeHelperMethodImport(ReadyToRunCodegenNodeFactory factory, MethodWithToken methodWithToken, Signature signature, SignatureContext signatureContext, bool useInstantiatingStub)
            : base(factory, signature)
        {
            _methodWithToken = methodWithToken;
            _useInstantiatingStub = useInstantiatingStub;
            _signatureContext = signatureContext;
        }

        protected override string GetName(NodeFactory factory)
        {
            return "PrecodeHelperMethodImport->" + ImportSignature.GetMangledName(factory.NameMangler);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            foreach (DependencyListEntry baseEntry in base.GetStaticDependencies(factory))
            {
                yield return baseEntry;
            }
            if (_useInstantiatingStub)
            {
                // Require compilation of the canonical version for instantiating stubs
                MethodDesc canonMethod = _methodWithToken.Method.GetCanonMethodTarget(CanonicalFormKind.Specific);
                ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
                ISymbolNode canonMethodNode = r2rFactory.MethodEntrypoint(
                    canonMethod,
                    constrainedType: null,
                    originalMethod: canonMethod,
                    methodToken: _methodWithToken.Token,
                    signatureContext: _signatureContext);
                yield return new DependencyListEntry(canonMethodNode, "Canonical method for instantiating stub");
            }
        }

        public override int ClassCode => 668765432;

        public MethodDesc Method => _methodWithToken.Method;
    }
}
