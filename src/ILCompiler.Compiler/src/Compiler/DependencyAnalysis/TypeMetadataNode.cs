// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using EcmaType = Internal.TypeSystem.Ecma.EcmaType;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a type that has metadata generated in the current compilation.
    /// </summary>
    /// <remarks>
    /// Only expected to be used during ILScanning when scanning for reflection.
    /// </remarks>
    internal class TypeMetadataNode : DependencyNodeCore<NodeFactory>
    {
        private readonly MetadataType _type;

        public TypeMetadataNode(MetadataType type)
        {
            Debug.Assert(type.IsTypeDefinition);
            _type = type;
        }

        public MetadataType Type => _type;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();

            CustomAttributeBasedDependencyAlgorithm.AddDependenciesDueToCustomAttributes(ref dependencies, factory, ((EcmaType)_type));

            DefType containingType = _type.ContainingType;
            if (containingType != null)
                dependencies.Add(factory.TypeMetadata((MetadataType)containingType), "Containing type of a reflectable type");
            else
                dependencies.Add(factory.ModuleMetadata(_type.Module), "Containing module of a reflectable type");

            // TODO: https://github.com/dotnet/corert/issues/3224
            // We don't currently track the exact list of fields used - assume all are used
            foreach (FieldDesc field in _type.GetFields())
            {
                if (factory.MetadataManager.CanGenerateMetadata(field))
                    dependencies.Add(factory.FieldMetadata(field), "Field of a reflectable type");
            }

            return dependencies;
        }

        /// <summary>
        /// Decomposes a constructed type into individual <see cref="TypeMetadataNode"/> units that will be needed to
        /// express the constructed type in metadata.
        /// </summary>
        public static void GetMetadataDependencies(ref DependencyList dependencies, NodeFactory nodeFactory, TypeDesc type, string reason)
        {
            MetadataManager mdManager = nodeFactory.MetadataManager;

            switch (type.Category)
            {
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Pointer:
                    GetMetadataDependencies(ref dependencies, nodeFactory, ((ParameterizedType)type).ParameterType, reason);
                    break;
                case TypeFlags.FunctionPointer:
                    throw new NotImplementedException();

                default:
                    Debug.Assert(type.IsDefType);

                    TypeDesc typeDefinition = type.GetTypeDefinition();
                    if (typeDefinition != type)
                    {
                        if (mdManager.CanGenerateMetadata((MetadataType)typeDefinition))
                        {
                            dependencies = dependencies ?? new DependencyList();
                            dependencies.Add(nodeFactory.TypeMetadata((MetadataType)typeDefinition), reason);
                        }

                        foreach (TypeDesc typeArg in type.Instantiation)
                        {
                            GetMetadataDependencies(ref dependencies, nodeFactory, typeArg, reason);
                        }
                    }
                    else
                    {
                        if (mdManager.CanGenerateMetadata((MetadataType)type))
                        {
                            dependencies = dependencies ?? new DependencyList();
                            dependencies.Add(nodeFactory.TypeMetadata((MetadataType)type), reason);
                        }
                    }
                    break;
            }
        }

        protected override string GetName(NodeFactory factory)
        {
            return "Reflectable type: " + _type.ToString();
        }

        protected override void OnMarked(NodeFactory factory)
        {
            Debug.Assert(!factory.MetadataManager.IsReflectionBlocked(_type));
            Debug.Assert(factory.MetadataManager.CanGenerateMetadata(_type));
        }

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
    }
}
