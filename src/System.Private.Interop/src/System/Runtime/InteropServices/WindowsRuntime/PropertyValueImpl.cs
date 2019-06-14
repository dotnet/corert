// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    [McgInternalTypeAttribute]
    public class PropertyValueImpl : BoxedValue, IPropertyValue
    {
        internal PropertyValueImpl(object val, int type) : base(val, type)
        {
        }

        public PropertyType get_Type()
        {
            return (PropertyType)m_type;
        }

        public bool IsNumericScalar
        {
            get
            {
                return IsNumericScalarImpl((PropertyType)m_type, m_data);
            }
        }

        public byte GetUInt8()
        {
            return CoerceScalarValue<byte>(PropertyType.UInt8);
        }

        public short GetInt16()
        {
            return CoerceScalarValue<short>(PropertyType.Int16);
        }

        public ushort GetUInt16()
        {
            return CoerceScalarValue<ushort>(PropertyType.UInt16);
        }

        public int GetInt32()
        {
            return CoerceScalarValue<int>(PropertyType.Int32);
        }

        public uint GetUInt32()
        {
            return CoerceScalarValue<uint>(PropertyType.UInt32);
        }

        public long GetInt64()
        {
            return CoerceScalarValue<long>(PropertyType.Int64);
        }

        public ulong GetUInt64()
        {
            return CoerceScalarValue<ulong>(PropertyType.UInt64);
        }

        public float GetSingle()
        {
            return CoerceScalarValue<float>(PropertyType.Single);
        }

        public double GetDouble()
        {
            return CoerceScalarValue<double>(PropertyType.Double);
        }

        public char GetChar16()
        {
            CheckType(PropertyType.Char16);
            return (char)m_data;
        }

        public bool GetBoolean()
        {
            CheckType(PropertyType.Boolean);
            return (bool)m_data;
        }

        public string GetString()
        {
            return CoerceScalarValue<string>(PropertyType.String);
        }

        public object GetInspectable()
        {
            CheckType(PropertyType.Inspectable);
            return m_data;
        }

        public System.Guid GetGuid()
        {
            return CoerceScalarValue<System.Guid>(PropertyType.Guid);
        }

        public System.DateTimeOffset GetDateTime()
        {
            CheckType(PropertyType.DateTime);
            return (System.DateTimeOffset)m_data;
        }

        public System.TimeSpan GetTimeSpan()
        {
            CheckType(PropertyType.TimeSpan);
            return (System.TimeSpan)m_data;
        }

        public global::Windows.Foundation.Point GetPoint()
        {
            CheckType(PropertyType.Point);
            return (global::Windows.Foundation.Point)m_data;
        }

        public global::Windows.Foundation.Size GetSize()
        {
            CheckType(PropertyType.Size);
            return (global::Windows.Foundation.Size)m_data;
        }

        public global::Windows.Foundation.Rect GetRect()
        {
            CheckType(PropertyType.Rect);
            return (global::Windows.Foundation.Rect)m_data;
        }

        public void GetUInt8Array(out byte[] array)
        {
            array = CoerceArrayValue<byte>(PropertyType.UInt8Array);
        }

        public void GetInt16Array(out short[] array)
        {
            array = CoerceArrayValue<short>(PropertyType.Int16Array);
        }

        public void GetUInt16Array(out ushort[] array)
        {
            array = CoerceArrayValue<ushort>(PropertyType.UInt16Array);
        }

        public void GetInt32Array(out int[] array)
        {
            array = CoerceArrayValue<int>(PropertyType.Int32Array);
        }

        public void GetUInt32Array(out uint[] array)
        {
            array = CoerceArrayValue<uint>(PropertyType.UInt32Array);
        }

        public void GetInt64Array(out long[] array)
        {
            array = CoerceArrayValue<long>(PropertyType.Int64Array);
        }

        public void GetUInt64Array(out ulong[] array)
        {
            array = CoerceArrayValue<ulong>(PropertyType.UInt64Array);
        }

        public void GetSingleArray(out float[] array)
        {
            array = CoerceArrayValue<float>(PropertyType.SingleArray);
        }

        public void GetDoubleArray(out double[] array)
        {
            array = CoerceArrayValue<double>(PropertyType.DoubleArray);
        }

        public void GetChar16Array(out char[] array)
        {
            CheckType(PropertyType.Char16Array);
            array = (char[])m_data;
        }

        public void GetBooleanArray(out bool[] array)
        {
            CheckType(PropertyType.BooleanArray);
            array = (bool[])m_data;
        }

        public void GetStringArray(out string[] array)
        {
            array = CoerceArrayValue<string>(PropertyType.StringArray);
        }

        public void GetInspectableArray(out object[] array)
        {
            CheckType(PropertyType.InspectableArray);
            array = (object[])m_data;
        }

        public void GetGuidArray(out System.Guid[] array)
        {
            array = CoerceArrayValue<System.Guid>(PropertyType.GuidArray);
        }

        public void GetDateTimeArray(out System.DateTimeOffset[] array)
        {
            CheckType(PropertyType.DateTimeArray);
            array = (System.DateTimeOffset[])m_data;
        }

        public void GetTimeSpanArray(out System.TimeSpan[] array)
        {
            CheckType(PropertyType.TimeSpanArray);
            array = (System.TimeSpan[])m_data;
        }

        public void GetPointArray(out global::Windows.Foundation.Point[] array)
        {
            CheckType(PropertyType.PointArray);
            array = (global::Windows.Foundation.Point[])m_data;
        }

        public void GetSizeArray(out global::Windows.Foundation.Size[] array)
        {
            CheckType(PropertyType.SizeArray);
            array = (global::Windows.Foundation.Size[])m_data;
        }

        public void GetRectArray(out global::Windows.Foundation.Rect[] array)
        {
            CheckType(PropertyType.RectArray);
            array = (global::Windows.Foundation.Rect[])m_data;
        }

        private T[] CoerceArrayValue<T>(PropertyType unboxType)
        {
            // If we contain the type being looked for directly, then take the fast-path
            if (m_type == (int)unboxType)
            {
                return (T[])m_data;
            }

            // Make sure we have an array to begin with
            System.Array dataArray = m_data as System.Array;

            if (dataArray == null)
            {
                throw CreateExceptionForInvalidCast((PropertyType)m_type, unboxType);
            }

            // Array types are 1024 larger than their equivilent scalar counterpart
            if ((m_type <= 1024) || ((int)unboxType <= 1024))
            {
                throw CreateExceptionForInvalidCast((PropertyType)m_type, unboxType);
            }

            PropertyType scalarType = (PropertyType)(m_type - 1024);
            PropertyType unboxTypeT = unboxType - 1024;

            // If we do not have the correct array type, then we need to convert the array element-by-element
            // to a new array of the requested type
            T[] coercedArray = new T[dataArray.Length];

            for (int i = 0; i < dataArray.Length; ++i)
            {
                coercedArray[i] = (T)CoerceScalarValue(scalarType, dataArray.GetValue(i), unboxTypeT);
            }

            return coercedArray;
        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private T CoerceScalarValue<T>(PropertyType unboxType)
        {
            object result = m_data;

            // If we are just a boxed version of the requested type, then take the fast path out
            if (m_type != (int)unboxType)
            {
                result = CoerceScalarValue((PropertyType)m_type, result, unboxType);
            }

            return (T)result;
        }

        static private object CoerceScalarValue(PropertyType type, object value, PropertyType unboxType)
        {
            // If the property type is neither one of the coercable numeric types nor IInspectable, we
            // should not attempt coersion, even if the underlying value is technically convertable
            if ((type == PropertyType.Guid) && (unboxType == PropertyType.String))
            {
                // String <--> Guid is allowed
                return ((System.Guid)value).ToString();
            }
            else if ((type == PropertyType.String) && (unboxType == PropertyType.Guid))
            {
                System.Guid result;

                if (System.Guid.TryParse((string)value, out result))
                {
                    return result;
                }
            }
            else if (type == PropertyType.Inspectable)
            {
                // If the property type is IInspectable, and we have a nested IPropertyValue, then we need
                // to pass along the request to coerce the value.
                IPropertyValue ipv = value as IPropertyValue;

                if (ipv != null)
                {
                    object result = ReferenceUtility.GetWellKnownScalar(ipv, unboxType);

                    if (result != null)
                    {
                        return result;
                    }

                    Debug.Assert(
                        false,
                        "T in coersion function wasn't understood as a type that can be coerced - make sure that CoerceScalarValue and NumericScalarTypes are in sync"
                    );
                }
            }
            else if (type == PropertyType.Boolean || type == PropertyType.Char16)
            {
                throw CreateExceptionForInvalidCoersion(type, value, unboxType, Interop.COM.TYPE_E_TYPEMISMATCH);
            }

            //
            // Let Convert handle all possible conversions - this include 
            // 1. string - which desktop code accidentally allowed
            // 2. object (IInspectable)
            // 
            try
            {
                switch (unboxType)
                {
                    case PropertyType.UInt8:
                        return System.Convert.ToByte(value);

                    case PropertyType.Int16:
                        return System.Convert.ToInt16(value);

                    case PropertyType.UInt16:
                        return System.Convert.ToUInt16(value);

                    case PropertyType.Int32:
                        return System.Convert.ToInt32(value);

                    case PropertyType.UInt32:
                        return System.Convert.ToUInt32(value);

                    case PropertyType.Int64:
                        return System.Convert.ToInt64(value);

                    case PropertyType.UInt64:
                        return System.Convert.ToUInt64(value);

                    case PropertyType.Single:
                        return System.Convert.ToSingle(value);

                    case PropertyType.Double:
                        return System.Convert.ToDouble(value);

                    default:
                        break;
                }
            }
            catch (System.FormatException)
            {
                throw CreateExceptionForInvalidCoersion(type, value, unboxType, Interop.COM.TYPE_E_TYPEMISMATCH);
            }
            catch (System.InvalidCastException)
            {
                throw CreateExceptionForInvalidCoersion(type, value, unboxType, Interop.COM.TYPE_E_TYPEMISMATCH);
            }
            catch (System.OverflowException)
            {
                throw CreateExceptionForInvalidCoersion(type, value, unboxType, Interop.COM.DISP_E_OVERFLOW);
            }

            throw CreateExceptionForInvalidCast(type, unboxType);
        }

        private static bool IsNumericScalarImpl(PropertyType type, object data)
        {
            switch (type)
            {
                case PropertyType.UInt8:
                case PropertyType.Int16:
                case PropertyType.UInt16:
                case PropertyType.Int32:
                case PropertyType.UInt32:
                case PropertyType.Int64:
                case PropertyType.UInt64:
                case PropertyType.Single:
                case PropertyType.Double:
                    return true;

                default:
                    return McgMarshal.IsEnum(data);
            }
        }

        private void CheckType(PropertyType unboxType)
        {
            if (this.get_Type() != unboxType)
            {
                throw CreateExceptionForInvalidCast(this.get_Type(), unboxType);
            }
        }

        private static System.InvalidCastException CreateExceptionForInvalidCast(
            PropertyType type,
            PropertyType unboxType)
        {
            return new System.InvalidCastException(SR.Format(SR.PropertyValue_InvalidCast, type, unboxType), Interop.COM.TYPE_E_TYPEMISMATCH);
        }

        private static System.InvalidCastException CreateExceptionForInvalidCoersion(
            PropertyType type,
            object value,
            PropertyType unboxType,
            int hr)
        {
            return new InvalidCastException(SR.Format(SR.PropertyValue_InvalidCoersion, type, value, unboxType), hr);
        }
    }

    internal class ReferenceUtility
    {
        internal static object GetWellKnownScalar(IPropertyValue ipv, PropertyType type)
        {
            switch (type)
            {
                case PropertyType.UInt8:
                    return ipv.GetUInt8();

                case PropertyType.Int16:
                    return ipv.GetInt16();

                case PropertyType.UInt16:
                    return ipv.GetUInt16();

                case PropertyType.Int32:
                    return ipv.GetInt32();

                case PropertyType.UInt32:
                    return ipv.GetUInt32();

                case PropertyType.Int64:
                    return ipv.GetInt64();

                case PropertyType.UInt64:
                    return ipv.GetUInt64();

                case PropertyType.Single:
                    return ipv.GetSingle();

                case PropertyType.Double:
                    return ipv.GetDouble();
            }

            Debug.Assert(false);
            return null;
        }
    }
}
