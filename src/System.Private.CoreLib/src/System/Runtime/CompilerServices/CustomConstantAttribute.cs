// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class CustomConstantAttribute : Attribute
    {
        public abstract Object Value { get; }
    }
}

