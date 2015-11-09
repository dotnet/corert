// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.InteropServices
{
    // Not used in Redhawk. Only here as C# compiler requires it
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class OutAttribute : Attribute
    {
        public OutAttribute()
        {
        }
    }
}
