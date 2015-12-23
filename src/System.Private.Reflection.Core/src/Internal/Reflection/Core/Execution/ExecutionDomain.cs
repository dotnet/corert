// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.MethodInfos;
using global::System.Reflection.Runtime.TypeParsing;
using global::System.Reflection.Runtime.CustomAttributes;
using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

namespace Internal.Reflection.Core.Execution
{
    //
    // This singleton class implements the domain used for "execution" reflection objects, e.g. Types obtained from RuntimeTypeHandles.
    // This class is only instantiated on Project N, as the desktop uses IRC only for LMR.
    //
    public sealed class ExecutionDomain : ReflectionDomain
    {
        internal ExecutionDomain(ReflectionDomainSetup executionDomainSetup, ExecutionEnvironment executionEnvironment)
            : base(executionDomainSetup, 0)
        {
            this.ExecutionEnvironment = executionEnvironment;
        }

        //
        // Retrieves a type by name. Helper to implement Type.GetType();
        //
        public Type GetType(String typeName, bool throwOnError, bool ignoreCase, IEnumerable<String> defaultAssemblyNames)
        {
            if (typeName == null)
                throw new ArgumentNullException();

            if (typeName.Length == 0)
            {
                if (throwOnError)
                    throw new TypeLoadException(SR.Arg_TypeLoadNullStr);
                else
                    return null;
            }

            AssemblyQualifiedTypeName assemblyQualifiedTypeName;
            try
            {
                assemblyQualifiedTypeName = TypeParser.ParseAssemblyQualifiedTypeName(typeName);
            }
            catch (ArgumentException)
            {
                // Input string was a syntactically invalid type name.
                if (throwOnError)
                    throw;
                return null;
            }

            if (assemblyQualifiedTypeName.AssemblyName != null)
            {
                defaultAssemblyNames = new String[] { null };
            }

            Exception lastTypeLoadException = null;
            foreach (String assemblyName in defaultAssemblyNames)
            {
                RuntimeAssembly defaultAssembly;
                if (assemblyName == null)
                {
                    defaultAssembly = null;
                }
                else
                {
                    RuntimeAssemblyName runtimeAssemblyName = AssemblyNameParser.Parse(assemblyName);
                    Exception e = RuntimeAssembly.TryGetRuntimeAssembly(this, runtimeAssemblyName, out defaultAssembly);
                    if (e != null)
                        continue;   // A default assembly such as "System.Runtime" might not "exist" in an app that opts heavily out of pay-for-play metadata. Just go on to the next one.
                }

                RuntimeType result;
                Exception typeLoadException = assemblyQualifiedTypeName.TryResolve(this, defaultAssembly, ignoreCase, out result);
                if (typeLoadException == null)
                    return result;
                lastTypeLoadException = typeLoadException;
            }

            if (throwOnError)
            {
                if (lastTypeLoadException == null)
                    throw new TypeLoadException(SR.Format(SR.TypeLoad_TypeNotFoundByGetType, typeName));
                else
                    throw lastTypeLoadException;
            }
            else
            {
                return null;
            }
        }

        //
        // Retrieves the MethodBase for a given method handle. Helper to implement Delegate.GetMethodInfo()
        //
        public MethodBase GetMethod(RuntimeTypeHandle declaringTypeHandle, MethodHandle methodHandle, RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            RuntimeType declaringType = ReflectionCoreNonPortable.GetTypeForRuntimeTypeHandle(declaringTypeHandle);
            RuntimeTypeInfo contextTypeInfo = declaringType.GetRuntimeTypeInfo();
            RuntimeNamedTypeInfo definingTypeInfo = contextTypeInfo.AnchoringTypeDefinitionForDeclaredMembers;
            MetadataReader reader = definingTypeInfo.Reader;
            if (methodHandle.IsConstructor(reader))
            {
                return RuntimePlainConstructorInfo.GetRuntimePlainConstructorInfo(methodHandle, definingTypeInfo, contextTypeInfo);
            }
            else
            {
                RuntimeNamedMethodInfo runtimeNamedMethodInfo = RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(methodHandle, definingTypeInfo, contextTypeInfo);
                if (!runtimeNamedMethodInfo.IsGenericMethod)
                {
                    return runtimeNamedMethodInfo;
                }
                else
                {
                    RuntimeType[] genericTypeArguments = new RuntimeType[genericMethodTypeArgumentHandles.Length];
                    for (int i = 0; i < genericMethodTypeArgumentHandles.Length; i++)
                    {
                        genericTypeArguments[i] = ReflectionCoreNonPortable.GetTypeForRuntimeTypeHandle(genericMethodTypeArgumentHandles[i]);
                    }
                    return RuntimeConstructedGenericMethodInfo.GetRuntimeConstructedGenericMethodInfo(runtimeNamedMethodInfo, genericTypeArguments);
                }
            }
        }

        //
        // Get or create a CustomAttributeData object for a specific type and arguments. 
        //
        public CustomAttributeData GetCustomAttributeData(Type attributeType, IList<CustomAttributeTypedArgument> constructorArguments, IList<CustomAttributeNamedArgument> namedArguments)
        {
            RuntimeType runtimeAttributeType = attributeType as RuntimeType;
            if (runtimeAttributeType == null)
                throw new InvalidOperationException();
            return new RuntimePseudoCustomAttributeData(runtimeAttributeType, constructorArguments, namedArguments);
        }

        //=======================================================================================
        // Flotsam and jetsam.
        //=======================================================================================
        //
        // ShadowTypes are a trick to make Types based on RuntimeTypeHandles "light up" on Project N when metadata and reflection are present.
        // This is exposed on the execution domain only as it makes no sense for LMR types.
        //
        public Type CreateShadowRuntimeInspectionOnlyNamedTypeIfAvailable(RuntimeTypeHandle runtimeTypeHandle)
        {
            MetadataReader metadataReader;
            TypeDefinitionHandle typeDefinitionHandle;

            if (!ReflectionCoreExecution.ExecutionEnvironment.TryGetMetadataForNamedType(runtimeTypeHandle, out metadataReader, out typeDefinitionHandle))
                return null;
            return new ShadowRuntimeInspectionOnlyNamedType(metadataReader, typeDefinitionHandle);
        }

        internal ExecutionEnvironment ExecutionEnvironment { get; private set; }
    }
}
