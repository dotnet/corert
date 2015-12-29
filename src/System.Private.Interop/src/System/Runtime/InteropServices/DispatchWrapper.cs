// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

//

/*=============================================================================
**
** Class: DispatchWrapper.
**
**
** Purpose: Wrapper that is converted to a variant with VT_DISPATCH.
**
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public sealed class DispatchWrapper
    {
#if FEATURE_DISPATCHWRAPPER
        public DispatchWrapper(Object obj)
        {
            if (obj != null)
            {
                // Make sure this guy has an IDispatch
                IntPtr pdisp = Marshal.GetIDispatchForObject(obj);

                // If we got here without throwing an exception, the QI for IDispatch succeeded.
                Marshal.Release(pdisp);
            }
            m_WrappedObject = obj;
        }

        public Object WrappedObject
        {
            get
            {
                return m_WrappedObject;
            }
        }

        private Object m_WrappedObject;
#else // FEATURE_DISPATCHWRAPPER
        public DispatchWrapper(object obj)
        {
            throw new PlatformNotSupportedException();
        }
        public object WrappedObject
        {
            get
            {
                throw new PlatformNotSupportedException();
            }
        }
#endif // FEATURE_DISPATCHWRAPPER
    }
}
