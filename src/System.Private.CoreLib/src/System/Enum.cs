// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Runtime;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;
using System.Collections.Concurrent;


using Internal.Runtime.Augments;
using Internal.Reflection.Core.NonPortable;

namespace System
{
    public abstract class Enum : ValueType, IComparable, IFormattable, IConvertible
    {
        public unsafe int CompareTo(Object target)
        {
            if (target == null)
                return 1;

            if (target == this)
                return 0;

            if (this.EETypePtr != target.EETypePtr)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, target.GetType().ToString(), this.GetType().ToString()));
            }

            fixed (IntPtr* pThisObj = &this.m_pEEType, pTargetObj = &target.m_pEEType)
            {
                IntPtr pThisValue = Object.GetAddrOfPinnedObjectFromEETypeField(pThisObj);
                IntPtr pTargetValue = Object.GetAddrOfPinnedObjectFromEETypeField(pTargetObj);

                switch (this.EETypePtr.CorElementType)
                {
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                        return (*(sbyte*)pThisValue == *(sbyte*)pTargetValue) ? 0 : (*(sbyte*)pThisValue < *(sbyte*)pTargetValue) ? -1 : 1;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                        return (*(byte*)pThisValue == *(byte*)pTargetValue) ? 0 : (*(byte*)pThisValue < *(byte*)pTargetValue) ? -1 : 1;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                        return (*(short*)pThisValue == *(short*)pTargetValue) ? 0 : (*(short*)pThisValue < *(short*)pTargetValue) ? -1 : 1;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                        return (*(ushort*)pThisValue == *(ushort*)pTargetValue) ? 0 : (*(ushort*)pThisValue < *(ushort*)pTargetValue) ? -1 : 1;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                        return (*(int*)pThisValue == *(int*)pTargetValue) ? 0 : (*(int*)pThisValue < *(int*)pTargetValue) ? -1 : 1;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                        return (*(uint*)pThisValue == *(uint*)pTargetValue) ? 0 : (*(uint*)pThisValue < *(uint*)pTargetValue) ? -1 : 1;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                        return (*(long*)pThisValue == *(long*)pTargetValue) ? 0 : (*(long*)pThisValue < *(long*)pTargetValue) ? -1 : 1;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                        return (*(ulong*)pThisValue == *(ulong*)pTargetValue) ? 0 : (*(ulong*)pThisValue < *(ulong*)pTargetValue) ? -1 : 1;
                    default:
                        Environment.FailFast("Unexpected enum underlying type");
                        return 0;
                }
            }
        }

        public override bool Equals(Object obj)
        {
            if (obj == null)
                return false;
            EETypePtr eeType = this.EETypePtr;
            if (!eeType.FastEquals(obj.EETypePtr))
                return false;
            unsafe
            {
                fixed (IntPtr* pThisObj = &this.m_pEEType, pOtherObj = &obj.m_pEEType)
                {
                    IntPtr pThisValue = Object.GetAddrOfPinnedObjectFromEETypeField(pThisObj);
                    IntPtr pOtherValue = Object.GetAddrOfPinnedObjectFromEETypeField(pOtherObj);

                    RuntimeImports.RhCorElementTypeInfo corElementTypeInfo = eeType.CorElementTypeInfo;
                    switch (corElementTypeInfo.Log2OfSize)
                    {
                        case 0:
                            return (*(byte*)pThisValue) == (*(byte*)pOtherValue);

                        case 1:
                            return (*(ushort*)pThisValue) == (*(ushort*)pOtherValue);

                        case 2:
                            return (*(uint*)pThisValue) == (*(uint*)pOtherValue);

                        case 3:
                            return (*(ulong*)pThisValue) == (*(ulong*)pOtherValue);

                        default:
                            Environment.FailFast("Unexpected enum underlying type");
                            return false;
                    }
                }
            }
        }

        public override int GetHashCode()
        {
            unsafe
            {
                fixed (IntPtr* pObj = &this.m_pEEType)
                {
                    IntPtr pValue = Object.GetAddrOfPinnedObjectFromEETypeField(pObj);

                    switch (this.EETypePtr.CorElementType)
                    {
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                            return (*(bool*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                            return (*(char*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                            return (*(sbyte*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                            return (*(byte*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                            return (*(short*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                            return (*(ushort*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                            return (*(int*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                            return (*(uint*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                            return (*(long*)pValue).GetHashCode();
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                            return (*(ulong*)pValue).GetHashCode();
                        default:
                            Environment.FailFast("Unexpected enum underlying type");
                            return 0;
                    }
                }
            }
        }

        public static String Format(Type enumType, Object value, String format)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");

            EnumInfo enumInfo = GetEnumInfo(enumType);

            if (value == null)
                throw new ArgumentNullException("value");

            if (format == null)
                throw new ArgumentNullException("format");
            Contract.EndContractBlock();

            if (value.EETypePtr.IsEnum)
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

        private static String Format(EnumInfo enumInfo, Object value, String format)
        {
            ulong rawValue;
            if (!TryGetUnboxedValueOfEnumOrInteger(value, out rawValue))
            {
                Debug.Assert(false, "Caller was expected to do enough validation to avoid reaching this.");
                throw new ArgumentException();
            }

            if (format.Length != 1)
            {
                // all acceptable format string are of length 1
                throw new FormatException(SR.Format_InvalidEnumFormatSpecification);
            }

            char formatCh = format[0];

            if (formatCh == 'D' || formatCh == 'd')
            {
                return DoFormatD(rawValue, value.EETypePtr.CorElementType);
            }

            if (formatCh == 'X' || formatCh == 'x')
            {
                return DoFormatX(rawValue, value.EETypePtr.CorElementType);
            }

            if (formatCh == 'G' || formatCh == 'g')
            {
                return DoFormatG(enumInfo, rawValue);
            }

            if (formatCh == 'F' || formatCh == 'f')
            {
                return DoFormatF(enumInfo, rawValue);
            }

            throw new FormatException(SR.Format_InvalidEnumFormatSpecification);
        }

        //
        // Helper for Enum.Format(,,"d")
        //
        private static String DoFormatD(ulong rawValue, RuntimeImports.RhCorElementType corElementType)
        {
            switch (corElementType)
            {
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                    {
                        SByte result = (SByte)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                    {
                        Byte result = (Byte)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                    {
                        // direct cast from bool to byte is not allowed
                        bool b = (rawValue != 0);
                        return b.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                    {
                        Int16 result = (Int16)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                    {
                        UInt16 result = (UInt16)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                    {
                        Char result = (Char)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                    {
                        UInt32 result = (UInt32)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                    {
                        Int32 result = (Int32)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                    {
                        UInt64 result = (UInt64)rawValue;

                        return result.ToString();
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                    {
                        Int64 result = (Int64)rawValue;

                        return result.ToString();
                    }

                default:
                    Contract.Assert(false, "Invalid Object type in Format");
                    throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
            }
        }


        //
        // Helper for Enum.Format(,,"x")
        //
        private static String DoFormatX(ulong rawValue, RuntimeImports.RhCorElementType corElementType)
        {
            switch (corElementType)
            {
                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                    {
                        Byte result = (byte)(sbyte)rawValue;

                        return result.ToString("X2", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                    {
                        Byte result = (byte)rawValue;

                        return result.ToString("X2", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                    {
                        // direct cast from bool to byte is not allowed
                        Byte result = (byte)rawValue;

                        return result.ToString("X2", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                    {
                        UInt16 result = (UInt16)(Int16)rawValue;

                        return result.ToString("X4", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                    {
                        UInt16 result = (UInt16)rawValue;

                        return result.ToString("X4", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                    {
                        UInt16 result = (UInt16)(Char)rawValue;

                        return result.ToString("X4", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                    {
                        UInt32 result = (UInt32)rawValue;

                        return result.ToString("X8", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                    {
                        UInt32 result = (UInt32)(int)rawValue;

                        return result.ToString("X8", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                    {
                        UInt64 result = (UInt64)rawValue;

                        return result.ToString("X16", null);
                    }

                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                    {
                        UInt64 result = (UInt64)(Int64)rawValue;

                        return result.ToString("X16", null);
                    }

                default:
                    Contract.Assert(false, "Invalid Object type in Format");
                    throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
            }
        }

        //
        // Helper for Enum.Format(,,"g")
        //
        private static String DoFormatG(EnumInfo enumInfo, ulong rawValue)
        {
            Contract.Requires(enumInfo != null);
            if (!enumInfo.HasFlagsAttribute) // Not marked with Flags attribute
            {
                // Try to see if its one of the enum values, then we return a String back else the value
                String name = GetNameIfAny(enumInfo, rawValue);
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
        private static String DoFormatF(EnumInfo enumInfo, ulong rawValue)
        {
            Contract.Requires(enumInfo != null);

            // These values are sorted by value. Don't change this
            KeyValuePair<String, ulong>[] namesAndValues = enumInfo.NamesAndValues;

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

        internal unsafe Object GetValue()
        {
            fixed (IntPtr* pObj = &this.m_pEEType)
            {
                IntPtr pValue = Object.GetAddrOfPinnedObjectFromEETypeField(pObj);

                switch (this.EETypePtr.CorElementType)
                {
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                        return *(bool*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                        return *(char*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                        return *(sbyte*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                        return *(byte*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                        return *(short*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                        return *(ushort*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                        return *(int*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                        return *(uint*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                        return *(long*)pValue;
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                        return *(ulong*)pValue;
                    default:
                        Environment.FailFast("Unexpected enum underlying type");
                        return 0;
                }
            }
        }

        public static String GetName(Type enumType, Object value)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            if (value == null)
                throw new ArgumentNullException("value");
            ulong rawValue;
            if (!TryGetUnboxedValueOfEnumOrInteger(value, out rawValue))
                throw new ArgumentException(SR.Arg_MustBeEnumBaseTypeOrEnum, "value");

            // For desktop compatibility, do not bounce an incoming integer that's the wrong size. 
            // Do a value-preserving cast of both it and the enum values and do a 64-bit compare.

            EnumInfo enumInfo = GetEnumInfo(enumType);
            String nameOrNull = GetNameIfAny(enumInfo, rawValue);
            return nameOrNull;
        }

        public static String[] GetNames(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            KeyValuePair<String, ulong>[] namesAndValues = GetEnumInfo(enumType).NamesAndValues;
            String[] names = new String[namesAndValues.Length];
            for (int i = 0; i < namesAndValues.Length; i++)
                names[i] = namesAndValues[i].Key;
            return names;
        }

        public static Type GetUnderlyingType(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");

            RuntimeTypeHandle runtimeTypeHandle = enumType.TypeHandle;
            EETypePtr eeType = runtimeTypeHandle.ToEETypePtr();
            if (!eeType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, "enumType");

            switch (eeType.CorElementType)
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
                    throw new ArgumentException();
            }
        }

        public static Array GetValues(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            Array values = GetEnumInfo(enumType).Values;
            int count = values.Length;
            EETypePtr enumArrayType = enumType.MakeArrayType().TypeHandle.ToEETypePtr();
            Array result = RuntimeImports.RhNewArray(enumArrayType, count);
            Array.CopyImplValueTypeArrayNoInnerGcRefs(values, 0, result, 0, count);
            return result;
        }

        public Boolean HasFlag(Enum flag)
        {
            if (flag == null)
                throw new ArgumentNullException("flag");
            Contract.EndContractBlock();

            if (!(this.EETypePtr == flag.EETypePtr))
                throw new ArgumentException(SR.Format(SR.Argument_EnumTypeDoesNotMatch, flag.GetType(), this.GetType()));

            unsafe
            {
                fixed (IntPtr* pThisObj = &this.m_pEEType, pFlagObj = &flag.m_pEEType)
                {
                    IntPtr pThisValue = Object.GetAddrOfPinnedObjectFromEETypeField(pThisObj);
                    IntPtr pFlagValue = Object.GetAddrOfPinnedObjectFromEETypeField(pFlagObj);

                    switch (this.EETypePtr.CorElementTypeInfo.Log2OfSize)
                    {
                        case 0:
                            return ((*(byte*)pThisValue) & (*(byte*)pFlagValue)) == *(byte*)pFlagValue;
                        case 1:
                            return ((*(ushort*)pThisValue) & (*(ushort*)pFlagValue)) == *(ushort*)pFlagValue;
                        case 2:
                            return ((*(uint*)pThisValue) & (*(uint*)pFlagValue)) == *(uint*)pFlagValue;
                        case 3:
                            return ((*(ulong*)pThisValue) & (*(ulong*)pFlagValue)) == *(ulong*)pFlagValue;
                        default:
                            Environment.FailFast("Unexpected enum underlying type");
                            return false;
                    }
                }
            }
        }

        public static bool IsDefined(Type enumType, Object value)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            if (value == null)
                throw new ArgumentNullException("value");

            if (value.EETypePtr == typeof(string).TypeHandle.ToEETypePtr())
            {
                EnumInfo enumInfo = GetEnumInfo(enumType);
                foreach (KeyValuePair<String, ulong> kv in enumInfo.NamesAndValues)
                {
                    if (value.Equals(kv.Key))
                        return true;
                }
                return false;
            }
            else
            {
                ulong rawValue;
                if (!TryGetUnboxedValueOfEnumOrInteger(value, out rawValue))
                {
                    if (IsIntegerType(value.GetType()))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumUnderlyingTypeAndObjectMustBeSameType, value.GetType(), Enum.GetUnderlyingType(enumType)));
                    else
                        throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
                }

                EnumInfo enumInfo = null;
                if (value.EETypePtr.IsEnum)
                {
                    if (!ValueTypeMatchesEnumType(enumType, value))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, value.GetType(), enumType));
                }
                else
                {
                    enumInfo = GetEnumInfo(enumType);
                    if (!(enumInfo.UnderlyingType.TypeHandle.ToEETypePtr() == value.EETypePtr))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumUnderlyingTypeAndObjectMustBeSameType, value.GetType(), enumInfo.UnderlyingType));
                }

                if (enumInfo == null)
                    enumInfo = GetEnumInfo(enumType);
                String nameOrNull = GetNameIfAny(enumInfo, rawValue);
                return nameOrNull != null;
            }
        }

        public static Object Parse(Type enumType, String value)
        {
            return Parse(enumType, value, ignoreCase: false);
        }

        public static Object Parse(Type enumType, String value, bool ignoreCase)
        {
            Object result;
            Exception exception;
            if (!TryParseEnum(enumType, value, ignoreCase, out result, out exception))
                throw exception;
            return result;
        }

        public static unsafe Object ToObject(Type enumType, Object value)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");

            if (!enumType.TypeHandle.ToEETypePtr().IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, "enumType");

            if (value == null)
                throw new ArgumentNullException("value");

            ulong rawValue;
            bool success = TryGetUnboxedValueOfEnumOrInteger(value, out rawValue);
            if (!success)
                throw new ArgumentException(SR.Arg_MustBeEnumBaseTypeOrEnum);

            if (value.EETypePtr.IsEnum && !ValueTypeMatchesEnumType(enumType, value))
                throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, value.GetType(), enumType));

            EETypePtr enumEEType = enumType.TypeHandle.ToEETypePtr();
            return RuntimeImports.RhBox(enumEEType, &rawValue);  //@todo: Not big-endian compatible.
        }

        public static bool TryParse<TEnum>(String value, bool ignoreCase, out TEnum result) where TEnum : struct
        {
            Exception exception;
            Object tempResult;
            if (!TryParseEnum(typeof(TEnum), value, ignoreCase, out tempResult, out exception))
            {
                result = default(TEnum);
                return false;
            }
            result = (TEnum)tempResult;
            return true;
        }

        public static bool TryParse<TEnum>(String value, out TEnum result) where TEnum : struct
        {
            return TryParse<TEnum>(value, false, out result);
        }

        public override String ToString()
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

        public String ToString(String format)
        {
            if (format == null || format.Length == 0)
                format = "G";

            EnumInfo enumInfo = GetEnumInfoIfAvailable(this.GetType());

            // Project N port note: If Reflection info isn't available, fallback to ToString() which will substitute a numeric value for the "correct" output.
            // This scenario has been hit frequently when throwing exceptions formatted with error strings containing enum substitations.
            // To avoid replacing the masking the actual exception with an uninteresting MissingMetadataException, we choose instead
            // to return a base-effort string.
            if (enumInfo == null)
                return this.LastResortToString;

            return Format(enumInfo, this, format);
        }

        String IFormattable.ToString(String format, IFormatProvider provider)
        {
            return ToString(format);
        }

        [Obsolete("The provider argument is not used. Please use ToString().")]
        String IConvertible.ToString(IFormatProvider provider)
        {
            return ToString();
        }

        //
        // Note: this helper also checks if the enumType is in fact an Enum and throws an user-visible ArgumentException if it's not.
        //
        private static EnumInfo GetEnumInfo(Type enumType)
        {
            EnumInfo enumInfo = GetEnumInfoIfAvailable(enumType);
            if (enumInfo == null)
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(enumType);
            return enumInfo;
        }

        //
        // Note: this helper also checks if the enumType is in fact an Enum and throws an user-visible ArgumentException if it's not.
        //
        private static EnumInfo GetEnumInfoIfAvailable(Type enumType)
        {
            RuntimeTypeHandle runtimeTypeHandle = enumType.TypeHandle;
            if (!runtimeTypeHandle.ToEETypePtr().IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum);

            // We know this cast will succeed as we already checked for the existence of a RuntimeTypeHandle.
            RuntimeType runtimeEnumType = (RuntimeType)enumType;
            return s_enumInfoCache.GetOrAdd(runtimeEnumType);
        }

        //
        // Checks if value.GetType() matches enumType exactly. 
        //
        private static bool ValueTypeMatchesEnumType(Type enumType, Object value)
        {
            EETypePtr enumEEType;
            if (!enumType.TryGetEEType(out enumEEType))
                return false;
            if (!(enumEEType == value.EETypePtr))
                return false;
            return true;
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
        private static bool TryGetUnboxedValueOfEnumOrInteger(Object value, out ulong result)
        {
            EETypePtr eeType = value.EETypePtr;
            // For now, this check is required to flush out pointers.
            RuntimeImports.RhEETypeClassification classification = RuntimeImports.RhGetEETypeClassification(eeType);
            if (classification != RuntimeImports.RhEETypeClassification.Regular)
            {
                result = 0;
                return false;
            }
            RuntimeImports.RhCorElementType corElementType = eeType.CorElementType;
            unsafe
            {
                fixed (IntPtr* pEEType = &value.m_pEEType)
                {
                    IntPtr pValue = Object.GetAddrOfPinnedObjectFromEETypeField(pEEType);
                    switch (corElementType)
                    {
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                            result = (*(bool*)pValue) ? 1UL : 0UL;
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                            result = (ulong)(long)(*(char*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                            result = (ulong)(long)(*(sbyte*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                            result = (ulong)(long)(*(byte*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                            result = (ulong)(long)(*(short*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                            result = (ulong)(long)(*(ushort*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                            result = (ulong)(long)(*(int*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                            result = (ulong)(long)(*(uint*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                            result = (ulong)(long)(*(long*)pValue);
                            return true;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                            result = (ulong)(long)(*(ulong*)pValue);
                            return true;

                        default:
                            result = 0;
                            return false;
                    }
                }
            }
        }

        //
        // Look up a name for rawValue if a matching one exists. Returns null if no matching name exists.
        //
        private static String GetNameIfAny(EnumInfo enumInfo, ulong rawValue)
        {
            KeyValuePair<String, ulong>[] namesAndValues = enumInfo.NamesAndValues;
            KeyValuePair<String, ulong> searchKey = new KeyValuePair<String, ulong>(null, rawValue);
            int index = Array.BinarySearch<KeyValuePair<String, ulong>>(namesAndValues, searchKey, s_nameAndValueComparer);
            if (index < 0)
                return null;
            return namesAndValues[index].Key;
        }

        //
        // Common funnel for Enum.Parse methods.
        //
        private static bool TryParseEnum(Type enumType, String value, bool ignoreCase, out Object result, out Exception exception)
        {
            exception = null;
            result = null;

            if (enumType == null)
                throw new ArgumentNullException("enumType");

            RuntimeType runtimeEnumType = enumType as RuntimeType;
            if (runtimeEnumType == null)
                throw new ArgumentException(SR.Arg_MustBeType, "enumType");

            if (value == null)
            {
                exception = new ArgumentNullException("null");
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
                exception = new ArgumentException(SR.Arg_MustContainEnumInfo);
                return false;
            }

            EETypePtr enumEEType = runtimeEnumType.TypeHandle.ToEETypePtr();
            if (!enumEEType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, "enumType");

            if (TryParseAsInteger(enumEEType, value, firstNonWhitespaceIndex, out result))
                return true;

            // Parse as string. Now (and only now) do we look for metadata information.
            EnumInfo enumInfo = RuntimeAugments.Callbacks.GetEnumInfoIfAvailable(runtimeEnumType);
            if (enumInfo == null)
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(runtimeEnumType);
            ulong v = 0;

            // Port note: The docs are silent on how multiple matches are resolved when doing case-insensitive parses.
            // The desktop's ad-hoc behavior is to pick the one with the smallest value after doing a value-preserving cast
            // to a ulong, so we'll follow that here.
            StringComparison comparison = ignoreCase ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;
            KeyValuePair<String, ulong>[] actualNamesAndValues = enumInfo.NamesAndValues;
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
                foreach (KeyValuePair<String, ulong> kv in actualNamesAndValues)
                {
                    String actualName = kv.Key;
                    if (actualName.Length == valueSubstringLength &&
                        String.Compare(actualName, 0, value, valueIndex, valueSubstringLength, comparison) == 0)
                    {
                        v |= kv.Value;
                        foundMatch = true;
                        break;
                    }
                }
                if (!foundMatch)
                {
                    exception = new ArgumentException(SR.Format(SR.Arg_EnumValueNotFound, value));
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

        private static bool TryParseAsInteger(EETypePtr enumEEType, String value, int valueOffset, out Object result)
        {
            Debug.Assert(value != null, "Expected non-null value");
            Debug.Assert(value.Length > 0, "Expected non-empty value");
            Debug.Assert(valueOffset >= 0 && valueOffset < value.Length, "Expected valueOffset to be within value");

            result = null;

            char firstNonWhitespaceChar = value[valueOffset];
            if (!(Char.IsDigit(firstNonWhitespaceChar) || firstNonWhitespaceChar == '+' || firstNonWhitespaceChar == '-'))
                return false;
            RuntimeImports.RhCorElementType corElementType = enumEEType.CorElementType;

            value = value.Trim();
            unsafe
            {
                switch (corElementType)
                {
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                        {
                            Boolean v;
                            if (!Boolean.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                        {
                            Char v;
                            if (!Char.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                        {
                            SByte v;
                            if (!SByte.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                        {
                            Byte v;
                            if (!Byte.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                        {
                            Int16 v;
                            if (!Int16.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                        {
                            UInt16 v;
                            if (!UInt16.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                        {
                            Int32 v;
                            if (!Int32.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                        {
                            UInt32 v;
                            if (!UInt32.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                        {
                            Int64 v;
                            if (!Int64.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                        {
                            UInt64 v;
                            if (!UInt64.TryParse(value, out v))
                                return false;
                            result = RuntimeImports.RhBox(enumEEType, &v);
                            return true;
                        }

                    default:
                        throw new NotSupportedException();
                }
            }
        }

        //
        // Sort comparer for NamesAndValues
        //
        private class NamesAndValueComparer : IComparer<KeyValuePair<String, ulong>>
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

        private static bool IsIntegerType(Type t)
        {
            return (t == typeof(int) ||
                    t == typeof(short) ||
                    t == typeof(ushort) ||
                    t == typeof(byte) ||
                    t == typeof(sbyte) ||
                    t == typeof(uint) ||
                    t == typeof(long) ||
                    t == typeof(ulong) ||
                    t == typeof(char) ||
                    t == typeof(bool));
        }

        private static NamesAndValueComparer s_nameAndValueComparer = new NamesAndValueComparer();

        private String LastResortToString
        {
            get
            {
                return String.Format("{0}", GetValue());
            }
        }


        private sealed class EnumInfoUnifier : ConcurrentUnifierW<RuntimeType, EnumInfo>
        {
            protected override EnumInfo Factory(RuntimeType key)
            {
                return RuntimeAugments.Callbacks.GetEnumInfoIfAvailable(key);
            }
        }

        private static EnumInfoUnifier s_enumInfoCache = new EnumInfoUnifier();


        #region IConvertible
        TypeCode IConvertible.GetTypeCode()
        {
            Type enumType = this.GetType();
            Type underlyingType = GetUnderlyingType(enumType);

            if (underlyingType == typeof(Int32))
            {
                return TypeCode.Int32;
            }

            if (underlyingType == typeof(sbyte))
            {
                return TypeCode.SByte;
            }

            if (underlyingType == typeof(Int16))
            {
                return TypeCode.Int16;
            }

            if (underlyingType == typeof(Int64))
            {
                return TypeCode.Int64;
            }

            if (underlyingType == typeof(UInt32))
            {
                return TypeCode.UInt32;
            }

            if (underlyingType == typeof(byte))
            {
                return TypeCode.Byte;
            }

            if (underlyingType == typeof(UInt16))
            {
                return TypeCode.UInt16;
            }

            if (underlyingType == typeof(UInt64))
            {
                return TypeCode.UInt64;
            }

            if (underlyingType == typeof(Boolean))
            {
                return TypeCode.Boolean;
            }

            if (underlyingType == typeof(Char))
            {
                return TypeCode.Char;
            }

            Contract.Assert(false, "Unknown underlying type.");
            throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
        }

        /// <internalonly/>
        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/>
        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Enum", "DateTime"));
        }

        /// <internalonly/>
        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
        #endregion
    }
}

