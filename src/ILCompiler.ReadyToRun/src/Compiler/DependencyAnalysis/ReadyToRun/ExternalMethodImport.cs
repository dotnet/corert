
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ExternalMethodImport : DelayLoadHelperImport, IMethodNode
    {
        private readonly MethodDesc _targetMethod;

        private readonly SignatureContext _signatureContext;

        public ExternalMethodImport(
            ReadyToRunCodegenNodeFactory factory,
            ReadyToRunFixupKind fixupKind,
            MethodDesc targetMethod,
            TypeDesc constrainedType,
            MethodDesc originalMethod,
            ModuleToken methodToken,
            bool isUnboxingStub,
            SignatureContext signatureContext)
            : base(
                  factory,
                  factory.MethodImports,
                  ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall,
                  factory.MethodSignature(
                      fixupKind,
                      targetMethod: targetMethod,
                      constrainedType: constrainedType,
                      originalMethod: originalMethod,
                      methodToken: methodToken,
                      signatureContext: signatureContext,
                      isUnboxingStub: isUnboxingStub,
                      isInstantiatingStub: false))
        {
            _targetMethod = targetMethod;
            _signatureContext = signatureContext;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            foreach (DependencyListEntry baseEntry in base.GetStaticDependencies(factory))
            {
                yield return baseEntry;
            }
            foreach (TypeDesc methodInstArg in _targetMethod.Instantiation)
            {
                yield return new DependencyListEntry(factory.NecessaryTypeSymbol(methodInstArg), "Method instantiation argument");
            }
            yield return new DependencyListEntry(factory.NecessaryTypeSymbol(_targetMethod.OwningType), "Method owning type");
            foreach (TypeDesc typeInstArg in _targetMethod.OwningType.Instantiation)
            {
                yield return new DependencyListEntry(factory.NecessaryTypeSymbol(typeInstArg), "Type instantiation argument");
            }
        }

        public MethodDesc Method => _targetMethod;

        public override int ClassCode => 458823351;
    }
}
