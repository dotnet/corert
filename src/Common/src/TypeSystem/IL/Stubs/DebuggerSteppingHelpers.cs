// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.IL.Stubs
{
    public static class DebuggerSteppingHelpers
    {
        public static void MarkDebuggerStepThroughPoint(this ILCodeStream codeStream)
        {
            codeStream.DefineSequencePoint("", 0xF00F00);
        }

        public static void MarkDebuggerStepInPoint(this ILCodeStream codeStream)
        {
            codeStream.DefineSequencePoint("", 0xFEEFEE);
        }
    }
}
