// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution
{
    internal sealed class EnumInfoImplementation : EnumInfo
    {
        public EnumInfoImplementation(Type enumType, MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle)
        {
            _enumType = enumType;
            _reader = reader;
            _typeDefinition = typeDefinitionHandle.GetTypeDefinition(reader);
        }

        public sealed override Type UnderlyingType
        {
            get
            {
                return Enum.GetUnderlyingType(_enumType);
            }
        }

        public sealed override Array Values
        {
            get
            {
                if (_lazyValues == null)
                {
                    RuntimeTypeHandle underlyingTypeHandle = Enum.GetUnderlyingType(_enumType).TypeHandle;
                    KeyValuePair<String, ulong>[] namesAndValues = this.NamesAndValues;
                    int count = namesAndValues.Length;
                    if (underlyingTypeHandle.Equals(typeof(Boolean).TypeHandle))
                    {
                        Boolean[] a = new Boolean[count];
                        for (int i = 0; i < count; i++)
                            a[i] = namesAndValues[i].Value != 0 ? true : false;
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(Byte).TypeHandle))
                    {
                        byte[] a = new byte[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((byte)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(SByte).TypeHandle))
                    {
                        sbyte[] a = new sbyte[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((sbyte)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(UInt16).TypeHandle))
                    {
                        ushort[] a = new ushort[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((ushort)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(Int16).TypeHandle))
                    {
                        short[] a = new short[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((short)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(Char).TypeHandle))
                    {
                        char[] a = new char[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((char)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(UInt32).TypeHandle))
                    {
                        uint[] a = new uint[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((uint)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(Int32).TypeHandle))
                    {
                        int[] a = new int[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((int)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(UInt64).TypeHandle))
                    {
                        ulong[] a = new ulong[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((ulong)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else if (underlyingTypeHandle.Equals(typeof(Int64).TypeHandle))
                    {
                        long[] a = new long[count];
                        for (int i = 0; i < count; i++)
                            a[i] = unchecked((long)(namesAndValues[i].Value));
                        _lazyValues = a;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                return _lazyValues;
            }
        }

        //
        // This returns the underlying enum values as "ulong" regardless of the actual underlying type. We first do a value-preserving
        // cast to long, then sort it as a ulong.
        //
        public sealed override KeyValuePair<String, ulong>[] NamesAndValues
        {
            get
            {
                if (_lazyNamesAndValues == null)
                {
                    LowLevelList<KeyValuePair<String, ulong>> namesAndUnboxedValues = new LowLevelList<KeyValuePair<String, ulong>>();
                    MetadataReader reader = _reader;
                    foreach (FieldHandle fieldHandle in _typeDefinition.Fields)
                    {
                        Field field = fieldHandle.GetField(reader);
                        if (0 != (field.Flags & FieldAttributes.Static))
                        {
                            String name = field.Name.GetString(reader);
                            Handle valueHandle = field.DefaultValue;
                            ulong lValue = ReadUnboxedEnumValue(reader, valueHandle);
                            namesAndUnboxedValues.Add(new KeyValuePair<String, ulong>(name, lValue));
                        }
                    }

                    KeyValuePair<String, ulong>[] sortedNamesAndUnboxedValues = namesAndUnboxedValues.ToArray();
                    Array.Sort<KeyValuePair<String, ulong>>(sortedNamesAndUnboxedValues, new NamesAndValueComparer());
                    _lazyNamesAndValues = sortedNamesAndUnboxedValues;
                }
                return _lazyNamesAndValues;
            }
        }

        public sealed override bool HasFlagsAttribute
        {
            get
            {
                EnumInfoFlags flags = this.Flags;
                return 0 != (flags & EnumInfoFlags.HasFlagsAttribute);
            }
        }

        //
        // Sort comparer for NamesAndValues
        //
        private sealed class NamesAndValueComparer : IComparer<KeyValuePair<String, ulong>>
        {
            public int Compare(KeyValuePair<String, ulong> kv1, KeyValuePair<String, ulong> kv2)
            {
                ulong x = kv1.Value;
                ulong y = kv2.Value;
                if (x < y)
                    return -1;
                else if (x > y)
                    return 1;
                else
                    return 0;
            }
        }

        //
        // This returns the underlying enum values as "ulong" regardless of the actual underlying type. Signed integral types 
        // get sign-extended into the 64-bit value, unsigned types get zero-extended.
        //
        private static ulong ReadUnboxedEnumValue(MetadataReader reader, Handle valueHandle)
        {
            HandleType handleType = valueHandle.HandleType;
            switch (handleType)
            {
                case HandleType.ConstantBooleanValue:
                    {
                        bool v = valueHandle.ToConstantBooleanValueHandle(reader).GetConstantBooleanValue(reader).Value;
                        return v ? 1UL : 0UL;
                    }

                case HandleType.ConstantCharValue:
                    {
                        char v = valueHandle.ToConstantCharValueHandle(reader).GetConstantCharValue(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantByteValue:
                    {
                        byte v = valueHandle.ToConstantByteValueHandle(reader).GetConstantByteValue(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantSByteValue:
                    {
                        sbyte v = valueHandle.ToConstantSByteValueHandle(reader).GetConstantSByteValue(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantUInt16Value:
                    {
                        UInt16 v = valueHandle.ToConstantUInt16ValueHandle(reader).GetConstantUInt16Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantInt16Value:
                    {
                        Int16 v = valueHandle.ToConstantInt16ValueHandle(reader).GetConstantInt16Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantUInt32Value:
                    {
                        UInt32 v = valueHandle.ToConstantUInt32ValueHandle(reader).GetConstantUInt32Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantInt32Value:
                    {
                        Int32 v = valueHandle.ToConstantInt32ValueHandle(reader).GetConstantInt32Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantUInt64Value:
                    {
                        UInt64 v = valueHandle.ToConstantUInt64ValueHandle(reader).GetConstantUInt64Value(reader).Value;
                        return (ulong)(long)v;
                    }

                case HandleType.ConstantInt64Value:
                    {
                        Int64 v = valueHandle.ToConstantInt64ValueHandle(reader).GetConstantInt64Value(reader).Value;
                        return (ulong)(long)v;
                    }

                default:
                    throw new BadImageFormatException();
            }
        }

        private Type _enumType;
        private MetadataReader _reader;
        private TypeDefinition _typeDefinition;

        private volatile Array _lazyValues;
        private volatile KeyValuePair<String, ulong>[] _lazyNamesAndValues;

        private EnumInfoFlags Flags
        {
            get
            {
                if (_lazyEnumInfoFlags == 0)
                {
                    EnumInfoFlags flags = EnumInfoFlags.Computed;

                    Type flagsAttributeType = typeof(FlagsAttribute);
                    foreach (CustomAttributeData cad in _enumType.CustomAttributes)
                    {
                        if (cad.AttributeType.Equals(flagsAttributeType))
                        {
                            flags |= EnumInfoFlags.HasFlagsAttribute;
                            break;
                        }
                    }

                    _lazyEnumInfoFlags = flags;
                }
                return _lazyEnumInfoFlags;
            }
        }



        [Flags]
        private enum EnumInfoFlags : uint
        {
            Computed = 0x00000001,        // always set (to distinguish between computed and not computed)
            HasFlagsAttribute = 0x00000002,
        }

        private volatile EnumInfoFlags _lazyEnumInfoFlags;
    }
}
