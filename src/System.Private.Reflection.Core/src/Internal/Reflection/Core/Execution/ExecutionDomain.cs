// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

                RuntimeTypeInfo result;
                Exception typeLoadException = assemblyQualifiedTypeName.TryResolve(this, defaultAssembly, ignoreCase, out result);
                if (typeLoadException == null)
                    return result.CastToType();
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
            RuntimeTypeInfo contextTypeInfo = declaringTypeHandle.GetTypeForRuntimeTypeHandle();
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
                    RuntimeTypeInfo[] genericTypeArguments = new RuntimeTypeInfo[genericMethodTypeArgumentHandles.Length];
                    for (int i = 0; i < genericMethodTypeArgumentHandles.Length; i++)
                    {
                        genericTypeArguments[i] = genericMethodTypeArgumentHandles[i].GetTypeForRuntimeTypeHandle();
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
            if (!attributeType.IsRuntimeImplemented())
                throw new InvalidOperationException();
            RuntimeTypeInfo runtimeAttributeType = attributeType.CastToRuntimeTypeInfo();
            return new RuntimePseudoCustomAttributeData(runtimeAttributeType, constructorArguments, namedArguments);
        }

        //=======================================================================================
        // This group of methods jointly service the Type.GetTypeFromHandle() path. The caller
        // is responsible for analyzing the RuntimeTypeHandle to figure out which flavor to call.
        //=======================================================================================
        public Type GetNamedTypeForHandle(RuntimeTypeHandle typeHandle, bool isGenericTypeDefinition)
        {
            MetadataReader reader;
            TypeDefinitionHandle typeDefinitionHandle;
            if (ExecutionEnvironment.TryGetMetadataForNamedType(typeHandle, out reader, out typeDefinitionHandle))
            {
                return typeDefinitionHandle.GetNamedType(reader, typeHandle).AsType();
            }
            else
            {
                if (ExecutionEnvironment.IsReflectionBlocked(typeHandle))
                {
                    return RuntimeBlockedTypeInfo.GetRuntimeBlockedTypeInfo(typeHandle, isGenericTypeDefinition).AsType();
                }
                else
                {
                    return RuntimeNoMetadataNamedTypeInfo.GetRuntimeNoMetadataNamedTypeInfo(typeHandle, isGenericTypeDefinition).AsType();
                }
            }
        }

        public Type GetArrayTypeForHandle(RuntimeTypeHandle typeHandle)
        {
            RuntimeTypeHandle elementTypeHandle;
            if (!ExecutionEnvironment.TryGetArrayTypeElementType(typeHandle, out elementTypeHandle))
                throw CreateMissingMetadataException((Type)null);

            return elementTypeHandle.GetTypeForRuntimeTypeHandle().GetArrayType(typeHandle).AsType();
        }

        public Type GetMdArrayTypeForHandle(RuntimeTypeHandle typeHandle, int rank)
        {
            RuntimeTypeHandle elementTypeHandle;
            if (!ExecutionEnvironment.TryGetMultiDimArrayTypeElementType(typeHandle, rank, out elementTypeHandle))
                throw CreateMissingMetadataException((Type)null);

            return elementTypeHandle.GetTypeForRuntimeTypeHandle().GetMultiDimArrayType(rank, typeHandle).AsType();
        }

        public Type GetPointerTypeForHandle(RuntimeTypeHandle typeHandle)
        {
            RuntimeTypeHandle targetTypeHandle;
            if (!ExecutionEnvironment.TryGetPointerTypeTargetType(typeHandle, out targetTypeHandle))
                throw CreateMissingMetadataException((Type)null);

            return targetTypeHandle.GetTypeForRuntimeTypeHandle().GetPointerType(typeHandle).AsType();
        }

        public Type GetConstructedGenericTypeForHandle(RuntimeTypeHandle typeHandle)
        {
            RuntimeTypeHandle genericTypeDefinitionHandle;
            RuntimeTypeHandle[] genericTypeArgumentHandles;
            if (!ExecutionEnvironment.TryGetConstructedGenericTypeComponents(typeHandle, out genericTypeDefinitionHandle, out genericTypeArgumentHandles))
                throw CreateMissingMetadataException((Type)null);

            RuntimeTypeInfo genericTypeDefinition = genericTypeDefinitionHandle.GetTypeForRuntimeTypeHandle();
            int count = genericTypeArgumentHandles.Length;
            RuntimeTypeInfo[] genericTypeArguments = new RuntimeTypeInfo[count];
            for (int i = 0; i < count; i++)
            {
                genericTypeArguments[i] = genericTypeArgumentHandles[i].GetTypeForRuntimeTypeHandle();
            }
            return genericTypeDefinition.GetConstructedGenericType(genericTypeArguments, typeHandle).AsType();
        }

        //=======================================================================================
        // Miscellaneous.
        //=======================================================================================
        public RuntimeTypeHandle GetTypeHandleIfAvailable(Type type)
        {
            if (!type.IsRuntimeImplemented())
                return default(RuntimeTypeHandle);

            RuntimeTypeInfo runtimeType = type.CastToRuntimeTypeInfo();
            if (runtimeType == null)
                return default(RuntimeTypeHandle);
            return runtimeType.InternalTypeHandleIfAvailable;
        }

        public bool SupportsReflection(Type type)
        {
            if (!type.IsRuntimeImplemented())
                return false;

            RuntimeTypeInfo runtimeType = type.CastToRuntimeTypeInfo();
            if (null == runtimeType.InternalNameIfAvailable)
                return false;

            if (ExecutionEnvironment.IsReflectionBlocked(type.TypeHandle))
            {
                // The type is an internal framework type and is blocked from reflection
                return false;
            }

            if (runtimeType.InternalFullNameOfAssembly == Internal.Runtime.Augments.RuntimeAugments.HiddenScopeAssemblyName)
            {
                // The type is an internal framework type but is reflectable for internal class library use
                // where we make the type appear in a hidden assembly
                return false;
            }

            return true;
        }

        internal ExecutionEnvironment ExecutionEnvironment { get; private set; }
    }
}
