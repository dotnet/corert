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

public class PreInitDataTest
{
    static int[] StaticIntArrayField = new int[] { 5, 6, 7, 8 };

    [System.Runtime.CompilerServices.PreInitialized]
    [System.Runtime.CompilerServices.InitDataBlob(typeof(Details), "PreInitializedField_DataBlob")]
    static int[] PreInitializedField = new int[] { 1, 2, 3, 4 };

    static string StaticStringField = "ABCDE";

    const int Pass = 100;
    const int Fail = -1;

    public static int Main(string[] args)
    {
        int result = Pass;

        if (!TestPreInitData())
        {
            Console.WriteLine("Failed");
            result = Fail;
        }
     
        // Make sure PreInitializedField works with other statics
        if (!TestOtherStatics())
        {
            Console.WriteLine("Failed");
            result = Fail;
        }

        return result;
    }

    static bool TestPreInitData()
    {
        Console.WriteLine("Testing preinitialized array...");

        for (int i = 0; i < PreInitializedField.Length; ++i)
        {
            if (PreInitializedField[i] != i + 1)
                return false;
        }

        return true;
    }

    static bool TestOtherStatics()
    {
        Console.WriteLine("Testing other statics work well with preinitialized data in the same type...");

        for (int i = 0; i < StaticIntArrayField.Length; ++i)
        {
            if (StaticIntArrayField[i] != i + 5)
                return false;
        }    

        if (StaticStringField != "ABCDE")
            return false;

        return true;
    }     
}
