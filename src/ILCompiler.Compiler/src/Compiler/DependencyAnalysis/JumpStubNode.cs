using System;
using ILCompiler.DependencyAnalysis.X64;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    public partial class JumpStubNode : AssemblyStubNode
    {
        private ISymbolNode _target;

        public JumpStubNode(ISymbolNode target)
        {
            this._target = target;
        }

        public override string MangledName
        {
            get
            {
                return "jmpstub_" + _target.MangledName;
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
    }
}