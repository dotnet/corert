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
        public MethodDesc TargetMethod { get; private set; }
        public PInvokeILEmitterConfiguration PInvokeILEmitterConfiguration { get; private set; }
        public TypeSystemContext Context { get; private set; }
        public PInvokeMetadata ImportMetadata { get; private set; }

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

        public bool IsSafeHandle(TypeDesc type)
        {
            var safeHandleType = this.SafeHandleType;
            while (type != null)
            {
                if (type == safeHandleType)
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