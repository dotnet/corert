// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class DllImportAttribute : Attribute
    {
        public CallingConvention CallingConvention;

        public DllImportAttribute(string dllName)
        {
        }
    }
}
