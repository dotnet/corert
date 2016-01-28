// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
