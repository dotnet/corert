// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//  Internal.Reflection.Augments
//  -------------------------------------------------
//  Why does this exist?:
//    Also, IntrospectionServices.GetTypeInfo() and Assembly.Load()
//    are defined in System.Reflection but need a way to "call into"
//    Reflection.Core.dll to do the real work.
//
//    This contract adds the additional entrypoints needed to System.Reflection.
//
//  Implemented by:
//    System.Reflection.dll on RH (may use ILMerging instead)
//    mscorlib.dll on desktop
//
//  Consumed by:
//    Reflection.Core.dll

using global::System;
using global::System.Reflection;

namespace Internal.Reflection.Augments
{
    public static class ReflectionAugments
    {
        //
        // One time start up initialization - called by Reflection.Core.dll to provide System.Reflection with a way to call back
        // into Reflection.Core.dll.
        //
        public static void Initialize(ReflectionCoreCallbacks reflectionCoreCallbacks)
        {
            _reflectionCoreCallbacks = reflectionCoreCallbacks;
        }

        public static CustomAttributeNamedArgument CreateCustomAttributeNamedArgument(Type attributeType, string memberName, bool isField, CustomAttributeTypedArgument typedValue)
        {
            return new CustomAttributeNamedArgument(attributeType, memberName, isField, typedValue);
        }

        internal static ReflectionCoreCallbacks ReflectionCoreCallbacks
        {
            get
            {
                ReflectionCoreCallbacks callbacks = _reflectionCoreCallbacks;
                if (callbacks == null)
                    throw new InvalidOperationException(SR.InvalidOperation_TooEarly);
                return callbacks;
            }
        }

        private static ReflectionCoreCallbacks _reflectionCoreCallbacks;
    }

    //
    // This class is implemented by Internal.Reflection.Core.dll and provides the actual implementation
    // of Type.GetTypeInfo() and (on Project N) (Assembly.Load()).
    //
    public abstract class ReflectionCoreCallbacks
    {
        public abstract TypeInfo GetTypeInfo(Type type);
        public abstract Assembly Load(AssemblyName refName);

        public abstract MethodBase GetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle);
        public abstract MethodBase GetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle, RuntimeTypeHandle declaringTypeHandle);
        public abstract FieldInfo GetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle);
        public abstract FieldInfo GetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle, RuntimeTypeHandle declaringTypeHandle);

        public abstract void InitializeAssemblyName(AssemblyName blank, String fullName);
        public abstract String ComputeAssemblyNameFullName(AssemblyName assemblyName);
        public abstract byte[] ComputePublicKeyToken(byte[] publicKey);
    }
}
