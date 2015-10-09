// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.Runtime.CompilerServices
{
    public unsafe struct FixupRuntimeTypeHandle
    {
        private IntPtr _value;

        public FixupRuntimeTypeHandle(RuntimeTypeHandle runtimeTypeHandle)
        {
            _value = *(IntPtr*)&runtimeTypeHandle;
        }

        public RuntimeTypeHandle RuntimeTypeHandle
        {
            get
            {
                // Managed debugger uses this logic to figure out the interface's type
                // Update managed debugger too whenever this is changed.
                // See CordbObjectValue::WalkPtrAndTypeData in debug\dbi\values.cpp

                if (((_value.ToInt64()) & 0x1) != 0)
                {
                    return *(RuntimeTypeHandle*)(_value.ToInt64() - 0x1);
                }
                else
                {
                    RuntimeTypeHandle returnValue = default(RuntimeTypeHandle);
                    *(IntPtr*)&returnValue = _value;
                    return returnValue;
                }
            }
        }
    }
}