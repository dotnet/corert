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
using System.Reflection;

namespace Internal.Runtime.Augments
{
    [CLSCompliant(false)]
    public abstract class ReflectionExecutionDomainCallbacks
    {
        // Api's that are exposed in System.Runtime but are really reflection apis.
        public abstract Type GetType(string typeName, Func<AssemblyName, Assembly> assemblyResolver, Func<Assembly, string, bool, Type> typeResolver, bool throwOnError, bool ignoreCase);

        public abstract IntPtr TryGetStaticClassConstructionContext(RuntimeTypeHandle runtimeTypeHandle);

        public abstract bool IsReflectionBlocked(RuntimeTypeHandle typeHandle);

        //=======================================================================================
        // This group of methods jointly service the Type.GetTypeFromHandle() path. The caller
        // is responsible for analyzing the RuntimeTypeHandle to figure out which flavor to call.
        //=======================================================================================
        public abstract Type GetNamedTypeForHandle(RuntimeTypeHandle typeHandle, bool isGenericTypeDefinition);
        public abstract Type GetArrayTypeForHandle(RuntimeTypeHandle typeHandle);
        public abstract Type GetMdArrayTypeForHandle(RuntimeTypeHandle typeHandle, int rank);
        public abstract Type GetPointerTypeForHandle(RuntimeTypeHandle typeHandle);
        public abstract Type GetByRefTypeForHandle(RuntimeTypeHandle typeHandle);
        public abstract Type GetConstructedGenericTypeForHandle(RuntimeTypeHandle typeHandle);

        // Flotsam and jetsam.
        public abstract Exception CreateMissingMetadataException(Type typeWithMissingMetadata);

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

        public abstract RuntimeTypeHandle GetTypeHandleIfAvailable(Type type);
        public abstract bool SupportsReflection(Type type);

        public abstract MethodInfo GetDelegateMethod(Delegate del);
    }
}
