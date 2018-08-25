
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ExternalMethodImport : Import, IMethodNode
    {
        private readonly MethodDesc _methodDesc;

        private readonly ModuleToken _token;

        private readonly MethodWithGCInfo _localMethod;

        public ExternalMethodImport(
            ReadyToRunCodegenNodeFactory factory,
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodDesc,
            ModuleToken token,
            TypeDesc constrainedType,
            bool isUnboxingStub,
            MethodWithGCInfo localMethod)
            : base(factory.MethodImports, factory.MethodSignature(fixupKind, methodDesc, token, constrainedType, isUnboxingStub, isInstantiatingStub: false))
        {
            _methodDesc = methodDesc;
            _token = token;
            _localMethod = localMethod;
        }

        public MethodDesc Method => _methodDesc;
        public MethodWithGCInfo MethodCodeNode => _localMethod;

        public override int ClassCode => 458823351;

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

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            base.AppendMangledName(nameMangler, sb);
            sb.Append(" = ");
            sb.Append(_token.ToString());
        }
    }
}
