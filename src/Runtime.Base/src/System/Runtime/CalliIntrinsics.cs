// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.InteropServices
{
    [AttributeUsageAttribute(AttributeTargets.Class)]
    internal class McgIntrinsicsAttribute : Attribute { }
}

namespace System.Runtime
{
    using System.Runtime.InteropServices;

    [McgIntrinsics]
    internal static unsafe partial class CalliIntrinsics
    {
        internal static void CallVoid(IntPtr pfn) { Call<int>(pfn); }
        internal static void CallVoid(IntPtr pfn, object arg0) { Call<int>(pfn, arg0); }
        internal static void CallVoid(IntPtr pfn, IntPtr arg0, object arg1) { Call<int>(pfn, arg0, arg1); }
        internal static void CallVoid(IntPtr pfn, RhFailFastReason arg0, object arg1, IntPtr arg2) { Call<int>(pfn, arg0, arg1, arg2); }
        internal static void CallVoid(IntPtr pfn, object arg0, IntPtr arg1, int arg2) { Call<int>(pfn, arg0, arg1, arg2); }

        internal static T Call<T>(IntPtr pfn) { throw new NotImplementedException(); }
        internal static T Call<T>(IntPtr pfn, object arg0) { throw new NotImplementedException(); }
        internal static T Call<T>(IntPtr pfn, IntPtr arg0, object arg1) { throw new NotImplementedException(); }
        internal static T Call<T>(IntPtr pfn, RhFailFastReason arg0, object arg1, IntPtr arg2) { throw new NotImplementedException(); }
        internal static T Call<T>(IntPtr pfn, object arg0, IntPtr arg1, int arg2) { throw new NotImplementedException(); }
        internal static T Call<T>(IntPtr pfn, object arg0, IntPtr arg1) { throw new NotImplementedException(); }
        internal static T Call<T>(IntPtr pfn, ExceptionIDs arg0) { throw new NotImplementedException(); }
        internal static T Call<T>(IntPtr pfn, object arg0, void* arg1, out Exception arg2) { throw new NotImplementedException(); }
    }
}
