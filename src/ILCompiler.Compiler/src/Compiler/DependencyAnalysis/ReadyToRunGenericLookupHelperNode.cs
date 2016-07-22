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

    public partial class ReadyToRunGenericLookupHelperNode : AssemblyStubNode
    {
        private object _typeOrMethodContext;
        private GenericContextKind _contextKind;
        DictionaryEntry _target;

        public ReadyToRunGenericLookupHelperNode(object context, GenericContextKind contextKind, DictionaryEntry target)
        {
            Debug.Assert((context is TypeDesc && ((TypeDesc)context).IsRuntimeDeterminedSubtype)
                || (context is MethodDesc && ((MethodDesc)context).HasInstantiation));
            Debug.Assert(contextKind != GenericContextKind.ThisObj || context is TypeDesc);

            // If the target is a concrete type, why is it in a dictionary?
            Debug.Assert(target.IsRuntimeDetermined);

            _typeOrMethodContext = context;
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
                string mangledTargetName = NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target.Target);

                string mangledContextName;
                if (_typeOrMethodContext is MethodDesc)
                    mangledContextName = NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_typeOrMethodContext);
                else
                    mangledContextName = NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_typeOrMethodContext);

                string prefix = _contextKind == GenericContextKind.Dictionary ?
                    "__GenericLookupFromDict_" : "__GenericLookupFromThis_";

                return string.Concat(prefix, mangledContextName, "_", _target.FixupKind.ToString(), "_", mangledTargetName);
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }

        protected override void OnMarked(NodeFactory factory)
        {
            // When the helper call gets marked, ensure the generic dictionaries will have this.
            factory.GenericDictionaryLayout(_typeOrMethodContext).EnsureEntry(_target);
        }
    }

    public static class ReadyToRunTargetLocator
    {
        /// <summary>
        /// When codegen requests a ready to run generic lookup helper, the token that triggered the
        /// need for the fixup might refer to something that is not a valid target for the fixup
        /// (e.g. a <see cref="MethodDesc"/> for a <see cref="ReadyToRunFixupKind.TypeHandle"/> fixup).
        /// This helper resolves the target to the thing that's applicable.
        /// </summary>
        public static object GetTargetForFixup(object resolvedToken, ReadyToRunFixupKind fixupKind)
        {
            // TODO: Handle other transformations

            if (resolvedToken is MethodDesc)
            {
                switch (fixupKind)
                {
                    case ReadyToRunFixupKind.TypeHandle:
                        return ((MethodDesc)resolvedToken).OwningType;
                }
            }

            return resolvedToken;
        }
    }
}
