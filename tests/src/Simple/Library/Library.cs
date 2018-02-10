﻿using System;
using System.Runtime.InteropServices;

namespace Library
{
    public class ClassLibrary
    {
        [NativeCallable(EntryPoint = "ReturnsPrimitiveInt", CallingConvention = CallingConvention.StdCall)]
        public static int ReturnsPrimitiveInt()
        {
            return 10;
        }

        [NativeCallable(EntryPoint = "ReturnsPrimitiveBool", CallingConvention = CallingConvention.StdCall)]
        public static bool ReturnsPrimitiveBool()
        {
            return true;
        }

        [NativeCallable(EntryPoint = "ReturnsPrimitiveChar", CallingConvention = CallingConvention.StdCall)]
        public static char ReturnsPrimitiveChar()
        {
            return 'a';
        }

        [NativeCallable(EntryPoint = "EnsureManagedClassLoaders", CallingConvention = CallingConvention.StdCall)]
        public static void EnsureManagedClassLoaders()
        {
            Random random = new Random();
            random.Next();
        }
    }
}
