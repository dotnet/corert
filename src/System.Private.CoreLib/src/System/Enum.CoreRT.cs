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
        private static TypeValuesAndNames GetCachedValuesAndNames(Type enumType, bool getNames)
        {
            TypeValuesAndNames entry = null;// EGOR: Should I add `GenericCache` property to Type? // = enumType.GenericCache as TypeValuesAndNames;
            if (entry == null || (getNames && entry.Names == null))
            {
                var info = GetEnumInfo(enumType);
                bool isFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);
                entry = new TypeValuesAndNames(isFlags, info.RawValues, info.RawNames);
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

        internal static EnumInfo GetEnumInfo(Type enumType)
        {
            Debug.Assert(enumType != null);
            Debug.Assert(enumType.IsRuntimeImplemented());
            Debug.Assert(enumType.IsEnum);

            return ReflectionAugments.ReflectionCoreCallbacks.GetEnumInfo(enumType);
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
