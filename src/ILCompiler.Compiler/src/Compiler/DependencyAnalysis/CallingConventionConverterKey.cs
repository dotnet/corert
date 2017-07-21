// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public struct CallingConventionConverterKey : IEquatable<CallingConventionConverterKey>
    {
        public CallingConventionConverterKey(Internal.NativeFormat.CallingConventionConverterKind converterKind,
                                             MethodSignature signature)
        {
            ConverterKind = converterKind;
            Signature = signature;
        }

        public Internal.NativeFormat.CallingConventionConverterKind ConverterKind { get; }
        public MethodSignature Signature { get; }

        public override bool Equals(object obj)
        {
            return obj is CallingConventionConverterKey && Equals((CallingConventionConverterKey)obj);
        }

        public bool Equals(CallingConventionConverterKey other)
        {
            if (ConverterKind != other.ConverterKind)
                return false;

            if (!Signature.Equals(other.Signature))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return Signature.GetHashCode() ^ (int)ConverterKind;
        }

        public string GetName()
        {
            return ConverterKind.ToString() + Signature.GetName();
        }
    }

    public static class MethodSignatureExtensions
    {
        public static string GetName(this MethodSignature signature)
        {
            StringBuilder nameBuilder = new StringBuilder();
            if (signature.GenericParameterCount > 0)
                nameBuilder.Append("GenParams:" + signature.GenericParameterCount);
            if (signature.IsStatic)
                nameBuilder.Append("Static");
            nameBuilder.Append(signature.ReturnType.ToString());
            for (int i = 0; i < signature.Length; i++)
                nameBuilder.Append(signature[i].ToString());

            return nameBuilder.ToString();
        }
    }
}
