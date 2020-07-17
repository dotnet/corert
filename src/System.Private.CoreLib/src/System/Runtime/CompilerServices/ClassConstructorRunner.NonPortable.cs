// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    //=========================================================================================================
    // This is the non-portable part of ClassConstructorRunner. It lives in a separate .cs file to make
    // it easier to include the main ClassConstructorRunner source into a desktop project for testing.
    //=========================================================================================================

    [McgIntrinsics]
    internal static partial class ClassConstructorRunner
    {
        //=========================================================================================================
        // Intrinsic to call the cctor given a pointer to the code (this method's body is ignored and replaced
        // with a calli during compilation).
        //=========================================================================================================
        private static void Call(System.IntPtr pfn)
        {
            throw NotImplemented.ByDesign;
        }

        private static int CurrentManagedThreadId
        {
            get
            {
                return ManagedThreadId.Current;
            }
        }

        private const int ManagedThreadIdNone = ManagedThreadId.IdNone;
    }
}
