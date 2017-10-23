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

        public global::Windows.Foundation.PropertyType Type
        {
            get
            {
                return (global::Windows.Foundation.PropertyType)m_type;
            }
        }

        public bool IsNumericScalar
        {
            get
            {
                return IsNumericScalarImpl((global::Windows.Foundation.PropertyType)m_type, m_data);
            }
        }

        public byte GetUInt8()
        {
            return CoerceScalarValue<byte>(global::Windows.Foundation.PropertyType.UInt8);
        }

        public short GetInt16()
        {
            return CoerceScalarValue<short>(global::Windows.Foundation.PropertyType.Int16);
        }

        public ushort GetUInt16()
        {
            return CoerceScalarValue<ushort>(global::Windows.Foundation.PropertyType.UInt16);
        }

        public int GetInt32()
        {
            return CoerceScalarValue<int>(global::Windows.Foundation.PropertyType.Int32);
        }

        public uint GetUInt32()
        {
            return CoerceScalarValue<uint>(global::Windows.Foundation.PropertyType.UInt32);
        }

        public long GetInt64()
        {
            return CoerceScalarValue<long>(global::Windows.Foundation.PropertyType.Int64);
        }

        public ulong GetUInt64()
        {
            return CoerceScalarValue<ulong>(global::Windows.Foundation.PropertyType.UInt64);
        }

        public float GetSingle()
        {
            return CoerceScalarValue<float>(global::Windows.Foundation.PropertyType.Single);
        }

        public double GetDouble()
        {
            return CoerceScalarValue<double>(global::Windows.Foundation.PropertyType.Double);
        }

        public char GetChar16()
        {
            CheckType(global::Windows.Foundation.PropertyType.Char16);
            return (char)m_data;
        }

        public bool GetBoolean()
        {
            CheckType(global::Windows.Foundation.PropertyType.Boolean);
            return (bool)m_data;
        }

        public string GetString()
        {
            return CoerceScalarValue<string>(global::Windows.Foundation.PropertyType.String);
        }

        public object GetInspectable()
        {
            CheckType(global::Windows.Foundation.PropertyType.Inspectable);
            return m_data;
        }

        public System.Guid GetGuid()
        {
            return CoerceScalarValue<System.Guid>(global::Windows.Foundation.PropertyType.Guid);
        }

        public System.DateTimeOffset GetDateTime()
        {
            CheckType(global::Windows.Foundation.PropertyType.DateTime);
            return (System.DateTimeOffset)m_data;
        }

        public System.TimeSpan GetTimeSpan()
        {
            CheckType(global::Windows.Foundation.PropertyType.TimeSpan);
            return (System.TimeSpan)m_data;
        }

        public global::Windows.Foundation.Point GetPoint()
        {
            CheckType(global::Windows.Foundation.PropertyType.Point);
            return (global::Windows.Foundation.Point)m_data;
        }

        public global::Windows.Foundation.Size GetSize()
        {
            CheckType(global::Windows.Foundation.PropertyType.Size);
            return (global::Windows.Foundation.Size)m_data;
        }

        public global::Windows.Foundation.Rect GetRect()
        {
            CheckType(global::Windows.Foundation.PropertyType.Rect);
            return (global::Windows.Foundation.Rect)m_data;
        }

        public void GetUInt8Array(out byte[] array)
        {
            array = CoerceArrayValue<byte>(global::Windows.Foundation.PropertyType.UInt8Array);
        }

        public void GetInt16Array(out short[] array)
        {
            array = CoerceArrayValue<short>(global::Windows.Foundation.PropertyType.Int16Array);
        }

        public void GetUInt16Array(out ushort[] array)
        {
            array = CoerceArrayValue<ushort>(global::Windows.Foundation.PropertyType.UInt16Array);
        }

        public void GetInt32Array(out int[] array)
        {
            array = CoerceArrayValue<int>(global::Windows.Foundation.PropertyType.Int32Array);
        }

        public void GetUInt32Array(out uint[] array)
        {
            array = CoerceArrayValue<uint>(global::Windows.Foundation.PropertyType.UInt32Array);
        }

        public void GetInt64Array(out long[] array)
        {
            array = CoerceArrayValue<long>(global::Windows.Foundation.PropertyType.Int64Array);
        }

        public void GetUInt64Array(out ulong[] array)
        {
            array = CoerceArrayValue<ulong>(global::Windows.Foundation.PropertyType.UInt64Array);
        }

        public void GetSingleArray(out float[] array)
        {
            array = CoerceArrayValue<float>(global::Windows.Foundation.PropertyType.SingleArray);
        }

        public void GetDoubleArray(out double[] array)
        {
            array = CoerceArrayValue<double>(global::Windows.Foundation.PropertyType.DoubleArray);
        }

        public void GetChar16Array(out char[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.Char16Array);
            array = (char[])m_data;
        }

        public void GetBooleanArray(out bool[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.BooleanArray);
            array = (bool[])m_data;
        }

        public void GetStringArray(out string[] array)
        {
            array = CoerceArrayValue<string>(global::Windows.Foundation.PropertyType.StringArray);
        }

        public void GetInspectableArray(out object[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.InspectableArray);
            array = (object[])m_data;
        }

        public void GetGuidArray(out System.Guid[] array)
        {
            array = CoerceArrayValue<System.Guid>(global::Windows.Foundation.PropertyType.GuidArray);
        }

        public void GetDateTimeArray(out System.DateTimeOffset[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.DateTimeArray);
            array = (System.DateTimeOffset[])m_data;
        }

        public void GetTimeSpanArray(out System.TimeSpan[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.TimeSpanArray);
            array = (System.TimeSpan[])m_data;
        }

        public void GetPointArray(out global::Windows.Foundation.Point[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.PointArray);
            array = (global::Windows.Foundation.Point[])m_data;
        }

        public void GetSizeArray(out global::Windows.Foundation.Size[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.SizeArray);
            array = (global::Windows.Foundation.Size[])m_data;
        }

        public void GetRectArray(out global::Windows.Foundation.Rect[] array)
        {
            CheckType(global::Windows.Foundation.PropertyType.RectArray);
            array = (global::Windows.Foundation.Rect[])m_data;
        }

        private T[] CoerceArrayValue<T>(global::Windows.Foundation.PropertyType unboxType)
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
                throw CreateExceptionForInvalidCast((global::Windows.Foundation.PropertyType)m_type, unboxType);
            }

            // Array types are 1024 larger than their equivilent scalar counterpart
            if ((m_type <= 1024) || ((int)unboxType <= 1024))
            {
                throw CreateExceptionForInvalidCast((global::Windows.Foundation.PropertyType)m_type, unboxType);
            }

            global::Windows.Foundation.PropertyType scalarType = (global::Windows.Foundation.PropertyType)(m_type - 1024);
            global::Windows.Foundation.PropertyType unboxTypeT = unboxType - 1024;

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
        private T CoerceScalarValue<T>(global::Windows.Foundation.PropertyType unboxType)
        {
            object result = m_data;

            // If we are just a boxed version of the requested type, then take the fast path out
            if (m_type != (int)unboxType)
            {
                result = CoerceScalarValue((global::Windows.Foundation.PropertyType)m_type, result, unboxType);
            }

            return (T)result;
        }

        static private object CoerceScalarValue(global::Windows.Foundation.PropertyType type, object value, global::Windows.Foundation.PropertyType unboxType)
        {
            // If the property type is neither one of the coercable numeric types nor IInspectable, we
            // should not attempt coersion, even if the underlying value is technically convertable
            if ((type == global::Windows.Foundation.PropertyType.Guid) && (unboxType == global::Windows.Foundation.PropertyType.String))
            {
                // String <--> Guid is allowed
                return ((System.Guid)value).ToString();
            }
            else if ((type == global::Windows.Foundation.PropertyType.String) && (unboxType == global::Windows.Foundation.PropertyType.Guid))
            {
                System.Guid result;

                if (System.Guid.TryParse((string)value, out result))
                {
                    return result;
                }
            }
            else if (type == global::Windows.Foundation.PropertyType.Inspectable)
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
            else if (type == global::Windows.Foundation.PropertyType.Boolean || type == global::Windows.Foundation.PropertyType.Char16)
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
                    case global::Windows.Foundation.PropertyType.UInt8:
                        return System.Convert.ToByte(value);

                    case global::Windows.Foundation.PropertyType.Int16:
                        return System.Convert.ToInt16(value);

                    case global::Windows.Foundation.PropertyType.UInt16:
                        return System.Convert.ToUInt16(value);

                    case global::Windows.Foundation.PropertyType.Int32:
                        return System.Convert.ToInt32(value);

                    case global::Windows.Foundation.PropertyType.UInt32:
                        return System.Convert.ToUInt32(value);

                    case global::Windows.Foundation.PropertyType.Int64:
                        return System.Convert.ToInt64(value);

                    case global::Windows.Foundation.PropertyType.UInt64:
                        return System.Convert.ToUInt64(value);

                    case global::Windows.Foundation.PropertyType.Single:
                        return System.Convert.ToSingle(value);

                    case global::Windows.Foundation.PropertyType.Double:
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

        private static bool IsNumericScalarImpl(global::Windows.Foundation.PropertyType type, object data)
        {
            switch (type)
            {
                case global::Windows.Foundation.PropertyType.UInt8:
                case global::Windows.Foundation.PropertyType.Int16:
                case global::Windows.Foundation.PropertyType.UInt16:
                case global::Windows.Foundation.PropertyType.Int32:
                case global::Windows.Foundation.PropertyType.UInt32:
                case global::Windows.Foundation.PropertyType.Int64:
                case global::Windows.Foundation.PropertyType.UInt64:
                case global::Windows.Foundation.PropertyType.Single:
                case global::Windows.Foundation.PropertyType.Double:
                    return true;

                default:
                    return McgMarshal.IsEnum(data);
            }
        }

        private void CheckType(global::Windows.Foundation.PropertyType unboxType)
        {
            if (this.Type != unboxType)
            {
                throw CreateExceptionForInvalidCast(this.Type, unboxType);
            }
        }

        private static System.InvalidCastException CreateExceptionForInvalidCast(
            global::Windows.Foundation.PropertyType type,
            global::Windows.Foundation.PropertyType unboxType)
        {
            System.InvalidCastException ex = new System.InvalidCastException(SR.Format(SR.PropertyValue_InvalidCast, type, unboxType));
            McgMarshal.SetExceptionErrorCode(ex, Interop.COM.TYPE_E_TYPEMISMATCH);
            return ex;
        }

        private static System.InvalidCastException CreateExceptionForInvalidCoersion(
            global::Windows.Foundation.PropertyType type,
            object value,
            global::Windows.Foundation.PropertyType unboxType,
            int hr)
        {
            InvalidCastException ex = new InvalidCastException(SR.Format(SR.PropertyValue_InvalidCoersion, type, value, unboxType));
            McgMarshal.SetExceptionErrorCode(ex, hr);
            return ex;
        }
    }

    internal class ReferenceUtility
    {
        internal static object GetWellKnownScalar(IPropertyValue ipv, global::Windows.Foundation.PropertyType type)
        {
            switch (type)
            {
                case global::Windows.Foundation.PropertyType.UInt8:
                    return ipv.GetUInt8();

                case global::Windows.Foundation.PropertyType.Int16:
                    return ipv.GetInt16();

                case global::Windows.Foundation.PropertyType.UInt16:
                    return ipv.GetUInt16();

                case global::Windows.Foundation.PropertyType.Int32:
                    return ipv.GetInt32();

                case global::Windows.Foundation.PropertyType.UInt32:
                    return ipv.GetUInt32();

                case global::Windows.Foundation.PropertyType.Int64:
                    return ipv.GetInt64();

                case global::Windows.Foundation.PropertyType.UInt64:
                    return ipv.GetUInt64();

                case global::Windows.Foundation.PropertyType.Single:
                    return ipv.GetSingle();

                case global::Windows.Foundation.PropertyType.Double:
                    return ipv.GetDouble();
            }

            Debug.Assert(false);
            return null;
        }
    }
}
