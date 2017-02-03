// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.Metadata.NativeFormat.Writer;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;

namespace ILCompiler
{
    public class GeneratedOnlyMetadataGeneration : MetadataGeneration
    {
        public GeneratedOnlyMetadataGeneration(NodeFactory factory) : base(factory)
        {
            InitMetadataPolicy(new GeneratedTypesAndCodeMetadataPolicy(this));
        }

        private HashSet<MetadataType> _typeDefinitionsGenerated = new HashSet<MetadataType>();
        private HashSet<MethodDesc> _methodDefinitionsGenerated = new HashSet<MethodDesc>();
        private HashSet<ModuleDesc> _modulesSeen = new HashSet<ModuleDesc>();

        protected override void AddGeneratedType(TypeDesc type)
        {
            if (type.IsDefType && type.IsTypeDefinition)
            {
                var mdType = type as MetadataType;
                if (mdType != null)
                {
                    _modulesSeen.Add(mdType.Module);
                    _typeDefinitionsGenerated.Add(mdType);
                }
            }

            base.AddGeneratedType(type);
        }

        public override HashSet<ModuleDesc> GetModulesWithMetadata()
        {
            return _modulesSeen;
        }

        protected override void AddGeneratedMethod(MethodDesc method)
        {
            _methodDefinitionsGenerated.Add(method.GetTypicalMethodDefinition());
            base.AddGeneratedMethod(method);
        }

        protected override void ComputeMetadata(out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings)
        {
            var transformed = MetadataTransform.Run(new GeneratedTypesAndCodeMetadataPolicy(this), _modulesSeen);

            // TODO: DeveloperExperienceMode: Use transformed.Transform.HandleType() to generate
            //       TypeReference records for _typeDefinitionsGenerated that don't have metadata.
            //       (To be used in MissingMetadataException messages)

            // Generate metadata blob
            var writer = new MetadataWriter();
            writer.ScopeDefinitions.AddRange(transformed.Scopes);
            var ms = new MemoryStream();
            writer.Write(ms);
            metadataBlob = ms.ToArray();

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();

            // Generate type definition mappings
            foreach (var definition in _typeDefinitionsGenerated)
            {
                MetadataRecord record = transformed.GetTransformedTypeDefinition(definition);

                // Reflection requires that we maintain type identity. Even if we only generated a TypeReference record,
                // if there is an EEType for it, we also need a mapping table entry for it.
                if (record == null)
                    record = transformed.GetTransformedTypeReference(definition);

                if (record != null)
                    typeMappings.Add(new MetadataMapping<MetadataType>(definition, writer.GetRecordHandle(record)));
            }

            foreach (var method in GetCompiledMethods())
            {
                if (method.IsCanonicalMethod(CanonicalFormKind.Specific))
                {
                    // Canonical methods are not interesting.
                    continue;
                }

                MetadataRecord record = transformed.GetTransformedMethodDefinition(method.GetTypicalMethodDefinition());

                if (record != null)
                    methodMappings.Add(new MetadataMapping<MethodDesc>(method, writer.GetRecordHandle(record)));
            }

            foreach (var eetypeGenerated in GetTypesWithEETypes())
            {
                if (eetypeGenerated.IsGenericDefinition)
                    continue;

                foreach (FieldDesc field in eetypeGenerated.GetFields())
                {
                    Field record = transformed.GetTransformedFieldDefinition(field.GetTypicalFieldDefinition());
                    if (record != null)
                        fieldMappings.Add(new MetadataMapping<FieldDesc>(field, writer.GetRecordHandle(record)));
                }
            }
        }

        private struct GeneratedTypesAndCodeMetadataPolicy : IMetadataPolicy
        {
            private GeneratedOnlyMetadataGeneration _parent;
            private ExplicitScopeAssemblyPolicyMixin _explicitScopeMixin;

            public GeneratedTypesAndCodeMetadataPolicy(GeneratedOnlyMetadataGeneration parent)
            {
                _parent = parent;
                _explicitScopeMixin = new ExplicitScopeAssemblyPolicyMixin();
            }

            public bool GeneratesMetadata(FieldDesc fieldDef)
            {
                return _parent._typeDefinitionsGenerated.Contains((MetadataType)fieldDef.OwningType);
            }

            public bool GeneratesMetadata(MethodDesc methodDef)
            {
                return _parent._methodDefinitionsGenerated.Contains(methodDef);
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
}
