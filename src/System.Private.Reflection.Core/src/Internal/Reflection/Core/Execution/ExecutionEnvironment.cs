// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using Internal.Metadata.NativeFormat;

using OpenMethodInvoker = System.Reflection.Runtime.MethodInfos.OpenMethodInvoker;

namespace Internal.Reflection.Core.Execution
{
    //
    // This class abstracts the underlying Redhawk (or whatever execution engine) runtime and exposes the services
    // that I.R.Core.Execution needs.
    //
    public abstract class ExecutionEnvironment
    {
        //==============================================================================================
        // Access to the underlying execution engine's object allocation routines.
        //==============================================================================================
        public abstract Object NewObject(RuntimeTypeHandle typeHandle);
        public abstract Array NewArray(RuntimeTypeHandle typeHandleForArrayType, int count);
        public abstract Array NewMultiDimArray(RuntimeTypeHandle typeHandleForArrayType, int[] lengths, int[] lowerBounds);

        //==============================================================================================
        // Execution engine policies.
        //==============================================================================================

        //
        // This returns a generic type with one generic parameter (representing the array element type)
        // whose base type and interface list determines what TypeInfo.BaseType and TypeInfo.ImplementedInterfaces
        // return for types that return true for IsArray.
        //
        public abstract RuntimeTypeHandle ProjectionTypeForArrays { get; }
        public abstract bool IsAssignableFrom(RuntimeTypeHandle dstType, RuntimeTypeHandle srcType);
        public abstract bool TryGetBaseType(RuntimeTypeHandle typeHandle, out RuntimeTypeHandle baseTypeHandle);
        public abstract IEnumerable<RuntimeTypeHandle> TryGetImplementedInterfaces(RuntimeTypeHandle typeHandle);
        public abstract bool IsReflectionBlocked(RuntimeTypeHandle typeHandle);
        public abstract string GetLastResortString(RuntimeTypeHandle typeHandle);

        //==============================================================================================
        // Default Value support.
        //==============================================================================================
        public abstract bool GetDefaultValueIfAny(MetadataReader reader, ParameterHandle parameterHandle, Type declaredType, IEnumerable<CustomAttributeData> customAttributes, out Object defaultValue);
        public abstract bool GetDefaultValueIfAny(MetadataReader reader, FieldHandle fieldHandle, Type declaredType, IEnumerable<CustomAttributeData> customAttributes, out Object defaultValue);
        public abstract bool GetDefaultValueIfAny(MetadataReader reader, PropertyHandle propertyHandle, Type declaredType, IEnumerable<CustomAttributeData> customAttributes, out Object defaultValue);

        //==============================================================================================
        // Reflection Mapping Tables
        //==============================================================================================
        public abstract bool TryGetMetadataForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeDefinitionHandle typeDefHandle);
        public abstract bool TryGetNamedTypeForMetadata(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, out RuntimeTypeHandle runtimeTypeHandle);

