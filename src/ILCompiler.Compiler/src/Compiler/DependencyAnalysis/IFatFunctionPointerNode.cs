// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    // Used to identify FatFunctionPointer nodes. Needed because of current limitations
    // in the ISymbolNode interface/Object writer that result in all users of FatFunctionPointerNodes
    // manually adjusting the reloc by the offset needed.
    interface IFatFunctionPointerNode : IMethodNode
    {
    }
}
