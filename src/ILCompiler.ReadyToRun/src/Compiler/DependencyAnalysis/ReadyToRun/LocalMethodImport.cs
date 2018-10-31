
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class LocalMethodImport : DelayLoadHelperImport, IMethodNode
    {
        private readonly SignatureContext _signatureContext;

        private readonly MethodWithGCInfo _localMethod;

        /// <summary>
        /// For shared generic methods, this is the original method before canonicalization.
        /// </summary>
        private readonly MethodDesc _originalMethod;

        public LocalMethodImport(
            ReadyToRunCodegenNodeFactory factory,
            ReadyToRunFixupKind fixupKind,
            MethodWithGCInfo localMethod,
            MethodDesc originalMethod,
            bool isUnboxingStub,
            SignatureContext signatureContext)
            : base(
                  factory,
                  factory.MethodImports,
                  ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall,
                  factory.MethodSignature(
                      fixupKind,
                      targetMethod: localMethod.Method,
                      constrainedType: null,
                      originalMethod: null,
                      methodToken: default(ModuleToken),
                      signatureContext: signatureContext,
                      isUnboxingStub: isUnboxingStub,
                      isInstantiatingStub: false))
        {
            _signatureContext = signatureContext;
            _localMethod = localMethod;
            _originalMethod = originalMethod;
        }

        public MethodDesc Method => _localMethod.Method;
        public MethodWithGCInfo MethodCodeNode => _localMethod;

        public override int ClassCode => 459923351;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            foreach (DependencyListEntry entry in base.GetStaticDependencies(factory))
            {
                yield return entry;
            }

            yield return new DependencyListEntry(_localMethod, "Local method import");

            if (_originalMethod != null)
            {
                foreach (TypeDesc methodInstArg in _originalMethod.Instantiation)
                {
                    yield return new DependencyListEntry(factory.NecessaryTypeSymbol(methodInstArg), "Method instantiation argument");
                }

                yield return new DependencyListEntry(factory.NecessaryTypeSymbol(_originalMethod.OwningType), "Method owning type");

                foreach (TypeDesc typeInstArg in _originalMethod.OwningType.Instantiation)
                {
                    yield return new DependencyListEntry(factory.NecessaryTypeSymbol(typeInstArg), "Type instantiation argument");
                }
            }
        }
    }
}