        public abstract bool TryGetTypeReferenceForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeReferenceHandle typeRefHandle);
        public abstract bool TryGetNamedTypeForTypeReference(MetadataReader metadataReader, TypeReferenceHandle typeRefHandle, out RuntimeTypeHandle runtimeTypeHandle);

        public abstract bool TryGetArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, out RuntimeTypeHandle arrayTypeHandle);
        public abstract bool TryGetArrayTypeElementType(RuntimeTypeHandle arrayTypeHandle, out RuntimeTypeHandle elementTypeHandle);

        public abstract bool TryGetMultiDimArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, int rank, out RuntimeTypeHandle arrayTypeHandle);

        public abstract bool TryGetPointerTypeForTargetType(RuntimeTypeHandle targetTypeHandle, out RuntimeTypeHandle pointerTypeHandle);
        public abstract bool TryGetPointerTypeTargetType(RuntimeTypeHandle pointerTypeHandle, out RuntimeTypeHandle targetTypeHandle);

        public abstract bool TryGetConstructedGenericTypeComponents(RuntimeTypeHandle runtimeTypeHandle, out RuntimeTypeHandle genericTypeDefinitionHandle, out RuntimeTypeHandle[] genericTypeArgumentHandles);
        public abstract bool TryGetConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle);

        public abstract bool TryGetMetadataNameForRuntimeTypeHandle(RuntimeTypeHandle rtth, out string name);

        //==============================================================================================
        // Invoke and field access support.
        //==============================================================================================
        public abstract MethodInvoker TryGetMethodInvoker(MetadataReader reader, RuntimeTypeHandle declaringTypeHandle, MethodHandle methodHandle, RuntimeTypeHandle[] genericMethodTypeArgumentHandles);
        public abstract FieldAccessor TryGetFieldAccessor(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle, FieldHandle fieldHandle);

        //==============================================================================================
        // Pseudo Custom Attributes
        //==============================================================================================
        public abstract IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, ScopeDefinitionHandle scopeDefinitionHandle);
        public abstract IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle);
        public abstract IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, MethodHandle methodHandle, TypeDefinitionHandle declaringTypeHandle);
        public abstract IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, ParameterHandle parameterHandle, MethodHandle declaringMethodHandle);
        public abstract IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, FieldHandle fieldHandle, TypeDefinitionHandle declaringTypeHandle);
        public abstract IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, PropertyHandle propertyHandle, TypeDefinitionHandle declaringTypeHandle);
        public abstract IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, EventHandle eventHandle, TypeDefinitionHandle declaringTypeHandle);

        //==============================================================================================
        // RuntimeMethodHandle and RuntimeFieldHandle support.
        //==============================================================================================
        public abstract bool TryGetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles);
        public abstract bool TryGetMethodFromHandleAndType(RuntimeMethodHandle runtimeMethodHandle, RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles);
        public abstract bool TryGetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle, out RuntimeTypeHandle declaringTypeHandle, out FieldHandle fieldHandle);
        public abstract bool TryGetFieldFromHandleAndType(RuntimeFieldHandle runtimeFieldHandle, RuntimeTypeHandle declaringTypeHandle, out FieldHandle fieldHandle);


        //==============================================================================================
        // Manifest resource stream support.
        //==============================================================================================
        public abstract ManifestResourceInfo GetManifestResourceInfo(Assembly assembly, String resourceName);
        public abstract String[] GetManifestResourceNames(Assembly assembly);
        public abstract Stream GetManifestResourceStream(Assembly assembly, String name);

        //==============================================================================================
        // Other
        //==============================================================================================
        public abstract MethodInvoker GetSyntheticMethodInvoker(RuntimeTypeHandle thisType, RuntimeTypeHandle[] parameterTypes, InvokerOptions options, Func<Object, Object[], Object> invoker);
        public abstract bool IsCOMObject(Type type);

        //==============================================================================================
        // Generic Virtual Method Support
        //==============================================================================================
        public abstract bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, ref string methodName, ref IntPtr methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated);

        //==============================================================================================
        // Non-public methods
        //==============================================================================================
        internal MethodInvoker GetMethodInvoker(MetadataReader reader, RuntimeTypeInfo declaringType, MethodHandle methodHandle, RuntimeTypeInfo[] genericMethodTypeArguments, MemberInfo exceptionPertainant)
        {
            if (declaringType.ContainsGenericParameters)
                return new OpenMethodInvoker();
            for (int i = 0; i < genericMethodTypeArguments.Length; i++)
            {
                if (genericMethodTypeArguments[i].ContainsGenericParameters)
                    return new OpenMethodInvoker();
            }

            RuntimeTypeHandle typeDefinitionHandle = declaringType.TypeHandle;
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles = new RuntimeTypeHandle[genericMethodTypeArguments.Length];

            for (int i = 0; i < genericMethodTypeArguments.Length; i++)
            {
                genericMethodTypeArgumentHandles[i] = genericMethodTypeArguments[i].TypeHandle;
            }
            MethodInvoker methodInvoker = TryGetMethodInvoker(reader, typeDefinitionHandle, methodHandle, genericMethodTypeArgumentHandles);
            if (methodInvoker == null)
                throw ReflectionCoreExecution.ExecutionDomain.CreateNonInvokabilityException(exceptionPertainant);
            return methodInvoker;
        }
    }
}
