// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System
{
    //
    // typeof() is quite expensive so if you need to compare against a well-known system type,
    // try adding it to this class so it only gets allocated once.
    //
    internal static class CommonRuntimeTypes
    {
        internal static Type Object { get; } = typeof(object);
        internal static Type ValueType { get; } = typeof(ValueType);
        internal static Type Type { get; } = typeof(Type);
        internal static Type Attribute { get; } = typeof(Attribute);
        internal static Type String { get; } = typeof(string);
        internal static Type Array { get; } = typeof(Array);
        internal static Type Enum { get; } = typeof(Enum);
        internal static Type Boolean { get; } = typeof(bool);
        internal static Type Char { get; } = typeof(char);
        internal static Type Byte { get; } = typeof(byte);
        internal static Type SByte { get; } = typeof(sbyte);
        internal static Type UInt16 { get; } = typeof(ushort);
        internal static Type Int16 { get; } = typeof(short);
        internal static Type UInt32 { get; } = typeof(uint);
        internal static Type Int32 { get; } = typeof(int);
        internal static Type UInt64 { get; } = typeof(ulong);
        internal static Type Int64 { get; } = typeof(long);
        internal static Type UIntPtr { get; } = typeof(UIntPtr);
        internal static Type IntPtr { get; } = typeof(IntPtr);
        internal static Type Single { get; } = typeof(float);
        internal static Type Double { get; } = typeof(double);
        internal static Type Decimal { get { return NotSoCommonTypes.s_decimal; } }
        internal static Type DateTime { get { return NotSoCommonTypes.s_datetime; } }
        internal static Type Nullable { get; } = typeof(Nullable<>);
        internal static Type Void { get; } = typeof(void);
        internal static Type MulticastDelegate { get; } = typeof(MulticastDelegate);

        // Following types are not so common and their ToString and formatting is particularly heavy.
        // Make it less likely that they'll get included in small projects by placing them into
        // a separate class constructor.
        private static class NotSoCommonTypes
        {
            internal static Type s_decimal = typeof(decimal);
            internal static Type s_datetime = typeof(DateTime);
        }
    }
}
