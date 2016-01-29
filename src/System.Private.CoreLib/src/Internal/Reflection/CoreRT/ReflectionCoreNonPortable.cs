// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;

using Internal.TypeSystem;

namespace Internal.Reflection.Core.NonPortable
{
    public static class ReflectionCoreNonPortable
    {
        public static RuntimeType GetArrayType(RuntimeType elementType)
        {
            // CORERT-TODO: Reflection
            throw new NotSupportedException();
        }

        public static RuntimeType GetMultiDimArrayType(RuntimeType elementType, int rank)
        {
            // CORERT-TODO: Reflection
            throw new NotImplementedException();
        }

        public static RuntimeType GetByRefType(RuntimeType targetType)
        {
            // CORERT-TODO: Reflection
            throw new NotImplementedException();
        }

        public static RuntimeType GetConstructedGenericType(RuntimeType genericTypeDefinition, RuntimeType[] genericTypeArguments)
        {
            // CORERT-TODO: Reflection
            throw new NotImplementedException();
        }

        public static RuntimeType GetPointerType(RuntimeType targetType)
        {
            // CORERT-TODO: Reflection
            throw new NotImplementedException();
        }

        private class RuntimeTypeHashtable : LockFreeReaderHashtable<EETypePtr, RuntimeType>
        {
            protected override int GetKeyHashCode(EETypePtr key)
            {
                return key.GetHashCode();
            }

            protected override int GetValueHashCode(RuntimeType value)
            {
                return value.ToEETypePtr().GetHashCode();
            }

            protected override bool CompareKeyToValue(EETypePtr key, RuntimeType value)
            {
                return RuntimeImports.AreTypesEquivalent(key, value.ToEETypePtr());
            }

            protected override bool CompareValueToValue(RuntimeType value1, RuntimeType value2)
            {
                return RuntimeImports.AreTypesEquivalent(value1.ToEETypePtr(), value2.ToEETypePtr());
            }

            protected override RuntimeType CreateValueFromKey(EETypePtr key)
            {
                return new RuntimeType(key);
            }
        }

        static private readonly RuntimeTypeHashtable g_runtimeTypeFactory = new RuntimeTypeHashtable();

        internal static RuntimeType GetRuntimeTypeForEEType(EETypePtr eeType)
        {
            return g_runtimeTypeFactory.GetOrCreateValue(eeType);
        }

        public static RuntimeType GetTypeForRuntimeTypeHandle(RuntimeTypeHandle runtimeTypeHandle)
        {
            return runtimeTypeHandle.RuntimeType;
        }

        public static TypeLoadException CreateTypeLoadException(String message, String typeName)
        {
            return new TypeLoadException(message, typeName);
        }
    }
}
