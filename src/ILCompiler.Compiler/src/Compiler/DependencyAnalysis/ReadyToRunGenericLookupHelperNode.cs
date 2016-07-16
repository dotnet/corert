// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public enum GenericContextKind
    {
        Dictionary,
        ThisObj,
    }

    public partial class ReadyToRunGenericLookupHelperNode : AssemblyStubNode
    {
        private object _target;
        private object _context;
        private ReadyToRunFixupKind _fixupKind;
        private GenericContextKind _contextKind;

        public ReadyToRunGenericLookupHelperNode(object context, GenericContextKind contextKind, ReadyToRunFixupKind fixupKind, object target)
        {
            Debug.Assert((context is TypeDesc && ((TypeDesc)context).HasInstantiation)
                || (context is MethodDesc && ((MethodDesc)context).HasInstantiation));

            Debug.Assert(contextKind != GenericContextKind.ThisObj || context is TypeDesc);

            _context = context;
            _contextKind = contextKind;
            _target = target;
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

                string mangledContextName;
                if (_context is MethodDesc)
                    mangledContextName = NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_context);
                else
                    mangledContextName = NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_context);

                string prefix = _contextKind == GenericContextKind.Dictionary ?
                    "__GenericLookupFromDict_" : "__GenericLookupFromThis_";

                return string.Concat(prefix, mangledContextName, "_", _fixupKind.ToString(), "_", mangledTargetName);
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
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
