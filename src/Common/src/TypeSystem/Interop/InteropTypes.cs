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

        public static MetadataType GetSafeHandleType(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "SafeHandle");
        }

        public static MetadataType GetPInvokeMarshal(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "PInvokeMarshal");
        }

        /*      
                TODO: Bring CriticalHandle to CoreLib
                https://github.com/dotnet/corert/issues/2570

                public static MetadataType GetCriticalHandle(TypeSystemContext context)
                {
                        return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "CriticalHandle");
                }


                TODO: Bring HandleRef to CoreLib
                https://github.com/dotnet/corert/issues/2570

                public static MetadataType GetHandleRef(TypeSystemContext context)
                {
                    get
                    {
                        return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "HandleRef");
                    }
                }
        */

        public static MetadataType GetNativeFunctionPointerWrapper(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System.Runtime.InteropServices", "NativeFunctionPointerWrapper");
        }

        public static MetadataType GetStringBuilder(TypeSystemContext context, bool throwIfNotFound = true)
        {
            return context.SystemModule.GetKnownType("System.Text", "StringBuilder", throwIfNotFound);
        }

        public static MetadataType GetSystemArray(TypeSystemContext context)
        {
            return context.SystemModule.GetKnownType("System", "Array");
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
            return IsOrDerivesFromType(type, GetSafeHandleType(context));
        }
        /*      
               TODO: Bring CriticalHandle to CoreLib
               https://github.com/dotnet/corert/issues/2570

               public static bool IsCriticalHandle(TypeSystemContext context, TypeDesc type)
                {
                    return IsOrDerivesFromType(type, context.GetCriticalHandle());
                }

                TODO: Bring HandleRef to CoreLib
                public static bool IsHandleRef(TypeSystemContext context, TypeDesc type)
                {
                    return IsOrDerivesFromType(type, this.HandleRef);
                }
        */

        public static bool IsSystemArray(TypeSystemContext context, TypeDesc type)
        {
            return type == GetSystemArray(context);
        }

        public static bool IsSystemDateTime(TypeSystemContext context, TypeDesc type)
        {
            return type == GetSystemDateTime(context);
        }

        public static bool IsStringBuilder(TypeSystemContext context, TypeDesc type)
        {
            Debug.Assert(type != null);
            return type == GetStringBuilder(context, throwIfNotFound: false);
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