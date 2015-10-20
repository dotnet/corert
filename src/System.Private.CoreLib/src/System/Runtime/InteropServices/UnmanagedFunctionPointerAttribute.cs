// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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