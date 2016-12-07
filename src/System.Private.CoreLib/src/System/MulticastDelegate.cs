// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Internal.Runtime.CompilerServices;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class MulticastDelegate : Delegate
    {
        // This ctor exists solely to prevent C# from generating a protected .ctor that violates the surface area. I really want this to be a
        // "protected-and-internal" rather than "internal" but C# has no keyword for the former.
        internal MulticastDelegate() { }

        private bool InvocationListEquals(MulticastDelegate d)
        {
            Delegate[] invocationList = m_helperObject as Delegate[];
            if (d.m_extraFunctionPointerOrData != m_extraFunctionPointerOrData)
                return false;

            int invocationCount = (int)m_extraFunctionPointerOrData;
            for (int i = 0; i < invocationCount; i++)
            {
                Delegate dd = invocationList[i];
                Delegate[] dInvocationList = d.m_helperObject as Delegate[];
                if (!dd.Equals(dInvocationList[i]))
                    return false;
            }
            return true;
        }

        public override sealed bool Equals(Object obj)
        {
            if (obj == null)
                return false;
            if (object.ReferenceEquals(this, obj))
                return true;
            if (!InternalEqualTypes(this, obj))
                return false;

            // Since this is a MulticastDelegate and we know
            // the types are the same, obj should also be a
            // MulticastDelegate
            Debug.Assert(obj is MulticastDelegate, "Shouldn't have failed here since we already checked the types are the same!");
            var d = Unsafe.As<MulticastDelegate>(obj);

            // there are 2 kind of delegate kinds for comparision
            // 1- Multicast (m_helperObject is Delegate[])
            // 2- Single-cast delegate, which can be compared with a structural comparision

            if (m_functionPointer == GetThunk(MulticastThunk))
            {
                return InvocationListEquals(d);
            }
            else
            {
                if (!Object.ReferenceEquals(m_helperObject, d.m_helperObject) ||
                    (!FunctionPointerOps.Compare(m_extraFunctionPointerOrData, d.m_extraFunctionPointerOrData)) ||
                    (!FunctionPointerOps.Compare(m_functionPointer, d.m_functionPointer)))
                {
                    return false;
                }

                // Those delegate kinds with thunks put themselves into the m_firstParamter, so we can't 
                // blindly compare the m_firstParameter fields for equality.
                if (Object.ReferenceEquals(m_firstParameter, this))
                {
                    return Object.ReferenceEquals(d.m_firstParameter, d);
                }

                return Object.ReferenceEquals(m_firstParameter, d.m_firstParameter);
            }
        }

        public override sealed int GetHashCode()
        {
            Delegate[] invocationList = m_helperObject as Delegate[];
            if (invocationList == null)
            {
                return base.GetHashCode();
            }
            else
            {
                int hash = 0;
                for (int i = 0; i < (int)m_extraFunctionPointerOrData; i++)
                {
                    hash = hash * 33 + invocationList[i].GetHashCode();
                }

                return hash;
            }
        }

        public static bool operator ==(MulticastDelegate d1, MulticastDelegate d2)
        {
            if ((Object)d1 == null)
                return (Object)d2 == null;

            return d1.Equals(d2);
        }

        public static bool operator !=(MulticastDelegate d1, MulticastDelegate d2)
        {
            if ((Object)d1 == null)
                return (Object)d2 != null;

            return !d1.Equals(d2);
        }

        public override sealed Delegate[] GetInvocationList()
        {
            return base.GetInvocationList();
        }
    }
}
