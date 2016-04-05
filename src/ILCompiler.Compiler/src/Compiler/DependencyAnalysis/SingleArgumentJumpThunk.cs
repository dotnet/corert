// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Provides a mechanism to call a generated method with a pointer to another generated
    /// node as a parameter.
    /// </summary>
    public partial class SingleArgumentJumpThunk : AssemblyStubNode
    {
        ExternSymbolNode _target;
        ISymbolNode _argument;

        public SingleArgumentJumpThunk(ExternSymbolNode target, ISymbolNode argument)
        {
            _target = target;
            _argument = argument;
        }

        public override string MangledName
        {
            get
            {
                return "jumpthunk_1_" + _target.MangledName + "__" + _argument.MangledName;
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
    }
}
