// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public enum GenericContextKind
    {
        Dictionary,
        ThisObj,
    }

    public sealed partial class ReadyToRunGenericLookupHelperNode : AssemblyStubNode
    {
        private TypeSystemEntity _typeOrMethodContext;
        private GenericContextKind _contextKind;
        private DictionaryEntry _target;

        public DictionaryEntry Lookup => _target;

        public ReadyToRunGenericLookupHelperNode(TypeSystemEntity associatedCanonMethodOrType, GenericContextKind contextKind, DictionaryEntry target)
        {
            Debug.Assert((
                associatedCanonMethodOrType is TypeDesc &&
                    ((TypeDesc)associatedCanonMethodOrType).IsCanonicalSubtype(CanonicalFormKind.Any))
                || (associatedCanonMethodOrType is MethodDesc &&
                    ((MethodDesc)associatedCanonMethodOrType).HasInstantiation && ((MethodDesc)associatedCanonMethodOrType).IsCanonicalMethod(CanonicalFormKind.Any)));
            Debug.Assert(contextKind != GenericContextKind.ThisObj || associatedCanonMethodOrType is TypeDesc);

            _typeOrMethodContext = associatedCanonMethodOrType;
            _contextKind = contextKind;
            _target = target;
        }

        public override string GetName()
        {
            return MangledName;
        }

        public override string MangledName
        {
            get
            {
                string mangledContextName;
                if (_typeOrMethodContext is MethodDesc)
                    mangledContextName = NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_typeOrMethodContext);
                else
                    mangledContextName = NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_typeOrMethodContext);

                string prefix = _contextKind == GenericContextKind.Dictionary ?
                    "__GenericLookupFromDict_" : "__GenericLookupFromThis_";

                return string.Concat(prefix, mangledContextName, "_", _target.GetMangledName(NodeFactory.NameMangler));
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory) => true;

        protected override void OnMarked(NodeFactory factory)
        {
            // When the helper call gets marked, ensure the generic dictionaries will have the entry it's referring to.
            factory.GenericDictionaryLayout(_typeOrMethodContext).EnsureEntry(_target);
        }
    }

    internal static class ReadyToRunTargetLocator
    {
        /// <summary>
        /// When codegen requests a ready to run generic lookup helper, the token that triggered the
        /// need for the fixup might refer to something that is not a valid target for the fixup
        /// (e.g. a <see cref="MethodDesc"/> for a <see cref="ReadyToRunFixupKind.TypeHandle"/> fixup).
        /// This helper resolves the target to the thing that's applicable.
        /// </summary>
        public static DictionaryEntry GetTargetForFixup(object resolvedToken, ReadyToRunFixupKind fixupKind)
        {
            switch (fixupKind)
            {
                case ReadyToRunFixupKind.TypeHandle:
                    if (resolvedToken is TypeDesc)
                        return new TypeHandleDictionaryEntry((TypeDesc)resolvedToken);
                    else
                        return new TypeHandleDictionaryEntry(((MethodDesc)resolvedToken).OwningType);
                default:
                    throw new System.NotImplementedException();
            }
        }
    }
}
