﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;

namespace ILVerify
{
    public class VerificationResult
    {
        public VerifierError Code { get; internal set; }
        public TypeDefinitionHandle Type { get; internal set; }
        public MethodDefinitionHandle Method { get; internal set; }
        public string Message { get; internal set; }
        public ErrorArgument[] ErrorArguments { get; set; }
        public T GetArgumentValue<T>(string name)
        {
            for (int i = 0; i < ErrorArguments.Length; i++)
            {
                if(ErrorArguments[i].Name == name)
                    return (T)ErrorArguments[i].Value;
            }

            return default;
        }
    }

    public class ErrorArgument
    {
        public ErrorArgument() { }

        public ErrorArgument(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; }
        public object Value { get; set; }
    }
}
