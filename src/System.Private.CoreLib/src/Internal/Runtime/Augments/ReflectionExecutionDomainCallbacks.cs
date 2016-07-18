// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//Internal.Runtime.Augments
//-------------------------------------------------
//  Why does this exist?:
//    Internal.Reflection.Execution cannot physically live in System.Private.CoreLib.dll
//    as it has a dependency on System.Reflection.Metadata. It's inherently
//    low-level nature means, however, it is closely tied to System.Private.CoreLib.dll.
//    This contract provides the two-communication between those two .dll's.
//
//
//  Implemented by:
//    System.Private.CoreLib.dll
//
//  Consumed by:
//    Reflection.Execution.dll

using System;

namespace Internal.Runtime.Augments
{
    [CLSCompliant(false)]
    public abstract class ReflectionExecutionDomainCallbacks
    {
        /// <summary>
        /// Register reflection module. Not thread safe with respect to reflection runtime.
        /// </summary>
        /// <param name="moduleHandle">Handle of module to register</param>
        public abstract void RegisterModule(IntPtr moduleHandle);
        
        // Api's that are exposed in System.Runtime but are really reflection apis.
        public abstract Object ActivatorCreateInstance(Type type, Object[] args);
        public abstract Type GetType(String typeName, bool throwOnError, bool ignoreCase);

        // Access to reflection mapping tables.
        public abstract bool TryGetArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, out RuntimeTypeHandle arrayTypeHandle);
        public abstract bool TryGetArrayTypeElementType(RuntimeTypeHandle arrayTypeHandle, out RuntimeTypeHandle elementTypeHandle);

        public abstract bool TryGetMultiDimArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, int rank, out RuntimeTypeHandle arrayTypeHandle);
        public abstract bool TryGetMultiDimArrayTypeElementType(RuntimeTypeHandle arrayTypeHandle, int rank, out RuntimeTypeHandle elementTypeHandle);

        public abstract bool TryGetPointerTypeForTargetType(RuntimeTypeHandle targetTypeHandle, out RuntimeTypeHandle pointerTypeHandle);
        public abstract bool TryGetPointerTypeTargetType(RuntimeTypeHandle pointerTypeHandle, out RuntimeTypeHandle targetTypeHandle);

        public abstract bool TryGetConstructedGenericTypeComponents(RuntimeTypeHandle runtimeTypeHandle, out RuntimeTypeHandle genericTypeDefinitionHandle, out RuntimeTypeHandle[] genericTypeArgumentHandles);
        public abstract bool TryGetConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle);

        public abstract IntPtr TryGetDefaultConstructorForType(RuntimeTypeHandle runtimeTypeHandle);
        public abstract IntPtr TryGetDefaultConstructorForTypeUsingLocator(object canonEquivalentEntryLocator);

        public abstract IntPtr TryGetStaticClassConstructionContext(RuntimeTypeHandle runtimeTypeHandle);

        public abstract bool IsReflectionBlocked(RuntimeTypeHandle typeHandle);

        public abstract bool TryGetMetadataNameForRuntimeTypeHandle(RuntimeTypeHandle rtth, out string name);

        // Generic Virtual Method Support
        public abstract bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, ref string methodName, ref IntPtr methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated);

        // Flotsam and jetsam.
        public abstract Exception CreateMissingMetadataException(Type typeWithMissingMetadata);
        public abstract Exception CreateMissingArrayTypeException(Type elementType, bool isMultiDim, int rank);
        public abstract Exception CreateMissingConstructedGenericTypeException(Type genericTypeDefinition, Type[] genericTypeArguments);

        public abstract Type CreateShadowRuntimeInspectionOnlyNamedTypeIfAvailable(RuntimeTypeHandle runtimeTypeHandle);
        public abstract EnumInfo GetEnumInfoIfAvailable(Type enumType);
        public abstract String GetBetterDiagnosticInfoIfAvailable(RuntimeTypeHandle runtimeTypeHandle);
        public abstract String GetMethodNameFromStartAddressIfAvailable(IntPtr methodStartAddress);
        public abstract int ValueTypeGetHashCodeUsingReflection(object valueType);
        public abstract bool ValueTypeEqualsUsingReflection(object left, object right);

        /// <summary>
        /// Retrieves the default value for a parameter of a method.
        /// </summary>
        /// <param name="defaultParametersContext">The default parameters context used to invoke the method,
        /// this should identify the method in question. This is passed to the RuntimeAugments.CallDynamicInvokeMethod.</param>
        /// <param name="thType">The type of the parameter to retrieve.</param>
        /// <param name="argIndex">The index of the parameter on the method to retrieve.</param>
        /// <param name="defaultValue">The default value of the parameter if available.</param>
        /// <returns>true if the default parameter value is available, otherwise false.</returns>
        public abstract bool TryGetDefaultParameterValue(object defaultParametersContext, RuntimeTypeHandle thType, int argIndex, out object defaultValue);
    }
}
