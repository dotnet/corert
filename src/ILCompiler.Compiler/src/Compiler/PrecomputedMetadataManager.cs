// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Internal.Compiler;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.Metadata.NativeFormat.Writer;

using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;
using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;

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
            public Dictionary<MetadataType, int> WeakReflectedTypeMappings = new Dictionary<MetadataType, int>();
            public Dictionary<MetadataType, int> AllTypeMappings = new Dictionary<MetadataType, int>();
            public Dictionary<MethodDesc, int> MethodMappings = new Dictionary<MethodDesc, int>();
            public Dictionary<FieldDesc, int> FieldMappings = new Dictionary<FieldDesc, int>();
            public HashSet<MethodDesc> DynamicInvokeCompiledMethods = new HashSet<MethodDesc>();
            public HashSet<TypeDesc> RequiredGenericTypes = new HashSet<TypeDesc>();
            public HashSet<MethodDesc> RequiredGenericMethods = new HashSet<MethodDesc>();
            public HashSet<FieldDesc> RequiredGenericFields = new HashSet<FieldDesc>();
            public HashSet<TypeDesc> RequiredTemplateTypes = new HashSet<TypeDesc>();
            public HashSet<MethodDesc> RequiredTemplateMethods = new HashSet<MethodDesc>();
            public HashSet<FieldDesc> RequiredTemplateFields = new HashSet<FieldDesc>();
        }

        private readonly ModuleDesc _metadataDescribingModule;
        private readonly HashSet<ModuleDesc> _compilationModules;
        private readonly HashSet<ModuleDesc> _metadataOnlyAssemblies;
        private readonly Lazy<MetadataLoadedInfo> _loadedMetadata;
        private Lazy<Dictionary<MethodDesc, MethodDesc>> _dynamicInvokeStubs;
        private readonly byte[] _metadataBlob;
        private readonly StackTraceEmissionPolicy _stackTraceEmissionPolicy;
        private byte[] _stackTraceBlob;
        private readonly CompilationModuleGroup _compilationModuleGroup;

        public PrecomputedMetadataManager(
            CompilationModuleGroup group, 
            CompilerTypeSystemContext typeSystemContext, 
            ModuleDesc metadataDescribingModule,
            IEnumerable<ModuleDesc> compilationModules,
            IEnumerable<ModuleDesc> inputMetadataOnlyAssemblies,
            byte[] metadataBlob,
            StackTraceEmissionPolicy stackTraceEmissionPolicy,
            ManifestResourceBlockingPolicy resourceBlockingPolicy,
            bool disableInvokeThunks)
            : base(typeSystemContext, new AttributeSpecifiedBlockingPolicy(), resourceBlockingPolicy,
                  disableInvokeThunks ? (DynamicInvokeThunkGenerationPolicy)new NoDynamicInvokeThunkGenerationPolicy() : new PrecomputedDynamicInvokeThunkGenerationPolicy())
        {
            // Need to do this dance because C# won't let us access `this` in the `base()` expression above. Sigh.
            (_dynamicInvokeThunkGenerationPolicy as PrecomputedDynamicInvokeThunkGenerationPolicy)?.SetParentWorkaround(this);

            _compilationModuleGroup = group;
            _metadataDescribingModule = metadataDescribingModule;
            _compilationModules = new HashSet<ModuleDesc>(compilationModules);
            _metadataOnlyAssemblies = new HashSet<ModuleDesc>(inputMetadataOnlyAssemblies);
            _loadedMetadata = new Lazy<MetadataLoadedInfo>(LoadMetadata);
            _dynamicInvokeStubs = new Lazy<Dictionary<MethodDesc, MethodDesc>>(LoadDynamicInvokeStubs);
            _metadataBlob = metadataBlob;
            _stackTraceEmissionPolicy = stackTraceEmissionPolicy;
        }

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            base.AddToReadyToRunHeader(header, nodeFactory, commonFixupsTableNode);

            var stackTraceEmbeddedMetadataNode = new StackTraceEmbeddedMetadataNode();
            header.Add(BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdStackTraceEmbeddedMetadata), stackTraceEmbeddedMetadataNode, stackTraceEmbeddedMetadataNode, stackTraceEmbeddedMetadataNode.EndSymbol);
        }

        /// <summary>
        /// Read a method that describes the type system to metadata mappings.
        /// </summary>
        private void ReadMetadataMethod(MethodIL methodOfMappings, Dictionary<MetadataType, int> typeMappings, Dictionary<MethodDesc, int> methodMappings, Dictionary<FieldDesc, int> fieldMappings, HashSet<ModuleDesc> metadataModules)
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
            bool needSecondPass = false;
            Dictionary<MethodDesc, long> openMethodToInstantiationCount = new Dictionary<MethodDesc, long>();

            // structure is 
            // REPEAT N TIMES    
            //ldtoken generic type/method/field
            //pop
            while (true)
            {
                if (il.TryReadRet()) // ret
                    break;

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

                    if (genericMethod.Instantiation.CheckValidInstantiationArguments() &&
                       genericMethod.OwningType.Instantiation.CheckValidInstantiationArguments() &&
                       genericMethod.CheckConstraints())
                    {
                        // If we encounter a large number of instantiations of the same generic method, add the universal generic form 
                        // and stop adding further instantiations over the same generic method definition
                        if (genericMethod.HasInstantiation || genericMethod.OwningType.HasInstantiation)
                        {
                            MethodDesc openMethod = genericMethod.GetTypicalMethodDefinition();
                            long count;
                            if (openMethodToInstantiationCount.TryGetValue(openMethod, out count))
                            {
                                openMethodToInstantiationCount[openMethod] = count + 1;
                            }
                            else
                            {
                                openMethodToInstantiationCount.Add(openMethod, 1);
                            }

                            needSecondPass = true;
                        }   
                        else
                        {
                            yield return tse;
                        }
                    }
                }
            }

            if (needSecondPass)
            {
                ILStreamReader ilpass2 = new ILStreamReader(method);

                while (true)
                {
                    if (ilpass2.TryReadRet()) 
                        yield break;

                    TypeSystemEntity tse;
                    ilpass2.TryReadLdtokenAsTypeSystemEntity(out tse);
                    ilpass2.ReadPop();

                    if (tse == null)
                        throw new BadImageFormatException();

                    if (tse is MethodDesc)
                    {
                        MethodDesc genericMethod = (MethodDesc)tse;

                        if (genericMethod.Instantiation.CheckValidInstantiationArguments() &&
                           genericMethod.OwningType.Instantiation.CheckValidInstantiationArguments() &&
                           genericMethod.CheckConstraints())
                        {
                            // If we encounter a large number of instantiations of the same generic method, add the universal generic form 
                            // and stop adding further instantiations over the same generic method definition
                            if (genericMethod.HasInstantiation || genericMethod.OwningType.HasInstantiation)
                            {
                                MethodDesc openMethod = genericMethod.GetTypicalMethodDefinition();
                                long count;
                                bool found = openMethodToInstantiationCount.TryGetValue(openMethod, out count);
                                Debug.Assert(found);

                                // We have 2 heuristics, one for GVMs and one for normal methods that happen to have generics
                                bool isGVM = genericMethod.IsVirtual && genericMethod.HasInstantiation;
                                long heuristicCount = isGVM ? _typeSystemContext.GenericsConfig.UniversalCanonGVMReflectionRootHeuristic_InstantiationCount :
                                                           _typeSystemContext.GenericsConfig.UniversalCanonReflectionMethodRootHeuristic_InstantiationCount;

                                if (count >= heuristicCount)
                                {
                                    // We've hit the threshold of instantiations so add the USG form
                                    tse = genericMethod.GetCanonMethodTarget(CanonicalFormKind.Universal);

                                    // Set the instantiation count to -1 as a sentinel value
                                    openMethodToInstantiationCount[openMethod] = -1;
                                }
                                else if (count == -1)
                                {
                                    // Previously we added the USG form to _SpecifiedGenericMethods, now just skip
                                    continue;
                                }

                                yield return tse;
                            }
                        }
                    }
                }
            }            
        }

        private Instantiation GetUniversalCanonicalInstantiation(int numArgs)
        {
            TypeDesc[] args = new TypeDesc[numArgs];
            for (int i = 0; i < numArgs; i++)
                args[i] = _typeSystemContext.UniversalCanonType;
            return new Instantiation(args);
        }

        private void ReadRequiredTemplates(MethodIL methodIL, HashSet<TypeDesc> typeTemplates, HashSet<MethodDesc> methodTemplates, HashSet<FieldDesc> fieldTemplates)
        {
            ILStreamReader il = new ILStreamReader(methodIL);

            if (!_typeSystemContext.SupportsUniversalCanon)
                return;

            //
            // Types, methods and field tokens listed here are *open* generic definitions determined by the reducer as needing reflection.
            // Type tokens listed are tokens of generic type definitions that will need reflection support.
            // Method tokens listed are either tokens of generic method definitions, or non-generic methods on generic containing type definitions, and need reflection support.
            // Field tokens listed are for fields on generic type definitions that will need reflection support
            // The canonical form supported by the typesystem context will determine how to instantiate the types/methods/fields listed here, and templatize them for 
            // reflection support at runtime (templates used by the dynamic TypeLoader component of the runtime).
            //

            // structure is 
            // REPEAT N TIMES    
            //ldtoken type
            //pop
            while (true)
            {
                if (il.TryReadRet()) // ret
                    break;

                TypeSystemEntity tse;
                il.TryReadLdtokenAsTypeSystemEntity(out tse);
                il.ReadPop();

                if (tse == null)
                    throw new BadImageFormatException();

                if (tse is MetadataType)
                {
                    MetadataType type = (MetadataType)tse;
                    Debug.Assert(type.IsGenericDefinition && type.HasInstantiation);

                    type = _typeSystemContext.GetInstantiatedType(type, GetUniversalCanonicalInstantiation(type.Instantiation.Length));

                    typeTemplates.Add(type);
                }
                else if (tse is MethodDesc)
                {
                    MethodDesc method = (MethodDesc)tse;
                    TypeDesc containingType = method.OwningType;

                    Debug.Assert(method.IsTypicalMethodDefinition);
                    Debug.Assert(method.HasInstantiation || method.OwningType.HasInstantiation);

                    if (containingType.HasInstantiation)
                    {
                        containingType = _typeSystemContext.GetInstantiatedType((MetadataType)containingType, GetUniversalCanonicalInstantiation(containingType.Instantiation.Length));
                        method = containingType.GetMethod(method.Name, method.GetTypicalMethodDefinition().Signature);
                        typeTemplates.Add(containingType);
                    }

                    if (method.HasInstantiation)
                    {
                        method = _typeSystemContext.GetInstantiatedMethod(method, GetUniversalCanonicalInstantiation(method.Instantiation.Length));
                    }

                    methodTemplates.Add(method);
                }
                else if (tse is FieldDesc)
                {
                    FieldDesc field = (FieldDesc)tse;
                    TypeDesc containingType = field.OwningType;

                    Debug.Assert(containingType.HasInstantiation && containingType.IsGenericDefinition);

                    containingType = _typeSystemContext.GetInstantiatedType((MetadataType)containingType, GetUniversalCanonicalInstantiation(containingType.Instantiation.Length));
                    field = containingType.GetField(field.Name);

                    typeTemplates.Add(containingType);
                    fieldTemplates.Add(field);
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
            MethodDesc requiredTemplatesMethod = typeWithMetadataMappings.GetMethod("CompilerDeterminedInstantiations", null);

            ILProvider ilProvider = new ILProvider(null);

            MetadataLoadedInfo result = new MetadataLoadedInfo();

            if (fullMetadataMethod != null)
            {
                MethodIL fullMethodIL = ilProvider.GetMethodIL(fullMetadataMethod);
                ReadMetadataMethod(fullMethodIL, result.AllTypeMappings, result.MethodMappings, result.FieldMappings, metadataModules);
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
                ReadMetadataMethod(weakMethodIL, result.WeakReflectedTypeMappings, weakMethodMappings, weakFieldMappings, metadataModules);
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

            if (requiredTemplatesMethod != null)
            {
                ReadRequiredTemplates(ilProvider.GetMethodIL(requiredTemplatesMethod),
                    result.RequiredTemplateTypes,
                    result.RequiredTemplateMethods,
                    result.RequiredTemplateFields);
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
            return _loadedMetadata.Value.LocalMetadataModules.Union(_metadataOnlyAssemblies);
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

        protected override void GetRuntimeMappingDependenciesDueToReflectability(ref DependencyNodeCore<NodeFactory>.DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            // Backwards compatible behavior with when this code was indiscriminately injected into all EETypes.
            // We might want to tweak this.

            if (type is MetadataType metadataType && !type.IsGenericDefinition)
            {
                Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Any));

                // For instantiated types, we write the static fields offsets directly into the table, and we do not reference the gc/non-gc statics nodes
                if (!type.HasInstantiation)
                {
                    if (metadataType.GCStaticFieldSize.AsInt > 0)
                    {
                        dependencies.Add(factory.TypeGCStaticsSymbol(metadataType), "GC statics for ReflectionFieldMap entry");
                    }

                    if (metadataType.NonGCStaticFieldSize.AsInt > 0)
                    {
                        dependencies.Add(factory.TypeNonGCStaticsSymbol(metadataType), "Non-GC statics for ReflectionFieldMap entry");
                    }
                }

                if (metadataType.ThreadGcStaticFieldSize.AsInt > 0)
                {
                    dependencies.Add(((UtcNodeFactory)factory).TypeThreadStaticsOffsetSymbol(metadataType), "Thread statics for ReflectionFieldMap entry");
                }
            }
        }

        private bool IsMethodSupportedInPrecomputedReflection(MethodDesc method)
        {
            if (!IsMethodSupportedInReflectionInvoke(method))
                return false;

            MethodDesc typicalInvokeTarget = method.GetTypicalMethodDefinition();
            MethodDesc typicalDynamicInvokeStub;

            return _dynamicInvokeStubs.Value.TryGetValue(typicalInvokeTarget, out typicalDynamicInvokeStub);
        }

        void ICompilationRootProvider.AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            MetadataLoadedInfo loadedMetadata = _loadedMetadata.Value;

            // Add all non-generic reflectable types as roots.
            foreach (var type in loadedMetadata.TypesWithStrongMetadataMappings)
            {
                rootProvider.AddCompilationRoot(type, "Required non-generic type");
            }

            // Add all non-generic reflectable methods as roots.
            // Virtual methods need special handling (e.g. with dependency tracking) since they can be abstract.
            foreach (var method in loadedMetadata.MethodMappings.Keys)
            {
                if (method.HasInstantiation || method.OwningType.HasInstantiation)
                    continue;

                if (!IsMethodSupportedInPrecomputedReflection(method))
                    continue;

                if (method.IsVirtual)
                    rootProvider.RootVirtualMethodForReflection(method, "Reflection root");
                else
                {
                    if (method.IsConstructor)
                    {
                        rootProvider.AddCompilationRoot(method.OwningType, "Type for method reflection root");
                    }

                    rootProvider.AddCompilationRoot(method, "Reflection root");
                }
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
                if (!IsMethodSupportedInPrecomputedReflection(method))
                    continue;

                if (method.IsVirtual)
                    rootProvider.RootVirtualMethodForReflection(method, "Required generic method");

                if (!method.IsAbstract)
                {
                    if (method.IsConstructor)
                    {
                        rootProvider.AddCompilationRoot(method.OwningType, "Type for method required generic method");
                    }

                    rootProvider.AddCompilationRoot(method, "Required generic method");
                }
            }

            foreach (var field in loadedMetadata.RequiredGenericFields)
            {
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

            foreach (var type in loadedMetadata.RequiredTemplateTypes)
            {
                Debug.Assert(type.IsCanonicalSubtype(CanonicalFormKind.Any));
                rootProvider.AddCompilationRoot(type, "Compiler determined template");
            }

            foreach (var method in loadedMetadata.RequiredTemplateMethods)
            {
                Debug.Assert(method.IsCanonicalMethod(CanonicalFormKind.Any));
                if (method.IsVirtual)
                    rootProvider.RootVirtualMethodForReflection(method, "Compiler determined template");

                if (!method.IsAbstract)
                {
                    if (method.IsConstructor)
                    {
                        rootProvider.AddCompilationRoot(method.OwningType, "Type for method compiler determined template method");
                    }

                    rootProvider.AddCompilationRoot(method, "Compiler determined template");
                }
            }
        }

        protected override void ComputeMetadata(NodeFactory factory,
                                                out byte[] metadataBlob, 
                                                out List<MetadataMapping<MetadataType>> typeMappings,
                                                out List<MetadataMapping<MethodDesc>> methodMappings,
                                                out List<MetadataMapping<FieldDesc>> fieldMappings,
                                                out List<MetadataMapping<MethodDesc>> stackTraceMapping)
        {
            MetadataLoadedInfo loadedMetadata = _loadedMetadata.Value;
            metadataBlob = _metadataBlob;

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();

            Dictionary<MethodDesc, MethodDesc> canonicalToSpecificMethods = new Dictionary<MethodDesc, MethodDesc>();

            HashSet<FieldDesc> canonicalFieldsAddedToMap = new HashSet<FieldDesc>();
            HashSet<MethodDesc> canonicalMethodsAddedToMap = new HashSet<MethodDesc>();

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
                if (loadedMetadata.AllTypeMappings.TryGetValue(definition, out token) || loadedMetadata.WeakReflectedTypeMappings.TryGetValue(definition, out token))
                {
                    typeMappings.Add(new MetadataMapping<MetadataType>(definition, token));
                }
            }

            // Mappings for all compiled methods
            foreach (var method in GetCompiledMethods())
            {
                AddMethodMapping(factory, method, canonicalMethodsAddedToMap, canonicalToSpecificMethods, methodMappings);
            }

            // Mappings for reflectable abstract non-generic methods (methods with compiled bodies are handled above)
            foreach (var method in loadedMetadata.MethodMappings.Keys)
            {
                if (!method.IsAbstract)
                    continue;

                if (method.HasInstantiation || method.OwningType.HasInstantiation)
                    continue;

                AddMethodMapping(factory, method, canonicalMethodsAddedToMap, canonicalToSpecificMethods, methodMappings);
            }

            foreach (var eetypeGenerated in GetTypesWithEETypes())
            {
                if (eetypeGenerated.IsGenericDefinition)
                    continue;

                foreach (FieldDesc field in eetypeGenerated.GetFields())
                    AddFieldMapping(field, canonicalFieldsAddedToMap, fieldMappings);
            }

            foreach (var typeMapping in loadedMetadata.WeakReflectedTypeMappings)
            {
                // Imported types that are also declared as weak reflected types need to be added to the TypeMap table, but only if they are also
                // reachable from static compilation (node marked in the dependency analysis graph)
                if (factory.CompilationModuleGroup.ShouldReferenceThroughImportTable(typeMapping.Key) && factory.NecessaryTypeSymbol(typeMapping.Key).Marked)
                    typeMappings.Add(new MetadataMapping<MetadataType>(typeMapping.Key, typeMapping.Value));
            }

            stackTraceMapping = GenerateStackTraceMetadata(factory);
        }

        private void AddFieldMapping(FieldDesc field, HashSet<FieldDesc> canonicalFieldsAddedToMap, List<MetadataMapping<FieldDesc>> fieldMappings)
        {
            if (field.OwningType.HasInstantiation)
            {
                // Not all fields of generic types are reflectable.
                if (!_loadedMetadata.Value.RequiredGenericFields.Contains(field) && !_loadedMetadata.Value.RequiredTemplateFields.Contains(field))
                    return;

                // Collapsing of field map entries based on canonicalization, to avoid redundant equivalent entries
                FieldDesc canonicalField = field.OwningType.ConvertToCanonForm(CanonicalFormKind.Specific).GetField(field.Name);
                if (!canonicalFieldsAddedToMap.Add(canonicalField))
                    return;
            }

            int token;
            if (_loadedMetadata.Value.FieldMappings.TryGetValue(field.GetTypicalFieldDefinition(), out token))
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

        private void AddMethodMapping(NodeFactory factory, MethodDesc method, HashSet<MethodDesc> canonicalMethodsAddedToMap, Dictionary<MethodDesc, MethodDesc> canonicalToSpecificMethods, List<MetadataMapping<MethodDesc>> methodMappings)
        {
            if (!MethodCanBeInvokedViaReflection(factory, method))
                return;

            if (!IsReflectionInvokable(method))
                return;

            MethodDesc canonicalMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);

            if (method.HasInstantiation || method.OwningType.HasInstantiation)
            {
                // Not all generic methods or methods on generic types are reflectable.
                // TODO: This might cause issues with delegate reverse lookups, especially in incremental compilation mode.
                // Delegate reverse lookups depend on the existance of entries in the InvokeMap table, for methods that were not necessarily 
                // considered to be reflectable by the DR.            
                if (!_loadedMetadata.Value.RequiredGenericMethods.Contains(method) && !_loadedMetadata.Value.RequiredTemplateMethods.Contains(method))
                    return;

                // Collapsing of invoke map entries based on canonicalization
                if (!canonicalMethodsAddedToMap.Add(canonicalMethod))
                    return;
            }

            int token;
            if (_loadedMetadata.Value.MethodMappings.TryGetValue(method.GetTypicalMethodDefinition(), out token))
            {
                MethodDesc invokeMapMethod = GetInvokeMapMethodForMethod(canonicalToSpecificMethods, method);

                if (invokeMapMethod != null)
                    methodMappings.Add(new MetadataMapping<MethodDesc>(invokeMapMethod, token));
            }
            else if (!WillUseMetadataTokenToReferenceMethod(method) && _compilationModuleGroup.ContainsMethodBody(canonicalMethod, false))
            {
                MethodDesc invokeMapMethod = GetInvokeMapMethodForMethod(canonicalToSpecificMethods, method);

                // For methods on types that are not in the current module, assume they must be reflectable
                // and generate a non-metadata backed invoke table entry
                // TODO, the above computation is overly generous with the set of methods that are placed into the method invoke map
                // It includes methods which are not reflectable at all.
                if (invokeMapMethod != null)
                    methodMappings.Add(new MetadataMapping<MethodDesc>(invokeMapMethod, 0));
            }
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

        private List<MetadataMapping<MethodDesc>> GenerateStackTraceMetadata(NodeFactory factory)
        {
            var transformed = MetadataTransform.Run(new NoDefinitionMetadataPolicy(), Array.Empty<ModuleDesc>());
            MetadataTransform transform = transformed.Transform;

            // Generate metadata blob
            var writer = new MetadataWriter();

            // Only emit stack trace metadata for those methods which don't have reflection metadata
            HashSet<MethodDesc> methodInvokeMap = new HashSet<MethodDesc>();
            foreach (var mappingEntry in GetMethodMapping(factory))
            {
                var method = mappingEntry.Entity;
                if (ShouldMethodBeInInvokeMap(method))
                    methodInvokeMap.Add(method);
            }

            // Generate entries in the blob for methods that will be necessary for stack trace purposes.
            var stackTraceRecords = new List<KeyValuePair<MethodDesc, MetadataRecord>>();
            foreach (var methodBody in GetCompiledMethodBodies())
            {
                NonExternMethodSymbolNode methodNode = methodBody as NonExternMethodSymbolNode;
                if (methodNode != null && !methodNode.HasCompiledBody)
                    continue;

                MethodDesc method = methodBody.Method;

                if (methodInvokeMap.Contains(method))
                    continue;

                if (!_stackTraceEmissionPolicy.ShouldIncludeMethod(method))
                    continue;

                MetadataRecord record = CreateStackTraceRecord(transform, method);

                stackTraceRecords.Add(new KeyValuePair<MethodDesc, MetadataRecord>(
                    method,
                    record));

                writer.AdditionalRootRecords.Add(record);
            }

            var ms = new MemoryStream();
            writer.Write(ms);

            _stackTraceBlob = ms.ToArray();

            var result = new List<MetadataMapping<MethodDesc>>();

            // Generate stack trace metadata mapping
            foreach (var stackTraceRecord in stackTraceRecords)
            {
                result.Add(new MetadataMapping<MethodDesc>(stackTraceRecord.Key, writer.GetRecordHandle(stackTraceRecord.Value)));
            }

            return result;
        }

        public byte[] GetStackTraceBlob(NodeFactory factory)
        {
            EnsureMetadataGenerated(factory);
            return _stackTraceBlob;
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

        private struct NoDefinitionMetadataPolicy : IMetadataPolicy
        {
            public bool GeneratesMetadata(FieldDesc fieldDef) => false;
            public bool GeneratesMetadata(MethodDesc methodDef) => false;
            public bool GeneratesMetadata(MetadataType typeDef) => false;
            public bool IsBlocked(MetadataType typeDef) => false;
            public bool IsBlocked(MethodDesc methodDef) => false;
            public ModuleDesc GetModuleOfType(MetadataType typeDef) => typeDef.Module;
        }

        private sealed class PrecomputedDynamicInvokeThunkGenerationPolicy : DynamicInvokeThunkGenerationPolicy
        {
            private PrecomputedMetadataManager _parent;

            public PrecomputedDynamicInvokeThunkGenerationPolicy()
            {
            }

            public void SetParentWorkaround(PrecomputedMetadataManager parent)
            {
                _parent = parent;
            }

            public override bool HasStaticInvokeThunk(MethodDesc method)
            {
                if (!ProjectNDependencyBehavior.EnableFullAnalysis)
                {
                    if (method.IsCanonicalMethod(CanonicalFormKind.Any))
                        return false;
                }
                else
                {
                    if (method.IsCanonicalMethod(CanonicalFormKind.Universal))
                        return false;
                }

                MethodDesc reflectionInvokeStub = _parent.GetCanonicalReflectionInvokeStub(method);

                if (reflectionInvokeStub == null)
                    return false;

                // TODO: Generate DynamicInvokeTemplateMap dependencies correctly. For now, force all canonical stubs to go through the 
                // calling convention converter interpreter path.
                if (reflectionInvokeStub.IsSharedByGenericInstantiations)
                    return false;

                return true;
            }
        }
    }
}
