// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;

namespace ILCompiler
{
    /// <summary>
    /// P/Invoke policy that generates direct calls for all methods and avoids lazy resolution
    /// of P/invokes at runtime.
    /// </summary>
    public sealed class DirectPInvokePolicy : PInvokeILEmitterConfiguration
    {
        public override bool GenerateDirectCall(string libraryName, string methodName)
        {
            return true;
        }
    }
}
