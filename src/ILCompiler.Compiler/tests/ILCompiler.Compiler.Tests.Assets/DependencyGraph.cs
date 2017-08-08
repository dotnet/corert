// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace ILCompiler.Compiler.Tests.Assets
{
    //
    // Classes nested under this class gets automatically discovered by the unit test.
    // The unit test will locate the Entrypoint method, run the IL scanner on it,
    // and validate the invariants declared by the Entrypoint method with custom attributes.
    //

    class DependencyGraph
    {
        /// <summary>
        /// Validates that a beforefieldinit type with a pinvoke drags the cctor of PInvoke's owning type
        /// when the PInvoke is used.
        /// </summary>
        class PInvokeCctorDependencyTest
        {
            class ClassWithPInvoke
            {
                static int s_cookie = GetCookie();

                public static int GetCookie() => 42;

                public static int NotUsed() => 123;

                [DllImport("*")]
                public static extern int PInvoke();
            }

            [GeneratesMethodBody(typeof(ClassWithPInvoke), nameof(ClassWithPInvoke.GetCookie))]
            [NoMethodBody(typeof(ClassWithPInvoke), nameof(ClassWithPInvoke.NotUsed))]
            [NoConstructedEEType(typeof(ClassWithPInvoke))]
            public static void Entrypoint()
            {
                ClassWithPInvoke.PInvoke();
            }
        }
    }

    #region Custom attributes that define invariants to check
    public class GeneratesConstructedEETypeAttribute : Attribute
    {
        public GeneratesConstructedEETypeAttribute(Type type) { }
    }

    public class NoConstructedEETypeAttribute : Attribute
    {
        public NoConstructedEETypeAttribute(Type type) { }
    }

    public class GeneratesMethodBodyAttribute : Attribute
    {
        public GeneratesMethodBodyAttribute(Type owningType, string methodName) { }
        public GeneratesMethodBodyAttribute(Type owningType, string methodName, Type[] methodInstantiation) { }
    }

    public class NoMethodBodyAttribute : Attribute
    {
        public NoMethodBodyAttribute(Type owningType, string methodName) { }
        public NoMethodBodyAttribute(Type owningType, string methodName, Type[] methodInstantiation) { }
    }
    #endregion
}
