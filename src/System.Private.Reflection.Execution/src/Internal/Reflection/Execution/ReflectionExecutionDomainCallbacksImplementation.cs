// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat;

using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;

using Internal.Reflection.Core.Execution;
using Internal.Reflection.Execution.PayForPlayExperience;
using Internal.Reflection.Extensions.NonPortable;

using Debug = System.Diagnostics.Debug;

namespace Internal.Reflection.Execution
{
    //==========================================================================================================================
    // This class provides various services down to System.Private.CoreLib. (Though we forward most or all of them directly up to Reflection.Core.) 
    //==========================================================================================================================
    internal sealed class ReflectionExecutionDomainCallbacksImplementation : ReflectionExecutionDomainCallbacks
    {
        public ReflectionExecutionDomainCallbacksImplementation(ExecutionDomain executionDomain, ExecutionEnvironmentImplementation executionEnvironment)
        {
            _executionDomain = executionDomain;
            _executionEnvironment = executionEnvironment;
        }

        public sealed override Type GetType(string typeName, Func<AssemblyName, Assembly> assemblyResolver, Func<Assembly, string, bool, Type> typeResolver, bool throwOnError, bool ignoreCase, string defaultAssemblyName)
        {
            if (defaultAssemblyName == null)
            {
                return _executionDomain.GetType(typeName, assemblyResolver, typeResolver, throwOnError, ignoreCase, ReflectionExecution.DefaultAssemblyNamesForGetType);
            }
            else
            {
                LowLevelListWithIList<String> defaultAssemblies = new LowLevelListWithIList<String>();
                defaultAssemblies.Add(defaultAssemblyName);
                defaultAssemblies.AddRange(ReflectionExecution.DefaultAssemblyNamesForGetType);
                return _executionDomain.GetType(typeName, assemblyResolver, typeResolver, throwOnError, ignoreCase, defaultAssemblies);
            }
        }

        public sealed override bool IsReflectionBlocked(RuntimeTypeHandle typeHandle)
        {
            return _executionEnvironment.IsReflectionBlocked(typeHandle);
        }

        //=======================================================================================
        // This group of methods jointly service the Type.GetTypeFromHandle() path. The caller
        // is responsible for analyzing the RuntimeTypeHandle to figure out which flavor to call.
        //=======================================================================================
        public sealed override Type GetNamedTypeForHandle(RuntimeTypeHandle typeHandle, bool isGenericTypeDefinition)
        {
            return _executionDomain.GetNamedTypeForHandle(typeHandle, isGenericTypeDefinition);
        }

        public sealed override Type GetArrayTypeForHandle(RuntimeTypeHandle typeHandle)
        {
            return _executionDomain.GetArrayTypeForHandle(typeHandle);
        }

        public sealed override Type GetMdArrayTypeForHandle(RuntimeTypeHandle typeHandle, int rank)
        {
            return _executionDomain.GetMdArrayTypeForHandle(typeHandle, rank);
        }

        public sealed override Type GetPointerTypeForHandle(RuntimeTypeHandle typeHandle)
        {
            return _executionDomain.GetPointerTypeForHandle(typeHandle);
        }

        public sealed override Type GetByRefTypeForHandle(RuntimeTypeHandle typeHandle)
        {
            return _executionDomain.GetByRefTypeForHandle(typeHandle);
        }

        public sealed override Type GetConstructedGenericTypeForHandle(RuntimeTypeHandle typeHandle)
        {
            return _executionDomain.GetConstructedGenericTypeForHandle(typeHandle);
        }

        //=======================================================================================
        // MissingMetadataException support.
        //=======================================================================================
        public sealed override Exception CreateMissingMetadataException(Type pertainant)
        {
            return _executionDomain.CreateMissingMetadataException(pertainant);
        }

        public sealed override EnumInfo GetEnumInfoIfAvailable(Type enumType)
        {
            // Handle the weird case of an enum type nested under a generic type that makes the
            // enum itself generic.
            if (enumType.IsConstructedGenericType)
            {
                enumType = enumType.GetGenericTypeDefinition();
            }

            MetadataReader reader;
            TypeDefinitionHandle typeDefinitionHandle;
            if (!ReflectionExecution.ExecutionEnvironment.TryGetMetadataForNamedType(enumType.TypeHandle, out reader, out typeDefinitionHandle))
                return null;

            return new EnumInfoImplementation(enumType, reader, typeDefinitionHandle);
        }

        // This is called from the ToString() helper of a RuntimeType that does not have full metadata.
        // This helper makes a "best effort" to give the caller something better than "EETypePtr nnnnnnnnn".
        public sealed override String GetBetterDiagnosticInfoIfAvailable(RuntimeTypeHandle runtimeTypeHandle)
        {
            return Type.GetTypeFromHandle(runtimeTypeHandle).ToDisplayStringIfAvailable(null);
        }

