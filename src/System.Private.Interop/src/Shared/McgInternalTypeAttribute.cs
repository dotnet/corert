// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Internal Use Only
    /// MCG will check whether a type contains McgInternalTypeAttribute attribute,
    /// if yes, then skip import this type as CCW
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class McgInternalTypeAttribute : System.Attribute
    {
        public McgInternalTypeAttribute() { }
    }
}
