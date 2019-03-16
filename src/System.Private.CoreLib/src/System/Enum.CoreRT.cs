// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerServices;
using Internal.Runtime.Augments;
using Internal.Reflection.Augments;

using CorElementType = System.Runtime.RuntimeImports.RhCorElementType;

namespace System
{
    public abstract partial class Enum : ValueType, IComparable, IFormattable, IConvertible
    {
        private static TypeValuesAndNames GetCachedValuesAndNames(Type enumType, bool getNames)
        {
            TypeValuesAndNames entry = null;// EGOR: Should I add `GenericCache` property to Type? // = enumType.GenericCache as TypeValuesAndNames;
            if (entry == null || (getNames && entry.Names == null))
            {
                var info = GetEnumInfo(enumType);
                bool isFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);

                int nameAndValuesCount = info.NamesAndValues.Length;
                ulong[] values = new ulong[nameAndValuesCount];
                string[] names = new string[nameAndValuesCount];
                for (int i = 0; i < nameAndValuesCount; i++)
                {
                    KeyValuePair<string, ulong> kv = info.NamesAndValues[i];
                    values[i] = kv.Value;
                    names[i] = kv.Key;
                }

                entry = new TypeValuesAndNames(isFlags, values, names);
                // TODO: save to the cache
            }
            return entry;
        }

