// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem.Interop
{
    public static class InteropTypes
    {
        public static MetadataType GetGC(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System", "GC");
        }

        public static MetadataType GetSafeHandle(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "SafeHandle");
        }

        public static MetadataType GetCriticalHandle(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "CriticalHandle");
        }

        public static MetadataType GetHandleRef(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "HandleRef");
        }

        public static MetadataType GetMissingMemberException(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System", "MissingMemberException");
        }

        public static MetadataType GetPInvokeMarshal(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "PInvokeMarshal");
        }

        public static MetadataType GetNativeFunctionPointerWrapper(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "NativeFunctionPointerWrapper");
        }

        public static MetadataType GetStringBuilder(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Text", "StringBuilder");
        }

        public static MetadataType GetSystemDateTime(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System", "DateTime");
        }

        public static MetadataType GetSystemDecimal(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System", "Decimal");
        }

        public static MetadataType GetSystemGuid(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System", "Guid");
        }

        public static bool IsSafeHandle(TypeSystemContext context, TypeDesc type)
        {
            return IsOrDerivesFromType(type, GetSafeHandle(context));
        }

        public static bool IsCriticalHandle(TypeSystemContext context, TypeDesc type)
        {
            return IsOrDerivesFromType(type, GetCriticalHandle(context));
        }

        public static bool IsHandleRef(TypeSystemContext context, TypeDesc type)
        {
            return type == GetHandleRef(context);
        }

        public static bool IsSystemDateTime(TypeSystemContext context, TypeDesc type)
        {
            return type == GetSystemDateTime(context);
        }

        public static bool IsStringBuilder(TypeSystemContext context, TypeDesc type)
        {
            return type == GetStringBuilder(context);
        }

        public static bool IsSystemDecimal(TypeSystemContext context, TypeDesc type)
        {
            return type == GetSystemDecimal(context);
        }

        public static bool IsSystemGuid(TypeSystemContext context, TypeDesc type)
        {
            return type == GetSystemGuid(context);
        }

        private static bool IsOrDerivesFromType(TypeDesc type, MetadataType targetType)
        {
            while (type != null)
            {
                if (type == targetType)
                    return true;
                type = type.BaseType;
            }
            return false;
        }
    }
}
