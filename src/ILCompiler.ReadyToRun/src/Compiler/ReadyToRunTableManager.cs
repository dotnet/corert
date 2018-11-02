// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.ReadyToRun;
using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public class ReadyToRunTableManager : MetadataManager
    {
        private readonly HashSet<TypeDesc> _typesWithAvailableTypesGenerated = new HashSet<TypeDesc>();

        public ReadyToRunTableManager(CompilerTypeSystemContext typeSystemContext)
            : base(typeSystemContext, new NoMetadataBlockingPolicy(), new NoManifestResourceBlockingPolicy(), new NoDynamicInvokeThunkGenerationPolicy()) {}

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            // We don't attach any metadata blobs.
        }

        protected override void Graph_NewMarkedNode(DependencyNodeCore<NodeFactory> obj)
        {
            base.Graph_NewMarkedNode(obj);
            
            var eetypeNode = obj as AvailableType;
            if (eetypeNode != null)
            {
                _typesWithAvailableTypesGenerated.Add(eetypeNode.Type);
                return;
            }
        }

        public IEnumerable<TypeDesc> GetTypesWithAvailableTypes()
        {
            return _typesWithAvailableTypesGenerated;
        }

        public override MethodDesc GetCanonicalReflectionInvokeStub(MethodDesc method) => throw new NotImplementedException();
        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata() => throw new NotImplementedException();
        public override bool WillUseMetadataTokenToReferenceField(FieldDesc field) => throw new NotImplementedException();
        public override bool WillUseMetadataTokenToReferenceMethod(MethodDesc method) => throw new NotImplementedException();
        protected override void ComputeMetadata(NodeFactory factory, out byte[] metadataBlob, out List<MetadataMapping<MetadataType>> typeMappings, out List<MetadataMapping<MethodDesc>> methodMappings, out List<MetadataMapping<FieldDesc>> fieldMappings, out List<MetadataMapping<MethodDesc>> stackTraceMapping) => throw new NotImplementedException();
        protected override MetadataCategory GetMetadataCategory(MethodDesc method) => throw new NotImplementedException();
        protected override MetadataCategory GetMetadataCategory(TypeDesc type) => throw new NotImplementedException();
        protected override MetadataCategory GetMetadataCategory(FieldDesc field) => throw new NotImplementedException();
    }
}
