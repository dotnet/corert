// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;

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

        protected readonly CompilationModuleGroup _compilationModuleGroup;
        protected readonly CompilerTypeSystemContext _typeSystemContext;

        private List<NonGCStaticsNode> _cctorContextsGenerated = new List<NonGCStaticsNode>();
        private HashSet<TypeDesc> _typesWithEETypesGenerated = new HashSet<TypeDesc>();
        private HashSet<TypeDesc> _typesWithConstructedEETypesGenerated = new HashSet<TypeDesc>();
        private HashSet<MethodDesc> _methodsGenerated = new HashSet<MethodDesc>();
        private HashSet<GenericDictionaryNode> _genericDictionariesGenerated = new HashSet<GenericDictionaryNode>();
        private List<TypeGVMEntriesNode> _typeGVMEntries = new List<TypeGVMEntriesNode>();

        internal NativeLayoutInfoNode NativeLayoutInfo { get; private set; }
        internal DynamicInvokeTemplateDataNode DynamicInvokeTemplateData { get; private set; }

        public MetadataManager(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext)
        {
            _compilationModuleGroup = compilationModuleGroup;
            _typeSystemContext = typeSystemContext;
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

        public void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory)
        {
            var metadataNode = new MetadataNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.EmbeddedMetadata), metadataNode, metadataNode, metadataNode.EndSymbol);

            var commonFixupsTableNode = new ExternalReferencesTableNode("CommonFixupsTable", nodeFactory);
            var nativeReferencesTableNode = new ExternalReferencesTableNode("NativeReferences", nodeFactory);
            var nativeStaticsTableNode = new ExternalReferencesTableNode("NativeStatics", nodeFactory);

            var resourceDataNode = new ResourceDataNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdResourceData), resourceDataNode, resourceDataNode, resourceDataNode.EndSymbol);

            var resourceIndexNode = new ResourceIndexNode(resourceDataNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdResourceIndex), resourceIndexNode, resourceIndexNode, resourceIndexNode.EndSymbol);
          
            var typeMapNode = new TypeMetadataMapNode(commonFixupsTableNode);

            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.TypeMap), typeMapNode, typeMapNode, typeMapNode.EndSymbol);

            var cctorContextMapNode = new ClassConstructorContextMap(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.CCtorContextMap), cctorContextMapNode, cctorContextMapNode, cctorContextMapNode.EndSymbol);

            DynamicInvokeTemplateData = new DynamicInvokeTemplateDataNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.DynamicInvokeTemplateData), DynamicInvokeTemplateData, DynamicInvokeTemplateData, DynamicInvokeTemplateData.EndSymbol);
            
            var invokeMapNode = new ReflectionInvokeMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.InvokeMap), invokeMapNode, invokeMapNode, invokeMapNode.EndSymbol);

            var delegateMapNode = new DelegateMarshallingStubMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.DelegateMarshallingStubMap), delegateMapNode, delegateMapNode, delegateMapNode.EndSymbol);

            var arrayMapNode = new ArrayMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.ArrayMap), arrayMapNode, arrayMapNode, arrayMapNode.EndSymbol);

            var fieldMapNode = new ReflectionFieldMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.FieldAccessMap), fieldMapNode, fieldMapNode, fieldMapNode.EndSymbol);

            NativeLayoutInfo = new NativeLayoutInfoNode(nativeReferencesTableNode, nativeStaticsTableNode);
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

            var genericTypesTemplatesMapNode = new GenericTypesTemplateMap(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.TypeTemplateMap), genericTypesTemplatesMapNode, genericTypesTemplatesMapNode, genericTypesTemplatesMapNode.EndSymbol);

            var blockReflectionTypeMapNode = new BlockReflectionTypeMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlockReflectionTypeMap), blockReflectionTypeMapNode, blockReflectionTypeMapNode, blockReflectionTypeMapNode.EndSymbol);

            var staticsInfoHashtableNode = new StaticsInfoHashtableNode(nativeReferencesTableNode, nativeStaticsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.StaticsInfoHashtable), staticsInfoHashtableNode, staticsInfoHashtableNode, staticsInfoHashtableNode.EndSymbol);

            var virtualInvokeMapNode = new ReflectionVirtualInvokeMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.VirtualInvokeMap), virtualInvokeMapNode, virtualInvokeMapNode, virtualInvokeMapNode.EndSymbol);

            // The external references tables should go last
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.CommonFixupsTable), commonFixupsTableNode, commonFixupsTableNode, commonFixupsTableNode.EndSymbol);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.NativeReferences), nativeReferencesTableNode, nativeReferencesTableNode, nativeReferencesTableNode.EndSymbol);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.NativeStatics), nativeStaticsTableNode, nativeStaticsTableNode, nativeStaticsTableNode.EndSymbol);
        }

        private void Graph_NewMarkedNode(DependencyNodeCore<NodeFactory> obj)
        {
            var eetypeNode = obj as EETypeNode;
            if (eetypeNode != null)
            {
                _typesWithEETypesGenerated.Add(eetypeNode.Type);

                if (eetypeNode is ConstructedEETypeNode || eetypeNode is CanonicalEETypeNode)
                {
                    _typesWithConstructedEETypesGenerated.Add(eetypeNode.Type);
                }

                return;
            }

            IMethodNode methodNode = obj as MethodCodeNode;
            if (methodNode == null)
                methodNode = obj as ShadowConcreteMethodNode;

            if (methodNode == null)
                methodNode = obj as NonExternMethodSymbolNode;

            if (methodNode != null)
            {
                _methodsGenerated.Add(methodNode.Method);
                return;
            }

            var reflectableMethodNode = obj as ReflectableMethodNode;
            if (reflectableMethodNode != null)
            {
                _methodsGenerated.Add(reflectableMethodNode.Method);
            }

            var nonGcStaticSectionNode = obj as NonGCStaticsNode;
            if (nonGcStaticSectionNode != null && _typeSystemContext.HasLazyStaticConstructor(nonGcStaticSectionNode.Type))
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

            // ----------------------------------------------------------------
            // TODO: support for methods returning pointer types - https://github.com/dotnet/corert/issues/2113
            // ----------------------------------------------------------------

            if (signature.ReturnType.IsPointer)
                return false;

            for (int i = 0; i < signature.Length; i++)
                if (signature[i].IsByRef && ((ByRefType)signature[i]).ParameterType.IsPointer)
                    return false;

            // ----------------------------------------------------------------
            // TODO: function pointer types are odd: https://github.com/dotnet/corert/issues/1929
            // ----------------------------------------------------------------

            if (signature.ReturnType.IsFunctionPointer)
                return false;

            for (int i = 0; i < signature.Length; i++)
                if (signature[i].IsFunctionPointer)
                    return false;

            // ----------------------------------------------------------------
            // Methods with ByRef returns can't be reflection invoked
            // ----------------------------------------------------------------

            if (signature.ReturnType.IsByRef)
                return false;

            // ----------------------------------------------------------------
            // Methods that return ByRef-like types or take them by reference can't be reflection invoked
            // ----------------------------------------------------------------

            if (signature.ReturnType.IsDefType && ((DefType)signature.ReturnType).IsByRefLike)
                return false;

            for (int i = 0; i < signature.Length; i++)
            {
                ByRefType paramType = signature[i] as ByRefType;
                if (paramType != null && paramType.ParameterType.IsDefType && ((DefType)paramType.ParameterType).IsByRefLike)
                    return false;
            }

            // ----------------------------------------------------------------
            // Delegate construction is only allowed through specific IL sequences
            // ----------------------------------------------------------------

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
        /// Given that a method is invokable, if it is inserted into the reflection invoke table
        /// will it use a method token to be referenced, or not?
        /// </summary>
        public abstract bool WillUseMetadataTokenToReferenceMethod(MethodDesc method);

        /// <summary>
        /// Given that a method is invokable, if it is inserted into the reflection invoke table
        /// will it use a field token to be referenced, or not?
        /// </summary>
        public abstract bool WillUseMetadataTokenToReferenceField(FieldDesc field);

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public abstract MethodDesc GetCanonicalReflectionInvokeStub(MethodDesc method);

        /// <summary>
        /// Compute the canonical instantiation of a dynamic invoke thunk needed to invoke a method
        /// This algorithm is shared with the runtime, so if a thunk requires generic instantiation
        /// to be used, it must match this algorithm, and cannot be different with different MetadataManagers
        /// NOTE: This function may return null in cases where an exact instantiation does not exist. (Universal Generics)
        /// </summary>
        protected MethodDesc InstantiateCanonicalDynamicInvokeMethodForMethod(MethodDesc thunk, MethodDesc method)
        {
            if (thunk.Instantiation.Length == 0)
            {
                // nothing to instantiate
                return thunk;
            }

            MethodSignature sig = method.Signature;
            TypeSystemContext context = method.Context;

            //
            // Instantiate the generic thunk over the parameters and the return type of the target method
            //

            ParameterMetadata[] paramMetadata = null;
            TypeDesc[] instantiation = new TypeDesc[sig.ReturnType.IsVoid ? sig.Length : sig.Length + 1];
            Debug.Assert(thunk.Instantiation.Length == instantiation.Length);
            for (int i = 0; i < sig.Length; i++)
            {
                TypeDesc parameterType = sig[i];
                if (parameterType.IsByRef)
                {
                    // strip ByRefType off the parameter (the method already has ByRef in the signature)
                    parameterType = ((ByRefType)parameterType).ParameterType;

                    Debug.Assert(!parameterType.IsPointer); // TODO: support for methods returning pointer types - https://github.com/dotnet/corert/issues/2113
                }
                else if (parameterType.IsPointer || parameterType.IsFunctionPointer)
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

            Debug.Assert(thunk.Instantiation.Length == instantiation.Length);

            // Check if at least one of the instantiation arguments is a universal canonical type, and if so, we 
            // won't create a dynamic invoker instantiation. The arguments will be interpreted at runtime by the
            // calling convention converter during the dynamic invocation
            foreach (TypeDesc type in instantiation)
            {
                if (type.IsCanonicalSubtype(CanonicalFormKind.Universal))
                    return null;
            }

            // If the thunk ends up being shared code, return the canonical method body.
            // The concrete dictionary for the thunk will be built at runtime and is not interesting for the compiler.
            Instantiation canonInstantiation = context.ConvertInstantiationToCanonForm(new Instantiation(instantiation), CanonicalFormKind.Specific);

            MethodDesc instantiatedDynamicInvokeMethod = thunk.Context.GetInstantiatedMethod(thunk, canonInstantiation);
            return instantiatedDynamicInvokeMethod;
        }

        private void EnsureMetadataGenerated(NodeFactory factory)
        {
            if (_metadataBlob != null)
                return;

            ComputeMetadata(factory, out _metadataBlob, out _typeMappings, out _methodMappings, out _fieldMappings);
        }

        protected abstract void ComputeMetadata(NodeFactory factory,
                                                out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings);



        /// <summary>
        /// Returns a set of modules that will get some metadata emitted into the output module
        /// </summary>
        public abstract IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata();

        public byte[] GetMetadataBlob(NodeFactory factory)
        {
            EnsureMetadataGenerated(factory);
            return _metadataBlob;
        }

        public IEnumerable<MetadataMapping<MetadataType>> GetTypeDefinitionMapping(NodeFactory factory)
        {
            EnsureMetadataGenerated(factory);
            return _typeMappings;
        }

        public IEnumerable<MetadataMapping<MethodDesc>> GetMethodMapping(NodeFactory factory)
        {
            EnsureMetadataGenerated(factory);
            return _methodMappings;
        }

        public IEnumerable<MetadataMapping<FieldDesc>> GetFieldMapping(NodeFactory factory)
        {
            EnsureMetadataGenerated(factory);
            return _fieldMappings;
        }

        internal IEnumerable<NonGCStaticsNode> GetCctorContextMapping()
        {
            return _cctorContextsGenerated;
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

        internal IEnumerable<TypeDesc> GetTypesWithConstructedEETypes()
        {
            return _typesWithConstructedEETypesGenerated;
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
