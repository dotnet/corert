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
    class PrecomputedMetadataManager : MetadataManager
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
        }

        private readonly HashSet<MetadataType> _typeDefinitionsGenerated = new HashSet<MetadataType>();
        private readonly ModuleDesc _metadataDescribingModule;
        private readonly HashSet<ModuleDesc> _compilationModules;
        private readonly Lazy<MetadataLoadedInfo> _loadedMetadata;
        private Lazy<Dictionary<MethodDesc, MethodDesc>> _dynamicInvokeStubs;
        private readonly byte[] _metadataBlob;

        public PrecomputedMetadataManager(CompilationModuleGroup group, CompilerTypeSystemContext typeSystemContext, ModuleDesc metadataDescribingModule, IEnumerable<ModuleDesc> compilationModules, byte[] metadataBlob) : base(group, typeSystemContext)
        {
            _metadataDescribingModule = metadataDescribingModule;
            _compilationModules = new HashSet<ModuleDesc>(compilationModules);
            _loadedMetadata = new Lazy<MetadataLoadedInfo>(LoadMetadata);
            _dynamicInvokeStubs = new Lazy<Dictionary<MethodDesc, MethodDesc>>(LoadDynamicInvokeStubs);
            _metadataBlob = metadataBlob;
        }

        protected override void AddGeneratedType(TypeDesc type)
        {
            if (type.IsDefType && type.IsTypeDefinition)
            {
                var mdType = type as MetadataType;
                if (mdType != null)
                {
                    _typeDefinitionsGenerated.Add(mdType);
                }
            }

            base.AddGeneratedType(type);
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
                        metadataModules.Add(((MetadataType)method.OwningType).Module);
                        methodMappings.Add(method, metadataTokenValue);
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

            // TODO! Replace with something more complete that capture the generic instantiations that the pre-analysis
            // indicates should have been present
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

                MethodDesc instantiatiatedDynamicInvokeStub = InstantiateDynamicInvokeMethodForMethod(typicalDynamicInvokeStub, reflectableMethod);
                result.DynamicInvokeCompiledMethods.Add(instantiatiatedDynamicInvokeStub.GetCanonMethodTarget(CanonicalFormKind.Specific));
            }

            return result;
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return _loadedMetadata.Value.LocalMetadataModules;
        }

        public override bool IsReflectionBlocked(MetadataType type)
        {
            return type.HasCustomAttribute("System.Runtime.CompilerServices", "ReflectionBlockedAttribute");
        }

        protected override void ComputeMetadata(NodeFactory factory, out byte[] metadataBlob, out List<MetadataMapping<MetadataType>> typeMappings, out List<MetadataMapping<MethodDesc>> methodMappings, out List<MetadataMapping<FieldDesc>> fieldMappings)
        {
            MetadataLoadedInfo loadedMetadata = _loadedMetadata.Value;
            metadataBlob = _metadataBlob;

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();

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
            foreach (var definition in _typeDefinitionsGenerated)
            {
                int token;
                if (loadedMetadata.AllTypeMappings.TryGetValue(definition, out token))
                {
                    typeMappings.Add(new MetadataMapping<MetadataType>(definition, token));
                }
            }

            foreach (var method in GetCompiledMethods())
            {
                int token;
                if (loadedMetadata.MethodMappings.TryGetValue(method.GetTypicalMethodDefinition(), out token))
                {
                    MethodDesc invokeMapMethod = method;
                    if (method.HasInstantiation && method.IsCanonicalMethod(CanonicalFormKind.Specific))
                    {
                        Debug.Assert(canonicalToSpecificMethods.ContainsKey(method));

                        invokeMapMethod = canonicalToSpecificMethods[method];
                    }

                    // Non-generic instance canonical methods on generic structures are only available in the invoke map
                    // if the unboxing stub entrypoint is marked already (which will mean that the unboxing stub
                    // has been compiled, On ProjectN abi, this may will not be triggered by the CodeBasedDependencyAlgorithm.
                    // See the ProjectN abi specific code in there.
                    if (!method.HasInstantiation && method.OwningType.IsValueType && method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any) && !method.Signature.IsStatic)
                    {
                        if (!factory.MethodEntrypoint(method, true).Marked)
                            continue;
                    }

                    methodMappings.Add(new MetadataMapping<MethodDesc>(invokeMapMethod, token));
                }
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
                    int token;
                    if (loadedMetadata.FieldMappings.TryGetValue(field.GetTypicalFieldDefinition(), out token))
                    {
                        fieldMappings.Add(new MetadataMapping<FieldDesc>(field, token));
                    }
                }
            }
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
            if (method.IsCanonicalMethod(CanonicalFormKind.Any))
                return false;

            MethodDesc reflectionInvokeStub = GetReflectionInvokeStub(method);

            if (reflectionInvokeStub == null)
                return false;

            // TODO: Generate DynamicInvokeTemplateMap. For now, force all canonical stubs to go through the 
            // calling convention converter interpreter path.
            if (reflectionInvokeStub.GetCanonMethodTarget(CanonicalFormKind.Specific) != reflectionInvokeStub)
                return false;

            return true;
        }


        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public override MethodDesc GetReflectionInvokeStub(MethodDesc method)
        {
            MethodDesc typicalInvokeTarget = method.GetTypicalMethodDefinition();
            MethodDesc typicalDynamicInvokeStub;

            if (!_dynamicInvokeStubs.Value.TryGetValue(typicalInvokeTarget, out typicalDynamicInvokeStub))
                return null;

            MethodDesc dynamicInvokeStubIfItExists = InstantiateDynamicInvokeMethodForMethod(typicalDynamicInvokeStub, method);

            if (dynamicInvokeStubIfItExists == null)
                return null;

            MethodDesc dynamicInvokeStubCanonicalized = dynamicInvokeStubIfItExists.GetCanonMethodTarget(CanonicalFormKind.Specific);
            if (_loadedMetadata.Value.DynamicInvokeCompiledMethods.Contains(dynamicInvokeStubCanonicalized))
                return dynamicInvokeStubIfItExists;
            else
                return null;
        }
    }
}
