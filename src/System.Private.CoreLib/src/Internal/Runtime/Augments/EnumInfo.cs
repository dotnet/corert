// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    [ReflectionBlocked]
    public sealed class EnumInfo
    {
        public EnumInfo(Type enumType)
        {
            Debug.Assert(enumType != null);
            Debug.Assert(enumType.IsRuntimeImplemented());
            Debug.Assert(enumType.IsEnum);

            UnderlyingType = ComputeUnderlyingType(enumType);

            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            int numValues = fields.Length;
            object[] rawValues = new object[numValues];
            KeyValuePair<string, ulong>[] namesAndValues = new KeyValuePair<string, ulong>[numValues];
            for (int i = 0; i < numValues; i++)
            {
                FieldInfo field = fields[i];
                object rawValue = field.GetRawConstantValue();
                rawValues[i] = rawValue;

                ulong rawUnboxedValue;
                if (rawValue is ulong)
                {
                    rawUnboxedValue = (ulong)rawValue;
                }
                else
                {
                    // This conversion is this way for compatibility: do a value-preseving cast to long - then store (and compare) as ulong. This affects
                    // the order in which the Enum apis return names and values.
                    rawUnboxedValue = (ulong)(((IConvertible)rawValue).ToInt64(null));
                }
                namesAndValues[i] = new KeyValuePair<string, ulong>(field.Name, rawUnboxedValue);
            }

            Array.Sort(keys: namesAndValues, items: rawValues, comparer: NamesAndValueComparer.Default);
            NamesAndValues = namesAndValues;

            // Create the unboxed version of values for the Values property to return. (We didn't do this earlier because
            // declaring "rawValues" as "Array" would prevent us from using the generic overload of Array.Sort()).
            //
            // The array element type is the underlying type, not the enum type. (The enum type could be an open generic.)
            Values = Array.CreateInstance(UnderlyingType, numValues);
            Array.Copy(rawValues, Values, numValues);

            HasFlagsAttribute = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);
        }

        internal Type UnderlyingType { get; }
        internal Array Values { get; }
        internal KeyValuePair<string, ulong>[] NamesAndValues { get; }
        internal bool HasFlagsAttribute { get; }

        private static RuntimeImports.RhCorElementType ComputeCorElementType(Type enumType)
        {
            if (enumType.ContainsGenericParameters)
            {
                // This is an open generic enum (typeof(Outer<>).NestedEnum). We cannot safely call EETypePtr.CorElementType for this case so fall back to Reflection.
                FieldInfo[] candidates = enumType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (candidates.Length == 0)
                    throw RuntimeAugments.Callbacks.CreateMissingMetadataException(enumType); // Most likely cause.
                if (candidates.Length > 1)
                    throw new BadImageFormatException();
                enumType = candidates[0].FieldType;
            }
            return enumType.TypeHandle.ToEETypePtr().CorElementType;
        }

        private static Type ComputeUnderlyingType(Type enumType)
        {
            RuntimeImports.RhCorElementType corElementType = ComputeCorElementType(enumType);
            switch (corElementType)
            {
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                    return CommonRuntimeTypes.Boolean;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                    return CommonRuntimeTypes.Char;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                    return CommonRuntimeTypes.SByte;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                    return CommonRuntimeTypes.Byte;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                    return CommonRuntimeTypes.Int16;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                    return CommonRuntimeTypes.UInt16;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                    return CommonRuntimeTypes.Int32;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                    return CommonRuntimeTypes.UInt32;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                    return CommonRuntimeTypes.Int64;
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                    return CommonRuntimeTypes.UInt64;
                default:
                    throw new NotSupportedException();
            }
        }

        //
        // Sort comparer for NamesAndValues
        //
        private sealed class NamesAndValueComparer : IComparer<KeyValuePair<string, ulong>>
        {
            public int Compare(KeyValuePair<string, ulong> kv1, KeyValuePair<string, ulong> kv2)
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

            public static IComparer<KeyValuePair<string, ulong>> Default = new NamesAndValueComparer();
        }
    }
}
