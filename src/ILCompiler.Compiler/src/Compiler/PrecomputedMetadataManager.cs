// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;

using Internal.Compiler;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    class PrecomputedMetadataManager : MetadataManager, ICompilationRootProvider
    {
        private const string MetadataMappingTypeName = "_$ILCT$+$ILT$ReflectionMapping$";

        class MetadataLoadedInfo
        {
            public ImmutableArray<ModuleDesc> MetadataModules = ImmutableArray<ModuleDesc>.Empty;
            public ImmutableArray<ModuleDesc> LocalMetadataModules = ImmutableArray<ModuleDesc>.Empty;
            public ImmutableArray<ModuleDesc> ExternalMetadataModules = ImmutableArray<ModuleDesc>.Empty;
            public List<MetadataType> TypesWithStrongMetadataMappings = new List<MetadataType>();
            public Dictionary<MetadataType, int> AllTypeMappings = new Dictionary<MetadataType, int>();
            public Dictionary<MethodDesc, int> MethodMappings = new Dictionary<MethodDesc, int>();
            public Dictionary<FieldDesc, int> FieldMappings = new Dictionary<FieldDesc, int>();
            public HashSet<MethodDesc> DynamicInvokeCompiledMethods = new HashSet<MethodDesc>();
            public HashSet<TypeDesc> RequiredGenericTypes = new HashSet<TypeDesc>();
            public HashSet<MethodDesc> RequiredGenericMethods = new HashSet<MethodDesc>();
            public HashSet<FieldDesc> RequiredGenericFields = new HashSet<FieldDesc>();
        }

        private readonly ModuleDesc _metadataDescribingModule;
        private readonly HashSet<ModuleDesc> _compilationModules;
        private readonly Lazy<MetadataLoadedInfo> _loadedMetadata;
        private Lazy<Dictionary<MethodDesc, MethodDesc>> _dynamicInvokeStubs;
        private readonly byte[] _metadataBlob;

        public PrecomputedMetadataManager(CompilationModuleGroup group, CompilerTypeSystemContext typeSystemContext, ModuleDesc metadataDescribingModule, IEnumerable<ModuleDesc> compilationModules, byte[] metadataBlob)
            : base(group, typeSystemContext, new AttributeSpecifiedBlockingPolicy())
        {
            _metadataDescribingModule = metadataDescribingModule;
            _compilationModules = new HashSet<ModuleDesc>(compilationModules);
            _loadedMetadata = new Lazy<MetadataLoadedInfo>(LoadMetadata);
            _dynamicInvokeStubs = new Lazy<Dictionary<MethodDesc, MethodDesc>>(LoadDynamicInvokeStubs);
            _metadataBlob = metadataBlob;
        }

        /// <summary>
        /// Read a method that describes the type system to metadata mappings.
        /// </summary>
        private void ReadMetadataMethod(MethodIL methodOfMappings, ref Dictionary<MetadataType, int> typeMappings, ref Dictionary<MethodDesc, int> methodMappings, ref Dictionary<FieldDesc, int> fieldMappings, ref HashSet<ModuleDesc> metadataModules)
        {
            ILStreamReader il = new ILStreamReader(methodOfMappings);
            // structure is 
            // REPEAT N TIMES    
            // ldtoken type/method/field OR ldc.i4 0. If ldtoken instruction is replaced with a ldc.i4, skip this entry
            // ldc.i4 metadata value
            // pop
            // pop
            while (true)
            {
                if (il.TryReadRet()) // ret
                    break;

                TypeSystemEntity tse;
                if (il.TryReadLdtokenAsTypeSystemEntity(out tse))
                {
                    int metadataTokenValue = il.ReadLdcI4();
                    il.ReadPop();
                    il.ReadPop();

                    if (tse is MetadataType)
                    {
                        MetadataType type = (MetadataType)tse;
                        metadataModules.Add(type.Module);
                        typeMappings.Add(type, metadataTokenValue);
                    }
                    else if (tse is MethodDesc)
                    {
                        MethodDesc method = (MethodDesc)tse;
                        // Finalizers are called via a field on the EEType. They should not be reflectable.
                        if (!method.IsFinalizer)
                        {
                            metadataModules.Add(((MetadataType)method.OwningType).Module);
                            methodMappings.Add(method, metadataTokenValue);
                        }
                    }
                    else if (tse is FieldDesc)
                    {
                        FieldDesc field = (FieldDesc)tse;
                        metadataModules.Add(((MetadataType)field.OwningType).Module);
                        fieldMappings.Add(field, metadataTokenValue);
                    }
                }
                else
                {
                    int deadLdtokenSignifier = il.ReadLdcI4();
                    Debug.Assert(deadLdtokenSignifier == 0);
                    il.ReadLdcI4();
                    il.ReadPop();
                    il.ReadPop();
                }
            }
        }

        private IEnumerable<TypeSystemEntity> ReadRequiredGenericsEntities(MethodIL method)
        {
            ILStreamReader il = new ILStreamReader(method);
            // structure is 
            // REPEAT N TIMES    
            //ldtoken generic type/method/field
            //pop
            while (true)
            {
                if (il.TryReadRet()) // ret
                    yield break;

                TypeSystemEntity tse;
                il.TryReadLdtokenAsTypeSystemEntity(out tse);
                il.ReadPop();

                if (tse == null)
                    throw new BadImageFormatException();

                if (tse is TypeDesc)
                {
                    if (tse is DefType)
                    {
                        if (((TypeDesc)tse).Instantiation.CheckValidInstantiationArguments() && ((TypeDesc)tse).CheckConstraints())
                            yield return tse;
                    }
                    else if (tse is ByRefType)
                    {
                        // Skip by ref types.
                    }
                    else
                        throw new BadImageFormatException();
                }
                else if (tse is FieldDesc)
                {
                    TypeDesc owningType = ((FieldDesc)tse).OwningType;

                    if (owningType.Instantiation.CheckValidInstantiationArguments() && owningType.CheckConstraints())
                        yield return tse;
                }
                else if (tse is MethodDesc)
                {
                    MethodDesc genericMethod = (MethodDesc)tse;

                    if(genericMethod.Instantiation.CheckValidInstantiationArguments() &&
                       genericMethod.OwningType.Instantiation.CheckValidInstantiationArguments() &&
                       genericMethod.CheckConstraints())
                    {
                        // TODO: Detect large number of instantiations of the same method and collapse to using dynamic 
                        // USG instantiations at runtime, to avoid infinite generic expansion and large compilation times.
                        yield return tse;
                    }
                }
            }
        }

        public static bool ModuleHasMetadataMappings(ModuleDesc metadataDescribingModule)
        {
            return metadataDescribingModule.GetTypeByCustomAttributeTypeName(MetadataMappingTypeName, throwIfNotFound: false) != null;
        }

        private MetadataLoadedInfo LoadMetadata()
        {
            HashSet<ModuleDesc> metadataModules = new HashSet<ModuleDesc>();
            MetadataType typeWithMetadataMappings = (MetadataType)_metadataDescribingModule.GetTypeByCustomAttributeTypeName(MetadataMappingTypeName);

            MethodDesc fullMetadataMethod = typeWithMetadataMappings.GetMethod("Metadata", null);
            MethodDesc weakMetadataMethod = typeWithMetadataMappings.GetMethod("WeakMetadata", null);
            MethodDesc requiredGenericTypesMethod = typeWithMetadataMappings.GetMethod("RequiredGenericTypes", null);
            MethodDesc requiredGenericMethodsMethod = typeWithMetadataMappings.GetMethod("RequiredGenericMethods", null);
            MethodDesc requiredGenericFieldsMethod = typeWithMetadataMappings.GetMethod("RequiredGenericFields", null);

            ILProvider ilProvider = new ILProvider(null);

            MetadataLoadedInfo result = new MetadataLoadedInfo();

            if (fullMetadataMethod != null)
            {
                MethodIL fullMethodIL = ilProvider.GetMethodIL(fullMetadataMethod);
                ReadMetadataMethod(fullMethodIL, ref result.AllTypeMappings, ref result.MethodMappings, ref result.FieldMappings, ref metadataModules);
                foreach (var mapping in result.AllTypeMappings)
                {
                    result.TypesWithStrongMetadataMappings.Add(mapping.Key);
                }
            }

            if (weakMetadataMethod != null)
            {
                MethodIL weakMethodIL = ilProvider.GetMethodIL(weakMetadataMethod);
                Dictionary<MethodDesc, int> weakMethodMappings = new Dictionary<MethodDesc, int>();
                Dictionary<FieldDesc, int> weakFieldMappings = new Dictionary<FieldDesc, int>();
                ReadMetadataMethod(weakMethodIL, ref result.AllTypeMappings, ref weakMethodMappings, ref weakFieldMappings, ref metadataModules);
                if ((weakMethodMappings.Count > 0) || (weakFieldMappings.Count > 0))
                {
                    // the format does not permit weak field/method mappings
                    throw new BadImageFormatException();
                }
            }

            if (requiredGenericTypesMethod != null)
            {
                foreach (var type in ReadRequiredGenericsEntities(ilProvider.GetMethodIL(requiredGenericTypesMethod)))
                {
                    Debug.Assert(type is DefType);
                    result.RequiredGenericTypes.Add((TypeDesc)type);
                }
            }

            if (requiredGenericMethodsMethod != null)
            {
                foreach (var method in ReadRequiredGenericsEntities(ilProvider.GetMethodIL(requiredGenericMethodsMethod)))
                    result.RequiredGenericMethods.Add((MethodDesc)method);
            }

            if (requiredGenericFieldsMethod != null)
            {
                foreach (var field in ReadRequiredGenericsEntities(ilProvider.GetMethodIL(requiredGenericFieldsMethod)))
                    result.RequiredGenericFields.Add((FieldDesc)field);
            }

            result.MetadataModules = ImmutableArray.CreateRange(metadataModules);

            ImmutableArray<ModuleDesc>.Builder externalMetadataModulesBuilder = ImmutableArray.CreateBuilder<ModuleDesc>();
            ImmutableArray<ModuleDesc>.Builder localMetadataModulesBuilder = ImmutableArray.CreateBuilder<ModuleDesc>();
            foreach (ModuleDesc module in result.MetadataModules)
            {
                if (!_compilationModules.Contains(module))
                    externalMetadataModulesBuilder.Add(module);
                else
                    localMetadataModulesBuilder.Add(module);
            }
            result.ExternalMetadataModules = externalMetadataModulesBuilder.ToImmutable();
            result.LocalMetadataModules = localMetadataModulesBuilder.ToImmutable();

            foreach (var pair in result.MethodMappings)
            {
                MethodDesc reflectableMethod = pair.Key;

                if (reflectableMethod.HasInstantiation)
                    continue;

                if (reflectableMethod.OwningType.HasInstantiation)
                    continue;

                MethodDesc typicalDynamicInvokeStub;
                if (!_dynamicInvokeStubs.Value.TryGetValue(reflectableMethod, out typicalDynamicInvokeStub))
                    continue;

                MethodDesc instantiatiatedDynamicInvokeStub = InstantiateCanonicalDynamicInvokeMethodForMethod(typicalDynamicInvokeStub, reflectableMethod);
                result.DynamicInvokeCompiledMethods.Add(instantiatiatedDynamicInvokeStub);
            }

            foreach (var reflectableMethod in result.RequiredGenericMethods)
            {
                MethodDesc typicalDynamicInvokeStub;
                if (!_dynamicInvokeStubs.Value.TryGetValue(reflectableMethod.GetTypicalMethodDefinition(), out typicalDynamicInvokeStub))
                    continue;

                MethodDesc instantiatiatedDynamicInvokeStub = InstantiateCanonicalDynamicInvokeMethodForMethod(typicalDynamicInvokeStub, reflectableMethod);
                result.DynamicInvokeCompiledMethods.Add(instantiatiatedDynamicInvokeStub);
            }

            return result;
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return _loadedMetadata.Value.LocalMetadataModules;
        }

        public override bool WillUseMetadataTokenToReferenceMethod(MethodDesc method)
        {
            return _compilationModuleGroup.ContainsType(method.GetTypicalMethodDefinition().OwningType);
        }

        public override bool WillUseMetadataTokenToReferenceField(FieldDesc field)
        {
            return _compilationModuleGroup.ContainsType(field.GetTypicalFieldDefinition().OwningType);
        }

        protected override MetadataCategory GetMetadataCategory(FieldDesc field)
        {
            // Backwards compatible behavior. We might want to tweak this.
            return MetadataCategory.RuntimeMapping;
        }

        protected override MetadataCategory GetMetadataCategory(MethodDesc method)
        {
            // Backwards compatible behavior. We might want to tweak this.
            return MetadataCategory.RuntimeMapping;
        }

        protected override MetadataCategory GetMetadataCategory(TypeDesc type)
        {
            // Backwards compatible behavior. We might want to tweak this.
            return MetadataCategory.RuntimeMapping;
        }

        void ICompilationRootProvider.AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            MetadataLoadedInfo loadedMetadata = _loadedMetadata.Value;

            // Add all non-generic reflectable methods as roots.
            // Virtual methods need special handling (e.g. with dependency tracking) since they can be abstract.
            foreach (var method in loadedMetadata.MethodMappings.Keys)
            {
                if (method.HasInstantiation || method.OwningType.HasInstantiation)
                    continue;

                if (method.IsVirtual)
                    rootProvider.RootVirtualMethodForReflection(method, "Reflection root");
                else
                    rootProvider.AddCompilationRoot(method, "Reflection root");
            }

            // Root all the generic type instantiations from the pre-computed metadata
            foreach (var type in loadedMetadata.RequiredGenericTypes)
            {
                rootProvider.AddCompilationRoot(type, "Required generic type");
            }

            // Root all the generic methods (either non-generic methods on generic types, or generic methods) from 
            // the pre-computed metadata.
            // Virtual methods need special handling (e.g. with dependency tracking) since they can be abstract.
            foreach (var method in loadedMetadata.RequiredGenericMethods)
            {
                if (method.IsVirtual)
                    rootProvider.RootVirtualMethodForReflection(method, "Required generic method");
                else
                    rootProvider.AddCompilationRoot(method, "Required generic method");
            }

            foreach (var field in loadedMetadata.RequiredGenericFields)
            {
                // TODO: Create metadata mappings only for reflectable fields, not all fields of all compiled types
                rootProvider.AddCompilationRoot(field.OwningType, "Required generic field's owning type");
                if (field.IsThreadStatic)
                {
                    rootProvider.RootThreadStaticBaseForType(field.OwningType, "Required generic field");
                }
                else if (field.IsStatic)
                {
                    if (field.HasGCStaticBase)
                        rootProvider.RootGCStaticBaseForType(field.OwningType, "Required generic field");
                    else
                        rootProvider.RootNonGCStaticBaseForType(field.OwningType, "Required generic field");
                }
            }
        }

        protected override void ComputeMetadata(NodeFactory factory, out byte[] metadataBlob, out List<MetadataMapping<MetadataType>> typeMappings, out List<MetadataMapping<MethodDesc>> methodMappings, out List<MetadataMapping<FieldDesc>> fieldMappings)
        {
            MetadataLoadedInfo loadedMetadata = _loadedMetadata.Value;
            metadataBlob = _metadataBlob;

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();

            MetadataMapping<MethodDesc> newMapping;

            Dictionary<MethodDesc, MethodDesc> canonicalToSpecificMethods = new Dictionary<MethodDesc, MethodDesc>();
            // The handling of generic methods which are implemented by canonical code is interesting, the invoke map
            // needs to have a specific instantiation for each canonical bit of code.
            foreach (GenericDictionaryNode dictionaryNode in GetCompiledGenericDictionaries())
            {
                MethodGenericDictionaryNode methodDictionary = dictionaryNode as MethodGenericDictionaryNode;
                if (methodDictionary == null)
                    continue;

                MethodDesc method = methodDictionary.OwningMethod;
                Debug.Assert(method.HasInstantiation && !method.IsCanonicalMethod(CanonicalFormKind.Any));

                MethodDesc canonicalMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);

                if (canonicalToSpecificMethods.ContainsKey(canonicalMethod))
                {
                    // We only need to record 1 specific to canonical method mapping
                    continue;
                }

                canonicalToSpecificMethods.Add(canonicalMethod, method);
            }

            // Generate type definition mappings
            foreach (var type in GetTypesWithEETypes())
            {
                MetadataType definition = type.IsTypeDefinition ? type as MetadataType : null;
                if (definition == null)
                    continue;

                int token;
                if (loadedMetadata.AllTypeMappings.TryGetValue(definition, out token))
                {
                    typeMappings.Add(new MetadataMapping<MetadataType>(definition, token));
                }
            }

            // Mappings for all compiled methods
            foreach (var method in GetCompiledMethods())
            {
                if (GetMethodMappingIfExists(factory, method, canonicalToSpecificMethods, out newMapping))
                    methodMappings.Add(newMapping);
            }

            // Mappings for reflectable abstract non-generic methods (methods with compiled bodies are handled above)
            foreach (var method in loadedMetadata.MethodMappings.Keys)
            {
                if (!method.IsAbstract)
                    continue;

                if (method.HasInstantiation || method.OwningType.HasInstantiation)
                    continue;

                if (GetMethodMappingIfExists(factory, method, canonicalToSpecificMethods, out newMapping))
                    methodMappings.Add(newMapping);
            }

            // Mappings for reflectable abstract generic methods (methods with compiled bodies are handled above)
            foreach (var method in loadedMetadata.RequiredGenericMethods)
            {
                if (!method.IsAbstract)
                    continue;

                // If there is a possible canonical method, use that instead of a specific method (folds canonically equivalent methods away)
                MethodDesc canonicalMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);

                if (!canonicalToSpecificMethods.ContainsKey(canonicalMethod))
                    canonicalToSpecificMethods.Add(canonicalMethod, method);

                if (GetMethodMappingIfExists(factory, canonicalMethod, canonicalToSpecificMethods, out newMapping))
                    methodMappings.Add(newMapping);
            }

            // TODO: Create metadata mappings only for reflectable fields, not all fields of all compiled types
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
                    int token;
                    if (loadedMetadata.FieldMappings.TryGetValue(field.GetTypicalFieldDefinition(), out token))
                    {
                        fieldMappings.Add(new MetadataMapping<FieldDesc>(field, token));
                    }
                    else if (!WillUseMetadataTokenToReferenceField(field))
                    {
                        // TODO, the above computation is overly generous with the set of fields that are placed into the field invoke map
                        // It includes fields which are not reflectable at all, and collapses static fields across generics

                        // TODO! enable this. Disabled due to cross module import of statics is not yet implemented
                        // fieldMappings.Add(new MetadataMapping<FieldDesc>(field, 0));
                    }
                }
            }
        }

        private bool GetMethodMappingIfExists(NodeFactory factory, MethodDesc method, Dictionary<MethodDesc, MethodDesc> canonicalToSpecificMethods, out MetadataMapping<MethodDesc> mapping)
        {
            mapping = default(MetadataMapping<MethodDesc>);

            if (!MethodCanBeInvokedViaReflection(factory, method))
                return false;

            // If there is a possible canonical method, use that instead of a specific method (folds canonically equivalent methods away)
            if (method.GetCanonMethodTarget(CanonicalFormKind.Specific) != method)
                return false;

            if (!IsReflectionInvokable(method))
                return false;

            int token;
            if (_loadedMetadata.Value.MethodMappings.TryGetValue(method.GetTypicalMethodDefinition(), out token))
            {
                MethodDesc invokeMapMethod = GetInvokeMapMethodForMethod(canonicalToSpecificMethods, method);

                if (invokeMapMethod != null)
                {
                    mapping = new MetadataMapping<MethodDesc>(invokeMapMethod, token);
                    return true;
                }
            }
            else if (!WillUseMetadataTokenToReferenceMethod(method) &&
                _compilationModuleGroup.ContainsMethodBody(method.GetCanonMethodTarget(CanonicalFormKind.Specific)))
            {
                MethodDesc invokeMapMethod = GetInvokeMapMethodForMethod(canonicalToSpecificMethods, method);

                // For methods on types that are not in the current module, assume they must be reflectable
                // and generate a non-metadata backed invoke table entry
                // TODO, the above computation is overly generous with the set of methods that are placed into the method invoke map
                // It includes methods which are not reflectable at all.
                if (invokeMapMethod != null)
                {
                    mapping = new MetadataMapping<MethodDesc>(invokeMapMethod, 0);
                    return true;
                }
            }

            return false;
        }

        private bool MethodCanBeInvokedViaReflection(NodeFactory factory, MethodDesc method)
        {
            // Non-generic instance canonical methods on generic structures are only available in the invoke map
            // if the unboxing stub entrypoint is marked already (which will mean that the unboxing stub
            // has been compiled, On ProjectN abi, this may will not be triggered by the CodeBasedDependencyAlgorithm.
            // See the ProjectN abi specific code in there.
            if (!method.HasInstantiation && method.OwningType.IsValueType && !method.Signature.IsStatic)
            {
                MethodDesc canonicalMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);

                if (canonicalMethod.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any))
                {
                    if (!factory.MethodEntrypoint(canonicalMethod, true).Marked)
                        return false;
                }
            }
            return true;
        }

        private MethodDesc GetInvokeMapMethodForMethod(Dictionary<MethodDesc, MethodDesc> canonicalToSpecificMethods, MethodDesc method)
        {
            MethodDesc invokeMapMethod = method;
            if (method.HasInstantiation && method.IsCanonicalMethod(CanonicalFormKind.Specific))
            {
                // Under optimization when a generic method makes no use of its generic dictionary
                // the compiler can sometimes generate code for a canonical generic
                // method, but not detect the need for a generic dictionary. We cannot currently
                // represent this state in our invoke mapping tables, and must skip emitting a record into them
                if (!canonicalToSpecificMethods.ContainsKey(method))
                    return null;

                invokeMapMethod = canonicalToSpecificMethods[method];
            }

            return invokeMapMethod;
        }

        private Dictionary<MethodDesc, MethodDesc> LoadDynamicInvokeStubs()
        {
            MetadataType typeWithMetadataMappings = (MetadataType)_metadataDescribingModule.GetTypeByCustomAttributeTypeName(MetadataMappingTypeName);
            Dictionary<MethodDesc, MethodDesc> dynamicInvokeMapTable = new Dictionary<MethodDesc, MethodDesc>();
            MethodDesc dynamicInvokeStubDescriptorMethod = typeWithMetadataMappings.GetMethod("DynamicInvokeStubs", null);

            if (dynamicInvokeStubDescriptorMethod == null)
                return dynamicInvokeMapTable;

            ILProvider ilProvider = new ILProvider(null);
            ILStreamReader il = new ILStreamReader(ilProvider.GetMethodIL(dynamicInvokeStubDescriptorMethod));
            // structure is 
            // REPEAT N TIMES    
            //ldtoken method
            //ldtoken dynamicInvokeStubMethod
            //pop
            //pop
            while (true)
            {
                if (il.TryReadRet()) // ret
                    break;

                MethodDesc method = il.ReadLdtokenAsTypeSystemEntity() as MethodDesc;
                MethodDesc dynamicMethodInvokeStub = il.ReadLdtokenAsTypeSystemEntity() as MethodDesc;
                il.ReadPop();
                il.ReadPop();

                if ((method != null) && (dynamicMethodInvokeStub != null))
                {
                    dynamicInvokeMapTable[method] = dynamicMethodInvokeStub;
                }
                else
                {
                    throw new BadImageFormatException();
                }
            }

            return dynamicInvokeMapTable;
        }

        /// <summary>
        /// Is there a reflection invoke stub for a method that is invokable?
        /// </summary>
        public override bool HasReflectionInvokeStubForInvokableMethod(MethodDesc method)
        {
            Debug.Assert(IsReflectionInvokable(method));

            if (method.IsCanonicalMethod(CanonicalFormKind.Any))
                return false;

            MethodDesc reflectionInvokeStub = GetCanonicalReflectionInvokeStub(method);

            if (reflectionInvokeStub == null)
                return false;

            // TODO: Generate DynamicInvokeTemplateMap dependencies correctly. For now, force all canonical stubs to go through the 
            // calling convention converter interpreter path.
            if (reflectionInvokeStub.IsSharedByGenericInstantiations)
                return false;

            return true;
        }


        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public override MethodDesc GetCanonicalReflectionInvokeStub(MethodDesc method)
        {
            MethodDesc typicalInvokeTarget = method.GetTypicalMethodDefinition();
            MethodDesc typicalDynamicInvokeStub;

            if (!_dynamicInvokeStubs.Value.TryGetValue(typicalInvokeTarget, out typicalDynamicInvokeStub))
                return null;

            MethodDesc dynamicInvokeStubCanonicalized = InstantiateCanonicalDynamicInvokeMethodForMethod(typicalDynamicInvokeStub, method);

            if (dynamicInvokeStubCanonicalized == null || !_loadedMetadata.Value.DynamicInvokeCompiledMethods.Contains(dynamicInvokeStubCanonicalized))
                return null;

            return dynamicInvokeStubCanonicalized;
        }

        private sealed class AttributeSpecifiedBlockingPolicy : MetadataBlockingPolicy
        {
            public override bool IsBlocked(MetadataType type)
            {
                Debug.Assert(type.IsTypeDefinition);
                return type.HasCustomAttribute("System.Runtime.CompilerServices", "ReflectionBlockedAttribute");
            }

            public override bool IsBlocked(MethodDesc method)
            {
                Debug.Assert(method.IsTypicalMethodDefinition);
                // TODO: we might need to do something here if we keep this policy.
                return false;
            }

            public override bool IsBlocked(FieldDesc field)
            {
                Debug.Assert(field.IsTypicalFieldDefinition);
                // TODO: we might need to do something here if we keep this policy.
                return false;
            }
        }
    }
}
