// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private static EnumInfo GetEnumInfo(Type enumType)
        {
            Debug.Assert(enumType != null);
            Debug.Assert(enumType.IsRuntimeImplemented());
            Debug.Assert(enumType.IsEnum);

            return ReflectionAugments.ReflectionCoreCallbacks.GetEnumInfo(enumType);
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

        public int CompareTo(object target)
        {
            if (target == null)
                return 1;

            if (target == this)
                return 0;

            if (this.EETypePtr != target.EETypePtr)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, target.GetType(), this.GetType()));
            }

            ref byte pThisValue = ref this.GetRawData();
            ref byte pTargetValue = ref target.GetRawData();

            // Compare the values. Note that we're required to return 0/1/-1 for backwards compat.
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
                    throw new InvalidOperationException(SR.InvalidOperation_UnknownEnumType);
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
                    Debug.Fail("Invalid primitive type");
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
                    Debug.Fail("Invalid primitive type");
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

            return GetEnumName(enumType, rawValue);
        }

        public static string[] GetNames(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsRuntimeImplemented())
                return enumType.GetEnumNames();

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum);

            string[] ret = GetEnumInfo(enumType).Names;

            // Make a copy since we can't hand out the same array since users can modify them
            return new ReadOnlySpan<string>(ret).ToArray();
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

            Array values = GetEnumInfo(enumType).ValuesAsUnderlyingType;
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

        public static bool IsDefined(Type enumType, object value)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsRuntimeImplemented())
                return enumType.IsEnumDefined(value);

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (!enumType.IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum);

            if (value is string valueAsString)
            {
                EnumInfo enumInfo = GetEnumInfo(enumType);
                foreach (string name in enumInfo.Names)
                {
                    if (valueAsString == name)
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

                EnumInfo enumInfo = GetEnumInfo(enumType);
                if (value is Enum)
                {
                    if (!ValueTypeMatchesEnumType(enumType, value))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumAndObjectMustBeSameType, value.GetType(), enumType));
                }
                else
                {
                    if (!(enumInfo.UnderlyingType.TypeHandle.ToEETypePtr() == value.EETypePtr))
                        throw new ArgumentException(SR.Format(SR.Arg_EnumUnderlyingTypeAndObjectMustBeSameType, value.GetType(), enumInfo.UnderlyingType));
                }

                return GetEnumName(enumInfo, rawValue) != null;
            }
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
