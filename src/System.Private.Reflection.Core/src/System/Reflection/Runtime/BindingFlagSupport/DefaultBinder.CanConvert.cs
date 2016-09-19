// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

using Internal.LowLevelLinq;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    internal sealed partial class DefaultBinder : Binder
    {

        // CanConvertPrimitive
        // This will determine if the source can be converted to the target type
        private static bool CanConvertPrimitive(Type source, Type target)
        {
            return CanPrimitiveWiden(source, target);
        }

        // CanConvertPrimitiveObjectToType
        private static bool CanConvertPrimitiveObjectToType(Object source, Type type)
        {
            return CanConvertPrimitive(source.GetType(), type);
        }

        private static bool CanPrimitiveWiden(Type source, Type target)
        {
            Primitives widerCodes = _primitiveConversions[(int)GetTypeCode(source)];
            Primitives targetCode = (Primitives)(1 << (int)GetTypeCode(target));

            return 0 != (widerCodes & targetCode);
        }

        [Flags]
        private enum Primitives
        {
            Boolean = 1 << (int)TypeCode.Boolean,
            Char = 1 << (int)TypeCode.Char,
            SByte = 1 << (int)TypeCode.SByte,
            Byte = 1 << (int)TypeCode.Byte,
            Int16 = 1 << (int)TypeCode.Int16,
            UInt16 = 1 << (int)TypeCode.UInt16,
            Int32 = 1 << (int)TypeCode.Int32,
            UInt32 = 1 << (int)TypeCode.UInt32,
            Int64 = 1 << (int)TypeCode.Int64,
            UInt64 = 1 << (int)TypeCode.UInt64,
            Single = 1 << (int)TypeCode.Single,
            Double = 1 << (int)TypeCode.Double,
            Decimal = 1 << (int)TypeCode.Decimal,
            DateTime = 1 << (int)TypeCode.DateTime,
            String = 1 << (int)TypeCode.String,
        }

        private static Primitives[] _primitiveConversions = new Primitives[]
        {
                /* Empty    */  0, // not primitive
                /* Object   */  0, // not primitive
                /* DBNull   */  0, // not exposed.
                /* Boolean  */  Primitives.Boolean,
                /* Char     */  Primitives.Char    | Primitives.UInt16 | Primitives.UInt32 | Primitives.Int32  | Primitives.UInt64 | Primitives.Int64  | Primitives.Single |  Primitives.Double,
                /* SByte    */  Primitives.SByte   | Primitives.Int16  | Primitives.Int32  | Primitives.Int64  | Primitives.Single | Primitives.Double,
                /* Byte     */  Primitives.Byte    | Primitives.Char   | Primitives.UInt16 | Primitives.Int16  | Primitives.UInt32 | Primitives.Int32  | Primitives.UInt64 |  Primitives.Int64 |  Primitives.Single |  Primitives.Double,
                /* Int16    */  Primitives.Int16   | Primitives.Int32  | Primitives.Int64  | Primitives.Single | Primitives.Double,
                /* UInt16   */  Primitives.UInt16  | Primitives.UInt32 | Primitives.Int32  | Primitives.UInt64 | Primitives.Int64  | Primitives.Single | Primitives.Double,
                /* Int32    */  Primitives.Int32   | Primitives.Int64  | Primitives.Single | Primitives.Double |
                /* UInt32   */  Primitives.UInt32  | Primitives.UInt64 | Primitives.Int64  | Primitives.Single | Primitives.Double,
                /* Int64    */  Primitives.Int64   | Primitives.Single | Primitives.Double,
                /* UInt64   */  Primitives.UInt64  | Primitives.Single | Primitives.Double,
                /* Single   */  Primitives.Single  | Primitives.Double,
                /* Double   */  Primitives.Double,
                /* Decimal  */  Primitives.Decimal,
                /* DateTime */  Primitives.DateTime,
                /* [Unused] */  0,
                /* String   */  Primitives.String,
        };

        private static TypeCode GetTypeCode(Type type)
        {
            if (type == typeof(Boolean))
                return TypeCode.Boolean;

            if (type == typeof(Char))
                return TypeCode.Char;

            if (type == typeof(SByte))
                return TypeCode.SByte;

            if (type == typeof(Byte))
                return TypeCode.Byte;

            if (type == typeof(Int16))
                return TypeCode.Int16;

            if (type == typeof(UInt16))
                return TypeCode.UInt16;

            if (type == typeof(Int32))
                return TypeCode.Int32;

            if (type == typeof(UInt32))
                return TypeCode.UInt32;

            if (type == typeof(Int64))
                return TypeCode.Int64;

            if (type == typeof(UInt64))
                return TypeCode.UInt64;

            if (type == typeof(Single))
                return TypeCode.Single;

            if (type == typeof(Double))
                return TypeCode.Double;

            if (type == typeof(Decimal))
                return TypeCode.Decimal;

            if (type == typeof(DateTime))
                return TypeCode.DateTime;

            if (type.GetTypeInfo().IsEnum)
                return GetTypeCode(Enum.GetUnderlyingType(type));

            return TypeCode.Object;
        }
    }
}