        public sealed override String GetMethodNameFromStartAddressIfAvailable(IntPtr methodStartAddress)
        {
            RuntimeTypeHandle declaringTypeHandle = default(RuntimeTypeHandle);
            MethodHandle methodHandle;
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles;
            if (!ReflectionExecution.ExecutionEnvironment.TryGetMethodForOriginalLdFtnResult(methodStartAddress,
                ref declaringTypeHandle, out methodHandle, out genericMethodTypeArgumentHandles))
            {
                return null;
            }

            MethodBase methodBase = ReflectionCoreExecution.ExecutionDomain.GetMethod(
                                        declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles);
            if (methodBase == null || string.IsNullOrEmpty(methodBase.Name))
                return null;

            // get type name
            string typeName = string.Empty;
            Type declaringType = Type.GetTypeFromHandle(declaringTypeHandle);
            if (declaringType != null)
                typeName = declaringType.ToDisplayStringIfAvailable(null);
            if (string.IsNullOrEmpty(typeName))
                typeName = "<unknown>";

            StringBuilder fullMethodName = new StringBuilder();
            fullMethodName.Append(typeName);
            fullMethodName.Append('.');
            fullMethodName.Append(methodBase.Name);
            fullMethodName.Append('(');

            // get parameter list
            ParameterInfo[] paramArr = methodBase.GetParametersNoCopy();
            for (int i = 0; i < paramArr.Length; ++i)
            {
                if (i != 0)
                    fullMethodName.Append(", ");

                ParameterInfo param = paramArr[i];
                string paramTypeName = string.Empty;
                if (param.ParameterType != null)
                    paramTypeName = param.ParameterType.ToDisplayStringIfAvailable(null);
                if (string.IsNullOrEmpty(paramTypeName))
                    paramTypeName = "<unknown>";
                else
                {
                    // remove namespace from param type-name
                    int idxSeparator = paramTypeName.IndexOf(".");
                    if (idxSeparator >= 0)
                        paramTypeName = paramTypeName.Remove(0, idxSeparator + 1);
                }

                string paramName = param.Name;
                if (string.IsNullOrEmpty(paramName))
                    paramName = "<unknown>";

                fullMethodName.Append(paramTypeName);
                fullMethodName.Append(' ');
                fullMethodName.Append(paramName);
            }
            fullMethodName.Append(')');
            return fullMethodName.ToString();
        }

        private String GetTypeFullNameFromTypeRef(TypeReferenceHandle typeReferenceHandle, MetadataReader reader)
        {
            String s = "";

            TypeReference typeReference = typeReferenceHandle.GetTypeReference(reader);
            s = typeReference.TypeName.GetString(reader);
            Handle parentHandle = typeReference.ParentNamespaceOrType;
            HandleType parentHandleType = parentHandle.HandleType;
            if (parentHandleType == HandleType.TypeReference)
            {
                String containingTypeName = GetTypeFullNameFromTypeRef(parentHandle.ToTypeReferenceHandle(reader), reader);
                s = containingTypeName + "+" + s;
            }
            else if (parentHandleType == HandleType.NamespaceReference)
            {
                NamespaceReferenceHandle namespaceReferenceHandle = parentHandle.ToNamespaceReferenceHandle(reader);
                for (;;)
                {
                    NamespaceReference namespaceReference = namespaceReferenceHandle.GetNamespaceReference(reader);
                    String namespacePart = namespaceReference.Name.GetStringOrNull(reader);
                    if (namespacePart == null)
                        break; // Reached the root namespace.
                    s = namespacePart + "." + s;
                    if (namespaceReference.ParentScopeOrNamespace.HandleType != HandleType.NamespaceReference)
                        break; // Should have reached the root namespace first but this helper is for ToString() - better to
                    // return partial information than crash.
                    namespaceReferenceHandle = namespaceReference.ParentScopeOrNamespace.ToNamespaceReferenceHandle(reader);
                }
            }
            else
            {
                // If we got here, the metadata is illegal but this helper is for ToString() - better to 
                // return something partial than throw.
            }

            return s;
        }

        public sealed override IntPtr TryGetStaticClassConstructionContext(RuntimeTypeHandle runtimeTypeHandle)
        {
            return _executionEnvironment.TryGetStaticClassConstructionContext(runtimeTypeHandle);
        }

