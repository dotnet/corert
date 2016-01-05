// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;

using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;

using Debug = System.Diagnostics.Debug;

namespace Internal.Reflection.Execution
{
    internal static class MetadataReaderExtensions
    {
        public static string GetString(this ConstantStringValueHandle handle, MetadataReader reader)
        {
            return reader.GetConstantStringValue(handle).Value;
        }

        // Useful for namespace Name string which can be a null handle.
        public static String GetStringOrNull(this ConstantStringValueHandle handle, MetadataReader reader)
        {
            if (reader.IsNull(handle))
                return null;
            return reader.GetConstantStringValue(handle).Value;
        }

        public static int AsInt(this TypeDefinitionHandle typeDefinitionHandle)
        {
            unsafe
            {
                return *(int*)&typeDefinitionHandle;
            }
        }

        public static TypeDefinitionHandle AsTypeDefinitionHandle(this int i)
        {
            unsafe
            {
                Debug.Assert((HandleType)((uint)i >> 24) == HandleType.TypeDefinition);
                return *(TypeDefinitionHandle*)&i;
            }
        }

        public static bool IsTypeDefinitionHandle(this int i)
        {
            return (HandleType)((uint)i >> 24) == HandleType.TypeDefinition;
        }

        public static int AsInt(this TypeReferenceHandle typeReferenceHandle)
        {
            unsafe
            {
                return *(int*)&typeReferenceHandle;
            }
        }

        public static TypeReferenceHandle AsTypeReferenceHandle(this int i)
        {
            unsafe
            {
                Debug.Assert((HandleType)((uint)i >> 24) == HandleType.TypeReference);
                return *(TypeReferenceHandle*)&i;
            }
        }

        public static bool IsTypeReferenceHandle(this int i)
        {
            return (HandleType)((uint)i >> 24) == HandleType.TypeReference;
        }

        public static bool IsMethodHandle(this int i)
        {
            return (HandleType)((uint)i >> 24) == HandleType.Method;
        }

        public static int AsInt(this MethodHandle methodHandle)
        {
            unsafe
            {
                return *(int*)&methodHandle;
            }
        }

        public static MethodHandle AsMethodHandle(this int i)
        {
            unsafe
            {
                Debug.Assert((HandleType)((uint)i >> 24) == HandleType.Method);
                return *(MethodHandle*)&i;
            }
        }

        public static int AsInt(this FieldHandle fieldHandle)
        {
            unsafe
            {
                return *(int*)&fieldHandle;
            }
        }

        public static FieldHandle AsFieldHandle(this int i)
        {
            unsafe
            {
                Debug.Assert((HandleType)((uint)i >> 24) == HandleType.Field);
                return *(FieldHandle*)&i;
            }
        }

        public static int WithoutHandleType(this int constantStringValueHandle)
        {
            unsafe
            {
                return constantStringValueHandle & 0x00ffffff;
            }
        }

        public static bool IsConstantStringValueHandle(this int i)
        {
            return (HandleType)((uint)i >> 24) == HandleType.ConstantStringValue;
        }
    }
}



