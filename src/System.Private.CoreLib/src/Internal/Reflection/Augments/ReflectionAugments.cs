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
using System.Globalization;

using RhCorElementType = System.Runtime.RuntimeImports.RhCorElementType;

using EnumInfo = Internal.Runtime.Augments.EnumInfo;

namespace Internal.Reflection.Augments
{
    [System.Runtime.CompilerServices.ReflectionBlocked]
    public static class ReflectionAugments
    {
        //
        // One time start up initialization - called by Reflection.Core.dll to provide System.Reflection with a way to call back
        // into Reflection.Core.dll.
        //
        public static void Initialize(ReflectionCoreCallbacks reflectionCoreCallbacks)
        {
            s_reflectionCoreCallbacks = reflectionCoreCallbacks;
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

        public static Type MakeGenericSignatureType(Type genericTypeDefinition, Type[] genericTypeArguments)
        {
            return new SignatureConstructedGenericType(genericTypeDefinition, genericTypeArguments);
        }

        public static TypeLoadException CreateTypeLoadException(string message, string typeName)
        {
            return new TypeLoadException(message, typeName);
        }

        internal static ReflectionCoreCallbacks ReflectionCoreCallbacks
        {
            get
            {
                ReflectionCoreCallbacks callbacks = s_reflectionCoreCallbacks;
                if (callbacks == null)
                    throw new InvalidOperationException(SR.InvalidOperation_TooEarly);
                return callbacks;
            }
        }

        private static ReflectionCoreCallbacks s_reflectionCoreCallbacks;
    }

    //
    // This class is implemented by Internal.Reflection.Core.dll and provides the actual implementation
    // of Type.GetTypeInfo() and (on Project N) (Assembly.Load()).
    //
    [System.Runtime.CompilerServices.ReflectionBlocked]
    public abstract class ReflectionCoreCallbacks
    {
        public abstract Assembly Load(AssemblyName refName, bool throwOnFileNotFound);
        public abstract Assembly Load(byte[] rawAssembly, byte[] pdbSymbolStore);

        public abstract MethodBase GetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle);
        public abstract MethodBase GetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle, RuntimeTypeHandle declaringTypeHandle);
        public abstract FieldInfo GetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle);
        public abstract FieldInfo GetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle, RuntimeTypeHandle declaringTypeHandle);

        public abstract EventInfo GetImplicitlyOverriddenBaseClassEvent(EventInfo e);
        public abstract MethodInfo GetImplicitlyOverriddenBaseClassMethod(MethodInfo m);
        public abstract PropertyInfo GetImplicitlyOverriddenBaseClassProperty(PropertyInfo p);

        public abstract object ActivatorCreateInstance(Type type, bool nonPublic);
        public abstract object ActivatorCreateInstance(Type type, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture, object[] activationAttributes);

        // V2 api: Creates open or closed delegates to static or instance methods - relaxed signature checking allowed. 
        public abstract Delegate CreateDelegate(Type type, object firstArgument, MethodInfo method, bool throwOnBindFailure);

        // V1 api: Creates open delegates to static or instance methods - relaxed signature checking allowed.
        public abstract Delegate CreateDelegate(Type type, MethodInfo method, bool throwOnBindFailure);

        // V1 api: Creates closed delegates to instance methods only, relaxed signature checking disallowed.
        public abstract Delegate CreateDelegate(Type type, object target, string method, bool ignoreCase, bool throwOnBindFailure);

        // V1 api: Creates open delegates to static methods only, relaxed signature checking disallowed.
        public abstract Delegate CreateDelegate(Type type, Type target, string method, bool ignoreCase, bool throwOnBindFailure);

        public abstract Type GetTypeFromCLSID(Guid clsid, string server, bool throwOnError);

        public abstract IntPtr GetFunctionPointer(RuntimeMethodHandle runtimeMethodHandle, RuntimeTypeHandle declaringTypeHandle);

        public abstract void RunModuleConstructor(Module module);

        public abstract void MakeTypedReference(object target, FieldInfo[] flds, out Type type, out int offset);

        public abstract Assembly[] GetLoadedAssemblies();

        public abstract EnumInfo GetEnumInfo(Type type);
    }
}
