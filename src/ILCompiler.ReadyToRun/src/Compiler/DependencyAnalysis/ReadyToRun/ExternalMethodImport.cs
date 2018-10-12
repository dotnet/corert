﻿
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

        private readonly MethodWithGCInfo _localMethod;

        public ExternalMethodImport(
            ReadyToRunCodegenNodeFactory factory,
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodDesc,
            TypeDesc constrainedType,
            SignatureContext signatureContext,
            bool isUnboxingStub,
            MethodWithGCInfo localMethod)
            : base(
                  factory,
                  factory.MethodImports, 
                  ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall,
                  factory.MethodSignature(
                      fixupKind, 
                      methodDesc, 
                      constrainedType, 
                      signatureContext, 
                      isUnboxingStub, 
                      isInstantiatingStub: false))
        {
            _methodDesc = methodDesc;
            _signatureContext = signatureContext;
            _localMethod = localMethod;
        }

        public MethodDesc Method => _methodDesc;
        public MethodWithGCInfo MethodCodeNode => _localMethod;

        public override int ClassCode => 458823351;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            foreach (DependencyListEntry entry in base.GetStaticDependencies(factory))
            {
                yield return entry;
            }
            if (_localMethod != null)
            {
                yield return new DependencyListEntry(_localMethod, "Local method import");
            }
        }
    }
}
