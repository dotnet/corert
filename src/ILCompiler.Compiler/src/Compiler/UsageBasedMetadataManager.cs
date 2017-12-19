// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing native metadata to be emitted into the compiled
    /// module. It applies a policy that every type/method that is statically used shall be reflectable.
    /// </summary>
    public sealed class UsageBasedMetadataManager : GeneratingMetadataManager
    {
        private readonly CompilationModuleGroup _compilationModuleGroup;

        private readonly List<ModuleDesc> _modulesWithMetadata = new List<ModuleDesc>();
        private readonly List<FieldDesc> _fieldsWithMetadata = new List<FieldDesc>();
        private readonly List<MethodDesc> _methodsWithMetadata = new List<MethodDesc>();
        private readonly List<MetadataType> _typesWithMetadata = new List<MetadataType>();

        public UsageBasedMetadataManager(
            CompilationModuleGroup group,
            CompilerTypeSystemContext typeSystemContext,
            MetadataBlockingPolicy blockingPolicy,
            string logFile,
            StackTraceEmissionPolicy stackTracePolicy)
            : base(group.GeneratedAssembly, typeSystemContext, blockingPolicy, logFile, stackTracePolicy)
        {
            _compilationModuleGroup = group;
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
            if (method.GetTypicalMethodDefinition().OwningType == factory.ArrayOfTClass)
                return;

            dependencies = dependencies ?? new DependencyList();
            dependencies.Add(factory.MethodMetadata(method.GetTypicalMethodDefinition()), "Reflectable method");
        }

        protected override void GetMetadataDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.GetTypeDefinition() == factory.ArrayOfTClass)
                return;

            TypeMetadataNode.GetMetadataDependencies(ref dependencies, factory, type, "Reflectable type");
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return _modulesWithMetadata;
        }

        public MetadataManager ToAnalysisBasedMetadataManager()
        {
            var reflectableTypes = ReflectableEntityBuilder<TypeDesc>.Create();

            // Collect the list of types that are generating reflection metadata
            foreach (var typeWithMetadata in _typesWithMetadata)
            {
                reflectableTypes[typeWithMetadata] = MetadataCategory.Description;
            }

            // All the constructed types we generated that are not blocked are required to have runtime artifacts
            foreach (var constructedType in GetTypesWithConstructedEETypes())
            {
                if (!IsReflectionBlocked(constructedType))
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
            }

            // All the necessary types for which this is the higest load level are required to have runtime artifacts
            foreach (var necessaryType in GetTypesWithEETypes())
            {
                if (!ConstructedEETypeNode.CreationAllowed(necessaryType) &&
                    !IsReflectionBlocked(necessaryType))
                {
                    reflectableTypes[necessaryType] |= MetadataCategory.RuntimeMapping;

                    // Also set the description bit if the definition is getting metadata.
                    TypeDesc necessaryTypeDefinition = necessaryType.GetTypeDefinition();
                    if (necessaryType != necessaryTypeDefinition &&
                        (reflectableTypes[necessaryTypeDefinition] & MetadataCategory.Description) != 0)
                    {
                        reflectableTypes[necessaryType] |= MetadataCategory.Description;
                    }
                }
            }

            var reflectableMethods = ReflectableEntityBuilder<MethodDesc>.Create();
            foreach (var methodWithMetadata in _methodsWithMetadata)
            {
                reflectableMethods[methodWithMetadata] = MetadataCategory.Description;
            }

            foreach (var method in GetCompiledMethods())
            {
                if (!method.IsCanonicalMethod(CanonicalFormKind.Specific) &&
                    !IsReflectionBlocked(method))
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

            // TODO: this should be more precise
            foreach (var reflectableType in reflectableTypes.ToEnumerable())
            {
                if (reflectableType.Entity.IsGenericDefinition)
                    continue;

                if (reflectableType.Entity.IsCanonicalSubtype(CanonicalFormKind.Specific))
                    continue;

                foreach (var field in reflectableType.Entity.GetFields())
                {
                    if (CanGenerateMetadata(field.GetTypicalFieldDefinition()))
                        reflectableFields[field] |= reflectableType.Category;
                }
            }

            return new AnalysisBasedMetadataManager(_compilationModuleGroup.GeneratedAssembly,
                _typeSystemContext, _blockingPolicy, _metadataLogFile, _stackTraceEmissionPolicy,
                _modulesWithMetadata, reflectableTypes.ToEnumerable(), reflectableMethods.ToEnumerable(),
                reflectableFields.ToEnumerable());
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
            private readonly ExplicitScopeAssemblyPolicyMixin _explicitScopeMixin;

            public GeneratedTypesAndCodeMetadataPolicy(MetadataBlockingPolicy blockingPolicy, NodeFactory factory)
            {
                _blockingPolicy = blockingPolicy;
                _factory = factory;
                _explicitScopeMixin = new ExplicitScopeAssemblyPolicyMixin();
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

            public ModuleDesc GetModuleOfType(MetadataType typeDef)
            {
                return _explicitScopeMixin.GetModuleOfType(typeDef);
            }
        }
    }
}
