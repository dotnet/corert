// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.IL;
using Internal.TypeSystem;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing native metadata to be emitted into the compiled
    /// module. It applies a policy that every type/method that is statically used shall be reflectable.
    /// </summary>
    public sealed class UsageBasedMetadataManager : GeneratingMetadataManager
    {
        private readonly CompilationModuleGroup _compilationModuleGroup;

        internal readonly UsageBasedMetadataGenerationOptions _generationOptions;
        private readonly bool _hasPreciseFieldUsageInformation;

        private readonly ILProvider _ilProvider;

        private readonly List<ModuleDesc> _modulesWithMetadata = new List<ModuleDesc>();
        private readonly List<FieldDesc> _fieldsWithMetadata = new List<FieldDesc>();
        private readonly List<MethodDesc> _methodsWithMetadata = new List<MethodDesc>();
        private readonly List<MetadataType> _typesWithMetadata = new List<MetadataType>();

        private readonly HashSet<ModuleDesc> _rootAllAssembliesExaminedModules = new HashSet<ModuleDesc>();

        private readonly MetadataType _serializationInfoType;

        public UsageBasedMetadataManager(
            CompilationModuleGroup group,
            CompilerTypeSystemContext typeSystemContext,
            MetadataBlockingPolicy blockingPolicy,
            ManifestResourceBlockingPolicy resourceBlockingPolicy,
            string logFile,
            StackTraceEmissionPolicy stackTracePolicy,
            DynamicInvokeThunkGenerationPolicy invokeThunkGenerationPolicy,
            ILProvider ilProvider,
            UsageBasedMetadataGenerationOptions generationOptions)
            : base(typeSystemContext, blockingPolicy, resourceBlockingPolicy, logFile, stackTracePolicy, invokeThunkGenerationPolicy)
        {
            // We use this to mark places that would behave differently if we tracked exact fields used. 
            _hasPreciseFieldUsageInformation = false;
            _compilationModuleGroup = group;
            _generationOptions = generationOptions;
            _ilProvider = ilProvider;

            _serializationInfoType = typeSystemContext.SystemModule.GetType("System.Runtime.Serialization", "SerializationInfo", false);
        }

        protected override void Graph_NewMarkedNode(DependencyNodeCore<NodeFactory> obj)
        {
            base.Graph_NewMarkedNode(obj);

            var moduleMetadataNode = obj as ModuleMetadataNode;
            if (moduleMetadataNode != null)
            {
                _modulesWithMetadata.Add(moduleMetadataNode.Module);
            }

            var fieldMetadataNode = obj as FieldMetadataNode;
            if (fieldMetadataNode != null)
            {
                _fieldsWithMetadata.Add(fieldMetadataNode.Field);
            }

            var methodMetadataNode = obj as MethodMetadataNode;
            if (methodMetadataNode != null)
            {
                _methodsWithMetadata.Add(methodMetadataNode.Method);
            }

            var typeMetadataNode = obj as TypeMetadataNode;
            if (typeMetadataNode != null)
            {
                _typesWithMetadata.Add(typeMetadataNode.Type);
            }
        }

        protected override MetadataCategory GetMetadataCategory(FieldDesc field)
        {
            MetadataCategory category = 0;

            if (!IsReflectionBlocked(field))
            {
                category = MetadataCategory.RuntimeMapping;

                if (_compilationModuleGroup.ContainsType(field.GetTypicalFieldDefinition().OwningType))
                    category |= MetadataCategory.Description;
            }

            return category;
        }

        protected override MetadataCategory GetMetadataCategory(MethodDesc method)
        {
            MetadataCategory category = 0;

            if (!IsReflectionBlocked(method))
            {
                category = MetadataCategory.RuntimeMapping;

                if (_compilationModuleGroup.ContainsType(method.GetTypicalMethodDefinition().OwningType))
                    category |= MetadataCategory.Description;
            }

            return category;
        }

        protected override MetadataCategory GetMetadataCategory(TypeDesc type)
        {
            MetadataCategory category = 0;

            if (!IsReflectionBlocked(type))
            {
                category = MetadataCategory.RuntimeMapping;

                if (_compilationModuleGroup.ContainsType(type.GetTypeDefinition()))
                    category |= MetadataCategory.Description;
            }

            return category;
        }

        protected override void ComputeMetadata(NodeFactory factory,
            out byte[] metadataBlob,
            out List<MetadataMapping<MetadataType>> typeMappings,
            out List<MetadataMapping<MethodDesc>> methodMappings,
            out List<MetadataMapping<FieldDesc>> fieldMappings,
            out List<MetadataMapping<MethodDesc>> stackTraceMapping)
        {
            ComputeMetadata(new GeneratedTypesAndCodeMetadataPolicy(_blockingPolicy, factory),
                factory, out metadataBlob, out typeMappings, out methodMappings, out fieldMappings, out stackTraceMapping);
        }

        protected override void GetMetadataDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            dependencies = dependencies ?? new DependencyList();
            dependencies.Add(factory.MethodMetadata(method.GetTypicalMethodDefinition()), "Reflectable method");
        }

        protected override void GetMetadataDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            TypeMetadataNode.GetMetadataDependencies(ref dependencies, factory, type, "Reflectable type");

            // If we don't have precise field usage information, apply policy that all fields that
            // are eligible to have metadata get metadata.
            if (!_hasPreciseFieldUsageInformation)
            {
                TypeDesc typeDefinition = type.GetTypeDefinition();

                foreach (FieldDesc field in typeDefinition.GetFields())
                {
                    if ((GetMetadataCategory(field) & MetadataCategory.Description) != 0)
                    {
                        dependencies = dependencies ?? new DependencyList();
                        dependencies.Add(factory.FieldMetadata(field), "Field of a reflectable type");
                    }
                }
            }

            // If anonymous type heuristic is turned on and this is an anonymous type, make sure we have
            // method bodies for all properties. It's common to have anonymous types used with reflection
            // and it's hard to specify them in RD.XML.
            if ((_generationOptions & UsageBasedMetadataGenerationOptions.AnonymousTypeHeuristic) != 0)
            {
                if (type is MetadataType metadataType &&
                    metadataType.HasInstantiation &&
                    !metadataType.IsGenericDefinition &&
                    metadataType.HasCustomAttribute("System.Runtime.CompilerServices", "CompilerGeneratedAttribute") &&
                    metadataType.Name.Contains("AnonymousType"))
                {
                    foreach (MethodDesc method in type.GetMethods())
                    {
                        if (!method.Signature.IsStatic && method.IsSpecialName)
                        {
                            dependencies = dependencies ?? new DependencyList();
                            dependencies.Add(factory.CanonicalEntrypoint(method), "Anonymous type accessor");
                        }
                    }
                }
            }

            // If the option was specified to root types and methods in all user assemblies, do that.
            if ((_generationOptions & UsageBasedMetadataGenerationOptions.FullUserAssemblyRooting) != 0)
            {
                if (type is MetadataType metadataType &&
                    !_rootAllAssembliesExaminedModules.Contains(metadataType.Module))
                {
                    _rootAllAssembliesExaminedModules.Add(metadataType.Module);

                    if (metadataType.Module is Internal.TypeSystem.Ecma.EcmaModule ecmaModule &&
                        !FrameworkStringResourceBlockingPolicy.IsFrameworkAssembly(ecmaModule))
                    {
                        dependencies = dependencies ?? new DependencyList();
                        var rootProvider = new RootingServiceProvider(factory, dependencies.Add);
                        foreach (TypeDesc t in ecmaModule.GetAllTypes())
                        {
                            RootingHelpers.TryRootType(rootProvider, t, "RD.XML root");
                        }
                    }
                }
            }

            // If a type is marked [Serializable], make sure a couple things are also included.
            if (type.IsSerializable && !type.IsGenericDefinition)
            {
                foreach (MethodDesc method in type.GetAllMethods())
                {
                    MethodSignature signature = method.Signature;

                    if (method.IsConstructor
                        && signature.Length == 2
                        && signature[0] == _serializationInfoType
                        /* && signature[1] is StreamingContext */)
                    {
                        dependencies = dependencies ?? new DependencyList();
                        dependencies.Add(factory.CanonicalEntrypoint(method), "Binary serialization");
                    }

                    // Methods with these attributes can be called during serialization
                    if (signature.Length == 1 && !signature.IsStatic && signature.ReturnType.IsVoid &&
                        (method.HasCustomAttribute("System.Runtime.Serialization", "OnSerializingAttribute")
                        || method.HasCustomAttribute("System.Runtime.Serialization", "OnSerializedAttribute")
                        || method.HasCustomAttribute("System.Runtime.Serialization", "OnDeserializingAttribute")
                        || method.HasCustomAttribute("System.Runtime.Serialization", "OnDeserializedAttribute")))
                    {
                        dependencies = dependencies ?? new DependencyList();
                        dependencies.Add(factory.CanonicalEntrypoint(method), "Binary serialization");
                    }
                }
            }

            // Event sources need their special nested types
            if (type is MetadataType mdType && mdType.HasCustomAttribute("System.Diagnostics.Tracing", "EventSourceAttribute"))
            {
                AddEventSourceSpecialTypeDependencies(ref dependencies, factory, mdType.GetNestedType("Keywords"));
                AddEventSourceSpecialTypeDependencies(ref dependencies, factory, mdType.GetNestedType("Tasks"));
                AddEventSourceSpecialTypeDependencies(ref dependencies, factory, mdType.GetNestedType("Opcodes"));

                void AddEventSourceSpecialTypeDependencies(ref DependencyList dependencies, NodeFactory factory, MetadataType type)
                {
                    if (type != null)
                    {
                        const string reason = "Event source";
                        dependencies = dependencies ?? new DependencyList();
                        dependencies.Add(factory.TypeMetadata(type), reason);
                        foreach (FieldDesc field in type.GetFields())
                        {
                            if (field.IsLiteral)
                                dependencies.Add(factory.FieldMetadata(field), reason);
                        }
                    }
                }
            }
        }

        protected override void GetRuntimeMappingDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            // If we precisely track field usage, we don't need the logic below.
            if (_hasPreciseFieldUsageInformation)
                return;

            const string reason = "Reflection";

            // This logic is applying policy: if a type is reflectable (has a runtime mapping), all of it's fields
            // are reflectable (with a runtime mapping) as well.
            // This is potentially overly broad (we don't know if any of the fields will actually be eligile
            // for metadata - e.g. they could all be reflection blocked). This is fine since lack of
            // precise field usage information is already not ideal from a size on disk perspective.
            // The more precise way to do this would be to go over each field, check that it's eligible for RuntimeMapping
            // according to the policy (e.g. it's not blocked), and only then root the base of the field.
            if (type is MetadataType metadataType && !type.IsGenericDefinition)
            {
                Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Any));

                if (metadataType.GCStaticFieldSize.AsInt > 0)
                {
                    dependencies.Add(factory.TypeGCStaticsSymbol(metadataType), reason);
                }

                if (metadataType.NonGCStaticFieldSize.AsInt > 0 || factory.PreinitializationManager.HasLazyStaticConstructor(metadataType))
                {
                    dependencies.Add(factory.TypeNonGCStaticsSymbol(metadataType), reason);
                }

                if (metadataType.ThreadGcStaticFieldSize.AsInt > 0)
                {
                    dependencies.Add(factory.TypeThreadStaticIndex(metadataType), reason);
                }

                Debug.Assert(metadataType.ThreadNonGcStaticFieldSize.AsInt == 0);
            }
        }

        public override void GetDependenciesDueToLdToken(ref DependencyList dependencies, NodeFactory factory, FieldDesc field)
        {
            // In order for the RuntimeFieldHandle data structure to be usable at runtime, ensure the field
            // is generating metadata.
            if ((GetMetadataCategory(field) & MetadataCategory.Description) == MetadataCategory.Description)
            {
                dependencies = dependencies ?? new DependencyList();
                dependencies.Add(factory.FieldMetadata(field.GetTypicalFieldDefinition()), "LDTOKEN field");
            }
        }

        public override void GetDependenciesDueToLdToken(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            // In order for the RuntimeMethodHandle data structure to be usable at runtime, ensure the method
            // is generating metadata.
            if ((GetMetadataCategory(method) & MetadataCategory.Description) == MetadataCategory.Description)
            {
                dependencies = dependencies ?? new DependencyList();
                dependencies.Add(factory.MethodMetadata(method.GetTypicalMethodDefinition()), "LDTOKEN method");
            }

            // Since this is typically used for LINQ expressions, let's also make sure there's runnable code
            // for this available, unless this is ldtoken of something we can't generate code for
            // (ldtoken of an uninstantiated generic method - F# makes this).
            if (!method.IsGenericMethodDefinition)
            {
                var deps = dependencies ?? new DependencyList();
                RootingHelpers.TryRootMethod(
                    new RootingServiceProvider(
                        factory, (o, reason) => deps.Add((DependencyNodeCore<NodeFactory>)o, reason)), method, "LDTOKEN method");
                dependencies = deps;
            }
        }

        public override bool ShouldConsiderLdTokenReferenceAConstruction(TypeDesc type)
        {
            // Tell codegen to use necessary type symbol. We're going to upgrade to a constructed
            // type symbol from our IL scanner if needed when we get the GetDependenciesDueToMethodCodePresence callback.
            return false;
        }

        protected override void GetDependenciesDueToMethodCodePresenceInternal(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            bool scanReflection = (_generationOptions & UsageBasedMetadataGenerationOptions.ReflectionILScanning) != 0;
            bool scanInterop = (_generationOptions & UsageBasedMetadataGenerationOptions.IteropILScanning) != 0;

            // NOTE: we will intentionally run the scanner even if both scan modes are disabled
            // because we rely on the scanner to report constructed EETypes for LDTOKEN references
            // to types.
            ReflectionMethodBodyScanner.ScanModes modes = 0;
            if (scanReflection)
                modes |= ReflectionMethodBodyScanner.ScanModes.Reflection;
            if (scanInterop)
                modes |= ReflectionMethodBodyScanner.ScanModes.Interop;

            MethodIL methodIL = _ilProvider.GetMethodIL(method);

            if (methodIL != null)
            {
                try
                {
                    ReflectionMethodBodyScanner.Scan(ref dependencies, factory, methodIL, modes);
                }
                catch (TypeSystemException)
                {
                    // A problem with the IL - we just don't scan it...
                }
            }
        }

        protected override IEnumerable<FieldDesc> GetFieldsWithRuntimeMapping()
        {
            if (_hasPreciseFieldUsageInformation)
            {
                // TODO
            }
            else
            {
                // This applies a policy that fields inherit runtime mapping from their owning type,
                // unless they are blocked.
                foreach (var type in GetTypesWithRuntimeMapping())
                {
                    if (type.IsGenericDefinition)
                        continue;

                    foreach (var field in type.GetFields())
                    {
                        if ((GetMetadataCategory(field) & MetadataCategory.RuntimeMapping) != 0)
                            yield return field;
                    }
                }
            }
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return _modulesWithMetadata;
        }

        private IEnumerable<TypeDesc> GetTypesWithRuntimeMapping()
        {
            // All constructed types that are not blocked get runtime mapping
            foreach (var constructedType in GetTypesWithConstructedEETypes())
            {
                if (!IsReflectionBlocked(constructedType))
                    yield return constructedType;
            }

            // All necessary types for which this is the highest load level that are not blocked
            // get runtime mapping.
            foreach (var necessaryType in GetTypesWithEETypes())
            {
                if (!ConstructedEETypeNode.CreationAllowed(necessaryType) &&
                    !IsReflectionBlocked(necessaryType))
                    yield return necessaryType;
            }
        }

        public MetadataManager ToAnalysisBasedMetadataManager()
        {
            var reflectableTypes = ReflectableEntityBuilder<TypeDesc>.Create();

            // Collect the list of types that are generating reflection metadata
            foreach (var typeWithMetadata in _typesWithMetadata)
            {
                reflectableTypes[typeWithMetadata] = MetadataCategory.Description;
            }

            foreach (var constructedType in GetTypesWithRuntimeMapping())
            {
                reflectableTypes[constructedType] |= MetadataCategory.RuntimeMapping;

                // Also set the description bit if the definition is getting metadata.
                TypeDesc constructedTypeDefinition = constructedType.GetTypeDefinition();
                if (constructedType != constructedTypeDefinition &&
                    (reflectableTypes[constructedTypeDefinition] & MetadataCategory.Description) != 0)
                {
                    reflectableTypes[constructedType] |= MetadataCategory.Description;
                }
            }

            var reflectableMethods = ReflectableEntityBuilder<MethodDesc>.Create();
            foreach (var methodWithMetadata in _methodsWithMetadata)
            {
                reflectableMethods[methodWithMetadata] = MetadataCategory.Description;
            }

            foreach (var method in GetCompiledMethods())
            {
                if (!IsReflectionBlocked(method))
                {
                    if ((reflectableTypes[method.OwningType] & MetadataCategory.RuntimeMapping) != 0)
                        reflectableMethods[method] |= MetadataCategory.RuntimeMapping;

                    // Also set the description bit if the definition is getting metadata.
                    MethodDesc typicalMethod = method.GetTypicalMethodDefinition();
                    if (method != typicalMethod &&
                        (reflectableMethods[typicalMethod] & MetadataCategory.Description) != 0)
                    {
                        reflectableMethods[method] |= MetadataCategory.Description;
                        reflectableTypes[method.OwningType] |= MetadataCategory.Description;
                    }
                }
            }

            var reflectableFields = ReflectableEntityBuilder<FieldDesc>.Create();
            foreach (var fieldWithMetadata in _fieldsWithMetadata)
            {
                reflectableFields[fieldWithMetadata] = MetadataCategory.Description;
            }

            if (_hasPreciseFieldUsageInformation)
            {
                // TODO
            }
            else
            {
                // If we don't have precise field usage information we apply a policy that
                // says the fields inherit the setting from the type, potentially restricted by blocking.
                // (I.e. if a type has RuntimeMapping metadata, the field has RuntimeMapping too, unless blocked.)
                foreach (var reflectableType in reflectableTypes.ToEnumerable())
                {
                    if (reflectableType.Entity.IsGenericDefinition)
                        continue;

                    if (reflectableType.Entity.IsCanonicalSubtype(CanonicalFormKind.Specific))
                        continue;

                    if ((reflectableType.Category & MetadataCategory.RuntimeMapping) == 0)
                        continue;

                    foreach (var field in reflectableType.Entity.GetFields())
                    {
                        if (!IsReflectionBlocked(field))
                        {
                            reflectableFields[field] |= MetadataCategory.RuntimeMapping;

                            // Also set the description bit if the definition is getting metadata.
                            FieldDesc typicalField = field.GetTypicalFieldDefinition();
                            if (field != typicalField &&
                                (reflectableFields[typicalField] & MetadataCategory.Description) != 0)
                            {
                                reflectableFields[field] |= MetadataCategory.Description;
                            }
                        }
                    }
                }
            }

            return new AnalysisBasedMetadataManager(
                _typeSystemContext, _blockingPolicy, _resourceBlockingPolicy, _metadataLogFile, _stackTraceEmissionPolicy, _dynamicInvokeThunkGenerationPolicy,
                _modulesWithMetadata, reflectableTypes.ToEnumerable(), reflectableMethods.ToEnumerable(),
                reflectableFields.ToEnumerable(), GetTypesWithConstructedEETypes());
        }

        private struct ReflectableEntityBuilder<T>
        {
            private Dictionary<T, MetadataCategory> _dictionary;

            public static ReflectableEntityBuilder<T> Create()
            {
                return new ReflectableEntityBuilder<T>
                {
                    _dictionary = new Dictionary<T, MetadataCategory>(),
                };
            }

            public MetadataCategory this[T key]
            {
                get
                {
                    if (_dictionary.TryGetValue(key, out MetadataCategory category))
                        return category;
                    return 0;
                }
                set
                {
                    _dictionary[key] = value;
                }
            }

            public IEnumerable<ReflectableEntity<T>> ToEnumerable()
            {
                foreach (var entry in _dictionary)
                {
                    yield return new ReflectableEntity<T>(entry.Key, entry.Value);
                }
            }
        }

        private struct GeneratedTypesAndCodeMetadataPolicy : IMetadataPolicy
        {
            private readonly MetadataBlockingPolicy _blockingPolicy;
            private readonly NodeFactory _factory;

            public GeneratedTypesAndCodeMetadataPolicy(MetadataBlockingPolicy blockingPolicy, NodeFactory factory)
            {
                _blockingPolicy = blockingPolicy;
                _factory = factory;
            }

            public bool GeneratesMetadata(FieldDesc fieldDef)
            {
                return _factory.FieldMetadata(fieldDef).Marked;
            }

            public bool GeneratesMetadata(MethodDesc methodDef)
            {
                return _factory.MethodMetadata(methodDef).Marked;
            }

            public bool GeneratesMetadata(MetadataType typeDef)
            {
                return _factory.TypeMetadata(typeDef).Marked;
            }

            public bool IsBlocked(MetadataType typeDef)
            {
                return _blockingPolicy.IsBlocked(typeDef);
            }

            public bool IsBlocked(MethodDesc methodDef)
            {
                return _blockingPolicy.IsBlocked(methodDef);
            }
        }
    }

    [Flags]
    public enum UsageBasedMetadataGenerationOptions
    {
        None = 0,

        /// <summary>
        /// Specifies that complete metadata should be generated for types.
        /// </summary>
        /// <remarks>
        /// If this option is set, generated metadata will no longer be pay for play,
        /// and a certain class of bugs will disappear (APIs returning "member doesn't
        /// exist" at runtime, even though the member exists and we just didn't generate the metadata).
        /// Reflection blocking still applies.
        /// </remarks>
        CompleteTypesOnly = 1,

        /// <summary>
        /// Specifies that heuristic that makes anonymous types work should be applied.
        /// </summary>
        /// <remarks>
        /// Generates method bodies for properties on anonymous types even if they're not
        /// statically used.
        /// </remarks>
        AnonymousTypeHeuristic = 2,

        /// <summary>
        /// Scan IL for common reflection patterns to find additional compilation roots.
        /// </summary>
        ReflectionILScanning = 4,

        /// <summary>
        /// Scan IL for common interop patterns to find additional compilation roots.
        /// </summary>
        IteropILScanning = 8,

        /// <summary>
        /// Specifies that all types and methods in user assemblies should be considered dynamically
        /// used.
        /// </summary>
        FullUserAssemblyRooting = 0x10,
    }
}
