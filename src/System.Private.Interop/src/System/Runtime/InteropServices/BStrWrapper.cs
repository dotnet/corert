// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: BStrWrapper.
**
**
** Purpose: Wrapper that is converted to a variant with VT_BSTR.
**
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public sealed class BStrWrapper
    {
        public BStrWrapper(String value)
        {
            m_WrappedObject = value;
        }

        public BStrWrapper(Object value)
        {
            m_WrappedObject = (String)value;
        }

        public String WrappedObject
        {
            get
            {
                return m_WrappedObject;
            }
        }

        private String m_WrappedObject;
    }
}
