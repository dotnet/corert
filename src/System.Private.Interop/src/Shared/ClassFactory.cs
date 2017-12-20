// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
// ---------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text;
using System.Runtime;
using Internal.NativeFormat;
using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    [Guid("4ffdd514-7dec-47cf-a0ad-4971868d8455")]
    public unsafe class ClassFactory : IClassFactory
    {        
        private McgModule parent;
        private RuntimeTypeHandle classType;

        public ClassFactory(McgModule parent, RuntimeTypeHandle classType)
        {
            this.parent = parent;
            this.classType = classType;
        }

        public int CreateInstance(IntPtr pUnkOuter, Guid* riid, IntPtr* ppv)
        {
            if (pUnkOuter != IntPtr.Zero)
            {
                // We do not currently support COM aggregation
                return Interop.COM.CLASS_E_NOAGGREGATION;
            }

            RuntimeTypeHandle interfaceTypeHandle = parent.GetTypeFromGuid(ref *riid);
            if (interfaceTypeHandle.Equals(default(RuntimeTypeHandle)))
            {
                return Interop.COM.E_NOINTERFACE;
            }
            else
            {
                object result = InteropExtensions.RuntimeNewObject(classType);
                *ppv = McgMarshal.ObjectToComInterface(result, interfaceTypeHandle);
                if (*ppv == IntPtr.Zero)
                {
                    return Interop.COM.E_NOINTERFACE;
                }
                else
                {
                    return Interop.COM.S_OK;
                }
            }
        }

        public int LockServer (int fLock)
        {
            return Interop.COM.E_NOTIMPL;
        }
    }
}