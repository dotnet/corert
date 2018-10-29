// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;

namespace ILVerify
{
    public class VerificationResult
    {        
        public VerificationErrorArgs Error { get; internal set; }
        public string Message { get; internal set; }
    }

    public class MethodVerificationResult : VerificationResult
    {
        public MethodDefinitionHandle Method { get; internal set; }
    }

    public class InterfaceVerificationResult : VerificationResult
    {
        public TypeDefinitionHandle Type { get; internal set; }
    }

    public class VerificationErrorArgs
    {
        public VerifierError Code { get; internal set; }
    }

    public class MethodVerificationErrorArgs : VerificationErrorArgs
    {        
        public int Offset { get; internal set; }
        public int Token { get; internal set; }
        public string Found { get; internal set; }
        public string Expected { get; internal set; }
    }

    public class InterfaceVerificationErrorArgs : VerificationErrorArgs
    {
        
    }
}