        private static Type ValidateRuntimeType(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));
            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, nameof(enumType));
            if (!enumType.IsRuntimeImplemented())
                throw new ArgumentException(SR.Arg_MustBeType, nameof(enumType));
            
            // Check for the unfortunate "typeof(Outer<>).InnerEnum" corner case.
            if (enumType.ContainsGenericParameters)
                throw new InvalidOperationException(SR.Format(SR.Arg_OpenType, enumType.ToString()));
            return enumType;
        }

        private static object InternalBoxEnum(Type enumType, long value)
        {
            return ToObject(enumType.TypeHandle.ToEETypePtr(), value);
        }

        private int InternalCompareTo(Enum enumObj, object target)
        {
            if (target == null)
                return 1;

            if (target == this)
                return 0;

            if (enumObj.EETypePtr != target.EETypePtr)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, target.GetType().ToString(), enumObj.GetType().ToString()));
            }

            ref byte pThisValue = ref enumObj.GetRawData();
            ref byte pTargetValue = ref target.GetRawData();

            switch (enumObj.EETypePtr.CorElementType)
            {
                case CorElementType.ELEMENT_TYPE_I1:
                    return (Unsafe.As<byte, sbyte>(ref pThisValue) == Unsafe.As<byte, sbyte>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, sbyte>(ref pThisValue) < Unsafe.As<byte, sbyte>(ref pTargetValue)) ? -1 : 1;

                case CorElementType.ELEMENT_TYPE_U1:
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return (Unsafe.As<byte, byte>(ref pThisValue) == Unsafe.As<byte, byte>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, byte>(ref pThisValue) < Unsafe.As<byte, byte>(ref pTargetValue)) ? -1 : 1;

                case CorElementType.ELEMENT_TYPE_I2:
                    return (Unsafe.As<byte, short>(ref pThisValue) == Unsafe.As<byte, short>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, short>(ref pThisValue) < Unsafe.As<byte, short>(ref pTargetValue)) ? -1 : 1;

                case CorElementType.ELEMENT_TYPE_U2:
                case CorElementType.ELEMENT_TYPE_CHAR:
                    return (Unsafe.As<byte, ushort>(ref pThisValue) == Unsafe.As<byte, ushort>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, ushort>(ref pThisValue) < Unsafe.As<byte, ushort>(ref pTargetValue)) ? -1 : 1;

                case CorElementType.ELEMENT_TYPE_I4:
                    return (Unsafe.As<byte, int>(ref pThisValue) == Unsafe.As<byte, int>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, int>(ref pThisValue) < Unsafe.As<byte, int>(ref pTargetValue)) ? -1 : 1;

                case CorElementType.ELEMENT_TYPE_U4:
                    return (Unsafe.As<byte, uint>(ref pThisValue) == Unsafe.As<byte, uint>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, uint>(ref pThisValue) < Unsafe.As<byte, uint>(ref pTargetValue)) ? -1 : 1;

                case CorElementType.ELEMENT_TYPE_I8:
                    return (Unsafe.As<byte, long>(ref pThisValue) == Unsafe.As<byte, long>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, long>(ref pThisValue) < Unsafe.As<byte, long>(ref pTargetValue)) ? -1 : 1;

                case CorElementType.ELEMENT_TYPE_U8:
                    return (Unsafe.As<byte, ulong>(ref pThisValue) == Unsafe.As<byte, ulong>(ref pTargetValue)) ?
                        0 : (Unsafe.As<byte, ulong>(ref pThisValue) < Unsafe.As<byte, ulong>(ref pTargetValue)) ? -1 : 1;

                default:
                    Environment.FailFast("Unexpected enum underlying type");
                    return 0;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            EETypePtr eeType = this.EETypePtr;
            if (!eeType.FastEquals(obj.EETypePtr))
                return false;

            ref byte pThisValue = ref this.GetRawData();
            ref byte pOtherValue = ref obj.GetRawData();

            RuntimeImports.RhCorElementTypeInfo corElementTypeInfo = eeType.CorElementTypeInfo;
            switch (corElementTypeInfo.Log2OfSize)
            {
                case 0:
                    return Unsafe.As<byte, byte>(ref pThisValue) == Unsafe.As<byte, byte>(ref pOtherValue);
                case 1:
                    return Unsafe.As<byte, ushort>(ref pThisValue) == Unsafe.As<byte, ushort>(ref pOtherValue);
                case 2:
                    return Unsafe.As<byte, uint>(ref pThisValue) == Unsafe.As<byte, uint>(ref pOtherValue);
                case 3:
                    return Unsafe.As<byte, ulong>(ref pThisValue) == Unsafe.As<byte, ulong>(ref pOtherValue);
                default:
                    Environment.FailFast("Unexpected enum underlying type");
                    return false;
            }
        }

        private CorElementType InternalGetCorElementType() => this.EETypePtr.CorElementType;

        [Intrinsic]
        public bool HasFlag(Enum flag)
        {
            if (flag == null)
                throw new ArgumentNullException(nameof(flag));

            if (!(this.EETypePtr == flag.EETypePtr))
                throw new ArgumentException(SR.Format(SR.Argument_EnumTypeDoesNotMatch, flag.GetType(), this.GetType()));

            ref byte pThisValue = ref this.GetRawData();
            ref byte pFlagValue = ref flag.GetRawData();

            switch (this.EETypePtr.CorElementTypeInfo.Log2OfSize)
            {
                case 0:
                    return (Unsafe.As<byte, byte>(ref pThisValue) & Unsafe.As<byte, byte>(ref pFlagValue)) == Unsafe.As<byte, byte>(ref pFlagValue);
                case 1:
                    return (Unsafe.As<byte, ushort>(ref pThisValue) & Unsafe.As<byte, ushort>(ref pFlagValue)) == Unsafe.As<byte, ushort>(ref pFlagValue);
                case 2:
                    return (Unsafe.As<byte, uint>(ref pThisValue) & Unsafe.As<byte, uint>(ref pFlagValue)) == Unsafe.As<byte, uint>(ref pFlagValue);
                case 3:
                    return (Unsafe.As<byte, ulong>(ref pThisValue) & Unsafe.As<byte, ulong>(ref pFlagValue)) == Unsafe.As<byte, ulong>(ref pFlagValue);
                default:
                    Environment.FailFast("Unexpected enum underlying type");
                    return false;
            }
        }



        //
        // Note: This works on both Enum's and underlying integer values.
        //
        //
        // This returns the underlying enum values as "ulong" regardless of the actual underlying type. Signed integral 
        // types get sign-extended into the 64-bit value, unsigned types get zero-extended.
        //
        // The return value is "bool" if "value" is not an enum or an "integer type" as defined by the BCL Enum apis.
        // 
        private static bool TryGetUnboxedValueOfEnumOrInteger(object value, out ulong result)
        {
            EETypePtr eeType = value.EETypePtr;
            // For now, this check is required to flush out pointers.
            if (!eeType.IsDefType)
            {
                result = 0;
                return false;
            }
            CorElementType corElementType = eeType.CorElementType;

            ref byte pValue = ref value.GetRawData();

            switch (corElementType)
            {
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    result = Unsafe.As<byte, bool>(ref pValue) ? 1UL : 0UL;
                    return true;

                case CorElementType.ELEMENT_TYPE_CHAR:
                    result = (ulong)(long)Unsafe.As<byte, char>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_I1:
                    result = (ulong)(long)Unsafe.As<byte, sbyte>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_U1:
                    result = (ulong)(long)Unsafe.As<byte, byte>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_I2:
                    result = (ulong)(long)Unsafe.As<byte, short>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_U2:
                    result = (ulong)(long)Unsafe.As<byte, ushort>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_I4:
                    result = (ulong)(long)Unsafe.As<byte, int>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_U4:
                    result = (ulong)(long)Unsafe.As<byte, uint>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_I8:
                    result = (ulong)(long)Unsafe.As<byte, long>(ref pValue);
                    return true;

                case CorElementType.ELEMENT_TYPE_U8:
                    result = (ulong)(long)Unsafe.As<byte, ulong>(ref pValue);
                    return true;

                default:
                    result = 0;
                    return false;
            }
        }

        //
        // Look up a name for rawValue if a matching one exists. Returns null if no matching name exists.
        //
        private static string GetNameIfAny(EnumInfo enumInfo, ulong rawValue)
        {
            KeyValuePair<string, ulong>[] namesAndValues = enumInfo.NamesAndValues;
            KeyValuePair<string, ulong> searchKey = new KeyValuePair<string, ulong>(null, rawValue);
            int index = Array.BinarySearch<KeyValuePair<String, ulong>>(namesAndValues, searchKey, s_nameAndValueComparer);
            if (index < 0)
                return null;
            return namesAndValues[index].Key;
        }

        internal static EnumInfo GetEnumInfo(Type enumType)
        {
            Debug.Assert(enumType != null);
            Debug.Assert(enumType.IsRuntimeImplemented());
            Debug.Assert(enumType.IsEnum);

            return ReflectionAugments.ReflectionCoreCallbacks.GetEnumInfo(enumType);
        }

        public static string GetName(Type enumType, object value)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));
            if (!enumType.IsRuntimeImplemented())
                return enumType.GetEnumName(value);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            ulong rawValue;
            if (!TryGetUnboxedValueOfEnumOrInteger(value, out rawValue))
                throw new ArgumentException(SR.Arg_MustBeEnumBaseTypeOrEnum, nameof(value));

            // For desktop compatibility, do not bounce an incoming integer that's the wrong size. 
            // Do a value-preserving cast of both it and the enum values and do a 64-bit compare.

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum);

            EnumInfo enumInfo = GetEnumInfo(enumType);
            string nameOrNull = GetNameIfAny(enumInfo, rawValue);
            return nameOrNull;
        }

        public static string[] GetNames(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsRuntimeImplemented())
                return enumType.GetEnumNames();

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum);

            KeyValuePair<string, ulong>[] namesAndValues = GetEnumInfo(enumType).NamesAndValues;
            string[] names = new string[namesAndValues.Length];
            for (int i = 0; i < namesAndValues.Length; i++)
                names[i] = namesAndValues[i].Key;
            return names;
        }

        public static Type GetUnderlyingType(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsRuntimeImplemented())
                return enumType.GetEnumUnderlyingType();

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, nameof(enumType));

            return GetEnumInfo(enumType).UnderlyingType;
        }

        public static Array GetValues(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsRuntimeImplemented())
                return enumType.GetEnumValues();

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum);

            Array values = GetEnumInfo(enumType).Values;
            int count = values.Length;
