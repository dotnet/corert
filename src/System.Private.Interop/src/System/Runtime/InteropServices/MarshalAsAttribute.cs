// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.ReturnValue, Inherited = false)]
    public unsafe sealed class MarshalAsAttribute : Attribute
    {
        internal UnmanagedType _val;
        public MarshalAsAttribute(UnmanagedType unmanagedType)
        {
            _val = unmanagedType;
        }
        public MarshalAsAttribute(short unmanagedType)
        {
            _val = (UnmanagedType)unmanagedType;
        }
        public UnmanagedType Value { get { return _val; } }

        // Fields used with SubType = SafeArray.
        public VarEnum SafeArraySubType;
        public Type SafeArrayUserDefinedSubType;

        // Field used with iid_is attribute (interface pointers).
        public int IidParameterIndex;

        // Fields used with SubType = ByValArray and LPArray.
        // Array size =  parameter(PI) * PM + C
        public UnmanagedType ArraySubType;
        public short SizeParamIndex;           // param index PI
        public int SizeConst;                // constant C

        // Fields used with SubType = CustomMarshaler
        public String MarshalType;              // Name of marshaler class
        public Type MarshalTypeRef;           // Type of marshaler class
        public String MarshalCookie;            // cookie to pass to marshaler
    }
}
