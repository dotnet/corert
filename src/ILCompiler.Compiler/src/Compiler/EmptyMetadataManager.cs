// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public class EmptyMetadataManager : MetadataManager
    {
        public override bool SupportsReflection => false;

        public EmptyMetadataManager(CompilerTypeSystemContext typeSystemContext)
            : base(typeSystemContext, new FullyBlockedMetadataPolicy(), new FullyBlockedManifestResourcePolicy(), new NoDynamicInvokeThunkGenerationPolicy())
        {
        }

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            // We don't attach any metadata blobs.
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
            metadataBlob = Array.Empty<byte>();

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();
            stackTraceMapping = new List<MetadataMapping<MethodDesc>>();
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
    }
}
