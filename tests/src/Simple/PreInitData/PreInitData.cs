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


namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NativeCallableAttribute : Attribute
    {
        public string EntryPoint;

        public CallingConvention CallingConvention;

        public NativeCallableAttribute()
        {
        }
    }
}
 
#endregion

namespace System.Runtime.InteropServices
{
    [AttributeUsage((System.AttributeTargets.Method | System.AttributeTargets.Class))]
    internal class McgIntrinsicsAttribute : Attribute
    {
    }

    [McgIntrinsics]
    internal static class AddrofIntrinsics
    {
        // This method is implemented elsewhere in the toolchain
        internal static IntPtr AddrOf<T>(T ftn) { throw new PlatformNotSupportedException(); }
    }
}

class Details
{
    private static IntPtr PreInitializedInt32Field_DataBlob;

    [TypeHandleFixupAttribute(0, typeof(IntPtr))]
    private static IntPtr PreInitializedIntField_DataBlob;

#if BIT64
    [TypeHandleFixupAttribute(0, typeof(RuntimeTypeHandle))]
    [TypeHandleFixupAttribute(16, typeof(int))]
    [TypeHandleFixupAttribute(24, typeof(short))]
    [TypeHandleFixupAttribute(32, typeof(long))]
    [TypeHandleFixupAttribute(40, typeof(string))]
#else
    [TypeHandleFixupAttribute(0, typeof(RuntimeTypeHandle))]
    [TypeHandleFixupAttribute(8, typeof(int))]
    [TypeHandleFixupAttribute(12, typeof(short))]
    [TypeHandleFixupAttribute(16, typeof(long))]
    [TypeHandleFixupAttribute(20, typeof(string))]
#endif
    private static IntPtr PreInitializedTypeField_DataBlob;

#if BIT64
    [TypeHandleFixupAttribute(0, typeof(IntPtr))]
    [MethodAddrFixupAttribute(16, typeof(NativeMethods), "Func1")]
    [MethodAddrFixupAttribute(24, typeof(NativeMethods), "Func2")]
#else
    [TypeHandleFixupAttribute(0, typeof(IntPtr))]
    [MethodAddrFixupAttribute(8, typeof(NativeMethods), "Func1")]
    [MethodAddrFixupAttribute(12, typeof(NativeMethods), "Func2")]
#endif
    private static IntPtr PreInitializedMethodTypeField_DataBlob;     
}

static class NativeMethods
{
    [NativeCallable]
    internal static void Func1(int a)
    {
    }

    [NativeCallable]
    internal static void Func2(float b)
    {
    }
}

class PreInitData
{
    internal static string StaticStringFieldBefore = "BEFORE";

    //
    // Reference type fields
    //
    [PreInitialized]
    [InitDataBlob(typeof(Details), "PreInitializedIntField_DataBlob")]
    internal static int[] PreInitializedIntField;

    [PreInitialized]
    [InitDataBlob(typeof(Details), "PreInitializedTypeField_DataBlob")]
    internal static RuntimeTypeHandle[] PreInitializedTypeField;

    [PreInitialized]
    [InitDataBlob(typeof(Details), "PreInitializedMethodField_DataBlob")]
    internal static IntPtr[] PreInitializedMethodField;

    //
    // Primitive type fields
    //
    [PreInitialized]
    [InitDataBlob(typeof(Details), "PreInitializedInt32Field_DataBlob")]    
    internal static int PreInitializedInt32Field;  // = 0x12345678

    internal static string StaticStringFieldAfter = "AFTER";
}

public class PreInitDataTest
{
    const int Pass = 100;
    const int Fail = -1;

    public static int Main(string[] args)
    {
        int result = Pass;

        if (!TestPreInitPrimitiveData())
        {
            Console.WriteLine("Failed");
            result = Fail;
        }

        if (!TestPreInitIntData())
        {
            Console.WriteLine("Failed");
            result = Fail;
        }
     
        if (!TestPreInitTypeData())
        {
            Console.WriteLine("Failed");
            result = Fail;
        }

        if (!TestPreInitMethodData())
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

    static bool TestPreInitPrimitiveData()
    {
        Console.WriteLine("Testing preinitialized primitive data...");

        if (PreInitData.PreInitializedInt32Field != 0x12345678)
            return false;

        return true;
    }

    static bool TestPreInitIntData()
    {
        Console.WriteLine("Testing preinitialized int array...");

        for (int i = 0; i < PreInitData.PreInitializedIntField.Length; ++i)
        {
            if (PreInitData.PreInitializedIntField[i] != i + 1)
                return false;
        }

        return true;
    }

    static bool TestPreInitTypeData()
    {
        Console.WriteLine("Testing preinitialized type array...");

        if (!PreInitData.PreInitializedTypeField[0].Equals(typeof(int).TypeHandle))
            return false;
        if (!PreInitData.PreInitializedTypeField[1].Equals(typeof(short).TypeHandle))
            return false;
        if (!PreInitData.PreInitializedTypeField[2].Equals(typeof(long).TypeHandle))
            return false;
        if (!PreInitData.PreInitializedTypeField[3].Equals(typeof(string).TypeHandle))
            return false;

        return true;
    }

    public delegate void Func1Proc(int a);
    public delegate void Func2Proc(float a);

    static bool TestPreInitMethodData()
    {
        Console.WriteLine("Testing preinitialized method array...");

        if (PreInitData.PreInitializedMethodField[0] != System.Runtime.InteropServices.AddrofIntrinsics.AddrOf<Func1Proc>(NativeMethods.Func1))
            return false;

        if (PreInitData.PreInitializedMethodField[1] != System.Runtime.InteropServices.AddrofIntrinsics.AddrOf<Func2Proc>(NativeMethods.Func2))
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
