// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.Metadata.NativeFormat.Writer;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing native metadata to be emitted into the compiled
    /// module. It applies a policy that every type/method emitted shall be reflectable.
    /// </summary>
    public class CompilerGeneratedMetadataManager : MetadataManager
    {
        private GeneratedTypesAndCodeMetadataPolicy _metadataPolicy;
        private string _metadataLogFile;

        public CompilerGeneratedMetadataManager(CompilationModuleGroup group, CompilerTypeSystemContext typeSystemContext, string logFile)
            : base(group, typeSystemContext)
        {
            _metadataPolicy = new GeneratedTypesAndCodeMetadataPolicy(this);
            _metadataLogFile = logFile;
        }

        private HashSet<MetadataType> _typeDefinitionsToGenerate = new HashSet<MetadataType>();
        private HashSet<MethodDesc> _methodDefinitionsToGenerate = new HashSet<MethodDesc>();
        private HashSet<ModuleDesc> _modulesSeen = new HashSet<ModuleDesc>();
        private Dictionary<DynamicInvokeMethodSignature, MethodDesc> _dynamicInvokeThunks = new Dictionary<DynamicInvokeMethodSignature, MethodDesc>();

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return _modulesSeen;
        }

        public override bool IsReflectionBlocked(MetadataType type)
        {
            return _metadataPolicy.IsBlocked(type);
        }

        public override bool WillUseMetadataTokenToReferenceMethod(MethodDesc method)
        {
            return _compilationModuleGroup.ContainsType(method.GetTypicalMethodDefinition().OwningType);
        }

        public override bool WillUseMetadataTokenToReferenceField(FieldDesc field)
        {
            return _compilationModuleGroup.ContainsType(field.GetTypicalFieldDefinition().OwningType);
        }

        protected override void ComputeMetadata(NodeFactory factory,
                                                out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings)
        {
            foreach (var type in factory.MetadataManager.GetTypesWithEETypes())
            {
                var definition = type.GetTypeDefinition() as Internal.TypeSystem.Ecma.EcmaType;
                if (definition == null)
                    continue;
                if (factory.CompilationModuleGroup.ContainsType(definition))
                {
                    _typeDefinitionsToGenerate.Add(definition);
                    _modulesSeen.Add(definition.Module);
                }
            }

            foreach (var method in GetCompiledMethods())
            {
                var typicalMethod = method.GetTypicalMethodDefinition() as Internal.TypeSystem.Ecma.EcmaMethod;
                if (typicalMethod != null)
                {
                    var owningType = (MetadataType)typicalMethod.OwningType;
                    if (factory.CompilationModuleGroup.ContainsType(owningType))
                    {
                        _typeDefinitionsToGenerate.Add(owningType);
                        _modulesSeen.Add(owningType.Module);
                        _methodDefinitionsToGenerate.Add(typicalMethod);
                    }
                }
            }

            var transformed = MetadataTransform.Run(new GeneratedTypesAndCodeMetadataPolicy(this), _modulesSeen);

            // TODO: DeveloperExperienceMode: Use transformed.Transform.HandleType() to generate
            //       TypeReference records for _typeDefinitionsGenerated that don't have metadata.
            //       (To be used in MissingMetadataException messages)

            // Generate metadata blob
            var writer = new MetadataWriter();
            writer.ScopeDefinitions.AddRange(transformed.Scopes);
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

                MetadataRecord record = transformed.GetTransformedMethodDefinition(method.GetTypicalMethodDefinition());

                if (record != null)
                    methodMappings.Add(new MetadataMapping<MethodDesc>(method, writer.GetRecordHandle(record)));
            }

            foreach (var eetypeGenerated in GetTypesWithEETypes())
            {
                if (eetypeGenerated.IsGenericDefinition)
                    continue;

                if (eetypeGenerated.HasInstantiation)
                {
                    // Collapsing of field map entries based on canonicalization, to avoid redundant equivalent entries

                    TypeDesc canonicalType = eetypeGenerated.ConvertToCanonForm(CanonicalFormKind.Specific);
                    if (canonicalType != eetypeGenerated && TypeGeneratesEEType(canonicalType))
                        continue;
                }

                foreach (FieldDesc field in eetypeGenerated.GetFields())
                {
                    Field record = transformed.GetTransformedFieldDefinition(field.GetTypicalFieldDefinition());
                    if (record != null)
                        fieldMappings.Add(new MetadataMapping<FieldDesc>(field, writer.GetRecordHandle(record)));
                }
            }
        }

        /// <summary>
        /// Is there a reflection invoke stub for a method that is invokable?
        /// </summary>
        public override bool HasReflectionInvokeStubForInvokableMethod(MethodDesc method)
        {
            Debug.Assert(IsReflectionInvokable(method));
            return true;
        }

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public override MethodDesc GetCanonicalReflectionInvokeStub(MethodDesc method)
        {
            TypeSystemContext context = method.Context;
            var sig = method.Signature;

            // Get a generic method that can be used to invoke method with this shape.
            MethodDesc thunk;
            var lookupSig = new DynamicInvokeMethodSignature(sig);
            if (!_dynamicInvokeThunks.TryGetValue(lookupSig, out thunk))
            {
                thunk = new DynamicInvokeMethodThunk(_compilationModuleGroup.GeneratedAssembly.GetGlobalModuleType(), lookupSig);
                _dynamicInvokeThunks.Add(lookupSig, thunk);
            }

            return InstantiateCanonicalDynamicInvokeMethodForMethod(thunk, method);
        }

        private struct GeneratedTypesAndCodeMetadataPolicy : IMetadataPolicy
        {
            private CompilerGeneratedMetadataManager _parent;
            private ExplicitScopeAssemblyPolicyMixin _explicitScopeMixin;
            private Dictionary<MetadataType, bool> _isAttributeCache;

            public GeneratedTypesAndCodeMetadataPolicy(CompilerGeneratedMetadataManager parent)
            {
                _parent = parent;
                _explicitScopeMixin = new ExplicitScopeAssemblyPolicyMixin();

                MetadataType systemAttributeType = parent._typeSystemContext.SystemModule.GetType("System", "Attribute", false);
                _isAttributeCache = new Dictionary<MetadataType, bool>();
                _isAttributeCache.Add(systemAttributeType, true);
            }

            public bool GeneratesMetadata(FieldDesc fieldDef)
            {
                return _parent._typeDefinitionsToGenerate.Contains((MetadataType)fieldDef.OwningType);
            }

            public bool GeneratesMetadata(MethodDesc methodDef)
            {
                return _parent._methodDefinitionsToGenerate.Contains(methodDef);
            }

            public bool GeneratesMetadata(MetadataType typeDef)
            {
                // Global module type always generates metadata. This is e.g. used in various places
                // where we need a metadata enabled type from an assembly but we don't have a convenient way
                // to find one.
                // We don't need to worry about metadata consistency (accidentally generating metadata
                // that can't be used with any reflection API at runtime because it's incomplete) because
                // global module types don't derive from anything and have an empty interface list.
                if (typeDef.IsModuleType)
                    return true;

                // Metadata consistency: if a nested type generates metadata, the containing type is
                // required to generate metadata, or metadata generation will fail.
                foreach (var nested in typeDef.GetNestedTypes())
                {
                    if (GeneratesMetadata(nested))
                        return true;
                }

                return _parent._typeDefinitionsToGenerate.Contains(typeDef);
            }

            public bool IsBlocked(MetadataType typeDef)
            {
                // If an attribute type would generate metadata in this blob (had we compiled it), consider it blocked.
                // Otherwise we end up with an attribute that is an unresolvable TypeRef and we would get a TypeLoadException
                // when enumerating attributes on anything that has it.
                if (!GeneratesMetadata(typeDef)
                    && _parent._compilationModuleGroup.ContainsType(typeDef)
                    && IsAttributeType(typeDef))
                {
                    return true;
                }

                return false;
            }

            private bool IsAttributeType(MetadataType type)
            {
                bool result;
                if (!_isAttributeCache.TryGetValue(type, out result))
                {
                    MetadataType baseType = type.MetadataBaseType;
                    result = baseType != null && IsAttributeType(baseType);
                    _isAttributeCache.Add(type, result);
                }
                return result;
            }

            public ModuleDesc GetModuleOfType(MetadataType typeDef)
            {
                return _explicitScopeMixin.GetModuleOfType(typeDef);
            }
        }
    }
}
