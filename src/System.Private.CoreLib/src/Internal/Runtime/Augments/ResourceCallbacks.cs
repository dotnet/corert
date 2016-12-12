// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Internal.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>
    /// This helper class is used to provide resource support
    /// </summary>
    [CLSCompliant(false)]
    public abstract class ResourceCallbacks
    {
        /// <summary>
        /// Read a resource out of an assembly
        /// </summary>
        public abstract string GetResource(Assembly assembly, string resourceName, string name);
    }
}
