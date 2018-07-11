// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime.CompilerServices
{
    /// <summary>
    /// Attribute to mark types that have been relocated from the CoreFX tree to CoreRT.
    /// This supports fixing up references to those types when CoreFX doesn't yet have 
    /// forwarders for them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class RelocatedTypeAttribute : Attribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="originalAssemblySimpleName">Simple name of the CoreFX assembly the type was relocated from.
        /// For example, System.Collections (with no version or public key token)</param>
        public RelocatedTypeAttribute(string originalAssemblySimpleName)
        {
        }
    }
}