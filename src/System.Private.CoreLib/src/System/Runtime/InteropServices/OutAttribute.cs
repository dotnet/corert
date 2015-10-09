// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// 

// 

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class OutAttribute : Attribute
    {
        public OutAttribute()
        {
        }
    }
}