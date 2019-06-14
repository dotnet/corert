// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.InteropServices.WindowsRuntime
{
    /// <summary>
    /// A managed wrapper of IReferenceArray<T>
    /// </summary>
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    [McgInternalTypeAttribute]
    public sealed class ReferenceArrayImpl<T> : ReferenceArrayImplBase, IReferenceArray<T>
    {
        private T[] _value;

        public ReferenceArrayImpl(T[] obj, PropertyType type) : base(obj, type)
        {
            _value = obj;
            m_unboxed = true;
        }

        public T[] get_Value()
        {
            if (!m_unboxed)
            {
                _value = (T[])m_data;
                m_unboxed = true;
            }

            return _value;
        }
    }
}
