// Licensed to the .NET Foundation under one or more agreements.
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

        public CompilerGeneratedMetadataManager(NodeFactory factory) : base(factory)
        {
            _metadataPolicy = new GeneratedTypesAndCodeMetadataPolicy(this);
        }

        private HashSet<MetadataType> _typeDefinitionsGenerated = new HashSet<MetadataType>();
        private HashSet<MethodDesc> _methodDefinitionsGenerated = new HashSet<MethodDesc>();
        private HashSet<ModuleDesc> _modulesSeen = new HashSet<ModuleDesc>();
        private Dictionary<DynamicInvokeMethodSignature, MethodDesc> _dynamicInvokeThunks = new Dictionary<DynamicInvokeMethodSignature, MethodDesc>();

        protected override void AddGeneratedType(TypeDesc type)
        {
            if (type.IsDefType && type.IsTypeDefinition)
            {
                var mdType = type as MetadataType;
                if (mdType != null)
                {
                    _modulesSeen.Add(mdType.Module);
                    _typeDefinitionsGenerated.Add(mdType);
                }
            }

            base.AddGeneratedType(type);
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return _modulesSeen;
        }

        protected override void AddGeneratedMethod(MethodDesc method)
        {
            _methodDefinitionsGenerated.Add(method.GetTypicalMethodDefinition());
            base.AddGeneratedMethod(method);
        }

        public override bool IsReflectionBlocked(MetadataType type)
        {
            return _metadataPolicy.IsBlocked(type);
        }

        protected override void ComputeMetadata(out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings)
        {
            var transformed = MetadataTransform.Run(new GeneratedTypesAndCodeMetadataPolicy(this), _modulesSeen);

            // TODO: DeveloperExperienceMode: Use transformed.Transform.HandleType() to generate
            //       TypeReference records for _typeDefinitionsGenerated that don't have metadata.
            //       (To be used in MissingMetadataException messages)

            // Generate metadata blob
            var writer = new MetadataWriter();
            writer.ScopeDefinitions.AddRange(transformed.Scopes);
            var ms = new MemoryStream();
            writer.Write(ms);
            metadataBlob = ms.ToArray();

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();

            // Generate type definition mappings
            foreach (var definition in _typeDefinitionsGenerated)
            {
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
        public override bool HasReflectionInvokeStub(MethodDesc method)
        {
            return true;
        }

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public override MethodDesc GetReflectionInvokeStub(MethodDesc method)
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
                thunk = new DynamicInvokeMethodThunk(NodeFactory.CompilationModuleGroup.GeneratedAssembly.GetGlobalModuleType(), lookupSig);
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

        private struct GeneratedTypesAndCodeMetadataPolicy : IMetadataPolicy
        {
            private CompilerGeneratedMetadataManager _parent;
            private ExplicitScopeAssemblyPolicyMixin _explicitScopeMixin;

            public GeneratedTypesAndCodeMetadataPolicy(CompilerGeneratedMetadataManager parent)
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
}
