// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// NOTE: This is a generated file - do not manually edit!

#pragma warning disable 649

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Internal.NativeFormat;
using Debug = System.Diagnostics.Debug;

namespace Internal.Metadata.NativeFormat
{
    internal static partial class MdBinaryReader
    {
        public static uint Read(this NativeReader reader, uint offset, out bool[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new bool[count];
            for (uint i = 0; i < count; ++i)
            {
                bool tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out char[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new char[count];
            for (uint i = 0; i < count; ++i)
            {
                char tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out string[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new string[count];
            for (uint i = 0; i < count; ++i)
            {
                string tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out byte[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new byte[count];
            for (uint i = 0; i < count; ++i)
            {
                byte tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out sbyte[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new sbyte[count];
            for (uint i = 0; i < count; ++i)
            {
                sbyte tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out short[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new short[count];
            for (uint i = 0; i < count; ++i)
            {
                short tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ushort[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new ushort[count];
            for (uint i = 0; i < count; ++i)
            {
                ushort tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out int[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new int[count];
            for (uint i = 0; i < count; ++i)
            {
                int tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out uint[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new uint[count];
            for (uint i = 0; i < count; ++i)
            {
                uint tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out long[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new long[count];
            for (uint i = 0; i < count; ++i)
            {
                long tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ulong[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new ulong[count];
            for (uint i = 0; i < count; ++i)
            {
                ulong tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out float[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new float[count];
            for (uint i = 0; i < count; ++i)
            {
                float tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out double[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            values = new double[count];
            for (uint i = 0; i < count; ++i)
            {
                double tmp;
                offset = reader.Read(offset, out tmp);
                values[i] = tmp;
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out AssemblyFlags value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (AssemblyFlags)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out AssemblyHashAlgorithm value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (AssemblyHashAlgorithm)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out CallingConventions value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (CallingConventions)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out EventAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (EventAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FieldAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (FieldAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FixedArgumentAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (FixedArgumentAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out GenericParameterAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (GenericParameterAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out GenericParameterKind value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (GenericParameterKind)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (MethodAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodImplAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (MethodImplAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodSemanticsAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (MethodSemanticsAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out NamedArgumentMemberKind value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (NamedArgumentMemberKind)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ParameterAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (ParameterAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PInvokeAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (PInvokeAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PropertyAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (PropertyAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeAttributes value)
        {
            uint ivalue;
            offset = reader.DecodeUnsigned(offset, out ivalue);
            value = (TypeAttributes)ivalue;
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out Handle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<Handle>();
            }
            else
            #endif
            {
                values = new Handle[count];
                for (uint i = 0; i < count; ++i)
                {
                    Handle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ArraySignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ArraySignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ArraySignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ArraySignatureHandle>();
            }
            else
            #endif
            {
                values = new ArraySignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ArraySignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ByReferenceSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ByReferenceSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ByReferenceSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ByReferenceSignatureHandle>();
            }
            else
            #endif
            {
                values = new ByReferenceSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ByReferenceSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantBooleanArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantBooleanArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantBooleanArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantBooleanArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantBooleanArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantBooleanArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantBooleanValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantBooleanValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantBooleanValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantBooleanValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantBooleanValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantBooleanValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantBoxedEnumValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantBoxedEnumValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantBoxedEnumValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantBoxedEnumValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantBoxedEnumValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantBoxedEnumValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantByteArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantByteArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantByteArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantByteArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantByteArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantByteArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantByteValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantByteValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantByteValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantByteValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantByteValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantByteValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantCharArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantCharArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantCharArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantCharArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantCharArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantCharArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantCharValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantCharValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantCharValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantCharValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantCharValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantCharValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantDoubleArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantDoubleArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantDoubleArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantDoubleArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantDoubleArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantDoubleArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantDoubleValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantDoubleValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantDoubleValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantDoubleValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantDoubleValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantDoubleValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantHandleArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantHandleArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantHandleArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantHandleArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantHandleArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantHandleArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt16ArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantInt16ArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt16ArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantInt16ArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantInt16ArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantInt16ArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt16ValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantInt16ValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt16ValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantInt16ValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantInt16ValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantInt16ValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt32ArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantInt32ArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt32ArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantInt32ArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantInt32ArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantInt32ArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt32ValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantInt32ValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt32ValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantInt32ValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantInt32ValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantInt32ValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt64ArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantInt64ArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt64ArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantInt64ArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantInt64ArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantInt64ArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt64ValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantInt64ValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantInt64ValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantInt64ValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantInt64ValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantInt64ValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantReferenceValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantReferenceValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantReferenceValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantReferenceValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantReferenceValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantReferenceValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSByteArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantSByteArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSByteArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantSByteArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantSByteArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantSByteArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSByteValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantSByteValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSByteValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantSByteValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantSByteValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantSByteValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSingleArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantSingleArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSingleArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantSingleArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantSingleArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantSingleArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSingleValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantSingleValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantSingleValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantSingleValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantSingleValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantSingleValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantStringArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantStringArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantStringArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantStringArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantStringArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantStringArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantStringValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantStringValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantStringValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantStringValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantStringValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantStringValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt16ArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantUInt16ArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt16ArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantUInt16ArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantUInt16ArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantUInt16ArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt16ValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantUInt16ValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt16ValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantUInt16ValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantUInt16ValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantUInt16ValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt32ArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantUInt32ArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt32ArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantUInt32ArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantUInt32ArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantUInt32ArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt32ValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantUInt32ValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt32ValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantUInt32ValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantUInt32ValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantUInt32ValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt64ArrayHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantUInt64ArrayHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt64ArrayHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantUInt64ArrayHandle>();
            }
            else
            #endif
            {
                values = new ConstantUInt64ArrayHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantUInt64ArrayHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt64ValueHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ConstantUInt64ValueHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ConstantUInt64ValueHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ConstantUInt64ValueHandle>();
            }
            else
            #endif
            {
                values = new ConstantUInt64ValueHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ConstantUInt64ValueHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out CustomAttributeHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new CustomAttributeHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out CustomAttributeHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<CustomAttributeHandle>();
            }
            else
            #endif
            {
                values = new CustomAttributeHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    CustomAttributeHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out CustomModifierHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new CustomModifierHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out CustomModifierHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<CustomModifierHandle>();
            }
            else
            #endif
            {
                values = new CustomModifierHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    CustomModifierHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out EventHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new EventHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out EventHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<EventHandle>();
            }
            else
            #endif
            {
                values = new EventHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    EventHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FieldHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new FieldHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FieldHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<FieldHandle>();
            }
            else
            #endif
            {
                values = new FieldHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    FieldHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FieldSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new FieldSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FieldSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<FieldSignatureHandle>();
            }
            else
            #endif
            {
                values = new FieldSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    FieldSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FixedArgumentHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new FixedArgumentHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out FixedArgumentHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<FixedArgumentHandle>();
            }
            else
            #endif
            {
                values = new FixedArgumentHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    FixedArgumentHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out GenericParameterHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new GenericParameterHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out GenericParameterHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<GenericParameterHandle>();
            }
            else
            #endif
            {
                values = new GenericParameterHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    GenericParameterHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MemberReferenceHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new MemberReferenceHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MemberReferenceHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<MemberReferenceHandle>();
            }
            else
            #endif
            {
                values = new MemberReferenceHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    MemberReferenceHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new MethodHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<MethodHandle>();
            }
            else
            #endif
            {
                values = new MethodHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    MethodHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodImplHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new MethodImplHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodImplHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<MethodImplHandle>();
            }
            else
            #endif
            {
                values = new MethodImplHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    MethodImplHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodInstantiationHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new MethodInstantiationHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodInstantiationHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<MethodInstantiationHandle>();
            }
            else
            #endif
            {
                values = new MethodInstantiationHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    MethodInstantiationHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodSemanticsHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new MethodSemanticsHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodSemanticsHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<MethodSemanticsHandle>();
            }
            else
            #endif
            {
                values = new MethodSemanticsHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    MethodSemanticsHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new MethodSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<MethodSignatureHandle>();
            }
            else
            #endif
            {
                values = new MethodSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    MethodSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodTypeVariableSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new MethodTypeVariableSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out MethodTypeVariableSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<MethodTypeVariableSignatureHandle>();
            }
            else
            #endif
            {
                values = new MethodTypeVariableSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    MethodTypeVariableSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out NamedArgumentHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new NamedArgumentHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out NamedArgumentHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<NamedArgumentHandle>();
            }
            else
            #endif
            {
                values = new NamedArgumentHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    NamedArgumentHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out NamespaceDefinitionHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new NamespaceDefinitionHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out NamespaceDefinitionHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<NamespaceDefinitionHandle>();
            }
            else
            #endif
            {
                values = new NamespaceDefinitionHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    NamespaceDefinitionHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out NamespaceReferenceHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new NamespaceReferenceHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out NamespaceReferenceHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<NamespaceReferenceHandle>();
            }
            else
            #endif
            {
                values = new NamespaceReferenceHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    NamespaceReferenceHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ParameterHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ParameterHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ParameterHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ParameterHandle>();
            }
            else
            #endif
            {
                values = new ParameterHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ParameterHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ParameterTypeSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ParameterTypeSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ParameterTypeSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ParameterTypeSignatureHandle>();
            }
            else
            #endif
            {
                values = new ParameterTypeSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ParameterTypeSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PointerSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new PointerSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PointerSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<PointerSignatureHandle>();
            }
            else
            #endif
            {
                values = new PointerSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    PointerSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PropertyHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new PropertyHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PropertyHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<PropertyHandle>();
            }
            else
            #endif
            {
                values = new PropertyHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    PropertyHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PropertySignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new PropertySignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out PropertySignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<PropertySignatureHandle>();
            }
            else
            #endif
            {
                values = new PropertySignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    PropertySignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out QualifiedFieldHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new QualifiedFieldHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out QualifiedFieldHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<QualifiedFieldHandle>();
            }
            else
            #endif
            {
                values = new QualifiedFieldHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    QualifiedFieldHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out QualifiedMethodHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new QualifiedMethodHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out QualifiedMethodHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<QualifiedMethodHandle>();
            }
            else
            #endif
            {
                values = new QualifiedMethodHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    QualifiedMethodHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ReturnTypeSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ReturnTypeSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ReturnTypeSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ReturnTypeSignatureHandle>();
            }
            else
            #endif
            {
                values = new ReturnTypeSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ReturnTypeSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out SZArraySignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new SZArraySignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out SZArraySignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<SZArraySignatureHandle>();
            }
            else
            #endif
            {
                values = new SZArraySignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    SZArraySignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ScopeDefinitionHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ScopeDefinitionHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ScopeDefinitionHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ScopeDefinitionHandle>();
            }
            else
            #endif
            {
                values = new ScopeDefinitionHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ScopeDefinitionHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ScopeReferenceHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new ScopeReferenceHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out ScopeReferenceHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<ScopeReferenceHandle>();
            }
            else
            #endif
            {
                values = new ScopeReferenceHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    ScopeReferenceHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeDefinitionHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new TypeDefinitionHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeDefinitionHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<TypeDefinitionHandle>();
            }
            else
            #endif
            {
                values = new TypeDefinitionHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    TypeDefinitionHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeForwarderHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new TypeForwarderHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeForwarderHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<TypeForwarderHandle>();
            }
            else
            #endif
            {
                values = new TypeForwarderHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    TypeForwarderHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeInstantiationSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new TypeInstantiationSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeInstantiationSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<TypeInstantiationSignatureHandle>();
            }
            else
            #endif
            {
                values = new TypeInstantiationSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    TypeInstantiationSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeReferenceHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new TypeReferenceHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeReferenceHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<TypeReferenceHandle>();
            }
            else
            #endif
            {
                values = new TypeReferenceHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    TypeReferenceHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeSpecificationHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new TypeSpecificationHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeSpecificationHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<TypeSpecificationHandle>();
            }
            else
            #endif
            {
                values = new TypeSpecificationHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    TypeSpecificationHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeVariableSignatureHandle handle)
        {
            uint value;
            offset = reader.DecodeUnsigned(offset, out value);
            handle = new TypeVariableSignatureHandle((int)value);
            handle._Validate();
            return offset;
        } // Read

        public static uint Read(this NativeReader reader, uint offset, out TypeVariableSignatureHandle[] values)
        {
            uint count;
            offset = reader.DecodeUnsigned(offset, out count);
            #if !NETFX_45
            if (count == 0)
            {
                values = Array.Empty<TypeVariableSignatureHandle>();
            }
            else
            #endif
            {
                values = new TypeVariableSignatureHandle[count];
                for (uint i = 0; i < count; ++i)
                {
                    TypeVariableSignatureHandle tmp;
                    offset = reader.Read(offset, out tmp);
                    values[i] = tmp;
                }
            }
            return offset;
        } // Read
    } // MdBinaryReader
} // Internal.Metadata.NativeFormat