#if PROJECTN
            Array result = Array.CreateInstance(enumType, count);
#else
            // Without universal shared generics, chances are slim that we'll have the appropriate
            // array type available. Offer an escape hatch that avoids a MissingMetadataException
            // at the cost of a small appcompat risk.
            Array result;
            if (AppContext.TryGetSwitch("Switch.System.Enum.RelaxedGetValues", out bool isRelaxed) && isRelaxed)
                result = Array.CreateInstance(Enum.GetUnderlyingType(enumType), count);
            else
                result = Array.CreateInstance(enumType, count);
#endif
            Array.CopyImplValueTypeArrayNoInnerGcRefs(values, 0, result, 0, count);
            return result;
        }

        [Conditional("BIGENDIAN")]
        private static unsafe void AdjustForEndianness(ref byte* pValue, EETypePtr enumEEType)
        {
            // On Debug builds, include the big-endian code to help deter bitrot (the "Conditional("BIGENDIAN")" will prevent it from executing on little-endian). 
            // On Release builds, exclude code to deter IL bloat and toolchain work.
#if BIGENDIAN || DEBUG
            CorElementType corElementType = enumEEType.CorElementType;
            switch (corElementType)
            {
                case CorElementType.ELEMENT_TYPE_I1:
                case CorElementType.ELEMENT_TYPE_U1:
                    pValue += sizeof(long) - sizeof(byte);
                    break;

                case CorElementType.ELEMENT_TYPE_I2:
                case CorElementType.ELEMENT_TYPE_U2:
                    pValue += sizeof(long) - sizeof(short);
                    break;

                case CorElementType.ELEMENT_TYPE_I4:
                case CorElementType.ELEMENT_TYPE_U4:
                    pValue += sizeof(long) - sizeof(int);
                    break;

                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U8:
                    break;

                default:
                    throw new NotSupportedException();
            }
#endif //BIGENDIAN || DEBUG
        }



        //
        // Sort comparer for NamesAndValues
        //
        private class NamesAndValueComparer : IComparer<KeyValuePair<string, ulong>>
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
        }

        private static NamesAndValueComparer s_nameAndValueComparer = new NamesAndValueComparer();

        #region ToObject

        internal unsafe static object ToObject(EETypePtr enumEEType, long value)
        {
            Debug.Assert(enumEEType.IsEnum);

            byte* pValue = (byte*)&value;
            AdjustForEndianness(ref pValue, enumEEType);
            return RuntimeImports.RhBox(enumEEType, pValue);
        }
        #endregion
    }
}
