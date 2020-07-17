// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

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

            return null;
        }

        public static string GetRuntimeImportDllName(this EcmaMethod This)
        {
            var decoded = This.GetDecodedCustomAttribute("System.Runtime", "RuntimeImportAttribute");
            if (decoded == null)
                return null;

            var decodedValue = decoded.Value;

            if (decodedValue.FixedArguments.Length == 2)
                return (string)decodedValue.FixedArguments[0].Value;

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

        public static string GetUnmanagedCallersOnlyExportName(this EcmaMethod This)
        {
            var decoded = This.GetDecodedCustomAttribute("System.Runtime.InteropServices", "UnmanagedCallersOnlyAttribute");
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
            return method.IsPInvoke && (method is Internal.IL.Stubs.PInvokeTargetNativeMethod);
        }

        /// <summary>
        /// Gets a value indicating whether GC transition should be suppressed on the given p/invoke.
        /// </summary>
        public static bool IsSuppressGCTransition(this MethodDesc method)
        {
            Debug.Assert(method.IsPInvoke);

            if (method is Internal.IL.Stubs.PInvokeTargetNativeMethod rawPinvoke)
                method = rawPinvoke.Target;

            return method.HasCustomAttribute("System.Runtime.InteropServices", "SuppressGCTransitionAttribute");
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

        /// <summary>
        /// Determine whether a method can go into the sealed vtable of a type. Such method must be a sealed virtual 
        /// method that is not overriding any method on a base type. 
        /// Given that such methods can never be overridden in any derived type, we can 
        /// save space in the vtable of a type, and all of its derived types by not emitting these methods in their vtables, 
        /// and storing them in a separate table on the side. This is especially beneficial for all array types,
        /// since all of their collection interface methods are sealed and implemented on the System.Array and 
        /// System.Array&lt;T&gt; base types, and therefore we can minimize the vtable sizes of all derived array types.
        /// </summary>
        public static bool CanMethodBeInSealedVTable(this MethodDesc method)
        {
            return method.IsFinal && method.IsNewSlot;
        }
    }
}
