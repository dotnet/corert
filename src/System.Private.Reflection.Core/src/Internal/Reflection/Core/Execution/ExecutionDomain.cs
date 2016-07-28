// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.TypeParsing;
using System.Reflection.Runtime.CustomAttributes;
using Internal.Metadata.NativeFormat;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

namespace Internal.Reflection.Core.Execution
{
    //
    // This singleton class acts as an entrypoint from System.Private.Reflection.Execution to System.Private.Reflection.Core.
    //
    public sealed class ExecutionDomain
    {
        internal ExecutionDomain(ReflectionDomainSetup executionDomainSetup, ExecutionEnvironment executionEnvironment)
        {
            ExecutionEnvironment = executionEnvironment;
            ReflectionDomainSetup = executionDomainSetup;
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
                    Exception e = RuntimeAssembly.TryGetRuntimeAssembly(runtimeAssemblyName, out defaultAssembly);
                    if (e != null)
                        continue;   // A default assembly such as "System.Runtime" might not "exist" in an app that opts heavily out of pay-for-play metadata. Just go on to the next one.
                }

                RuntimeTypeInfo result;
                Exception typeLoadException = assemblyQualifiedTypeName.TryResolve(defaultAssembly, ignoreCase, out result);
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
        // MissingMetadataExceptions.
        //=======================================================================================
        public Exception CreateMissingMetadataException(Type pertainant)
        {
            return this.ReflectionDomainSetup.CreateMissingMetadataException(pertainant);
        }

        public Exception CreateMissingMetadataException(TypeInfo pertainant)
        {
            return this.ReflectionDomainSetup.CreateMissingMetadataException(pertainant);
        }

        public Exception CreateMissingMetadataException(TypeInfo pertainant, string nestedTypeName)
        {
            return this.ReflectionDomainSetup.CreateMissingMetadataException(pertainant, nestedTypeName);
        }

        public Exception CreateNonInvokabilityException(MemberInfo pertainant)
        {
            return this.ReflectionDomainSetup.CreateNonInvokabilityException(pertainant);
        }

        public Exception CreateMissingArrayTypeException(Type elementType, bool isMultiDim, int rank)
        {
            return ReflectionDomainSetup.CreateMissingArrayTypeException(elementType, isMultiDim, rank);
        }

        public Exception CreateMissingConstructedGenericTypeException(Type genericTypeDefinition, Type[] genericTypeArguments)
        {
            return ReflectionDomainSetup.CreateMissingConstructedGenericTypeException(genericTypeDefinition, genericTypeArguments);
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

        internal ExecutionEnvironment ExecutionEnvironment { get; }

        internal ReflectionDomainSetup ReflectionDomainSetup { get; }

        internal FoundationTypes FoundationTypes
        {
            get
            {
                return this.ReflectionDomainSetup.FoundationTypes;
            }
        }

        internal IEnumerable<Type> PrimitiveTypes
        {
            get
            {
                FoundationTypes foundationTypes = this.FoundationTypes;
                return new Type[]
                {
                    foundationTypes.SystemBoolean,
                    foundationTypes.SystemChar,
                    foundationTypes.SystemSByte,
                    foundationTypes.SystemByte,
                    foundationTypes.SystemInt16,
                    foundationTypes.SystemUInt16,
                    foundationTypes.SystemInt32,
                    foundationTypes.SystemUInt32,
                    foundationTypes.SystemInt64,
                    foundationTypes.SystemUInt64,
                    foundationTypes.SystemSingle,
                    foundationTypes.SystemDouble,
                    foundationTypes.SystemIntPtr,
                    foundationTypes.SystemUIntPtr,
                };
            }
        }
    }
}
