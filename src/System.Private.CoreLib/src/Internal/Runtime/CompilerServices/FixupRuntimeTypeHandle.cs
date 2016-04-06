// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime.CompilerServices
{
    public unsafe struct FixupRuntimeTypeHandle
    {
#if !CLR_RUNTIMETYPEHANDLE
        private IntPtr _value;
#endif

        public FixupRuntimeTypeHandle(RuntimeTypeHandle runtimeTypeHandle)
        {
#if CLR_RUNTIMETYPEHANDLE
            throw new NotImplementedException(); // CORERT-TODO: RuntimeTypeHandle
#else
            _value = *(IntPtr*)&runtimeTypeHandle;
#endif
        }

        public RuntimeTypeHandle RuntimeTypeHandle
        {
            get
            {
#if CLR_RUNTIMETYPEHANDLE
                throw new NotImplementedException(); // CORERT-TODO: RuntimeTypeHandle
#else
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
#endif
            }
        }
    }
}
