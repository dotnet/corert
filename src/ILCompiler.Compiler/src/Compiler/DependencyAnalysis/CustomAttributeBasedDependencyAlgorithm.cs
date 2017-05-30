﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Computes the list of dependencies that are necessary to generate metadata for a custom attribute, but also the dependencies to
    /// make the custom attributes usable by the reflection stack at runtime.
    /// </summary>
    internal class CustomAttributeBasedDependencyAlgorithm
    {
        public static void AddDependenciesDueToCustomAttributes(ref DependencyList dependencies, NodeFactory factory, EcmaMethod method)
        {
            MetadataReader reader = method.MetadataReader;
            MethodDefinition methodDef = reader.GetMethodDefinition(method.Handle);
            AddDependenciesDueToCustomAttributes(ref dependencies, factory, method.Module, methodDef.GetCustomAttributes());
        }

        public static void AddDependenciesDueToCustomAttributes(ref DependencyList dependencies, NodeFactory factory, EcmaType type)
        {
            TypeDefinition typeDef = type.MetadataReader.GetTypeDefinition(type.Handle);
            AddDependenciesDueToCustomAttributes(ref dependencies, factory, type.EcmaModule, typeDef.GetCustomAttributes());
        }

        public static void AddDependenciesDueToCustomAttributes(ref DependencyList dependencies, NodeFactory factory, EcmaField field)
        {
            FieldDefinition fieldDef = field.MetadataReader.GetFieldDefinition(field.Handle);
            AddDependenciesDueToCustomAttributes(ref dependencies, factory, field.Module, fieldDef.GetCustomAttributes());
        }

        public static void AddDependenciesDueToCustomAttributes(ref DependencyList dependencies, NodeFactory factory, EcmaAssembly assembly)
        {
            AssemblyDefinition asmDef = assembly.MetadataReader.GetAssemblyDefinition();
            AddDependenciesDueToCustomAttributes(ref dependencies, factory, assembly, asmDef.GetCustomAttributes());

            // This is rather awkward because ModuleDefinition doesn't offer means to get to the custom attributes
            CustomAttributeHandleCollection moduleAttributes =
                assembly.MetadataReader.GetCustomAttributes(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(0x1));
            AddDependenciesDueToCustomAttributes(ref dependencies, factory, assembly, moduleAttributes);
        }

        private static void AddDependenciesDueToCustomAttributes(ref DependencyList dependencies, NodeFactory factory, EcmaModule module, CustomAttributeHandleCollection attributeHandles)
        {
            MetadataReader reader = module.MetadataReader;
            MetadataManager mdManager = factory.MetadataManager;
            var attributeTypeProvider = new CustomAttributeTypeProvider(module);


            foreach (CustomAttributeHandle caHandle in attributeHandles)
            {
                CustomAttribute attribute = reader.GetCustomAttribute(caHandle);

                try
                {
                    MethodDesc constructor = module.GetMethod(attribute.Constructor);
                    if (mdManager.IsReflectionBlocked(constructor))
                        continue;

                    // Make a new list in case we need to abort.
                    var caDependencies = new DependencyList();

                    caDependencies.Add(factory.CanonicalEntrypoint(constructor), "Attribute constructor");
                    caDependencies.Add(factory.ConstructedTypeSymbol(constructor.OwningType), "Attribute type");

                    CustomAttributeValue<TypeDesc> decodedValue = attribute.DecodeValue(attributeTypeProvider);

                    if (AddDependenciesFromCustomAttributeBlob(caDependencies, factory, constructor.OwningType, decodedValue))
                    {
                        dependencies = dependencies ?? new DependencyList();
                        dependencies.AddRange(caDependencies);
                    }
                }
                catch (TypeSystemException)
                {
                    // We could end up seeing an exception here for a multitude of reasons:
                    // * Attribute ctor doesn't resolve
                    // * There's a typeof() that refers to something that can't be loaded
                    // * Attribute refers to a non-existing field
                    // * Etc.
                    //
                    // If we really wanted to, we could probably come up with a way to still make this
                    // work with the same failure modes at runtime as the CLR, but it might not be
                    // worth the hassle: the input was invalid. The most important thing is that we
                    // don't crash the compilation.
                }
            }
        }

        private static bool AddDependenciesFromCustomAttributeBlob(DependencyList dependencies, NodeFactory factory, TypeDesc attributeType, CustomAttributeValue<TypeDesc> value)
        {
            MetadataManager mdManager = factory.MetadataManager;

            foreach (CustomAttributeTypedArgument<TypeDesc> decodedArgument in value.FixedArguments)
            {
                if (!AddDependenciesFromCustomAttributeArgument(dependencies, factory, decodedArgument.Type, decodedArgument.Value))
                    return false;
            }

            foreach (CustomAttributeNamedArgument<TypeDesc> decodedArgument in value.NamedArguments)
            {
                if (decodedArgument.Kind == CustomAttributeNamedArgumentKind.Field)
                {
                    // This is an instance field. We don't track them right now.
                }
                else
                {
                    Debug.Assert(decodedArgument.Kind == CustomAttributeNamedArgumentKind.Property);

                    // Reflection will need to reflection-invoke the setter at runtime.
                    if (!AddDependenciesFromPropertySetter(dependencies, factory, attributeType, decodedArgument.Name))
                        return false;
                }

                if (!AddDependenciesFromCustomAttributeArgument(dependencies, factory, decodedArgument.Type, decodedArgument.Value))
                    return false;
            }

            return true;
        }

        private static bool AddDependenciesFromPropertySetter(DependencyList dependencies, NodeFactory factory, TypeDesc attributeType, string propertyName)
        {
            EcmaType attributeTypeDefinition = (EcmaType)attributeType.GetTypeDefinition();

            MetadataReader reader = attributeTypeDefinition.MetadataReader;
            var typeDefinition = reader.GetTypeDefinition(attributeTypeDefinition.Handle);

            foreach (PropertyDefinitionHandle propDefHandle in typeDefinition.GetProperties())
            {
                PropertyDefinition propDef = reader.GetPropertyDefinition(propDefHandle);
                if (reader.StringComparer.Equals(propDef.Name, propertyName))
                {
                    PropertyAccessors accessors = propDef.GetAccessors();

                    if (!accessors.Setter.IsNil)
                    {
                        MethodDesc setterMethod = (MethodDesc)attributeTypeDefinition.EcmaModule.GetObject(accessors.Setter);
                        if (factory.MetadataManager.IsReflectionBlocked(setterMethod))
                            return false;

                        // Method on a generic attribute
                        if (attributeType != attributeTypeDefinition)
                        {
                            setterMethod = factory.TypeSystemContext.GetMethodForInstantiatedType(setterMethod, (InstantiatedType)attributeType);
                        }

                        // TODO: what if the setter is virtual/abstract?
                        dependencies.Add(factory.CanonicalEntrypoint(setterMethod), "Custom attribute blob");
                    }

                    return true;
                }
            }

            // Haven't found it in current type. Check the base type.
            TypeDesc baseType = attributeType.BaseType;

            if (baseType != null)
                return AddDependenciesFromPropertySetter(dependencies, factory, baseType, propertyName);

            // Not found. This is bad metadata that will result in a runtime failure, but we shouldn't fail the compilation.
            return true;
        }

        private static bool AddDependenciesFromCustomAttributeArgument(DependencyList dependencies, NodeFactory factory, TypeDesc type, object value)
        {
            if (type.UnderlyingType.IsPrimitive || type.IsString || value == null)
                return true;

            if (type.IsSzArray)
            {
                TypeDesc elementType = ((ArrayType)type).ElementType;
                if (elementType.UnderlyingType.IsPrimitive || elementType.IsString)
                    return true;

                foreach (CustomAttributeTypedArgument<TypeDesc> arrayElement in (ImmutableArray<CustomAttributeTypedArgument<TypeDesc>>)value)
                {
                    if (!AddDependenciesFromCustomAttributeArgument(dependencies, factory, arrayElement.Type, arrayElement.Value))
                        return false;
                }

                return true;
            }

            // typeof() should be the only remaining option.

            Debug.Assert(value is TypeDesc);

            TypeDesc typeofType = (TypeDesc)value;

            if (factory.MetadataManager.IsReflectionBlocked(typeofType))
                return false;

            TypeMetadataNode.GetMetadataDependencies(ref dependencies, factory, typeofType, "Custom attribute blob");

            return true;
        }
    }
}
