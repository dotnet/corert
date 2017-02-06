// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using System.Reflection;
using Internal.IL;

namespace Internal.TypeSystem.Interop
{
    public class PInvokeMethodData
    {
        public MethodDesc TargetMethod { get; }
        public PInvokeILEmitterConfiguration PInvokeILEmitterConfiguration { get;  }
        public TypeSystemContext Context { get; }
        public PInvokeMetadata ImportMetadata { get; }

        public PInvokeMethodData(MethodDesc method, PInvokeILEmitterConfiguration config)
        {
            TargetMethod = method;
            PInvokeILEmitterConfiguration = config;
            Context = method.Context;
            ImportMetadata = method.GetPInvokeMethodMetadata();
        }

        public MetadataType SafeHandleType
        {
            get
            {
                return Context.SystemModule.GetKnownType("System.Runtime.InteropServices", "SafeHandle");
            }
        }

        public MetadataType PInvokeMarshal
        {
            get
            {
                return Context.SystemModule.GetKnownType("System.Runtime.InteropServices", "PInvokeMarshal");
            }
        }

/*      
        TODO: Bring CriticalHandle to CoreLib
        https://github.com/dotnet/corert/issues/2570

        public MetadataType CriticalHandle
        {
            get
            {
                return Context.SystemModule.GetKnownType("System.Runtime.InteropServices", "CriticalHandle");
            }
        }


        TODO: Bring HandleRef to CoreLib
        https://github.com/dotnet/corert/issues/2570

        public MetadataType HandleRef
        {
            get
            {
                return Context.SystemModule.GetKnownType("System.Runtime.InteropServices", "HandleRef");
            }
        }
*/
        public MetadataType StringBuilder
        {
            get
            {
                return Context.SystemModule.GetKnownType("System.Text", "StringBuilder");
            }
        }
        public MetadataType SystemArray
        {
            get
            {
                return Context.SystemModule.GetKnownType("System", "Array");
            }
        }

        public MetadataType SystemDateTime
        {
            get
            {
                return Context.SystemModule.GetKnownType("System", "DateTime");
            }
        }
        public MetadataType SystemDecimal
        {
            get
            {
                return Context.SystemModule.GetKnownType("System", "Decimal");
            }
        }
        public MetadataType SystemGuid
        {
            get
            {
                return Context.SystemModule.GetKnownType("System", "Guid");
            }
        }

        public bool IsSafeHandle(TypeDesc type)
        {
            return IsOfType(type, this.SafeHandleType);
        }

/*      
       TODO: Bring CriticalHandle to CoreLib
       https://github.com/dotnet/corert/issues/2570

       public bool IsCriticalHandle(TypeDesc type)
        {
            return IsOfType(type, this.CriticalHandle);
        }

        TODO: Bring HandleRef to CoreLib
        public bool IsHandleRef(TypeDesc type)
        {
            return IsOfType(type, this.HandleRef);
        }
*/

        public bool IsSystemArray(TypeDesc type)
        {
            return type == SystemArray;
        }

        public bool IsSystemDateTime(TypeDesc type)
        {
            return type == SystemDateTime;
        }

        public bool IsStringBuilder(TypeDesc type)
        {
            return type == StringBuilder;
        }
        public bool IsSystemDecimal(TypeDesc type)
        {
            return type == SystemDecimal;
        }

        public bool IsSystemGuid(TypeDesc type)
        {
            return type == SystemGuid;
        }

        public static bool IsOfType(TypeDesc type, MetadataType targetType)
        {
            while (type != null)
            {
                if (type == targetType)
                    return true;
                type = type.BaseType;
            }
            return false;
        }


        /// <summary>
        /// Charset for marshalling strings
        /// </summary>
        public PInvokeAttributes GetCharSet()
        {
            PInvokeAttributes charset = ImportMetadata.Attributes & PInvokeAttributes.CharSetMask;

            if (charset == 0)
            {
                // ECMA-335 II.10.1.5 - Default value is Ansi.
                charset = PInvokeAttributes.CharSetAnsi;
            }

            if (charset == PInvokeAttributes.CharSetAuto)
            {
                charset = PInvokeAttributes.CharSetUnicode;
            }

            return charset;
        }
    }
}