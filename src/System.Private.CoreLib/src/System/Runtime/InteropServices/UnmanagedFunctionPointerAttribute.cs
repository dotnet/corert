// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    //BARTOK expects
    [AttributeUsage(AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
    public sealed class UnmanagedFunctionPointerAttribute : Attribute
    {
        private CallingConvention _callingConvention;
        public bool BestFitMapping;
        public bool SetLastError;
        public bool ThrowOnUnmappableChar;
        public CharSet CharSet;

        public UnmanagedFunctionPointerAttribute()
        {
            _callingConvention = CallingConvention.Winapi;
        }

        public UnmanagedFunctionPointerAttribute(CallingConvention callingConvention)
        {
            _callingConvention = callingConvention;
        }

        public CallingConvention CallingConvention
        {
            get
            {
                return _callingConvention;
            }
        }
    }
}
