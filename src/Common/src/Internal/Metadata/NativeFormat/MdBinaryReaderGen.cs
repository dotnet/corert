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
    /// <summary>
    /// MdBinaryReader
    /// </summary>
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
            if (count == 0)
            {
                values = s_emptyHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyArraySignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyByReferenceSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantBooleanArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantBooleanValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantBoxedEnumValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantByteArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantByteValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantCharArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantCharValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantDoubleArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantDoubleValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantHandleArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantInt16ArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantInt16ValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantInt32ArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantInt32ValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantInt64ArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantInt64ValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantReferenceValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantSByteArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantSByteValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantSingleArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantSingleValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantStringArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantStringValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantUInt16ArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantUInt16ValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantUInt32ArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantUInt32ValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantUInt64ArrayHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyConstantUInt64ValueHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyCustomAttributeHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyCustomModifierHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyEventHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyFieldHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyFieldSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyFixedArgumentHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyGenericParameterHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyMemberReferenceHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyMethodHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyMethodImplHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyMethodInstantiationHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyMethodSemanticsHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyMethodSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyMethodTypeVariableSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyNamedArgumentHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyNamespaceDefinitionHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyNamespaceReferenceHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyParameterHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyParameterTypeSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyPointerSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyPropertyHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyPropertySignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyQualifiedFieldHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyQualifiedMethodHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyReturnTypeSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptySZArraySignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyScopeDefinitionHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyScopeReferenceHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyTypeDefinitionHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyTypeForwarderHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyTypeInstantiationSignatureHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyTypeReferenceHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyTypeSpecificationHandleArray;
            }
            else
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
            if (count == 0)
            {
                values = s_emptyTypeVariableSignatureHandleArray;
            }
            else
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

        private static Handle[] s_emptyHandleArray = new Handle[0];

        private static ArraySignatureHandle[] s_emptyArraySignatureHandleArray = new ArraySignatureHandle[0];

        private static ByReferenceSignatureHandle[] s_emptyByReferenceSignatureHandleArray = new ByReferenceSignatureHandle[0];

        private static ConstantBooleanArrayHandle[] s_emptyConstantBooleanArrayHandleArray = new ConstantBooleanArrayHandle[0];

        private static ConstantBooleanValueHandle[] s_emptyConstantBooleanValueHandleArray = new ConstantBooleanValueHandle[0];

        private static ConstantBoxedEnumValueHandle[] s_emptyConstantBoxedEnumValueHandleArray = new ConstantBoxedEnumValueHandle[0];

        private static ConstantByteArrayHandle[] s_emptyConstantByteArrayHandleArray = new ConstantByteArrayHandle[0];

        private static ConstantByteValueHandle[] s_emptyConstantByteValueHandleArray = new ConstantByteValueHandle[0];

        private static ConstantCharArrayHandle[] s_emptyConstantCharArrayHandleArray = new ConstantCharArrayHandle[0];

        private static ConstantCharValueHandle[] s_emptyConstantCharValueHandleArray = new ConstantCharValueHandle[0];

        private static ConstantDoubleArrayHandle[] s_emptyConstantDoubleArrayHandleArray = new ConstantDoubleArrayHandle[0];

        private static ConstantDoubleValueHandle[] s_emptyConstantDoubleValueHandleArray = new ConstantDoubleValueHandle[0];

        private static ConstantHandleArrayHandle[] s_emptyConstantHandleArrayHandleArray = new ConstantHandleArrayHandle[0];

        private static ConstantInt16ArrayHandle[] s_emptyConstantInt16ArrayHandleArray = new ConstantInt16ArrayHandle[0];

        private static ConstantInt16ValueHandle[] s_emptyConstantInt16ValueHandleArray = new ConstantInt16ValueHandle[0];

        private static ConstantInt32ArrayHandle[] s_emptyConstantInt32ArrayHandleArray = new ConstantInt32ArrayHandle[0];

        private static ConstantInt32ValueHandle[] s_emptyConstantInt32ValueHandleArray = new ConstantInt32ValueHandle[0];

        private static ConstantInt64ArrayHandle[] s_emptyConstantInt64ArrayHandleArray = new ConstantInt64ArrayHandle[0];

        private static ConstantInt64ValueHandle[] s_emptyConstantInt64ValueHandleArray = new ConstantInt64ValueHandle[0];

        private static ConstantReferenceValueHandle[] s_emptyConstantReferenceValueHandleArray = new ConstantReferenceValueHandle[0];

        private static ConstantSByteArrayHandle[] s_emptyConstantSByteArrayHandleArray = new ConstantSByteArrayHandle[0];

        private static ConstantSByteValueHandle[] s_emptyConstantSByteValueHandleArray = new ConstantSByteValueHandle[0];

        private static ConstantSingleArrayHandle[] s_emptyConstantSingleArrayHandleArray = new ConstantSingleArrayHandle[0];

        private static ConstantSingleValueHandle[] s_emptyConstantSingleValueHandleArray = new ConstantSingleValueHandle[0];

        private static ConstantStringArrayHandle[] s_emptyConstantStringArrayHandleArray = new ConstantStringArrayHandle[0];

        private static ConstantStringValueHandle[] s_emptyConstantStringValueHandleArray = new ConstantStringValueHandle[0];

        private static ConstantUInt16ArrayHandle[] s_emptyConstantUInt16ArrayHandleArray = new ConstantUInt16ArrayHandle[0];

        private static ConstantUInt16ValueHandle[] s_emptyConstantUInt16ValueHandleArray = new ConstantUInt16ValueHandle[0];

        private static ConstantUInt32ArrayHandle[] s_emptyConstantUInt32ArrayHandleArray = new ConstantUInt32ArrayHandle[0];

        private static ConstantUInt32ValueHandle[] s_emptyConstantUInt32ValueHandleArray = new ConstantUInt32ValueHandle[0];

        private static ConstantUInt64ArrayHandle[] s_emptyConstantUInt64ArrayHandleArray = new ConstantUInt64ArrayHandle[0];

        private static ConstantUInt64ValueHandle[] s_emptyConstantUInt64ValueHandleArray = new ConstantUInt64ValueHandle[0];

        private static CustomAttributeHandle[] s_emptyCustomAttributeHandleArray = new CustomAttributeHandle[0];

        private static CustomModifierHandle[] s_emptyCustomModifierHandleArray = new CustomModifierHandle[0];

        private static EventHandle[] s_emptyEventHandleArray = new EventHandle[0];

        private static FieldHandle[] s_emptyFieldHandleArray = new FieldHandle[0];

        private static FieldSignatureHandle[] s_emptyFieldSignatureHandleArray = new FieldSignatureHandle[0];

        private static FixedArgumentHandle[] s_emptyFixedArgumentHandleArray = new FixedArgumentHandle[0];

        private static GenericParameterHandle[] s_emptyGenericParameterHandleArray = new GenericParameterHandle[0];

        private static MemberReferenceHandle[] s_emptyMemberReferenceHandleArray = new MemberReferenceHandle[0];

        private static MethodHandle[] s_emptyMethodHandleArray = new MethodHandle[0];

        private static MethodImplHandle[] s_emptyMethodImplHandleArray = new MethodImplHandle[0];

        private static MethodInstantiationHandle[] s_emptyMethodInstantiationHandleArray = new MethodInstantiationHandle[0];

        private static MethodSemanticsHandle[] s_emptyMethodSemanticsHandleArray = new MethodSemanticsHandle[0];

        private static MethodSignatureHandle[] s_emptyMethodSignatureHandleArray = new MethodSignatureHandle[0];

        private static MethodTypeVariableSignatureHandle[] s_emptyMethodTypeVariableSignatureHandleArray = new MethodTypeVariableSignatureHandle[0];

        private static NamedArgumentHandle[] s_emptyNamedArgumentHandleArray = new NamedArgumentHandle[0];

        private static NamespaceDefinitionHandle[] s_emptyNamespaceDefinitionHandleArray = new NamespaceDefinitionHandle[0];

        private static NamespaceReferenceHandle[] s_emptyNamespaceReferenceHandleArray = new NamespaceReferenceHandle[0];

        private static ParameterHandle[] s_emptyParameterHandleArray = new ParameterHandle[0];

        private static ParameterTypeSignatureHandle[] s_emptyParameterTypeSignatureHandleArray = new ParameterTypeSignatureHandle[0];

        private static PointerSignatureHandle[] s_emptyPointerSignatureHandleArray = new PointerSignatureHandle[0];

        private static PropertyHandle[] s_emptyPropertyHandleArray = new PropertyHandle[0];

        private static PropertySignatureHandle[] s_emptyPropertySignatureHandleArray = new PropertySignatureHandle[0];

        private static QualifiedFieldHandle[] s_emptyQualifiedFieldHandleArray = new QualifiedFieldHandle[0];

        private static QualifiedMethodHandle[] s_emptyQualifiedMethodHandleArray = new QualifiedMethodHandle[0];

        private static ReturnTypeSignatureHandle[] s_emptyReturnTypeSignatureHandleArray = new ReturnTypeSignatureHandle[0];

        private static SZArraySignatureHandle[] s_emptySZArraySignatureHandleArray = new SZArraySignatureHandle[0];

        private static ScopeDefinitionHandle[] s_emptyScopeDefinitionHandleArray = new ScopeDefinitionHandle[0];

        private static ScopeReferenceHandle[] s_emptyScopeReferenceHandleArray = new ScopeReferenceHandle[0];

        private static TypeDefinitionHandle[] s_emptyTypeDefinitionHandleArray = new TypeDefinitionHandle[0];

        private static TypeForwarderHandle[] s_emptyTypeForwarderHandleArray = new TypeForwarderHandle[0];

        private static TypeInstantiationSignatureHandle[] s_emptyTypeInstantiationSignatureHandleArray = new TypeInstantiationSignatureHandle[0];

        private static TypeReferenceHandle[] s_emptyTypeReferenceHandleArray = new TypeReferenceHandle[0];

        private static TypeSpecificationHandle[] s_emptyTypeSpecificationHandleArray = new TypeSpecificationHandle[0];

        private static TypeVariableSignatureHandle[] s_emptyTypeVariableSignatureHandleArray = new TypeVariableSignatureHandle[0];
    } // MdBinaryReader
} // Internal.Metadata.NativeFormat
