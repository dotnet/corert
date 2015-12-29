// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

using System;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    // Applies to read-only array parameters
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
    public sealed class ReadOnlyArrayAttribute : Attribute
    {
        public ReadOnlyArrayAttribute() { }
    }
}
