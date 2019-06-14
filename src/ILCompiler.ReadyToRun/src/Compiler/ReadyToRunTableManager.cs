// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.ReadyToRun;
using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public struct TypeInfo<THandle>
    {
        public readonly MetadataReader MetadataReader;
        public readonly THandle Handle;

        public TypeInfo(MetadataReader metadataReader, THandle handle)
        {
            MetadataReader = metadataReader;
            Handle = handle;
        }
    }

    public class ReadyToRunTableManager : MetadataManager
    {
        public ReadyToRunTableManager(CompilerTypeSystemContext typeSystemContext)
            : base(typeSystemContext, new NoMetadataBlockingPolicy(), new NoManifestResourceBlockingPolicy(), new NoDynamicInvokeThunkGenerationPolicy()) {}

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            // We don't attach any metadata blobs.
        }

        public IEnumerable<TypeInfo<TypeDefinitionHandle>> GetDefinedTypes()
        {
            foreach (string inputFile in _typeSystemContext.InputFilePaths.Values)
            {
                EcmaModule module = _typeSystemContext.GetModuleFromPath(inputFile);
                foreach (TypeDefinitionHandle typeDefHandle in module.MetadataReader.TypeDefinitions)
                {
                    yield return new TypeInfo<TypeDefinitionHandle>(module.MetadataReader, typeDefHandle);
                }
            }
        }

            public IEnumerable<TypeInfo<ExportedTypeHandle>> GetExportedTypes()
        {
            foreach (string inputFile in _typeSystemContext.InputFilePaths.Values)
            {
                EcmaModule module = _typeSystemContext.GetModuleFromPath(inputFile);
                foreach (ExportedTypeHandle exportedTypeHandle in module.MetadataReader.ExportedTypes)
                {
                    yield return new TypeInfo<ExportedTypeHandle>(module.MetadataReader, exportedTypeHandle);
                }
            }
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
