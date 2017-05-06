// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    class PreInitializedAttribute: Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    class InitDataBlobAttribute: Attribute
    {
        public InitDataBlobAttribute(Type type, string fieldName)
        {

        }
    }
}

class Details
{
    private static IntPtr PreInitializedField_DataBlob = IntPtr.Zero;
}

internal class Program
{
    [System.Runtime.CompilerServices.PreInitialized]
    [System.Runtime.CompilerServices.InitDataBlob(typeof(Details), "PreInitializedField_DataBlob")]
    static int[] PreInitializedField = new int[] { 1, 2, 3, 4 };

    private static void Main(string[] args)
    {
        for (int i = 0; i < PreInitializedField.Length; ++i)
        {
            Console.WriteLine(PreInitializedField[i]);
        }
    }
}
