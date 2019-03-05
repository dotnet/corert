
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ExternalMethodImport : DelayLoadHelperImport, IMethodNode
    {
        private readonly MethodDesc _methodDesc;

        private readonly SignatureContext _signatureContext;

        public ExternalMethodImport(
            ReadyToRunCodegenNodeFactory factory,
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodDesc,
            TypeDesc constrainedType,
            ModuleToken methodToken,
            bool isUnboxingStub,
            bool isInstantiatingStub,
            SignatureContext signatureContext)
            : base(
                  factory,
                  factory.MethodImports,
                  ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall,
                  factory.MethodSignature(
                      fixupKind,
                      methodDesc,
                      constrainedType,
                      methodToken,
                      isUnboxingStub,
                      isInstantiatingStub,
                      signatureContext))
        {
            _methodDesc = methodDesc;
            _signatureContext = signatureContext;
        }

        public MethodDesc Method => _methodDesc;

        public override int ClassCode => 458823351;
    }
}