        /// <summary>
        /// Compares FieldInfos, sorting by name.
        /// </summary>
        private class FieldInfoNameComparer : IComparer<FieldInfo>
        {
            private static FieldInfoNameComparer s_instance = new FieldInfoNameComparer();
            public static FieldInfoNameComparer Instance
            {
                get
                {
                    return s_instance;
                }
            }

            public int Compare(FieldInfo x, FieldInfo y)
            {
                return x.Name.CompareTo(y.Name);
            }
        }

        /// <summary>
        /// Reflection-based implementation of ValueType.GetHashCode. Matches the implementation created by the ValueTypeTransform.
        /// </summary>
        /// <param name="valueType">Boxed value type</param>
        /// <returns>Hash code for the value type</returns>
        public sealed override int ValueTypeGetHashCodeUsingReflection(object valueType)
        {
            // The algorithm is to use the hash of the first non-null instance field sorted by name.
            List<FieldInfo> sortedFilteredFields = new List<FieldInfo>();
            foreach (FieldInfo field in valueType.GetType().GetTypeInfo().DeclaredFields)
            {
                if (field.IsStatic)
                {
                    continue;
                }

                sortedFilteredFields.Add(field);
            }
            sortedFilteredFields.Sort(FieldInfoNameComparer.Instance);

            foreach (FieldInfo field in sortedFilteredFields)
            {
                object fieldValue = field.GetValue(valueType);
                if (fieldValue != null)
                {
                    return fieldValue.GetHashCode();
                }
            }

            // Fallback path if no non-null instance field. The desktop hashes the GetType() object, but this seems like a lot of effort
            // for a corner case - let's wait and see if we really need that.
            return 1;
        }

        /// <summary>
        /// Reflection-based implementation of ValueType.Equals. Matches the implementation created by the ValueTypeTransform.
        /// </summary>
        /// <param name="left">Boxed 'this' value type</param>
        /// <param name="right">Boxed 'that' value type</param>
        /// <returns>True if all nonstatic fields of the objects are equal</returns>
        public sealed override bool ValueTypeEqualsUsingReflection(object left, object right)
        {
            if (right == null)
            {
                return false;
            }

            if (left.GetType() != right.GetType())
            {
                return false;
            }

            foreach (FieldInfo field in left.GetType().GetTypeInfo().DeclaredFields)
            {
                if (field.IsStatic)
                {
                    continue;
                }

                object leftField = field.GetValue(left);
                object rightField = field.GetValue(right);

                if (leftField == null)
                {
                    if (rightField != null)
                    {
                        return false;
                    }
                }
                else if (!leftField.Equals(rightField))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Retrieves the default value for a parameter of a method.
        /// </summary>
        /// <param name="defaultParametersContext">The default parameters context used to invoke the method,
        /// this should identify the method in question. This is passed to the RuntimeAugments.CallDynamicInvokeMethod.</param>
        /// <param name="thType">The type of the parameter to retrieve.</param>
        /// <param name="argIndex">The index of the parameter on the method to retrieve.</param>
        /// <param name="defaultValue">The default value of the parameter if available.</param>
        /// <returns>true if the default parameter value is available, otherwise false.</returns>
        public sealed override bool TryGetDefaultParameterValue(object defaultParametersContext, RuntimeTypeHandle thType, int argIndex, out object defaultValue)
        {
            defaultValue = null;

            MethodBase methodInfo = defaultParametersContext as MethodBase;
            if (methodInfo == null)
            {
                return false;
            }

            ParameterInfo parameterInfo = methodInfo.GetParametersNoCopy()[argIndex];
            if (!parameterInfo.HasDefaultValue)
            {
                // If the parameter is optional, with no default value and we're asked for its default value,
                // it means the caller specified Missing.Value as the value for the parameter. In this case the behavior
                // is defined as passing in the Missing.Value, regardless of the parameter type.
                // If Missing.Value is convertible to the parameter type, it will just work, otherwise we will fail
                // due to type mismatch.
                if (parameterInfo.IsOptional)
                {
                    defaultValue = Missing.Value;
                    return true;
                }

                return false;
            }

            defaultValue = parameterInfo.DefaultValue;
            return true;
        }

        public sealed override RuntimeTypeHandle GetTypeHandleIfAvailable(Type type)
        {
            return _executionDomain.GetTypeHandleIfAvailable(type);
        }

        public sealed override bool SupportsReflection(Type type)
        {
            return _executionDomain.SupportsReflection(type);
        }

        public sealed override MethodInfo GetDelegateMethod(Delegate del)
        {
            return DelegateMethodInfoRetriever.GetDelegateMethodInfo(del);
        }

        private ExecutionDomain _executionDomain;
        private ExecutionEnvironmentImplementation _executionEnvironment;
    }
}
