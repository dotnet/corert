﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    class GenericDictionary : ObjectNode, ISymbolNode
    {
        private object _owner;

        private GenericDictionary(object owner)
        {
            _owner = owner;
        }

        public GenericDictionary(TypeDesc type)
            : this((object)type)
        {
            //Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Any));
            //Debug.Assert(!type.IsRuntimeDeterminedSubtype);
            //Debug.Assert(type.HasInstantiation);
        }

        public GenericDictionary(MethodDesc method)
            : this((object)method)
        {
            //Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Any));
            //Debug.Assert(method.HasInstantiation);
        }

        private TypeSystemContext Context
        {
            get
            {
                TypeDesc type = _owner as TypeDesc;
                return type != null ? type.Context : ((MethodDesc)_owner).Context;
            }
        }

        private DictionaryLayout GetDictionaryLayout(NodeFactory factory)
        {
            TypeDesc type = _owner as TypeDesc;
            return type != null ? factory.TypeDictionaryLayout(type) : factory.MethodDictionaryLayout((MethodDesc)_owner);
        }

        private void GetInstantiations(out Instantiation typeInstantiation, out Instantiation methodInstantiation)
        {
            TypeDesc type = _owner as TypeDesc;
            if (type != null)
            {
                typeInstantiation = type.Instantiation;
                methodInstantiation = new Instantiation();
            }
            else
            {
                MethodDesc method = (MethodDesc)_owner;
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
                TypeDesc owningType = _owner as TypeDesc;
                string mangledTargetName = owningType != null ?
                    NodeFactory.NameMangler.GetMangledTypeName(owningType) :
                    NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_owner);
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
            // should all already have been marked and it should be safe to disclose the dependencies
            // at that point.
            if (!relocsOnly)
            {
                DictionaryLayout layout = GetDictionaryLayout(factory);
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
