// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerServices;

#region Place holder types for internal System.Private.CoreLib types

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

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    class TypeHandleFixupAttribute: Attribute
    {
        public TypeHandleFixupAttribute(int offset, Type fixupType)
        {
        }
    }  

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    class MethodAddrFixupAttribute: Attribute
    {
        public MethodAddrFixupAttribute(int offset, Type fixupType, string methodName)
        {
        }
    }      
}

namespace Internal.Runtime.CompilerServices
{
    public struct FixupRuntimeTypeHandle
    {
        public RuntimeTypeHandle RuntimeTypeHandle => default(RuntimeTypeHandle);
    }
}

#endregion

class Details
{
    private static IntPtr PreInitializedIntField_DataBlob;

#if BIT64
    [TypeHandleFixupAttribute(0, typeof(int))]
    [TypeHandleFixupAttribute(8, typeof(short))]
    [TypeHandleFixupAttribute(16, typeof(long))]
    [TypeHandleFixupAttribute(24, typeof(string))]
#else
    [TypeHandleFixupAttribute(0, typeof(int))]
    [TypeHandleFixupAttribute(4, typeof(short))]
    [TypeHandleFixupAttribute(8, typeof(long))]
    [TypeHandleFixupAttribute(12, typeof(string))]
#endif
    private static IntPtr PreInitializedTypeField_DataBlob; 
}

class PreInitData
{
    internal static string StaticStringFieldBefore = "BEFORE";

    [PreInitialized]
    [InitDataBlob(typeof(Details), "PreInitializedIntField_DataBlob")]
    internal static int[] PreInitializedIntField;

    [PreInitialized]
    [InitDataBlob(typeof(Details), "PreInitializedTypeField_DataBlob")]
    internal static FixupRuntimeTypeHandle[] PreInitializedTypeField;

    internal static string StaticStringFieldAfter = "AFTER";
}

public class PreInitDataTest
{
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

        for (int i = 0; i < PreInitData.PreInitializedIntField.Length; ++i)
        {
            if (PreInitData.PreInitializedIntField[i] != i + 1)
                return false;
        }

        if (!PreInitData.PreInitializedTypeField[0].RuntimeTypeHandle.Equals(typeof(int).TypeHandle))
            return false;
        if (!PreInitData.PreInitializedTypeField[1].RuntimeTypeHandle.Equals(typeof(short).TypeHandle))
            return false;
        if (!PreInitData.PreInitializedTypeField[2].RuntimeTypeHandle.Equals(typeof(long).TypeHandle))
            return false;
        if (!PreInitData.PreInitializedTypeField[3].RuntimeTypeHandle.Equals(typeof(string).TypeHandle))
            return false;

        return true;
    }

    static bool TestOtherStatics()
    {
        Console.WriteLine("Testing other statics work well with preinitialized data in the same type...");

        if (PreInitData.StaticStringFieldBefore != "BEFORE")
            return false;

        if (PreInitData.StaticStringFieldAfter != "AFTER")
            return false;

        return true;
    }     
}
