// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public enum GenericContextKind
    {
        ThisObj,
        TypeDictionary,
        MethodDictionary,
    }

    public partial class ReadyToRunGenericLookupHelperNode : AssemblyStubNode
    {
        private object _target;
        GenericContextKind _context;
        ReadyToRunFixupKind _fixupKind;

        public ReadyToRunGenericLookupHelperNode(GenericContextKind context, ReadyToRunFixupKind fixupKind, object target)
        {
            _target = target;
            _context = context;
            _fixupKind = fixupKind;
        }

        public override string GetName()
        {
            return MangledName;
        }

        public override string MangledName
        {
            get
            {
                string mangledTargetName = NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                return string.Concat("__GenericLookup_", _context.ToString(), "_", _fixupKind.ToString(), "_", mangledTargetName);
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }
    }
}
