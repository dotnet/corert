// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  

namespace System.Runtime.InteropServices.WindowsRuntime
{
    /// <summary>
    /// A managed wrapper for IPropertyValue and IReference<T>
    /// </summary>
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    [McgInternalTypeAttribute]
    public class ReferenceImpl<T> : PropertyValueImpl, global::Windows.Foundation.IReference<T>
    {
        private T m_value;

        public ReferenceImpl(T data, int type)
            : base(data, type)
        {
            m_unboxed = true;
            m_value = data;
        }

        internal ReferenceImpl(T data, PropertyType type)
            : base(data, (int)type)
        {
            m_unboxed = true;
            m_value = data;
        }

        public T get_Value()
        {
            if (!m_unboxed)
            {
                m_value = (T)m_data;
                m_unboxed = true;
            }
            return m_value;
        }
    }
}
