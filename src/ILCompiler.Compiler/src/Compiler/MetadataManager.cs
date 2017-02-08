﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;

using Internal.IL.Stubs;
using Internal.TypeSystem;

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
    public abstract class MetadataManager
    {
        internal const int MetadataOffsetMask = 0xFFFFFF;

        private byte[] _metadataBlob;
        private List<MetadataMapping<MetadataType>> _typeMappings;
        private List<MetadataMapping<FieldDesc>> _fieldMappings;
        private List<MetadataMapping<MethodDesc>> _methodMappings;

        private NodeFactory _nodeFactory;

        private HashSet<ArrayType> _arrayTypesGenerated = new HashSet<ArrayType>();
        private List<NonGCStaticsNode> _cctorContextsGenerated = new List<NonGCStaticsNode>();
        private HashSet<TypeDesc> _typesWithEETypesGenerated = new HashSet<TypeDesc>();
        private HashSet<MethodDesc> _methodsGenerated = new HashSet<MethodDesc>();
        private HashSet<GenericDictionaryNode> _genericDictionariesGenerated = new HashSet<GenericDictionaryNode>();
        private List<TypeGVMEntriesNode> _typeGVMEntries = new List<TypeGVMEntriesNode>();

        internal NativeLayoutInfoNode NativeLayoutInfo { get; private set; }

        public MetadataManager(NodeFactory factory)
        {
            _nodeFactory = factory;
        }

        public NodeFactory NodeFactory { get { return _nodeFactory; } }

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

            var blockReflectionTypeMapNode = new BlockReflectionTypeMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlockReflectionTypeMap), blockReflectionTypeMapNode, blockReflectionTypeMapNode, blockReflectionTypeMapNode.EndSymbol);

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
                AddGeneratedMethod(method);
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

        /// <summary>
        /// Is a method that is reflectable a method which should be placed into the invoke map as invokable?
        /// </summary>
        public bool IsReflectionInvokable (MethodDesc method)
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
        /// Is there a reflection invoke stub for a method that is invokable?
        /// </summary>
        public bool HasReflectionInvokeStub(MethodDesc method)
        {
            if (!IsReflectionInvokable(method))
                return false;

            return HasReflectionInvokeStubForInvokableMethod(method);
        }

        /// <summary>
        /// Given that a method is invokable, does there exist a reflection invoke stub?
        /// </summary>
        public abstract bool HasReflectionInvokeStubForInvokableMethod(MethodDesc method);

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public abstract MethodDesc GetReflectionInvokeStub(MethodDesc method);

        protected virtual void AddGeneratedType(TypeDesc type)
        {
            Debug.Assert(_metadataBlob == null, "Created a new EEType after metadata generation finished");

            if (type.IsArray)
            {
                var arrayType = (ArrayType)type;
                _arrayTypesGenerated.Add(arrayType);
            }

            // TODO: track generic types, etc.
        }

        protected virtual void AddGeneratedMethod(MethodDesc method)
        {
            Debug.Assert(_metadataBlob == null, "Created a new EEType after metadata generation finished");
            _methodsGenerated.Add(method);
        }

        private void EnsureMetadataGenerated()
        {
            if (_metadataBlob != null)
                return;

            ComputeMetadata(out _metadataBlob, out _typeMappings, out _methodMappings, out _fieldMappings);
        }

        protected abstract void ComputeMetadata(out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings);



        /// <summary>
        /// Returns a set of modules that will get some metadata emitted into the output module
        /// </summary>
        public abstract IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata();

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

        public abstract bool IsReflectionBlocked(MetadataType type);
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
