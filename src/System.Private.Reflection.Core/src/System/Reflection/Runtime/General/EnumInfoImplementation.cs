// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using EnumInfo = Internal.Runtime.Augments.EnumInfo;

namespace System.Reflection.Runtime.General
{
    internal sealed class EnumInfoImplementation : EnumInfo
    {
        internal EnumInfoImplementation(Type enumType)
        {
            _underlyingType = Enum.GetUnderlyingType(enumType);

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
            _namesAndValues = namesAndValues;

            // Create the unboxed version of values for the Values property to return. (We didn't do this earlier because
            // declaring "rawValues" as "Array" would prevent us from using the generic overload of Array.Sort()).
            //
            // The array element type is the underlying type, not the enum type. (The enum type could be an open generic.)
            _values = Array.CreateInstance(_underlyingType, numValues);
            Array.Copy(rawValues, _values, numValues);

            _hasFlagsAttribute = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);
        }

        public sealed override Type UnderlyingType => _underlyingType;
        public sealed override Array Values => _values;
        public sealed override KeyValuePair<string, ulong>[] NamesAndValues => _namesAndValues;
        public sealed override bool HasFlagsAttribute => _hasFlagsAttribute;

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

        private readonly Type _underlyingType;
        private readonly Array _values;
        private readonly KeyValuePair<string, ulong>[] _namesAndValues;
        private readonly bool _hasFlagsAttribute;
    }
}
