// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    [ComImport]
    [Guid("4bd682dd-7554-40e9-9a9b-82654ede7e62")]
    [WindowsRuntimeImport]
    public interface IPropertyValue
    {
        PropertyType get_Type();

        bool IsNumericScalar
        {
            get;
        }

        Byte GetUInt8();
        Int16 GetInt16();
        UInt16 GetUInt16();
        Int32 GetInt32();
        UInt32 GetUInt32();
        Int64 GetInt64();
        UInt64 GetUInt64();
        Single GetSingle();
        Double GetDouble();
        char GetChar16();
        Boolean GetBoolean();
        String GetString();
        Guid GetGuid();
        DateTimeOffset GetDateTime();
        TimeSpan GetTimeSpan();
        Point GetPoint();
        Size GetSize();
        Rect GetRect();
        void GetUInt8Array(out byte[] array);
        void GetInt16Array(out Int16[] array);
        void GetUInt16Array(out UInt16[] array);
        void GetInt32Array(out Int32[] array);
        void GetUInt32Array(out UInt32[] array);
        void GetInt64Array(out Int64[] array);
        void GetUInt64Array(out UInt64[] array);
        void GetSingleArray(out Single[] array);
        void GetDoubleArray(out Double[] array);
        void GetChar16Array(out char[] array);
        void GetBooleanArray(out Boolean[] array);
        void GetStringArray(out String[] array);
        void GetInspectableArray(out object[] array);
        void GetGuidArray(out Guid[] array);
        void GetDateTimeArray(out DateTimeOffset[] array);
        void GetTimeSpanArray(out TimeSpan[] array);
        void GetPointArray(out Point[] array);
        void GetSizeArray(out Size[] array);
        void GetRectArray(out Rect[] array);
    }
}
