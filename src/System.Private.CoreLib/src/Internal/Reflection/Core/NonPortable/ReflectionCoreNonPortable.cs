// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//  Internal.Reflection.Core.NonPortable
//  -------------------------------------------------
//  Why does this exist?:
//    This is an artifact of the fact that on MRT, System.Private.CoreLib.dll needs to
//    be able to create and properly unify System.Type objects before
//    Reflection.Core.dll has been loaded into the process.
//
//    Because of this, a small part of Reflection.Core.dll
//    needs to live in System.Private.CoreLib.dll on MRT. In an ideal world,
//    this contract wouldn't exist and the contents of this contract
//    entirely encapsulated in Reflection.Core.dll.
//
//  Implemented by:
//    System.Private.CoreLib.dll
//
//  Consumed by:
//    Reflection.Core.dll 

using System;

namespace Internal.Reflection.Core.NonPortable
{
    public static class ReflectionCoreNonPortable
    {
        public static RuntimeType GetArrayType(RuntimeType elementType)
        {
            return RuntimeTypeUnifier.GetArrayType(elementType);
        }

        public static RuntimeType GetMultiDimArrayType(RuntimeType elementType, int rank)
        {
            return RuntimeTypeUnifier.GetMultiDimArrayType(elementType, rank);
        }

        public static RuntimeType GetByRefType(RuntimeType targetType)
        {
            return RuntimeTypeUnifier.GetByRefType(targetType);
        }

        public static RuntimeType GetConstructedGenericType(RuntimeType genericTypeDefinition, RuntimeType[] genericTypeArguments)
        {
            return RuntimeTypeUnifier.GetConstructedGenericType(genericTypeDefinition, genericTypeArguments);
        }

        public static RuntimeType GetPointerType(RuntimeType targetType)
        {
            return RuntimeTypeUnifier.GetPointerType(targetType);
        }

        public static RuntimeType GetTypeForRuntimeTypeHandle(RuntimeTypeHandle runtimeTypeHandle)
        {
            return RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(runtimeTypeHandle);
        }

        internal static RuntimeType GetRuntimeTypeForEEType(EETypePtr eeType)
        {
            return RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(new RuntimeTypeHandle(eeType));
        }

        public static TypeLoadException CreateTypeLoadException(String message, String typeName)
        {
            return new TypeLoadException(message, typeName);
        }
    }
}


