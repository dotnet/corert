// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.Metadata.NativeFormat.Writer;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;
using ReadyToRunSectionType = Internal.Runtime.ReadyToRunSectionType;
using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing native metadata to be emitted into the compiled
    /// module. It also helps facilitate mappings between generated runtime structures or code,
    /// and the native metadata.
    /// </summary>
    public class MetadataGeneration
    {
        private byte[] _metadataBlob;
        private List<MetadataMapping<MetadataType>> _typeMappings = new List<MetadataMapping<MetadataType>>();

        private HashSet<ModuleDesc> _modulesSeen = new HashSet<ModuleDesc>();
        private HashSet<MetadataType> _typeDefinitionsGenerated = new HashSet<MetadataType>();

        public void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            graph.NewMarkedNode += Graph_NewMarkedNode;
        }

        private static ReadyToRunSectionType BlobIdToReadyToRunSection(ReflectionMapBlob blobId)
        {
            var result = (ReadyToRunSectionType)((int)blobId + (int)ReadyToRunSectionType.ReadonlyBlobRegionStart);
            Debug.Assert(result <= ReadyToRunSectionType.ReadonlyBlobRegionEnd);
            return result;
        }

        public void AddToReadyToRunHeader(ReadyToRunHeaderNode header)
        {
            var metadataNode = new MetadataNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.EmbeddedMetadata), metadataNode, metadataNode, metadataNode.EndSymbol);

            var typeMapNode = new TypeMetadataMapNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.TypeMap), typeMapNode, typeMapNode, typeMapNode.EndSymbol);
        }

        private void Graph_NewMarkedNode(DependencyNodeCore<NodeFactory> obj)
        {
            var eetypeNode = obj as EETypeNode;
            if (eetypeNode != null)
            {
                AddGeneratedType(eetypeNode.Type);
                return;
            }

            var methodNode = obj as MethodCodeNode;
            if (methodNode != null)
            {
                AddGeneratedType(methodNode.Method.OwningType);
                return;
            }
        }

        private void AddGeneratedType(TypeDesc type)
        {
            Debug.Assert(_metadataBlob == null, "Created a new EEType after metadata generation finished");

            if (type.IsTypeDefinition)
            {
                var mdType = type as MetadataType;
                if (mdType != null)
                {
                    _typeDefinitionsGenerated.Add(mdType);
                    _modulesSeen.Add(mdType.Module);
                }
            }
            else if (type.HasInstantiation)
            {
                AddGeneratedType(type.GetTypeDefinition());
                foreach (var argument in type.Instantiation)
                    AddGeneratedType(argument);
            }

            // TODO: track generic types, array types, etc.
        }

        private void EnsureMetadataGenerated()
        {
            if (_metadataBlob != null)
                return;

            var transformed = MetadataTransform.Run(new DummyMetadataPolicy(this), _modulesSeen);

            // TODO: DeveloperExperienceMode: Use transformed.Transform.HandleType() to generate
            //       TypeReference records for _typeDefinitionsGenerated that don't have metadata.
            //       (To be used in MissingMetadataException messages)

            // Generate metadata blob
            var writer = new MetadataWriter();
            writer.ScopeDefinitions.AddRange(transformed.Scopes);
            var ms = new MemoryStream();
            writer.Write(ms);
            _metadataBlob = ms.ToArray();

            // Generate type definition mappings
            foreach (var definition in _typeDefinitionsGenerated)
            {
                MetadataRecord record = transformed.GetTransformedTypeDefinition(definition);

                // Reflection requires that we maintain type identity. Even if we only generated a TypeReference record,
                // if there is an EEType for it, we also need a mapping table entry for it.
                if (record == null)
                    record = transformed.GetTransformedTypeReference(definition);

                if (record != null)
                    _typeMappings.Add(new MetadataMapping<MetadataType>(definition, writer.GetRecordHandle(record)));
            }
        }

        public byte[] GetMetadataBlob()
        {
            EnsureMetadataGenerated();
            return _metadataBlob;
        }

        public IEnumerable<MetadataMapping<MetadataType>> GetTypeDefinitionMapping()
        {
            EnsureMetadataGenerated();
            return _typeMappings;
        }

        private struct DummyMetadataPolicy : IMetadataPolicy
        {
            private MetadataGeneration _parent;
            private ExplicitScopeAssemblyPolicyMixin _explicitScopeMixin;

            public DummyMetadataPolicy(MetadataGeneration parent)
            {
                _parent = parent;
                _explicitScopeMixin = new ExplicitScopeAssemblyPolicyMixin();
            }

            public bool GeneratesMetadata(FieldDesc fieldDef)
            {
                // Literal fields are fine even for this very dummy policy. Maybe Enum.Parse/ToString will work.
                if (fieldDef.IsLiteral)
                {
                    MetadataType owningType = (MetadataType)fieldDef.OwningType as MetadataType;
                    return _parent._typeDefinitionsGenerated.Contains(owningType);
                }
                return false;
            }

            public bool GeneratesMetadata(MethodDesc methodDef)
            {
                return false;
            }

            public bool GeneratesMetadata(MetadataType typeDef)
            {
                // Metadata consistency: if a nested type generates metadata, the containing type is
                // required to generate metadata, or metadata generation will fail.
                foreach (var nested in typeDef.GetNestedTypes())
                {
                    if (GeneratesMetadata(nested))
                        return true;
                }

                return _parent._typeDefinitionsGenerated.Contains(typeDef);
            }

            public bool IsBlocked(MetadataType typeDef)
            {
                return false;
            }

            public ModuleDesc GetModuleOfType(MetadataType typeDef)
            {
                return _explicitScopeMixin.GetModuleOfType(typeDef);
            }
        }
    }

    public struct MetadataMapping<TEntity>
    {
        public readonly TEntity Entity;
        public readonly int MetadataHandle;

        public MetadataMapping(TEntity entity, int metadataHandle)
        {
            Entity = entity;
            MetadataHandle = metadataHandle;
        }
    }
}
