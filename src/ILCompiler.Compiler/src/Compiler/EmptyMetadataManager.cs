// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.Metadata.NativeFormat.Writer;

using ILCompiler.DependencyAnalysis;
using ILCompiler.Metadata;

using Debug = System.Diagnostics.Debug;
using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;

namespace ILCompiler
{
    public class EmptyMetadataManager : MetadataManager
    {
        private readonly StackTraceEmissionPolicy _stackTraceEmissionPolicy;

        public override bool SupportsReflection => false;

        public EmptyMetadataManager(CompilerTypeSystemContext typeSystemContext)
            : this(typeSystemContext, new NoStackTraceEmissionPolicy())
        {
        }

        public EmptyMetadataManager(CompilerTypeSystemContext typeSystemContext, StackTraceEmissionPolicy stackTraceEmissionPolicy)
            : base(typeSystemContext, new FullyBlockedMetadataPolicy(), new FullyBlockedManifestResourcePolicy(), new NoDynamicInvokeThunkGenerationPolicy())
        {
            _stackTraceEmissionPolicy = stackTraceEmissionPolicy;
        }

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            var metadataNode = new MetadataNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.EmbeddedMetadata), metadataNode, metadataNode, metadataNode.EndSymbol);

            var stackTraceMethodMappingNode = new StackTraceMethodMappingNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdStackTraceMethodRvaToTokenMapping), stackTraceMethodMappingNode, stackTraceMethodMappingNode, stackTraceMethodMappingNode.EndSymbol);
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return Array.Empty<ModuleDesc>();
        }

        protected override MetadataCategory GetMetadataCategory(FieldDesc field)
        {
            return MetadataCategory.None;
        }

        protected override MetadataCategory GetMetadataCategory(MethodDesc method)
        {
            return MetadataCategory.None;
        }

        protected override MetadataCategory GetMetadataCategory(TypeDesc type)
        {
            return MetadataCategory.None;
        }

        protected override void ComputeMetadata(NodeFactory factory,
                                                out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings,
                                                out List<MetadataMapping<MethodDesc>> stackTraceMapping)
        {
            var ms = new MemoryStream();
            var writer = new MetadataWriter();

            // Run an empty transform pass. This doesn't matter. We just need an instance of the MetadataTransform.
            var transformed = MetadataTransform.Run(new Policy(), Array.Empty<ModuleDesc>());
            MetadataTransform transform = transformed.Transform;

            // Generate entries in the blob for methods that will be necessary for stack trace purposes.
            var stackTraceRecords = new List<KeyValuePair<MethodDesc, MetadataRecord>>();
            foreach (var methodBody in GetCompiledMethodBodies())
            {
                MethodDesc method = methodBody.Method;

                MethodDesc typicalMethod = method.GetTypicalMethodDefinition();

                if (!_stackTraceEmissionPolicy.ShouldIncludeMethod(method))
                    continue;

                MetadataRecord record = CreateStackTraceRecord(transform, method);

                stackTraceRecords.Add(new KeyValuePair<MethodDesc, MetadataRecord>(
                    method,
                    record));

                writer.AdditionalRootRecords.Add(record);
            }

            writer.Write(ms);
            metadataBlob = ms.ToArray();

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();
            stackTraceMapping = new List<MetadataMapping<MethodDesc>>();

            // Generate stack trace metadata mapping
            foreach (var stackTraceRecord in stackTraceRecords)
            {
                stackTraceMapping.Add(new MetadataMapping<MethodDesc>(stackTraceRecord.Key, writer.GetRecordHandle(stackTraceRecord.Value)));
            }
        }

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public override MethodDesc GetCanonicalReflectionInvokeStub(MethodDesc method)
        {
            return null;
        }

        public override bool WillUseMetadataTokenToReferenceMethod(MethodDesc method)
        {
            return false;
        }

        public override bool WillUseMetadataTokenToReferenceField(FieldDesc field)
        {
            return false;
        }

        private sealed class FullyBlockedMetadataPolicy : MetadataBlockingPolicy
        {
            public override bool IsBlocked(MetadataType type)
            {
                Debug.Assert(type.IsTypeDefinition);
                return true;
            }

            public override bool IsBlocked(MethodDesc method)
            {
                Debug.Assert(method.IsTypicalMethodDefinition);
                return true;
            }

            public override bool IsBlocked(FieldDesc field)
            {
                Debug.Assert(field.IsTypicalFieldDefinition);
                return true;
            }
        }

        private sealed class FullyBlockedManifestResourcePolicy : ManifestResourceBlockingPolicy
        {
            public override bool IsManifestResourceBlocked(ModuleDesc module, string resourceName)
            {
                return true;
            }
        }

        private struct Policy : IMetadataPolicy
        {
            public bool GeneratesMetadata(MetadataType typeDef) => false;
            public bool GeneratesMetadata(MethodDesc methodDef) => false;
            public bool GeneratesMetadata(FieldDesc fieldDef) => false;
            public ModuleDesc GetModuleOfType(MetadataType typeDef) => typeDef.Module;
            public bool IsBlocked(MetadataType typeDef) => true;
            public bool IsBlocked(MethodDesc methodDef) => true;
        }
    }
}
