// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    [CLSCompliant(false)]
    public abstract class InteropCallbacks
    {
        public abstract IntPtr TryGetMarshallerForDelegate(RuntimeTypeHandle delegateTypeHandle);
    }
}
