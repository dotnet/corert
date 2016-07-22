// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a generic dictionary for a concrete generic type instantiation
    /// or generic method instantiation. The dictionary is used from canonical code
    /// at runtime to look up runtime artifacts that depend on the concrete
    /// context the generic type or method was instantiated with.
    /// </summary>
    class GenericDictionary : ObjectNode, ISymbolNode
    {
        private object _owningMethodOrType;

        public GenericDictionary(object owningMethodOrType)
        {
            _owningMethodOrType = owningMethodOrType;
            Validate();
        }

        [Conditional("DEBUG")]
        private void Validate()
        {
            TypeDesc owningType = _owningMethodOrType as TypeDesc;
            if (owningType != null)
            {
                Debug.Assert(!owningType.IsCanonicalSubtype(CanonicalFormKind.Any));
                Debug.Assert(!owningType.IsRuntimeDeterminedSubtype);
                Debug.Assert(owningType.HasInstantiation);
            }
            else
            {
                MethodDesc owningMethod = (MethodDesc)_owningMethodOrType;
                Debug.Assert(!owningMethod.IsCanonicalMethod(CanonicalFormKind.Any));
                Debug.Assert(owningMethod.HasInstantiation);
            }
        }

        private TypeSystemContext Context
        {
            get
            {
                TypeDesc type = _owningMethodOrType as TypeDesc;
                return type != null ? type.Context : ((MethodDesc)_owningMethodOrType).Context;
            }
        }

        private void GetInstantiations(out Instantiation typeInstantiation, out Instantiation methodInstantiation)
        {
            TypeDesc type = _owningMethodOrType as TypeDesc;
            if (type != null)
            {
                typeInstantiation = type.Instantiation;
                methodInstantiation = new Instantiation();
            }
            else
            {
                MethodDesc method = (MethodDesc)_owningMethodOrType;
                typeInstantiation = method.OwningType.Instantiation;
                methodInstantiation = method.Instantiation;
            }
        }

        public override ObjectNodeSection Section
        {
            get
            {
                if (Context.Target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public string MangledName
        {
            get
            {
                TypeDesc owningType = _owningMethodOrType as TypeDesc;
                string mangledTargetName = owningType != null ?
                    NodeFactory.NameMangler.GetMangledTypeName(owningType) :
                    NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_owningMethodOrType);
                return "__GenericDict_" + mangledTargetName;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);
            builder.DefinedSymbols.Add(this);
            builder.RequireAlignment(factory.Target.PointerSize);

            // Node representing the generic dictionary doesn't have any dependencies for
            // dependency analysis purposes. The dependencies are tracked as dependencies of the
            // concrete method bodies. When we reach the object data emission phase, the dependencies
            // should all already have been marked.
            if (!relocsOnly)
            {
                object canonOwningMethodOrType;
                if (_owningMethodOrType is TypeDesc)
                    canonOwningMethodOrType = ((TypeDesc)_owningMethodOrType).ConvertToCanonForm(CanonicalFormKind.Specific);
                else
                    canonOwningMethodOrType = ((MethodDesc)_owningMethodOrType).GetCanonMethodTarget(CanonicalFormKind.Specific);

                DictionaryLayout layout = factory.GenericDictionaryLayout(canonOwningMethodOrType);
                Instantiation typeInstantiation;
                Instantiation methodInstantiation;
                GetInstantiations(out typeInstantiation, out methodInstantiation);

                foreach (var entry in layout.Entries)
                {
                    ISymbolNode targetNode = factory.GetGenericFixupTarget(entry.FixupKind, entry.Target, typeInstantiation, methodInstantiation);
                    builder.EmitPointerReloc(targetNode);
                }
            }

            return builder.ToObjectData();
        }

        public override string GetName()
        {
            return MangledName;
        }
    }

    internal static class DictionaryEntryExtensions
    {
        public static ISymbolNode GetGenericFixupTarget(this NodeFactory factory,
            ReadyToRunFixupKind fixupKind, object targetOfFixup, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            switch (fixupKind)
            {
                case ReadyToRunFixupKind.TypeHandle:
                    {
                        Debug.Assert(targetOfFixup is TypeDesc);
                        TypeDesc targetType = ((TypeDesc)targetOfFixup).InstantiateSignature(typeInstantiation, methodInstantiation);

                        // TODO: we should communicate if a constructed EEType is really needed
                        return factory.ConstructedTypeSymbol(targetType);
                    }

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
