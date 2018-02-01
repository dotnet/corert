﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.Metadata.NativeFormat.Writer;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Base class for metadata managers that generate metadata blobs.
    /// </summary>
    public abstract class GeneratingMetadataManager : MetadataManager
    {
        protected readonly string _metadataLogFile;
        protected readonly StackTraceEmissionPolicy _stackTraceEmissionPolicy;
        private readonly Dictionary<DynamicInvokeMethodSignature, MethodDesc> _dynamicInvokeThunks;
        private readonly ModuleDesc _generatedAssembly;

        public GeneratingMetadataManager(ModuleDesc generatedAssembly, CompilerTypeSystemContext typeSystemContext, MetadataBlockingPolicy blockingPolicy, string logFile, StackTraceEmissionPolicy stackTracePolicy)
            : base(typeSystemContext, blockingPolicy)
        {
            _metadataLogFile = logFile;
            _stackTraceEmissionPolicy = stackTracePolicy;
            _generatedAssembly = generatedAssembly;

            if (DynamicInvokeMethodThunk.SupportsThunks(typeSystemContext))
            {
                _dynamicInvokeThunks = new Dictionary<DynamicInvokeMethodSignature, MethodDesc>();
            }
        }

        public sealed override bool WillUseMetadataTokenToReferenceMethod(MethodDesc method)
        {
            return (GetMetadataCategory(method) & MetadataCategory.Description) != 0;
        }

        public sealed override bool WillUseMetadataTokenToReferenceField(FieldDesc field)
        {
            return (GetMetadataCategory(field) & MetadataCategory.Description) != 0;
        }

        protected void ComputeMetadata<TPolicy>(
            TPolicy policy,
            NodeFactory factory,
            out byte[] metadataBlob,
            out List<MetadataMapping<MetadataType>> typeMappings,
            out List<MetadataMapping<MethodDesc>> methodMappings,
            out List<MetadataMapping<FieldDesc>> fieldMappings,
            out List<MetadataMapping<MethodDesc>> stackTraceMapping) where TPolicy : struct, IMetadataPolicy
        {
            var transformed = MetadataTransform.Run(policy, GetCompilationModulesWithMetadata());
            MetadataTransform transform = transformed.Transform;

            // TODO: DeveloperExperienceMode: Use transformed.Transform.HandleType() to generate
            //       TypeReference records for _typeDefinitionsGenerated that don't have metadata.
            //       (To be used in MissingMetadataException messages)

            // Generate metadata blob
            var writer = new MetadataWriter();
            writer.ScopeDefinitions.AddRange(transformed.Scopes);

            // Generate entries in the blob for methods that will be necessary for stack trace purposes.
            var stackTraceRecords = new List<KeyValuePair<MethodDesc, MetadataRecord>>();
            foreach (var methodBody in GetCompiledMethodBodies())
            {
                MethodDesc method = methodBody.Method;

                MethodDesc typicalMethod = method.GetTypicalMethodDefinition();

                // Methods that will end up in the reflection invoke table should not have an entry in stack trace table
                // We'll try looking them up in reflection data at runtime.
                if (transformed.GetTransformedMethodDefinition(typicalMethod) != null &&
                    ShouldMethodBeInInvokeMap(method) &&
                    (GetMetadataCategory(method) & MetadataCategory.RuntimeMapping) != 0)
                    continue;

                if (!_stackTraceEmissionPolicy.ShouldIncludeMethod(method))
                    continue;

                MetadataRecord record = CreateStackTraceRecord(transform, method);

                stackTraceRecords.Add(new KeyValuePair<MethodDesc, MetadataRecord>(
                    method,
                    record));

                writer.AdditionalRootRecords.Add(record);
            }

            var ms = new MemoryStream();

            // .NET metadata is UTF-16 and UTF-16 contains code points that don't translate to UTF-8.
            var noThrowUtf8Encoding = new UTF8Encoding(false, false);

            using (var logWriter = _metadataLogFile != null ? new StreamWriter(File.Open(_metadataLogFile, FileMode.Create, FileAccess.Write, FileShare.Read), noThrowUtf8Encoding) : null)
            {
                writer.LogWriter = logWriter;
                writer.Write(ms);
            }

            metadataBlob = ms.ToArray();

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();
            stackTraceMapping = new List<MetadataMapping<MethodDesc>>();

            // Generate type definition mappings
            foreach (var type in factory.MetadataManager.GetTypesWithEETypes())
            {
                MetadataType definition = type.IsTypeDefinition ? type as MetadataType : null;
                if (definition == null)
                    continue;

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

                if (IsReflectionBlocked(method.Instantiation) || IsReflectionBlocked(method.OwningType.Instantiation))
                    continue;

                if ((GetMetadataCategory(method) & MetadataCategory.RuntimeMapping) == 0)
                    continue;

                MetadataRecord record = transformed.GetTransformedMethodDefinition(method.GetTypicalMethodDefinition());

                if (record != null)
                    methodMappings.Add(new MetadataMapping<MethodDesc>(method, writer.GetRecordHandle(record)));
            }

            foreach (var field in GetFieldsWithRuntimeMapping())
            {
                Field record = transformed.GetTransformedFieldDefinition(field.GetTypicalFieldDefinition());
                if (record != null)
                    fieldMappings.Add(new MetadataMapping<FieldDesc>(field, writer.GetRecordHandle(record)));
            }

            // Generate stack trace metadata mapping
            foreach (var stackTraceRecord in stackTraceRecords)
            {
                stackTraceMapping.Add(new MetadataMapping<MethodDesc>(stackTraceRecord.Key, writer.GetRecordHandle(stackTraceRecord.Value)));
            }
        }

        /// <summary>
        /// Gets a list of fields that got "compiled" and are eligible for a runtime mapping.
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<FieldDesc> GetFieldsWithRuntimeMapping();

        /// <summary>
        /// Is there a reflection invoke stub for a method that is invokable?
        /// </summary>
        public sealed override bool HasReflectionInvokeStubForInvokableMethod(MethodDesc method)
        {
            Debug.Assert(IsReflectionInvokable(method));

            if (_dynamicInvokeThunks == null)
                return false;

            // Place an upper limit on how many parameters a method can have to still get a static stub.
            // From the past experience, methods taking 8000+ parameters get a stub that can hit various limitations
            // in the codegen. On Project N, we were limited to 256 parameters because of MDIL limitations.
            // We don't have such limitations here, but it's a nice round number.
            // Reflection invoke will still work, but will go through the calling convention converter.

            return method.Signature.Length <= 256;
        }

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public sealed override MethodDesc GetCanonicalReflectionInvokeStub(MethodDesc method)
        {
            TypeSystemContext context = method.Context;
            var sig = method.Signature;

            // Get a generic method that can be used to invoke method with this shape.
            MethodDesc thunk;
            var lookupSig = new DynamicInvokeMethodSignature(sig);
            if (!_dynamicInvokeThunks.TryGetValue(lookupSig, out thunk))
            {
                thunk = new DynamicInvokeMethodThunk(_generatedAssembly.GetGlobalModuleType(), lookupSig);
                _dynamicInvokeThunks.Add(lookupSig, thunk);
            }

            return InstantiateCanonicalDynamicInvokeMethodForMethod(thunk, method);
        }
    }
}
