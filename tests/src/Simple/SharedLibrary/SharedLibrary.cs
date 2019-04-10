// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharedLibrary
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

        [NativeCallable(EntryPoint = "CheckSimpleExceptionHandling", CallingConvention = CallingConvention.StdCall)]
        public static int CheckSimpleExceptionHandling()
        {
            int result = 10;

            try
            {
                Console.WriteLine("Throwing exception");
                throw new Exception();
            }
            catch when (result == 10)
            {
                result += 20;
            }
            finally
            {
                result += 70;
            }

            return result;
        }

        private static bool s_collected;

        class ClassWithFinalizer
        {
            ~ClassWithFinalizer() { s_collected = true; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MakeGarbage()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            object[] arr = new object[1024 * 1024];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = new object();

            new ClassWithFinalizer();
        }

        [NativeCallable(EntryPoint = "CheckSimpleGCCollect", CallingConvention = CallingConvention.StdCall)]
        public static int CheckSimpleGCCollect()
        {
            string myString = string.Format("Hello {0}", "world");

            MakeGarbage();

            Console.WriteLine("Triggering GC");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return s_collected ? (myString == "Hello world" ? 100 : 1) : 2;
        }
    }
}
