// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;

using Internal.Runtime.CompilerServices;
using Internal.Runtime.Augments;
using Internal.Reflection.Augments;

using CorElementType = System.Runtime.RuntimeImports.RhCorElementType;

namespace System
{
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public abstract class Enum : ValueType, IComparable, IFormattable, IConvertible
    {
        public int CompareTo(object target)
        {
            if (target == null)
                return 1;

            if (target == this)
                return 0;

            if (this.EETypePtr != target.EETypePtr)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, target.GetType().ToString(), this.GetType().ToString()));
            }

            ref byte pThisValue = ref this.GetRawData();
            ref byte pTargetValue = ref target.GetRawData();

            switch (this.EETypePtr.CorElementType)
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

        public override int GetHashCode()
        {
            ref byte pValue = ref this.GetRawData();

            switch (this.EETypePtr.CorElementType)
            {
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return Unsafe.As<byte, bool>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_CHAR:
                    return Unsafe.As<byte, char>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_I1:
                    return Unsafe.As<byte, sbyte>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_U1:
                    return Unsafe.As<byte, byte>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_I2:
                    return Unsafe.As<byte, short>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_U2:
                    return Unsafe.As<byte, ushort>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_I4:
                    return Unsafe.As<byte, int>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_U4:
                    return Unsafe.As<byte, uint>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_I8:
                    return Unsafe.As<byte, long>(ref pValue).GetHashCode();
                case CorElementType.ELEMENT_TYPE_U8:
                    return Unsafe.As<byte, ulong>(ref pValue).GetHashCode();
                default:
                    Environment.FailFast("Unexpected enum underlying type");
                    return 0;
            }
        }

        public static string Format(Type enumType, object value, string format)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, nameof(enumType));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (format == null)
                throw new ArgumentNullException(nameof(format));

            if (!enumType.IsRuntimeImplemented())
                throw new ArgumentException(SR.Arg_MustBeType, nameof(enumType));

            EnumInfo enumInfo = GetEnumInfo(enumType);
            if (value is Enum)
            {
                EETypePtr enumTypeEEType;
                if ((!enumType.TryGetEEType(out enumTypeEEType)) || enumTypeEEType != value.EETypePtr)
                    throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, value.GetType().ToString(), enumType.ToString()));
            }
            else
            {
                if (value.EETypePtr != enumInfo.UnderlyingType.TypeHandle.ToEETypePtr())
                    throw new ArgumentException(SR.Format(SR.Arg_EnumFormatUnderlyingTypeAndObjectMustBeSameType, value.GetType().ToString(), enumInfo.UnderlyingType.ToString()));
            }

            return Format(enumInfo, value, format);
        }

        private static string Format(EnumInfo enumInfo, object value, string format)
        {
            ulong rawValue;
            if (!TryGetUnboxedValueOfEnumOrInteger(value, out rawValue))
            {
                Debug.Fail("Caller was expected to do enough validation to avoid reaching this.");
                throw new ArgumentException();
            }

            if (format.Length == 1)
            {
                switch (format[0])
                {
                    case 'G':
                    case 'g':
                        return DoFormatG(enumInfo, rawValue);

                    case 'D':
                    case 'd':
                        return DoFormatD(rawValue, value.EETypePtr.CorElementType);

                    case 'X':
                    case 'x':
                        return DoFormatX(rawValue, value.EETypePtr.CorElementType);

                    case 'F':
                    case 'f':
                        return DoFormatF(enumInfo, rawValue);
                }
            }

            throw new FormatException(SR.Format_InvalidEnumFormatSpecification);
        }

        //
        // Helper for Enum.Format(,,"d")
        //
        private static string DoFormatD(ulong rawValue, CorElementType corElementType)
        {
            switch (corElementType)
            {
                case CorElementType.ELEMENT_TYPE_I1:
                    {
                        sbyte result = (sbyte)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_U1:
                    {
                        byte result = (byte)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    {
                        // direct cast from bool to byte is not allowed
                        bool b = (rawValue != 0);
                        return b.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_I2:
                    {
                        short result = (short)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_U2:
                    {
                        ushort result = (ushort)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_CHAR:
                    {
                        char result = (char)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_U4:
                    {
                        uint result = (uint)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_I4:
                    {
                        int result = (int)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_U8:
                    {
                        ulong result = (ulong)rawValue;

                        return result.ToString();
                    }

                case CorElementType.ELEMENT_TYPE_I8:
                    {
                        long result = (long)rawValue;

                        return result.ToString();
                    }

                default:
                    Debug.Fail("Invalid Object type in Format");
                    throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
            }
        }


        //
        // Helper for Enum.Format(,,"x")
        //
        private static string DoFormatX(ulong rawValue, CorElementType corElementType)
        {
            switch (corElementType)
            {
                case CorElementType.ELEMENT_TYPE_I1:
                    {
                        byte result = (byte)(sbyte)rawValue;

                        return result.ToString("X2", null);
                    }

                case CorElementType.ELEMENT_TYPE_U1:
                    {
                        byte result = (byte)rawValue;

                        return result.ToString("X2", null);
                    }

                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    {
                        // direct cast from bool to byte is not allowed
                        byte result = (byte)rawValue;

                        return result.ToString("X2", null);
                    }

                case CorElementType.ELEMENT_TYPE_I2:
                    {
                        ushort result = (ushort)(short)rawValue;

                        return result.ToString("X4", null);
                    }

                case CorElementType.ELEMENT_TYPE_U2:
                    {
                        ushort result = (ushort)rawValue;

                        return result.ToString("X4", null);
                    }

                case CorElementType.ELEMENT_TYPE_CHAR:
                    {
                        ushort result = (ushort)(char)rawValue;

                        return result.ToString("X4", null);
                    }

                case CorElementType.ELEMENT_TYPE_U4:
                    {
                        uint result = (uint)rawValue;

                        return result.ToString("X8", null);
                    }

                case CorElementType.ELEMENT_TYPE_I4:
                    {
                        uint result = (uint)(int)rawValue;

                        return result.ToString("X8", null);
                    }

                case CorElementType.ELEMENT_TYPE_U8:
                    {
                        ulong result = (ulong)rawValue;

                        return result.ToString("X16", null);
                    }

                case CorElementType.ELEMENT_TYPE_I8:
                    {
                        ulong result = (ulong)(long)rawValue;

                        return result.ToString("X16", null);
                    }

                default:
                    Debug.Fail("Invalid Object type in Format");
                    throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
            }
        }

        //
        // Helper for Enum.Format(,,"g")
        //
        private static string DoFormatG(EnumInfo enumInfo, ulong rawValue)
        {
            Debug.Assert(enumInfo != null);
            if (!enumInfo.HasFlagsAttribute) // Not marked with Flags attribute
            {
                // Try to see if its one of the enum values, then we return a String back else the value
                string name = GetNameIfAny(enumInfo, rawValue);
                if (name == null)
                    return DoFormatD(rawValue, enumInfo.UnderlyingType.TypeHandle.ToEETypePtr().CorElementType);
                else
                    return name;
            }
            else // These are flags OR'ed together (We treat everything as unsigned types)
            {
                return DoFormatF(enumInfo, rawValue);
            }
        }

        //
        // Helper for Enum.Format(,,"f")
        //
        private static string DoFormatF(EnumInfo enumInfo, ulong rawValue)
        {
            Debug.Assert(enumInfo != null);

            // These values are sorted by value. Don't change this
            KeyValuePair<string, ulong>[] namesAndValues = enumInfo.NamesAndValues;

            int index = namesAndValues.Length - 1;
            StringBuilder retval = new StringBuilder();
            bool firstTime = true;
            ulong result = rawValue;

            // We will not optimize this code further to keep it maintainable. There are some boundary checks that can be applied
            // to minimize the comparsions required. This code works the same for the best/worst case. In general the number of
            // items in an enum are sufficiently small and not worth the optimization.
            while (index >= 0)
            {
                if ((index == 0) && (namesAndValues[index].Value == 0))
                    break;

                if ((result & namesAndValues[index].Value) == namesAndValues[index].Value)
                {
                    result -= namesAndValues[index].Value;
                    if (!firstTime)
                        retval.Insert(0, ", ");

                    retval.Insert(0, namesAndValues[index].Key);
                    firstTime = false;
                }

                index--;
            }

            // We were unable to represent this number as a bitwise or of valid flags
            if (result != 0)
                return DoFormatD(rawValue, enumInfo.UnderlyingType.TypeHandle.ToEETypePtr().CorElementType);

            // For the case when we have zero
            if (rawValue == 0)
            {
                if (namesAndValues.Length > 0 && namesAndValues[0].Value == 0)
                    return namesAndValues[0].Key; // Zero was one of the enum values.
                else
                    return "0";
            }
            else
                return retval.ToString();  // Built a list of matching names. Return it.
        }

        internal object GetValue()
        {
            ref byte pValue = ref this.GetRawData();

            switch (this.EETypePtr.CorElementType)
            {
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return Unsafe.As<byte, bool>(ref pValue);
                case CorElementType.ELEMENT_TYPE_CHAR:
                    return Unsafe.As<byte, char>(ref pValue);
                case CorElementType.ELEMENT_TYPE_I1:
                    return Unsafe.As<byte, sbyte>(ref pValue);
                case CorElementType.ELEMENT_TYPE_U1:
                    return Unsafe.As<byte, byte>(ref pValue);
                case CorElementType.ELEMENT_TYPE_I2:
                    return Unsafe.As<byte, short>(ref pValue);
                case CorElementType.ELEMENT_TYPE_U2:
                    return Unsafe.As<byte, ushort>(ref pValue);
                case CorElementType.ELEMENT_TYPE_I4:
                    return Unsafe.As<byte, int>(ref pValue);
                case CorElementType.ELEMENT_TYPE_U4:
                    return Unsafe.As<byte, uint>(ref pValue);
                case CorElementType.ELEMENT_TYPE_I8:
                    return Unsafe.As<byte, long>(ref pValue);
                case CorElementType.ELEMENT_TYPE_U8:
                    return Unsafe.As<byte, ulong>(ref pValue);
                default:
                    Environment.FailFast("Unexpected enum underlying type");
                    return 0;
            }
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

        public static bool IsDefined(Type enumType, object value)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsRuntimeImplemented())
                return enumType.IsEnumDefined(value);

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value is string valueAsString)
            {
                if (!enumType.IsEnum)
                    throw new ArgumentException(SR.Arg_MustBeEnum);

                EnumInfo enumInfo = GetEnumInfo(enumType);
                foreach (KeyValuePair<string, ulong> kv in enumInfo.NamesAndValues)
                {
                    if (valueAsString == kv.Key)
                        return true;
                }
                return false;
            }
            else
            {
                ulong rawValue;
                if (!TryGetUnboxedValueOfEnumOrInteger(value, out rawValue))
                {
                    if (Type.IsIntegerType(value.GetType()))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumUnderlyingTypeAndObjectMustBeSameType, value.GetType(), Enum.GetUnderlyingType(enumType)));
                    else
                        throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
                }

                EnumInfo enumInfo = null;
                if (value is Enum)
                {
                    if (!ValueTypeMatchesEnumType(enumType, value))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, value.GetType(), enumType));
                }
                else
                {
                    if (!enumType.IsEnum)
                        throw new ArgumentException(SR.Arg_MustBeEnum);

                    enumInfo = GetEnumInfo(enumType);
                    if (!(enumInfo.UnderlyingType.TypeHandle.ToEETypePtr() == value.EETypePtr))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumUnderlyingTypeAndObjectMustBeSameType, value.GetType(), enumInfo.UnderlyingType));
                }

                if (enumInfo == null)
                {
                    if (!enumType.IsEnum)
                        throw new ArgumentException(SR.Arg_MustBeEnum);
                    enumInfo = GetEnumInfo(enumType);
                }
                string nameOrNull = GetNameIfAny(enumInfo, rawValue);
                return nameOrNull != null;
            }
        }

        public static object Parse(Type enumType, string value) =>
            Parse(enumType, value, ignoreCase: false);

        public static object Parse(Type enumType, string value, bool ignoreCase)
        {
            bool success = TryParse(enumType, value, ignoreCase, throwOnFailure: true, out object result);
            Debug.Assert(success);
            return result;
        }

        public static TEnum Parse<TEnum>(string value) where TEnum : struct =>
            Parse<TEnum>(value, ignoreCase: false);

        public static TEnum Parse<TEnum>(string value, bool ignoreCase) where TEnum : struct
        {
            bool success = TryParse<TEnum>(value, ignoreCase, throwOnFailure: true, out TEnum result);
            Debug.Assert(success);
            return result;
        }

        public static bool TryParse(Type enumType, string value, out object result) =>
            TryParse(enumType, value, ignoreCase: false, out result);

        public static bool TryParse(Type enumType, string value, bool ignoreCase, out object result) =>
            TryParse(enumType, value, ignoreCase, throwOnFailure: false, out result);

        public static bool TryParse<TEnum>(string value, out TEnum result) where TEnum : struct =>
            TryParse<TEnum>(value, ignoreCase: false, out result);

        public static bool TryParse<TEnum>(string value, bool ignoreCase, out TEnum result) where TEnum : struct =>
            TryParse<TEnum>(value, ignoreCase, throwOnFailure: false, out result);

        private static bool TryParse<TEnum>(string value, bool ignoreCase, bool throwOnFailure, out TEnum result) where TEnum : struct
        {
            object tempResult;
            if (!TryParse(typeof(TEnum), value, ignoreCase, throwOnFailure, out tempResult))
            {
                result = default(TEnum);
                return false;
            }
            result = (TEnum)tempResult;
            return true;
        }

        public override string ToString()
        {
            try
            {
                return this.ToString("G");
            }
            catch (Exception)
            {
                return this.LastResortToString;
            }
        }

        public string ToString(string format)
        {
            if (format == null || format.Length == 0)
                format = "G";

            EnumInfo enumInfo = GetEnumInfo(this.GetType());

            // Project N port note: If Reflection info isn't available, fallback to ToString() which will substitute a numeric value for the "correct" output.
            // This scenario has been hit frequently when throwing exceptions formatted with error strings containing enum substitations.
            // To avoid replacing the masking the actual exception with an uninteresting MissingMetadataException, we choose instead
            // to return a base-effort string.
            if (enumInfo == null)
                return this.LastResortToString;

            return Format(enumInfo, this, format);
        }

        [Obsolete("The provider argument is not used. Please use ToString().")]
        public string ToString(string format, IFormatProvider provider)
        {
            return ToString(format);
        }

        [Obsolete("The provider argument is not used. Please use ToString().")]
        public string ToString(IFormatProvider provider)
        {
            return ToString();
        }

        private static EnumInfo GetEnumInfo(Type enumType)
        {
            Debug.Assert(enumType != null);
            Debug.Assert(enumType.IsRuntimeImplemented());
            Debug.Assert(enumType.IsEnum);

            return ReflectionAugments.ReflectionCoreCallbacks.GetEnumInfo(enumType);
        }

        //
        // Checks if value.GetType() matches enumType exactly. 
        //
        private static bool ValueTypeMatchesEnumType(Type enumType, object value)
        {
            EETypePtr enumEEType;
            if (!enumType.TryGetEEType(out enumEEType))
                return false;
            if (!(enumEEType == value.EETypePtr))
                return false;
            return true;
        }

        // Exported for use by legacy user-defined Type Enum apis.
        internal static ulong ToUInt64(object value)
        {
            ulong result;
            if (!TryGetUnboxedValueOfEnumOrInteger(value, out result))
                throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
            return result;
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

        //
        // Common funnel for Enum.Parse methods.
        //
        private static bool TryParse(Type enumType, string value, bool ignoreCase, bool throwOnFailure, out object result)
        {
            result = null;

            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsRuntimeImplemented())
                throw new ArgumentException(SR.Arg_MustBeType, nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, nameof(enumType));

            if (value == null)
            {
                if (throwOnFailure)
                    throw new ArgumentNullException(nameof(value));
                return false;
            }

            int firstNonWhitespaceIndex = -1;
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    firstNonWhitespaceIndex = i;
                    break;
                }
            }
            if (firstNonWhitespaceIndex == -1)
            {
                if (throwOnFailure)
                    throw new ArgumentException(SR.Arg_MustContainEnumInfo);
                return false;
            }

            // Check for the unfortunate "typeof(Outer<>).InnerEnum" corner case.
            if (enumType.ContainsGenericParameters)
                throw new InvalidOperationException(SR.Format(SR.Arg_OpenType, enumType.ToString()));

            EETypePtr enumEEType = enumType.TypeHandle.ToEETypePtr();
            if (TryParseAsInteger(enumEEType, value, firstNonWhitespaceIndex, out result))
                return true;

            if (StillLooksLikeInteger(value, firstNonWhitespaceIndex))
            {
                if (throwOnFailure)
                    throw new OverflowException();
                return false;
            }

            // Parse as string. Now (and only now) do we look for metadata information.
            EnumInfo enumInfo = GetEnumInfo(enumType);
            ulong v = 0;

            // Port note: The docs are silent on how multiple matches are resolved when doing case-insensitive parses.
            // The desktop's ad-hoc behavior is to pick the one with the smallest value after doing a value-preserving cast
            // to a ulong, so we'll follow that here.
            StringComparison comparison = ignoreCase ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;
            KeyValuePair<string, ulong>[] actualNamesAndValues = enumInfo.NamesAndValues;
            int valueIndex = firstNonWhitespaceIndex;
            while (valueIndex <= value.Length) // '=' is to handle invalid case of an ending comma
            {
                // Find the next separator, if there is one, otherwise the end of the string.
                int endIndex = value.IndexOf(',', valueIndex);
                if (endIndex == -1)
                {
                    endIndex = value.Length;
                }

                // Shift the starting and ending indices to eliminate whitespace
                int endIndexNoWhitespace = endIndex;
                while (valueIndex < endIndex && char.IsWhiteSpace(value[valueIndex])) valueIndex++;
                while (endIndexNoWhitespace > valueIndex && char.IsWhiteSpace(value[endIndexNoWhitespace - 1])) endIndexNoWhitespace--;
                int valueSubstringLength = endIndexNoWhitespace - valueIndex;

                // Try to match this substring against each enum name
                bool foundMatch = false;
                foreach (KeyValuePair<string, ulong> kv in actualNamesAndValues)
                {
                    string actualName = kv.Key;
                    if (actualName.Length == valueSubstringLength &&
                        string.Compare(actualName, 0, value, valueIndex, valueSubstringLength, comparison) == 0)
                    {
                        v |= kv.Value;
                        foundMatch = true;
                        break;
                    }
                }
                if (!foundMatch)
                {
                    if (throwOnFailure)
                        throw new ArgumentException(SR.Format(SR.Arg_EnumValueNotFound, value));
                    return false;
                }

                // Move our pointer to the ending index to go again.
                valueIndex = endIndex + 1;
            }
            unsafe
            {
                result = RuntimeImports.RhBox(enumEEType, &v);  //@todo: Not compatible with big-endian platforms.
            }
            return true;
        }

        private static bool TryParseAsInteger(EETypePtr enumEEType, string value, int valueOffset, out object result)
        {
            Debug.Assert(value != null, "Expected non-null value");
            Debug.Assert(value.Length > 0, "Expected non-empty value");
            Debug.Assert(valueOffset >= 0 && valueOffset < value.Length, "Expected valueOffset to be within value");

            result = null;

            char firstNonWhitespaceChar = value[valueOffset];
            if (!(char.IsDigit(firstNonWhitespaceChar) || firstNonWhitespaceChar == '+' || firstNonWhitespaceChar == '-'))
                return false;
            CorElementType corElementType = enumEEType.CorElementType;

            value = value.Trim();
            unsafe
            {
                switch (corElementType)
                {
                    case CorElementType.ELEMENT_TYPE_BOOLEAN:
                        {
                            bool v;
                            if (!bool.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_CHAR:
                        {
                            char v;
                            if (!char.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_I1:
                        {
                            sbyte v;
                            if (!sbyte.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_U1:
                        {
                            byte v;
                            if (!byte.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_I2:
                        {
                            short v;
                            if (!short.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_U2:
                        {
                            ushort v;
                            if (!ushort.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_I4:
                        {
                            int v;
                            if (!int.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_U4:
                        {
                            uint v;
                            if (!uint.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_I8:
                        {
                            long v;
                            if (!long.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case CorElementType.ELEMENT_TYPE_U8:
                        {
                            ulong v;
                            if (!ulong.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    default:
                        throw new NotSupportedException();
                }
            }
        }

        private static bool StillLooksLikeInteger(string value, int index)
        {
            if (index != value.Length && (value[index] == '-' || value[index] == '+'))
            {
                index++;
            }

            if (index == value.Length || !char.IsDigit(value[index]))
                return false;

            index++;

            while (index != value.Length && char.IsDigit(value[index]))
            {
                index++;
            }

            while (index != value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            return index == value.Length;
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

        private string LastResortToString
        {
            get
            {
                return string.Format("{0}", GetValue());
            }
        }

        #region IConvertible
        public TypeCode GetTypeCode()
        {
            CorElementType corElementType = this.EETypePtr.CorElementType;

            switch (corElementType)
            {
                case CorElementType.ELEMENT_TYPE_I1:
                    return TypeCode.SByte;
                case CorElementType.ELEMENT_TYPE_U1:
                    return TypeCode.Byte;
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return TypeCode.Boolean;
                case CorElementType.ELEMENT_TYPE_I2:
                    return TypeCode.Int16;
                case CorElementType.ELEMENT_TYPE_U2:
                    return TypeCode.UInt16;
                case CorElementType.ELEMENT_TYPE_CHAR:
                    return TypeCode.Char;
                case CorElementType.ELEMENT_TYPE_I4:
                    return TypeCode.Int32;
                case CorElementType.ELEMENT_TYPE_U4:
                    return TypeCode.UInt32;
                case CorElementType.ELEMENT_TYPE_I8:
                    return TypeCode.Int64;
                case CorElementType.ELEMENT_TYPE_U8:
                    return TypeCode.UInt64;
                default:
                    Debug.Fail("Unknown underlying type.");
                    throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
            }
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(GetValue(), CultureInfo.CurrentCulture);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(GetValue(), CultureInfo.CurrentCulture);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(GetValue(), CultureInfo.CurrentCulture);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(GetValue(), CultureInfo.CurrentCulture);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(GetValue(), CultureInfo.CurrentCulture);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(GetValue(), CultureInfo.CurrentCulture);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(GetValue(), CultureInfo.CurrentCulture);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(GetValue(), CultureInfo.CurrentCulture);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(GetValue(), CultureInfo.CurrentCulture);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(GetValue(), CultureInfo.CurrentCulture);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(GetValue(), CultureInfo.CurrentCulture);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(GetValue(), CultureInfo.CurrentCulture);
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(GetValue(), CultureInfo.CurrentCulture);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(SR.Format(SR.InvalidCast_FromTo, "Enum", "DateTime"));
        }

        object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
        #endregion

        #region ToObject
        public static object ToObject(Type enumType, object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            // Delegate rest of error checking to the other functions

            switch (Convert.GetTypeCode(value))
            {
                case TypeCode.Int32:
                    return ToObject(enumType, (int)value);

                case TypeCode.SByte:
                    return ToObject(enumType, (sbyte)value);

                case TypeCode.Int16:
                    return ToObject(enumType, (short)value);

                case TypeCode.Int64:
                    return ToObject(enumType, (long)value);

                case TypeCode.UInt32:
                    return ToObject(enumType, (uint)value);

                case TypeCode.Byte:
                    return ToObject(enumType, (byte)value);

                case TypeCode.UInt16:
                    return ToObject(enumType, (ushort)value);

                case TypeCode.UInt64:
                    return ToObject(enumType, (ulong)value);

                case TypeCode.Char:
                    return ToObject(enumType, (char)value);

                case TypeCode.Boolean:
                    return ToObject(enumType, (bool)value);

                default:
                    throw new ArgumentException(SR.Arg_MustBeEnumBaseTypeOrEnum, nameof(value));
            }
        }

        //
        // Non-boxing overloads for Enum.ToObject().
        //
        // The underlying integer type of the enum does not have to match the type of "value." If
        // the enum type is larger, this api does a value-preserving widening if possible (or a 2's complement
        // conversion if not.) If the enum type is smaller, the api discards the higher order bits.
        // Either way, no exception is thrown upon overflow.
        //
        [CLSCompliant(false)]
        public static object ToObject(Type enumType, sbyte value) => ToObjectWorker(enumType, value);
        public static object ToObject(Type enumType, byte value) => ToObjectWorker(enumType, value);
        [CLSCompliant(false)]
        public static object ToObject(Type enumType, ushort value) => ToObjectWorker(enumType, value);
        public static object ToObject(Type enumType, short value) => ToObjectWorker(enumType, value);
        [CLSCompliant(false)]
        public static object ToObject(Type enumType, uint value) => ToObjectWorker(enumType, value);
        public static object ToObject(Type enumType, int value) => ToObjectWorker(enumType, value);
        [CLSCompliant(false)]
        public static object ToObject(Type enumType, ulong value) => ToObjectWorker(enumType, (long)value);
        public static object ToObject(Type enumType, long value) => ToObjectWorker(enumType, value);

        // These are needed to service ToObject(Type, Object).
        private static object ToObject(Type enumType, char value) => ToObjectWorker(enumType, value);
        private static object ToObject(Type enumType, bool value) => ToObjectWorker(enumType, value ? 1 : 0);

        // Common helper for the non-boxing Enum.ToObject() overloads.
        private static object ToObjectWorker(Type enumType, long value)
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

            EETypePtr enumEEType = enumType.TypeHandle.ToEETypePtr();
            return ToObject(enumEEType, value);
        }

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
