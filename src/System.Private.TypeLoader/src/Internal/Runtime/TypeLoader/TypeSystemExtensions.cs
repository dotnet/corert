// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.NativeFormat;
using Internal.Runtime.Augments;
using Internal.TypeSystem;
using Internal.TypeSystem.NoMetadata;

namespace Internal.TypeSystem.NativeFormat
{
    // When SUPPORTS_NATIVE_METADATA_TYPE_LOADING is not set we may see compile errors from using statements.
    // Add a namespace definition for Internal.TypeSystem.NativeFormat
}

namespace Internal.Runtime.TypeLoader
{
    internal static class TypeDescExtensions
    {
        public static bool CanShareNormalGenericCode(this TypeDesc type)
        {
            return (type != type.ConvertToCanonForm(CanonicalFormKind.Specific));
        }

        public static bool IsGeneric(this TypeDesc type)
        {
            DefType typeAsDefType = type as DefType;
            return typeAsDefType != null && typeAsDefType.HasInstantiation;
        }

        static public DefType GetClosestDefType(this TypeDesc type)
        {
            if (type is DefType)
                return (DefType)type;
            else
                return type.BaseType;
        }
    }

    internal static class MethodDescExtensions
    {
        public static bool CanShareNormalGenericCode(this InstantiatedMethod method)
        {
            return (method != method.GetCanonMethodTarget(CanonicalFormKind.Specific));
        }
    }

    internal static class RuntimeHandleExtensions
    {
        public static bool IsNull(this RuntimeTypeHandle rtth)
        {
            return RuntimeAugments.GetRuntimeTypeHandleRawValue(rtth) == IntPtr.Zero;
        }

        public unsafe static bool IsDynamic(this RuntimeFieldHandle rtfh)
        {
            IntPtr rtfhValue = *(IntPtr*)&rtfh;
            return (rtfhValue.ToInt64() & 0x1) == 0x1;
        }

        public unsafe static bool IsDynamic(this RuntimeMethodHandle rtfh)
        {
            IntPtr rtfhValue = *(IntPtr*)&rtfh;
            return (rtfhValue.ToInt64() & 0x1) == 0x1;
        }
    }
}
