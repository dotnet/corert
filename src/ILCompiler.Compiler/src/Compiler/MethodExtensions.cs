// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public static class MethodExtensions
    {
        public static string GetRuntimeImportName(this EcmaMethod This)
        {
            var decoded = This.GetDecodedCustomAttribute("System.Runtime", "RuntimeImportAttribute");
            if (decoded == null)
                return null;

            var decodedValue = decoded.Value;

            if (decodedValue.FixedArguments.Length != 0)
                return (string)decodedValue.FixedArguments[decodedValue.FixedArguments.Length - 1].Value;

            foreach (var argument in decodedValue.NamedArguments)
            {
                if (argument.Name == "EntryPoint")
                    return (string)argument.Value;
            }

            return null;
        }

        public static string GetRuntimeExportName(this EcmaMethod This)
        {
            var decoded = This.GetDecodedCustomAttribute("System.Runtime", "RuntimeExportAttribute");
            if (decoded == null)
                return null;

            var decodedValue = decoded.Value;

            if (decodedValue.FixedArguments.Length != 0)
                return (string)decodedValue.FixedArguments[0].Value;

            foreach (var argument in decodedValue.NamedArguments)
            {
                if (argument.Name == "EntryPoint")
                    return (string)argument.Value;
            }

            return null;
        }

        public static string GetNativeCallableExportName(this EcmaMethod This)
        {
            var decoded = This.GetDecodedCustomAttribute("System.Runtime.InteropServices", "NativeCallableAttribute");
            if (decoded == null)
                return null;

            var decodedValue = decoded.Value;

            foreach (var argument in decodedValue.NamedArguments)
            {
                if (argument.Name == "EntryPoint")
                    return (string)argument.Value;
            }

            return null;
        }

        /// <summary>
        /// Returns true if <paramref name="method"/> is an actual native entrypoint.
        /// There's a distinction between when a method reports it's a PInvoke in the metadata
        /// versus how it's treated in the compiler. For many PInvoke methods the compiler will generate
        /// an IL body. The methods with an IL method body shouldn't be treated as PInvoke within the compiler.
        /// </summary>
        public static bool IsRawPInvoke(this MethodDesc method)
        {
            return method.IsPInvoke && ((method is Internal.IL.Stubs.PInvokeTargetNativeMethod) || Internal.IL.McgInteropSupport.IsPregeneratedInterop(method));
        }

        /// <summary>
        /// What is the maximum number of steps that need to be taken from this type to its most contained generic type.
        /// i.e.
        /// SomeGenericType&lt;System.Int32&gt;.Method&lt;System.Int32&gt; => 1
        /// SomeType.Method&lt;System.Int32&gt; => 0
        /// SomeType.Method&lt;List&lt;System.Int32&gt;&gt; => 1
        /// </summary>
        public static int GetGenericDepth(this MethodDesc method)
        {
            int genericDepth = method.OwningType.GetGenericDepth();
            foreach (TypeDesc type in method.Instantiation)
            {
                genericDepth = Math.Max(genericDepth, type.GetGenericDepth());
            }
            return genericDepth;
        }

        /// <summary>
        /// Determine if a type has a generic depth greater than a given value
        /// </summary>
        /// <param name="depth"></param>
        /// <returns></returns>
        public static bool IsGenericDepthGreaterThan(this MethodDesc method, int depth)
        {
            if (method.OwningType.IsGenericDepthGreaterThan(depth))
                return true;

            foreach (TypeDesc type in method.Instantiation)
            {
                if (type.IsGenericDepthGreaterThan(depth))
                    return true;
            }

            return false;
        }
    }
}
