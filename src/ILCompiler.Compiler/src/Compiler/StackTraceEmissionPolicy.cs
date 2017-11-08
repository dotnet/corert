// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Represents a stack trace emission policy.
    /// </summary>
    public abstract class StackTraceEmissionPolicy
    {
        public abstract bool ShouldIncludeMethod(MethodDesc method);
    }

    public class NoStackTraceEmissionPolicy : StackTraceEmissionPolicy
    {
        public override bool ShouldIncludeMethod(MethodDesc method)
        {
            return false;
        }
    }
}
