﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;
using ReadyToRunSectionType = Internal.Runtime.ReadyToRunSectionType;
using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;
using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

using MetadataRecord = Internal.Metadata.NativeFormat.Writer.MetadataRecord;
using MemberReference = Internal.Metadata.NativeFormat.Writer.MemberReference;
using TypeReference = Internal.Metadata.NativeFormat.Writer.TypeReference;
using TypeSpecification = Internal.Metadata.NativeFormat.Writer.TypeSpecification;
using ConstantStringValue = Internal.Metadata.NativeFormat.Writer.ConstantStringValue;
using TypeInstantiationSignature = Internal.Metadata.NativeFormat.Writer.TypeInstantiationSignature;
using MethodInstantiation = Internal.Metadata.NativeFormat.Writer.MethodInstantiation;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing native metadata to be emitted into the compiled
    /// module. It also helps facilitate mappings between generated runtime structures or code,
    /// and the native metadata.
    /// </summary>
    public abstract class MetadataManager : ICompilationRootProvider
    {
        internal const int MetadataOffsetMask = 0xFFFFFF;

        private byte[] _metadataBlob;
        private List<MetadataMapping<MetadataType>> _typeMappings;
        private List<MetadataMapping<FieldDesc>> _fieldMappings;
        private List<MetadataMapping<MethodDesc>> _methodMappings;
        private List<MetadataMapping<MethodDesc>> _stackTraceMappings;

        protected readonly CompilerTypeSystemContext _typeSystemContext;
        protected readonly MetadataBlockingPolicy _blockingPolicy;

        private List<NonGCStaticsNode> _cctorContextsGenerated = new List<NonGCStaticsNode>();
        private HashSet<TypeDesc> _typesWithEETypesGenerated = new HashSet<TypeDesc>();
        private HashSet<TypeDesc> _typesWithConstructedEETypesGenerated = new HashSet<TypeDesc>();
        private HashSet<MethodDesc> _methodsGenerated = new HashSet<MethodDesc>();
        private HashSet<GenericDictionaryNode> _genericDictionariesGenerated = new HashSet<GenericDictionaryNode>();
        private HashSet<IMethodBodyNode> _methodBodiesGenerated = new HashSet<IMethodBodyNode>();
        private List<TypeGVMEntriesNode> _typeGVMEntries = new List<TypeGVMEntriesNode>();
        private HashSet<DefaultConstructorFromLazyNode> _defaultConstructorsNeeded = new HashSet<DefaultConstructorFromLazyNode>();

        internal NativeLayoutInfoNode NativeLayoutInfo { get; private set; }
        internal DynamicInvokeTemplateDataNode DynamicInvokeTemplateData { get; private set; }
        public virtual bool SupportsReflection => true;

        public MetadataManager(CompilerTypeSystemContext typeSystemContext, MetadataBlockingPolicy blockingPolicy)
        {
            _typeSystemContext = typeSystemContext;
            _blockingPolicy = blockingPolicy;
        }

        public void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            graph.NewMarkedNode += Graph_NewMarkedNode;
        }

        internal static ReadyToRunSectionType BlobIdToReadyToRunSection(ReflectionMapBlob blobId)
        {
            var result = (ReadyToRunSectionType)((int)blobId + (int)ReadyToRunSectionType.ReadonlyBlobRegionStart);
            Debug.Assert(result <= ReadyToRunSectionType.ReadonlyBlobRegionEnd);
            return result;
        }

        public virtual void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            var metadataNode = new MetadataNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.EmbeddedMetadata), metadataNode, metadataNode, metadataNode.EndSymbol);

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

            var defaultConstructorMapNode = new DefaultConstructorMapNode(commonFixupsTableNode);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.DefaultConstructorMap), defaultConstructorMapNode, defaultConstructorMapNode, defaultConstructorMapNode.EndSymbol);

            var stackTraceMethodMappingNode = new StackTraceMethodMappingNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdStackTraceMethodRvaToTokenMapping), stackTraceMethodMappingNode, stackTraceMethodMappingNode, stackTraceMethodMappingNode.EndSymbol);
            
            // The external references tables should go last
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.NativeReferences), nativeReferencesTableNode, nativeReferencesTableNode, nativeReferencesTableNode.EndSymbol);
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.NativeStatics), nativeStaticsTableNode, nativeStaticsTableNode, nativeStaticsTableNode.EndSymbol);
        }

        protected virtual void Graph_NewMarkedNode(DependencyNodeCore<NodeFactory> obj)
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

            IMethodBodyNode methodBodyNode = obj as IMethodBodyNode;
            if (methodBodyNode != null)
            {
                _methodBodiesGenerated.Add(methodBodyNode);
            }

            IMethodNode methodNode = methodBodyNode;
            if (methodNode == null)
                methodNode = obj as ShadowConcreteMethodNode;

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

            var ctorFromLazyGenericsNode = obj as DefaultConstructorFromLazyNode;
            if (ctorFromLazyGenericsNode != null)
            {
                _defaultConstructorsNeeded.Add(ctorFromLazyGenericsNode);
            }
        }

        /// <summary>
        /// Is a method that is reflectable a method which should be placed into the invoke map as invokable?
        /// </summary>
        public virtual bool IsReflectionInvokable(MethodDesc method)
        {
            return IsMethodSignatureSupportedInReflectionInvoke(method)
                && IsMethodSupportedInReflectionInvoke(method);
        }

        protected bool IsMethodSignatureSupportedInReflectionInvoke(MethodDesc method)
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

            if (signature.ReturnType.IsByRefLike)
                return false;

            for (int i = 0; i < signature.Length; i++)
            {
                ByRefType paramType = signature[i] as ByRefType;
                if (paramType != null && paramType.ParameterType.IsByRefLike)
                    return false;
            }

            return true;
        }

        protected bool IsMethodSupportedInReflectionInvoke(MethodDesc method)
        {
            TypeDesc owningType = method.OwningType;

            // Methods on nullable are special cased in the runtime reflection
            if (owningType.IsNullable)
                return false;

            // Finalizers are not reflection invokable
            if (method.IsFinalizer)
                return false;

            // Static constructors are not reflection invokable
            if (method.IsStaticConstructor)
                return false;

            if (method.IsConstructor)
            {
                // Delegate construction is only allowed through specific IL sequences
                if (owningType.IsDelegate)
                    return false;

                // String constructors are intrinsic and special cased in runtime reflection
                if (owningType.IsString)
                    return false;
            }

            // Everything else can go in the mapping table.
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
        /// Is there a reflection invoke stub for a method that is invokable?
        /// </summary>
        public bool ShouldMethodBeInInvokeMap(MethodDesc method)
        {
            // The current format requires us to have an EEType for the owning type. We might want to lift this.
            if (!TypeGeneratesEEType(method.OwningType))
                return false;

            // We have a method body, we have a metadata token, but we can't get an invoke stub. Bail.
            if (!IsReflectionInvokable(method))
                return false;

            return true;
        }

        /// <summary>
        /// This method is an extension point that can provide additional metadata-based dependencies to compiled method bodies.
        /// </summary>
        public void GetDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            MetadataCategory category = GetMetadataCategory(method);

            if ((category & MetadataCategory.Description) != 0)
            {
                GetMetadataDependenciesDueToReflectability(ref dependencies, factory, method);
            }

            if ((category & MetadataCategory.RuntimeMapping) != 0)
            {
                if (IsReflectionInvokable(method))
                {
                    // We're going to generate a mapping table entry for this. Collect dependencies.
                    CodeBasedDependencyAlgorithm.AddDependenciesDueToReflectability(ref dependencies, factory, method);
                }
            }
        }

        /// <summary>
        /// This method is an extension point that can provide additional metadata-based dependencies on a virtual method.
        /// </summary>
        public void GetDependenciesDueToVirtualMethodReflectability(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            // If we have a use of an abstract method, GetDependenciesDueToReflectability is not going to see the method
            // as being used since there's no body. We inject a dependency on a new node that serves as a logical method body
            // for the metadata manager. Metadata manager treats that node the same as a body.
            if (method.IsAbstract && GetMetadataCategory(method) != 0)
            {
                dependencies = dependencies ?? new DependencyList();
                dependencies.Add(factory.ReflectableMethod(method), "Abstract reflectable method");
            }
        }

        protected virtual void GetMetadataDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            // MetadataManagers can override this to provide additional dependencies caused by the emission of metadata
            // (E.g. dependencies caused by the method having custom attributes applied to it: making sure we compile the attribute constructor
            // and property setters)
        }

        /// <summary>
        /// This method is an extension point that can provide additional metadata-based dependencies to generated EETypes.
        /// </summary>
        public void GetDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            MetadataCategory category = GetMetadataCategory(type);

            if ((category & MetadataCategory.Description) != 0)
            {
                GetMetadataDependenciesDueToReflectability(ref dependencies, factory, type);
            }

            if ((category & MetadataCategory.RuntimeMapping) != 0)
            {
                // We're going to generate a mapping table entry for this. Collect dependencies.

                // Nothing special is needed for the mapping table (we only emit the EEType and we already
                // have one, since we got this callback). But check if a child wants to do something extra.
                GetRuntimeMappingDependenciesDueToReflectability(ref dependencies, factory, type);
            }
        }

        protected virtual void GetMetadataDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            // MetadataManagers can override this to provide additional dependencies caused by the emission of metadata
            // (E.g. dependencies caused by the type having custom attributes applied to it: making sure we compile the attribute constructor
            // and property setters)
        }

        protected virtual void GetRuntimeMappingDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            // MetadataManagers can override this to provide additional dependencies caused by the emission of a runtime
            // mapping for a type.
        }

        /// <summary>
        /// This method is an extension point that can provide additional metadata-based dependencies to generated RuntimeMethodHandles.
        /// </summary>
        public virtual void GetDependenciesDueToLdToken(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            // MetadataManagers can override this to provide additional dependencies caused by the presence of a
            // RuntimeMethodHandle data structure.
        }

        /// <summary>
        /// This method is an extension point that can provide additional metadata-based dependencies to generated RuntimeFieldHandles.
        /// </summary>
        public virtual void GetDependenciesDueToLdToken(ref DependencyList dependencies, NodeFactory factory, FieldDesc field)
        {
            // MetadataManagers can override this to provide additional dependencies caused by the presence of a
            // RuntimeFieldHandle data structure.
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

        protected void EnsureMetadataGenerated(NodeFactory factory)
        {
            if (_metadataBlob != null)
                return;

            ComputeMetadata(factory, out _metadataBlob, out _typeMappings, out _methodMappings, out _fieldMappings, out _stackTraceMappings);
        }

        void ICompilationRootProvider.AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            // MetadataManagers can override this to provide metadata compilation roots that need to be added to the graph ahead of time.
            // (E.g. reflection roots computed by IL analyzers, or non-compilation-based roots)
        }

        protected abstract void ComputeMetadata(NodeFactory factory,
                                                out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings,
                                                out List<MetadataMapping<MethodDesc>> stackTraceMapping);

        protected MetadataRecord CreateStackTraceRecord(Metadata.MetadataTransform transform, MethodDesc method)
        {
            // In the metadata, we only represent the generic definition
            MethodDesc methodToGenerateMetadataFor = method.GetTypicalMethodDefinition();
            MetadataRecord record = transform.HandleQualifiedMethod(methodToGenerateMetadataFor);

            // If we're generating a MemberReference to a method on a generic type, the owning type
            // should appear as if instantiated over its formals
            TypeDesc owningTypeToGenerateMetadataFor = methodToGenerateMetadataFor.OwningType;
            if (owningTypeToGenerateMetadataFor.HasInstantiation
                && record is MemberReference memberRefRecord
                && memberRefRecord.Parent is TypeReference)
            {
                List<MetadataRecord> genericArgs = new List<MetadataRecord>();
                foreach (Internal.TypeSystem.Ecma.EcmaGenericParameter genericParam in owningTypeToGenerateMetadataFor.Instantiation)
                {
                    genericArgs.Add(new TypeReference
                    {
                        TypeName = (ConstantStringValue)genericParam.Name,
                    });
                }

                memberRefRecord.Parent = new TypeSpecification
                {
                    Signature = new TypeInstantiationSignature
                    {
                        GenericType = memberRefRecord.Parent,
                        GenericTypeArguments = genericArgs,
                    }
                };
            }

            // As a twist, instantiated generic methods appear as if instantiated over their formals.
            if (methodToGenerateMetadataFor.HasInstantiation)
            {
                var methodInst = new MethodInstantiation
                {
                    Method = record,
                };
                methodInst.GenericTypeArguments.Capacity = methodToGenerateMetadataFor.Instantiation.Length;
                foreach (Internal.TypeSystem.Ecma.EcmaGenericParameter typeArgument in methodToGenerateMetadataFor.Instantiation)
                {
                    var genericParam = new TypeReference
                    {
                        TypeName = (ConstantStringValue)typeArgument.Name,
                    };
                    methodInst.GenericTypeArguments.Add(genericParam);
                }
                record = methodInst;
            }

            return record;
        }

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

        public IEnumerable<MetadataMapping<MethodDesc>> GetStackTraceMapping(NodeFactory factory)
        {
            EnsureMetadataGenerated(factory);
            return _stackTraceMappings;
        }

        internal IEnumerable<NonGCStaticsNode> GetCctorContextMapping()
        {
            return _cctorContextsGenerated;
        }

        internal IEnumerable<TypeGVMEntriesNode> GetTypeGVMEntries()
        {
            return _typeGVMEntries;
        }

        internal IReadOnlyCollection<GenericDictionaryNode> GetCompiledGenericDictionaries()
        {
            return _genericDictionariesGenerated;
        }

        internal IEnumerable<MethodDesc> GetCompiledMethods()
        {
            return _methodsGenerated;
        }

        internal IEnumerable<IMethodBodyNode> GetCompiledMethodBodies()
        {
            return _methodBodiesGenerated;
        }

        internal IEnumerable<DefaultConstructorFromLazyNode> GetDefaultConstructorsNeeded()
        {
            return _defaultConstructorsNeeded;
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

        public bool IsReflectionBlocked(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.SzArray:
                case TypeFlags.Array:
                case TypeFlags.Pointer:
                case TypeFlags.ByRef:
                    return IsReflectionBlocked(((ParameterizedType)type).ParameterType);

                case TypeFlags.FunctionPointer:
                    throw new NotImplementedException();

                default:
                    Debug.Assert(type.IsDefType);

                    TypeDesc typeDefinition = type.GetTypeDefinition();
                    if (type != typeDefinition)
                    {
                        if (_blockingPolicy.IsBlocked((MetadataType)typeDefinition))
                            return true;

                        foreach (var arg in type.Instantiation)
                            if (IsReflectionBlocked(arg))
                                return true;

                        return false;
                    }

                    if (type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
                        return false;

                    return _blockingPolicy.IsBlocked((MetadataType)type);
            }
        }

        protected bool IsReflectionBlocked(Instantiation instantiation)
        {
            foreach (TypeDesc type in instantiation)
            {
                if (IsReflectionBlocked(type))
                    return true;
            }
            return false;
        }

        public bool IsReflectionBlocked(FieldDesc field)
        {
            FieldDesc typicalFieldDefinition = field.GetTypicalFieldDefinition();
            if (typicalFieldDefinition != field && IsReflectionBlocked(field.OwningType.Instantiation))
            {
                return true;
            }

            return _blockingPolicy.IsBlocked(typicalFieldDefinition);
        }

        public bool IsReflectionBlocked(MethodDesc method)
        {
            MethodDesc methodDefinition = method.GetMethodDefinition();
            if (method != methodDefinition && IsReflectionBlocked(method.Instantiation))
            {
                return true;
            }

            MethodDesc typicalMethodDefinition = methodDefinition.GetTypicalMethodDefinition();
            if (typicalMethodDefinition != methodDefinition && IsReflectionBlocked(method.OwningType.Instantiation))
            {
                return true;
            }

            return _blockingPolicy.IsBlocked(typicalMethodDefinition);
        }

        public bool CanGenerateMetadata(MetadataType type)
        {
            return (GetMetadataCategory(type) & MetadataCategory.Description) != 0;
        }

        public bool CanGenerateMetadata(MethodDesc method)
        {
            Debug.Assert(method.IsTypicalMethodDefinition);
            return (GetMetadataCategory(method) & MetadataCategory.Description) != 0;
        }

        public bool CanGenerateMetadata(FieldDesc field)
        {
            Debug.Assert(field.IsTypicalFieldDefinition);
            return (GetMetadataCategory(field) & MetadataCategory.Description) != 0;
        }

        /// <summary>
        /// Gets the metadata category for a compiled method body in the current compilation.
        /// The method will only get called with '<paramref name="method"/>' that has a compiled method body
        /// in this compilation.
        /// Note that if this method doesn't return <see cref="MetadataCategory.Description"/>, it doesn't mean
        /// that the method never has metadata. The metadata might just be generated in a different compilation.
        /// </summary>
        protected abstract MetadataCategory GetMetadataCategory(MethodDesc method);

        /// <summary>
        /// Gets the metadata category for a generated type in the current compilation.
        /// The method can assume it will only get called with '<paramref name="type"/>' that has an EEType generated
        /// in the current compilation.
        /// Note that if this method doesn't return <see cref="MetadataCategory.Description"/>, it doesn't mean
        /// that the method never has metadata. The metadata might just be generated in a different compilation.
        /// </summary>
        protected abstract MetadataCategory GetMetadataCategory(TypeDesc type);
        protected abstract MetadataCategory GetMetadataCategory(FieldDesc field);
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

    [Flags]
    public enum MetadataCategory
    {
        None = 0x00,
        Description = 0x01,
        RuntimeMapping = 0x02,
    }
}
