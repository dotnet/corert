// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// A metadata manager that knows the full set of metadata ahead of time.
    /// </summary>
    public sealed class AnalysisBasedMetadataManager : GeneratingMetadataManager, ICompilationRootProvider
    {
        private readonly List<ModuleDesc> _modulesWithMetadata;

        private readonly Dictionary<TypeDesc, MetadataCategory> _reflectableTypes = new Dictionary<TypeDesc, MetadataCategory>();
        private readonly Dictionary<MethodDesc, MetadataCategory> _reflectableMethods = new Dictionary<MethodDesc, MetadataCategory>();
        private readonly Dictionary<FieldDesc, MetadataCategory> _reflectableFields = new Dictionary<FieldDesc, MetadataCategory>();

        private readonly HashSet<TypeDesc> _ldtokenReferenceableTypes;

        public AnalysisBasedMetadataManager(CompilerTypeSystemContext typeSystemContext)
            : this(typeSystemContext, new FullyBlockedMetadataBlockingPolicy(),
                new FullyBlockedManifestResourceBlockingPolicy(), null, new NoStackTraceEmissionPolicy(),
                new NoDynamicInvokeThunkGenerationPolicy(), Array.Empty<ModuleDesc>(),
                Array.Empty<ReflectableEntity<TypeDesc>>(), Array.Empty<ReflectableEntity<MethodDesc>>(),
                Array.Empty<ReflectableEntity<FieldDesc>>(), Array.Empty<TypeDesc>())
        {
        }

        public AnalysisBasedMetadataManager(
            CompilerTypeSystemContext typeSystemContext,
            MetadataBlockingPolicy blockingPolicy,
            ManifestResourceBlockingPolicy resourceBlockingPolicy,
            string logFile,
            StackTraceEmissionPolicy stackTracePolicy,
            DynamicInvokeThunkGenerationPolicy invokeThunkGenerationPolicy,
            IEnumerable<ModuleDesc> modulesWithMetadata,
            IEnumerable<ReflectableEntity<TypeDesc>> reflectableTypes,
            IEnumerable<ReflectableEntity<MethodDesc>> reflectableMethods,
            IEnumerable<ReflectableEntity<FieldDesc>> reflectableFields,
            IEnumerable<TypeDesc> ldtokenReferenceableTypes)
            : base(typeSystemContext, blockingPolicy, resourceBlockingPolicy, logFile, stackTracePolicy, invokeThunkGenerationPolicy)
        {
            _modulesWithMetadata = new List<ModuleDesc>(modulesWithMetadata);
            
            foreach (var refType in reflectableTypes)
            {
                _reflectableTypes.Add(refType.Entity, refType.Category);
            }

            foreach (var refMethod in reflectableMethods)
            {
                // Asking for description or runtime mapping for a member without asking
                // for the owning type would mean we can't actually satisfy the request.
                Debug.Assert((refMethod.Category & MetadataCategory.Description) == 0
                    || (_reflectableTypes[refMethod.Entity.OwningType] & MetadataCategory.Description) != 0);
                Debug.Assert((refMethod.Category & MetadataCategory.RuntimeMapping) == 0
                    || (_reflectableTypes[refMethod.Entity.OwningType] & MetadataCategory.RuntimeMapping) != 0);
                _reflectableMethods.Add(refMethod.Entity, refMethod.Category);
            }

            foreach (var refField in reflectableFields)
            {
                // Asking for description or runtime mapping for a member without asking
                // for the owning type would mean we can't actually satisfy the request.
                Debug.Assert((refField.Category & MetadataCategory.Description) == 0
                    || (_reflectableTypes[refField.Entity.OwningType] & MetadataCategory.Description) != 0);
                Debug.Assert((refField.Category & MetadataCategory.RuntimeMapping) == 0
                    || (_reflectableTypes[refField.Entity.OwningType] & MetadataCategory.RuntimeMapping) != 0);
                _reflectableFields.Add(refField.Entity, refField.Category);
            }

            _ldtokenReferenceableTypes = new HashSet<TypeDesc>(ldtokenReferenceableTypes);

#if DEBUG
            HashSet<ModuleDesc> moduleHash = new HashSet<ModuleDesc>(_modulesWithMetadata);
            foreach (var refType in reflectableTypes)
            {
                // The instantiated types need to agree on the Description bit with the definition.
                // GetMetadataCategory relies on that.
                Debug.Assert((GetMetadataCategory(refType.Entity.GetTypeDefinition()) & MetadataCategory.Description)
                    == (GetMetadataCategory(refType.Entity) & MetadataCategory.Description));

                Debug.Assert(!(refType.Entity is MetadataType) || moduleHash.Contains(((MetadataType)refType.Entity).Module));
            }

            foreach (var refMethod in reflectableMethods)
            {
                // The instantiated methods need to agree on the Description bit with the definition.
                // GetMetadataCategory relies on that.
                Debug.Assert((GetMetadataCategory(refMethod.Entity.GetTypicalMethodDefinition()) & MetadataCategory.Description)
                    == (GetMetadataCategory(refMethod.Entity) & MetadataCategory.Description));

                // Canonical form of the method needs to agree with the logical form
                Debug.Assert(GetMetadataCategory(refMethod.Entity) == GetMetadataCategory(refMethod.Entity.GetCanonMethodTarget(CanonicalFormKind.Specific)));
            }

            foreach (var refField in reflectableFields)
            {
                // The instantiated fields need to agree on the Description bit with the definition.
                // GetMetadataCategory relies on that.
                Debug.Assert((GetMetadataCategory(refField.Entity.GetTypicalFieldDefinition()) & MetadataCategory.Description)
                    == (GetMetadataCategory(refField.Entity) & MetadataCategory.Description));
            }
#endif
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return _modulesWithMetadata;
        }

        public override bool ShouldConsiderLdTokenReferenceAConstruction(TypeDesc type)
        {
            return _ldtokenReferenceableTypes.Contains(type);
        }

        protected override void ComputeMetadata(NodeFactory factory,
            out byte[] metadataBlob,
            out List<MetadataMapping<MetadataType>> typeMappings,
            out List<MetadataMapping<MethodDesc>> methodMappings,
            out List<MetadataMapping<FieldDesc>> fieldMappings,
            out List<MetadataMapping<MethodDesc>> stackTraceMapping)
        {
            ComputeMetadata(new Policy(_blockingPolicy, this), factory,
                out metadataBlob,
                out typeMappings,
                out methodMappings,
                out fieldMappings,
                out stackTraceMapping);
        }

        protected sealed override MetadataCategory GetMetadataCategory(MethodDesc method)
        {
            if (_reflectableMethods.TryGetValue(method, out MetadataCategory value))
                return value;
            return 0;
        }

        protected sealed override MetadataCategory GetMetadataCategory(TypeDesc type)
        {
            if (_reflectableTypes.TryGetValue(type, out MetadataCategory value))
                return value;
            return 0;
        }

        protected sealed override MetadataCategory GetMetadataCategory(FieldDesc field)
        {
            if (_reflectableFields.TryGetValue(field, out MetadataCategory value))
                return value;
            return 0;
        }

        protected override IEnumerable<FieldDesc> GetFieldsWithRuntimeMapping()
        {
            foreach (var pair in _reflectableFields)
            {
                if ((pair.Value & MetadataCategory.RuntimeMapping) != 0)
                    yield return pair.Key;
            }
        }

        void ICompilationRootProvider.AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            // We go over all the types and members that need a runtime artifact present in the
            // compiled executable and root it.

            const string reason = "Reflection";

            foreach (var pair in _reflectableTypes)
            {
                if ((pair.Value & MetadataCategory.RuntimeMapping) != 0)
                    rootProvider.AddCompilationRoot(pair.Key, reason);
            }

            foreach (var pair in _reflectableMethods)
            {
                if ((pair.Value & MetadataCategory.RuntimeMapping) != 0)
                {
                    MethodDesc method = pair.Key;

                    // We need to root virtual methods as if they were called virtually.
                    // This will possibly trigger the generation of other overrides too.
                    if (method.IsVirtual)
                        rootProvider.RootVirtualMethodForReflection(method, reason);

                    if (!method.IsAbstract)
                        rootProvider.AddCompilationRoot(pair.Key, reason);
                }
            }

            foreach (var pair in _reflectableFields)
            {
                if ((pair.Value & MetadataCategory.RuntimeMapping) != 0)
                {
                    FieldDesc field = pair.Key;

                    // We only care about static fields at this point. Instance fields don't need
                    // runtime artifacts generated in the image.
                    if (field.IsStatic && !field.IsLiteral)
                    {
                        if (field.IsThreadStatic)
                            rootProvider.RootThreadStaticBaseForType(field.OwningType, reason);
                        else if (field.HasGCStaticBase)
                            rootProvider.RootGCStaticBaseForType(field.OwningType, reason);
                        else
                            rootProvider.RootNonGCStaticBaseForType(field.OwningType, reason);
                    }
                }
            }
        }

        private struct Policy : IMetadataPolicy
        {
            private readonly MetadataBlockingPolicy _blockingPolicy;
            private readonly AnalysisBasedMetadataManager _parent;

            public Policy(MetadataBlockingPolicy blockingPolicy, 
                AnalysisBasedMetadataManager parent)
            {
                _blockingPolicy = blockingPolicy;
                _parent = parent;
            }

            public bool GeneratesMetadata(FieldDesc fieldDef)
            {
                return (_parent.GetMetadataCategory(fieldDef) & MetadataCategory.Description) != 0;
            }

            public bool GeneratesMetadata(MethodDesc methodDef)
            {
                return (_parent.GetMetadataCategory(methodDef) & MetadataCategory.Description) != 0;
            }

            public bool GeneratesMetadata(MetadataType typeDef)
            {
                return (_parent.GetMetadataCategory(typeDef) & MetadataCategory.Description) != 0;
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

    public struct ReflectableEntity<TEntity>
    {
        public readonly TEntity Entity;
        public readonly MetadataCategory Category;

        public ReflectableEntity(TEntity entity, MetadataCategory category)
        {
            Entity = entity;
            Category = category;
        }
    }
}
