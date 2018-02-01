// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

namespace ILCompiler
{
    public class CodeGenerationFailedException : InternalCompilerErrorException
    {
        private const string MessageText = "Code generation failed";

        public MethodDesc Method { get; }

        public CodeGenerationFailedException(MethodDesc method)
            : this(method, null)
        {
        }

        public CodeGenerationFailedException(MethodDesc method, Exception inner)
            : base(MessageText, inner)
        {
            Method = method;
        }
    }
}
