﻿// Licensed to the .NET Foundation under one or more agreements.
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
        internal const int MetadataOffsetMask = 0xFFFFFF;

        private byte[] _metadataBlob;
        private List<MetadataMapping<MetadataType>> _typeMappings = new List<MetadataMapping<MetadataType>>();
        private List<MetadataMapping<FieldDesc>> _fieldMappings = new List<MetadataMapping<FieldDesc>>();
        private List<MetadataMapping<MethodDesc>> _methodMappings = new List<MetadataMapping<MethodDesc>>();

        private NodeFactory _nodeFactory;

        private HashSet<ModuleDesc> _modulesSeen = new HashSet<ModuleDesc>();
        private HashSet<MetadataType> _typeDefinitionsGenerated = new HashSet<MetadataType>();
        private HashSet<ArrayType> _arrayTypesGenerated = new HashSet<ArrayType>();
        private List<NonGCStaticsNode> _cctorContextsGenerated = new List<NonGCStaticsNode>();
        private HashSet<TypeDesc> _typesWithEETypesGenerated = new HashSet<TypeDesc>();
        private HashSet<MethodDesc> _methodDefinitionsGenerated = new HashSet<MethodDesc>();
        private HashSet<MethodDesc> _methodsGenerated = new HashSet<MethodDesc>();
        private HashSet<GenericDictionaryNode> _genericDictionariesGenerated = new HashSet<GenericDictionaryNode>();
        private List<TypeGVMEntriesNode> _typeGVMEntries = new List<TypeGVMEntriesNode>();

        private Dictionary<DynamicInvokeMethodSignature, MethodDesc> _dynamicInvokeThunks = new Dictionary<DynamicInvokeMethodSignature, MethodDesc>();

        internal NativeLayoutInfoNode NativeLayoutInfo { get; private set; }

        public MetadataGeneration(NodeFactory factory)
        {
            _nodeFactory = factory;
        }

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

            var commonFixupsTableNode = new ExternalReferencesTableNode("CommonFixupsTable");
            var nativeReferencesTableNode = new ExternalReferencesTableNode("NativeReferences");

            var resourceDataNode = new ResourceDataNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdResourceData), resourceDataNode, resourceDataNode, resourceDataNode.EndSymbol);

            var resourceIndexNode = new ResourceIndexNode(resourceDataNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdResourceIndex), resourceIndexNode, resourceIndexNode, resourceIndexNode.EndSymbol);
          
            var typeMapNode = new TypeMetadataMapNode(commonFixupsTableNode);

            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.TypeMap), typeMapNode, typeMapNode, typeMapNode.EndSymbol);

            var cctorContextMapNode = new ClassConstructorContextMap(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.CCtorContextMap), cctorContextMapNode, cctorContextMapNode, cctorContextMapNode.EndSymbol);

            var invokeMapNode = new ReflectionInvokeMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.InvokeMap), invokeMapNode, invokeMapNode, invokeMapNode.EndSymbol);

            var arrayMapNode = new ArrayMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.ArrayMap), arrayMapNode, arrayMapNode, arrayMapNode.EndSymbol);

            var fieldMapNode = new ReflectionFieldMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.FieldAccessMap), fieldMapNode, fieldMapNode, fieldMapNode.EndSymbol);

            NativeLayoutInfo = new NativeLayoutInfoNode(nativeReferencesTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.NativeLayoutInfo), NativeLayoutInfo, NativeLayoutInfo, NativeLayoutInfo.EndSymbol);

            var exactMethodInstantiations = new ExactMethodInstantiationsNode(nativeReferencesTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.ExactMethodInstantiationsHashtable), exactMethodInstantiations, exactMethodInstantiations, exactMethodInstantiations.EndSymbol);

            var genericsTypesHashtableNode = new GenericTypesHashtableNode(nativeReferencesTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.GenericsHashtable), genericsTypesHashtableNode, genericsTypesHashtableNode, genericsTypesHashtableNode.EndSymbol);

            var genericMethodsHashtableNode = new GenericMethodsHashtableNode(nativeReferencesTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.GenericMethodsHashtable), genericMethodsHashtableNode, genericMethodsHashtableNode, genericMethodsHashtableNode.EndSymbol);

            var genericVirtualMethodTableNode = new GenericVirtualMethodTableNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.GenericVirtualMethodTable), genericVirtualMethodTableNode, genericVirtualMethodTableNode, genericVirtualMethodTableNode.EndSymbol);

            var interfaceGenericVirtualMethodTableNode = new InterfaceGenericVirtualMethodTableNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.InterfaceGenericVirtualMethodTable), interfaceGenericVirtualMethodTableNode, interfaceGenericVirtualMethodTableNode, interfaceGenericVirtualMethodTableNode.EndSymbol);

            var genericMethodsTemplatesMapNode = new GenericMethodsTemplateMap(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.GenericMethodsTemplateMap), genericMethodsTemplatesMapNode, genericMethodsTemplatesMapNode, genericMethodsTemplatesMapNode.EndSymbol);

            // The external references tables should go last
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.CommonFixupsTable), commonFixupsTableNode, commonFixupsTableNode, commonFixupsTableNode.EndSymbol);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.NativeReferences), nativeReferencesTableNode, nativeReferencesTableNode, nativeReferencesTableNode.EndSymbol);
        }

        private void Graph_NewMarkedNode(DependencyNodeCore<NodeFactory> obj)
        {
            var eetypeNode = obj as EETypeNode;
            if (eetypeNode != null)
            {
                _typesWithEETypesGenerated.Add(eetypeNode.Type);
                AddGeneratedType(eetypeNode.Type);
                return;
            }

            IMethodNode methodNode = obj as MethodCodeNode;
            if (methodNode == null)
                methodNode = obj as ShadowConcreteMethodNode<MethodCodeNode>;

            if (methodNode != null)
            {
                MethodDesc method = methodNode.Method;

                AddGeneratedType(method.OwningType);
                _methodDefinitionsGenerated.Add(method.GetTypicalMethodDefinition());
                _methodsGenerated.Add(method);
                return;
            }

            var nonGcStaticSectionNode = obj as NonGCStaticsNode;
            if (nonGcStaticSectionNode != null && _nodeFactory.TypeSystemContext.HasLazyStaticConstructor(nonGcStaticSectionNode.Type))
            {
                _cctorContextsGenerated.Add(nonGcStaticSectionNode);
            }

            var gvmEntryNode = obj as TypeGVMEntriesNode;
            if (gvmEntryNode != null)
            {
                _typeGVMEntries.Add(gvmEntryNode);
            }

            var dictionaryNode = obj as GenericDictionaryNode;
            if (dictionaryNode != null)
            {
                _genericDictionariesGenerated.Add(dictionaryNode);
            }
        }

        public bool HasReflectionInvokeStub(MethodDesc method)
        {
            var signature = method.Signature;

            // TODO: support for methods returning pointer types - https://github.com/dotnet/corert/issues/2113
            if (signature.ReturnType.IsPointer)
                return false;

            for (int i = 0; i < signature.Length; i++)
                if (signature[i].IsByRef && ((ByRefType)signature[i]).ParameterType.IsPointer)
                    return false;

            // TODO: function pointer types are odd: https://github.com/dotnet/corert/issues/1929
            if (signature.ReturnType.IsFunctionPointer)
                return false;

            for (int i = 0; i < signature.Length; i++)
                if (signature[i].IsFunctionPointer)
                    return false;

            // Methods with ByRef returns can't be reflection invoked
            if (signature.ReturnType.IsByRef)
                return false;

            // Delegate construction is only allowed through specific IL sequences
            if (method.OwningType.IsDelegate && method.IsConstructor)
                return false;

            // Everything else should get a stub.
            return true;
        }

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public MethodDesc GetReflectionInvokeStub(MethodDesc method)
        {
            // Methods we see here shouldn't be canonicalized, or we'll end up creating bastardized instantiations
            // (e.g. we instantiate over System.Object below.)
            Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Any));

            TypeSystemContext context = method.Context;
            var sig = method.Signature;
            ParameterMetadata[] paramMetadata = null;

            // Get a generic method that can be used to invoke method with this shape.
            MethodDesc thunk;
            var lookupSig = new DynamicInvokeMethodSignature(sig);
            if (!_dynamicInvokeThunks.TryGetValue(lookupSig, out thunk))
            {
                thunk = new DynamicInvokeMethodThunk(_nodeFactory.CompilationModuleGroup.GeneratedAssembly.GetGlobalModuleType(), lookupSig);
                _dynamicInvokeThunks.Add(lookupSig, thunk);
            }

            // If the method has no parameters and returns void, we don't need to specialize
            if (sig.ReturnType.IsVoid && sig.Length == 0)
            {
                Debug.Assert(!thunk.HasInstantiation);
                return thunk;
            }

            //
            // Instantiate the generic thunk over the parameters and the return type of the target method
            //

            TypeDesc[] instantiation = new TypeDesc[sig.ReturnType.IsVoid ? sig.Length : sig.Length + 1];
            Debug.Assert(thunk.Instantiation.Length == instantiation.Length);
            for (int i = 0; i < sig.Length; i++)
            {
                TypeDesc parameterType = sig[i];
                if (parameterType.IsByRef)
                {
                    // strip ByRefType off the parameter (the method already has ByRef in the signature)
                    parameterType = ((ByRefType)parameterType).ParameterType;
                }

                if (parameterType.IsPointer || parameterType.IsFunctionPointer)
                {
                    // For pointer typed parameters, instantiate the method over IntPtr
                    parameterType = context.GetWellKnownType(WellKnownType.IntPtr);
                }
                else if (parameterType.IsEnum)
                {
                    // If the invoke method takes an enum as an input parameter and there is no default value for
                    // that paramter, we don't need to specialize on the exact enum type (we only need to specialize
                    // on the underlying integral type of the enum.)
                    if (paramMetadata == null)
                        paramMetadata = method.GetParameterMetadata();

                    bool hasDefaultValue = false;
                    foreach (var p in paramMetadata)
                    {
                        // Parameter metadata indexes are 1-based (0 is reserved for return "parameter")
                        if (p.Index == (i + 1) && p.HasDefault)
                        {
                            hasDefaultValue = true;
                            break;
                        }
                    }

                    if (!hasDefaultValue)
                        parameterType = parameterType.UnderlyingType;
                }

                instantiation[i] = parameterType;
            }

            if (!sig.ReturnType.IsVoid)
            {
                TypeDesc returnType = sig.ReturnType;
                Debug.Assert(!returnType.IsByRef);

                // If the invoke method return an object reference, we don't need to specialize on the
                // exact type of the object reference, as the behavior is not different.
                if ((returnType.IsDefType && !returnType.IsValueType) || returnType.IsArray)
                {
                    returnType = context.GetWellKnownType(WellKnownType.Object);
                }

                instantiation[sig.Length] = returnType;
            }

            return context.GetInstantiatedMethod(thunk, new Instantiation(instantiation));
        }

        private void AddGeneratedType(TypeDesc type)
        {
            Debug.Assert(_metadataBlob == null, "Created a new EEType after metadata generation finished");

            if (type.IsDefType && type.IsTypeDefinition)
            {
                var mdType = type as MetadataType;
                if (mdType != null)
                {
                    _typeDefinitionsGenerated.Add(mdType);
                    _modulesSeen.Add(mdType.Module);
                }
            }
            else if (type.IsArray)
            {
                var arrayType = (ArrayType)type;
                _arrayTypesGenerated.Add(arrayType);
            }

            // TODO: track generic types, etc.
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

            foreach (var method in _methodsGenerated)
            {
                if (method.IsCanonicalMethod(CanonicalFormKind.Specific))
                {
                    // Canonical methods are not interesting.
                    continue;
                }

                MetadataRecord record = transformed.GetTransformedMethodDefinition(method.GetTypicalMethodDefinition());

                if (record != null)
                    _methodMappings.Add(new MetadataMapping<MethodDesc>(method, writer.GetRecordHandle(record)));
            }

            foreach (var eetypeGenerated in _typesWithEETypesGenerated)
            {
                if (eetypeGenerated.IsGenericDefinition)
                    continue;

                foreach (FieldDesc field in eetypeGenerated.GetFields())
                {
                    Field record = transformed.GetTransformedFieldDefinition(field.GetTypicalFieldDefinition());
                    if (record != null)
                        _fieldMappings.Add(new MetadataMapping<FieldDesc>(field, writer.GetRecordHandle(record)));
                }
            }
        }

        /// <summary>
        /// Returns a set of modules that will get some metadata emitted into the output module
        /// </summary>
        public HashSet<ModuleDesc> GetModulesWithMetadata()
        {
            return _modulesSeen;
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

        public IEnumerable<MetadataMapping<MethodDesc>> GetMethodMapping()
        {
            EnsureMetadataGenerated();
            return _methodMappings;
        }

        public IEnumerable<MetadataMapping<FieldDesc>> GetFieldMapping()
        {
            EnsureMetadataGenerated();
            return _fieldMappings;
        }

        internal IEnumerable<NonGCStaticsNode> GetCctorContextMapping()
        {
            return _cctorContextsGenerated;
        }

        internal IEnumerable<ArrayType> GetArrayTypeMapping()
        {
            return _arrayTypesGenerated;
        }

        internal IEnumerable<TypeGVMEntriesNode> GetTypeGVMEntries()
        {
            return _typeGVMEntries;
        }

        internal IEnumerable<GenericDictionaryNode> GetCompiledGenericDictionaries()
        {
            return _genericDictionariesGenerated;
        }

        internal IEnumerable<MethodDesc> GetCompiledMethods()
        {
            return _methodsGenerated;
        }

        internal bool TypeGeneratesEEType(TypeDesc type)
        {
            return _typesWithEETypesGenerated.Contains(type);
        }

        internal IEnumerable<TypeDesc> GetTypesWithEETypes()
        {
            return _typesWithEETypesGenerated;
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
