// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Runtime.InteropServices;

namespace System.Private.WebAssembly
{
    // Provides end points for the hard coded mappings in ILToWebAssemblyImporter.cs that provide a drop in replacement for mono InternalCall methods.  There are 4 :
    // https://github.com/dotnet/runtime/blob/9ba9a300a08170c8170ea52981810f41fad68cf0/src/mono/wasm/runtime/driver.c#L400-L407
    // Have only done what has been hit so far in trying to bring up Uno Platform
    internal static class InternalCalls
    {
        //Uno compatibility
        [DllImport("*", EntryPoint = "corert_wasm_invoke_js")]
        private static extern string InvokeJSInternal(string js, int length, out int exception);

        public static string InvokeJS(string js, out int exception)
        {
            return InvokeJSInternal(js, js.Length, out exception);
        }

        [DllImport("*", EntryPoint = "corert_wasm_invoke_js_unmarshalled")]
        internal static extern IntPtr InvokeJSUnmarshalledInternal(string js, int length, IntPtr p1, IntPtr p2, IntPtr p3, out string exception);

        // Matches this signature:
        // https://github.com/mono/mono/blob/f24d652d567c4611f9b4e3095be4e2a1a2ab23a4/sdks/wasm/driver.c#L21
        public static IntPtr InvokeJSUnmarshalled(out string exception, string js, IntPtr p1, IntPtr p2, IntPtr p3)
        {
            // convention : if the methodId is known, then js is null and p1 is the method id
            return System.Private.WebAssembly.InternalCalls.InvokeJSUnmarshalledInternal(js, js?.Length ?? 0, p1, p2, p3, out exception);
        }

        //TODO:
        //mono_add_internal_call ("WebAssembly.JSInterop.InternalCalls::InvokeJS", mono_wasm_invoke_js_blazor); // blazor specific
        //mono_add_internal_call("WebAssembly.JSInterop.InternalCalls::InvokeJSMarshalled", mono_wasm_invoke_js_marshalled);
    }
}

namespace System.Runtime
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class RuntimeExportAttribute : Attribute
    {
        public string EntryPoint;

        public RuntimeExportAttribute(string entry)
        {
            EntryPoint = entry;
        }
    }
}
