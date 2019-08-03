using System;
using System.Collections.Generic;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class WebAssemblyUnboxingThunkNode : WebAssemblyMethodCodeNode, IMethodNode
    {
        public WebAssemblyUnboxingThunkNode(MethodDesc method)
            : base(method)
        {
            if (method.ToString().Contains("KeyValuePair") && method.ToString().Contains("Unbox"))
            {

            }
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[] {
                new DependencyListEntry(factory.MethodEntrypoint(Method), "Target of unboxing") };
        }

        int ISortableNode.ClassCode => -18942467;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((WebAssemblyUnboxingThunkNode)other)._method);
        }
    }
}
