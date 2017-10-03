// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices.WindowsRuntime
{   

    /// <summary>
    /// This one is used by MCG as anchor type for the real IActivationFactory interface
    /// </summary>
    [Guid("00000035-0000-0000-C000-000000000046")]
    public interface IActivationFactoryInternal
    {
        /// <summary>
        /// Creates an new instance
        /// </summary>
        /// <returns>The IInspectable of the created object</returns>
        object ActivateInstance();
    }
}
