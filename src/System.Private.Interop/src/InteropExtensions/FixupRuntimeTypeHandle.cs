// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerServices
{
    /// <summary>
    /// FixupRuntimeTypeHandle is a do nothing class for consistency with ProjectN 
    /// </summary>
    public unsafe struct FixupRuntimeTypeHandle
    {
        private RuntimeTypeHandle _handle;

        public FixupRuntimeTypeHandle(RuntimeTypeHandle runtimeTypeHandle)
        { 
            this._handle = runtimeTypeHandle;
        }

        public RuntimeTypeHandle RuntimeTypeHandle
        {
            get
            {
                return _handle;
            }
        }
    }
}
