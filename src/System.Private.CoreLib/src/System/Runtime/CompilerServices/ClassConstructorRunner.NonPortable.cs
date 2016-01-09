// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    //=========================================================================================================
    // This is the non-portable part of ClassConstructorRunner. It lives in a seaparate .cs file to make
    // it easier to include the main ClassConstructorRunner source into a desktop project for testing.
    //=========================================================================================================

    [McgIntrinsics]
    internal static partial class ClassConstructorRunner
    {
        //=========================================================================================================
        // Intrinsic to call the cctor given a pointer to the code (this method's body is ignored and replaced
        // with a calli during compilation). The transform doesn't handle non-generic versions yet (i.e.
        // functions that are void).
        //=========================================================================================================
        private static T Call<T>(System.IntPtr pfn)
        {
            throw NotImplemented.ByDesign;
        }

        private static int CurrentManagedThreadId
        {
            get
            {
                return Environment.CurrentManagedThreadId;
            }
        }

        private const int ManagedThreadIdNone = ManagedThreadId.ManagedThreadIdNone;
    }
}
