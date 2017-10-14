// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file provides an implementation of the pieces of the Marshal class which are required by the Interop
// API contract but are not provided by the version of Marshal which is part of the Redhawk test library.
// This partial class is combined with the version from the Redhawk test library, in order to provide the
// Marshal implementation for System.Private.CoreLib.
//

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Reflection;

namespace System.Runtime.InteropServices
{
    /// <summary>
    ///  Interface to expose MCG marshalling APIs to coreclr,the marshalredirect transform
    ///  redirect the public marshalling API calls in user code to MarshalAdapter functions.
    ///  This gives us an oppurtunity to intercept user's public marshal API call to see
    ///  if the requested operation is  MCG aware.
    /// </summary>
    public static partial class MarshalAdapter
    {
#pragma warning disable 618 // error CS0618: '%' is obsolete:
        //====================================================================
        // return the IUnknown* for an Object if the current context
        // is the one where the RCW was first seen. Will return null
        // otherwise.
        //====================================================================
        public static IntPtr /* IUnknown* */ GetIUnknownForObject(Object o)
        {
            return MarshalImpl.GetIUnknownForObject(o);
        }

        //====================================================================
        // return an Object for IUnknown
        //====================================================================
        public static Object GetObjectForIUnknown(IntPtr /* IUnknown* */ pUnk)
        {
            object obj = MarshalImpl.GetObjectForIUnknown(pUnk);
#if CORECLR
            if (obj == null)
            {
                return Marshal.GetObjectForIUnknown(pUnk);
            }
#endif
            return obj;
        }

        public static IntPtr GetComInterfaceForObject<T, TInterface>(T o)
        {
            return GetComInterfaceForObject(o, typeof(TInterface));
        }

        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type T)
        {
            IntPtr ptr = MarshalImpl.GetComInterfaceForObject(o, T);
#if CORECLR
            if (ptr == default(IntPtr))
            {
                return Marshal.GetComInterfaceForObject(o, T);
            }
#endif
            return ptr;
        }

        public static TDelegate GetDelegateForFunctionPointer<TDelegate>(IntPtr ptr)
        {
            return (TDelegate)(object)GetDelegateForFunctionPointer(ptr, typeof(TDelegate));
        }

        //====================================================================
        // Checks if "t" is MCG generated , if not fallback to public  API
        //====================================================================
        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, Type t)
        {
            Delegate dlg = MarshalImpl.GetDelegateForFunctionPointer(ptr, t);
#if CORECLR
            if (dlg == null) // fall back to public marshal API
            {
                return Marshal.GetDelegateForFunctionPointer(ptr, t);
            }
#endif
            return dlg;
        }

        public static bool IsComObject(object o)
        {
            return MarshalImpl.IsComObject(o);            
        }

        public static int ReleaseComObject(object o)
        {
            return MarshalImpl.ReleaseComObject(o);            
        }

        public static int FinalReleaseComObject(object o)
        {
            return MarshalImpl.FinalReleaseComObject(o);
        }

        public static int QueryInterface(IntPtr pUnk, ref Guid iid, out IntPtr ppv)
        {
            return MarshalImpl.QueryInterface(pUnk, ref iid, out ppv);
        }

        public static int AddRef(IntPtr pUnk)
        {
            return MarshalImpl.AddRef(pUnk);
        }

        public static int Release(IntPtr pUnk)
        {
            return MarshalImpl.Release(pUnk);
        }
    }
#pragma warning restore 618
}
