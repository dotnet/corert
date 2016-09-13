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

using System;
using System.Reflection;
using System.Diagnostics;

using RhCorElementType = System.Runtime.RuntimeImports.RhCorElementType;

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

        public static TypeCode GetRuntimeTypeCode(Type type)
        {
            Debug.Assert(type != null);

            EETypePtr eeType;
            if (!type.TryGetEEType(out eeType))
            {
                // Type exists in metadata only. Aside from the enums, there is no chance a type with a TypeCode would not have an EEType,
                // so if it's not an enum, return the default.
                if (!type.IsEnum)
                    return TypeCode.Object;
                Type underlyingType = Enum.GetUnderlyingType(type);
                eeType = underlyingType.TypeHandle.EETypePtr;
            }

            // Note: Type.GetTypeCode() is expected to return the underlying type's TypeCode for enums. EETypePtr.CorElementType does the same,
            // so this one switch handles both cases.
            RhCorElementType rhType = eeType.CorElementType;
            switch (rhType)
            {
                case RhCorElementType.ELEMENT_TYPE_BOOLEAN: return TypeCode.Boolean;
                case RhCorElementType.ELEMENT_TYPE_CHAR: return TypeCode.Char;
                case RhCorElementType.ELEMENT_TYPE_I1: return TypeCode.SByte;
                case RhCorElementType.ELEMENT_TYPE_U1: return TypeCode.Byte;
                case RhCorElementType.ELEMENT_TYPE_I2: return TypeCode.Int16;
                case RhCorElementType.ELEMENT_TYPE_U2: return TypeCode.UInt16;
                case RhCorElementType.ELEMENT_TYPE_I4: return TypeCode.Int32;
                case RhCorElementType.ELEMENT_TYPE_U4: return TypeCode.UInt32;
                case RhCorElementType.ELEMENT_TYPE_I8: return TypeCode.Int64;
                case RhCorElementType.ELEMENT_TYPE_U8: return TypeCode.UInt64;
                case RhCorElementType.ELEMENT_TYPE_R4: return TypeCode.Single;
                case RhCorElementType.ELEMENT_TYPE_R8: return TypeCode.Double;
                default:
                    break;
            }

            if (type.Equals(CommonRuntimeTypes.String))
                return TypeCode.String;

            if (type.Equals(CommonRuntimeTypes.DateTime))
                return TypeCode.DateTime;

            if (type.Equals(CommonRuntimeTypes.Decimal))
                return TypeCode.Decimal;

            if (eeType == DBNull.Value.EETypePtr)
                return TypeCode.DBNull;

            return TypeCode.Object;
        }

        public static ReflectionCoreCallbacks ReflectionCoreCallbacks
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
        public abstract Assembly Load(AssemblyName refName);

        public abstract MethodBase GetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle);
        public abstract MethodBase GetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle, RuntimeTypeHandle declaringTypeHandle);
        public abstract FieldInfo GetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle);
        public abstract FieldInfo GetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle, RuntimeTypeHandle declaringTypeHandle);

        public abstract void InitializeAssemblyName(AssemblyName blank, String fullName);
        public abstract String ComputeAssemblyNameFullName(AssemblyName assemblyName);
        public abstract byte[] ComputePublicKeyToken(byte[] publicKey);

        public abstract EventInfo GetImplicitlyOverriddenBaseClassEvent(EventInfo e);
        public abstract MethodInfo GetImplicitlyOverriddenBaseClassMethod(MethodInfo m);
        public abstract PropertyInfo GetImplicitlyOverriddenBaseClassProperty(PropertyInfo p);

        public abstract Binder CreateDefaultBinder();
    }
}
