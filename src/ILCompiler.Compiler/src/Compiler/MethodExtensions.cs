// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public enum SpecialMethodKind
    {
        Unknown,
        PInvoke,
        RuntimeImport
    };

    internal static class MethodExtensions
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

        public static SpecialMethodKind DetectSpecialMethodKind(this MethodDesc method)
        {
            if (method.IsPInvoke)
            {
                // Marshalling is never required for pregenerated interop code
                if (Internal.IL.McgInteropSupport.IsPregeneratedInterop(method))
                {
                    return SpecialMethodKind.PInvoke;
                }

                if (!Internal.IL.Stubs.PInvokeMarshallingILEmitter.RequiresMarshalling(method))
                {
                    return SpecialMethodKind.PInvoke;
                }
            }

            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                return SpecialMethodKind.RuntimeImport;
            }

            return SpecialMethodKind.Unknown;
        }
    }
}
