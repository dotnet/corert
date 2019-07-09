using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class WebAssemblyReadyToRunGenericLookupFromTypeNode : ReadyToRunGenericLookupFromTypeNode
    {
        public WebAssemblyReadyToRunGenericLookupFromTypeNode(NodeFactory factory, ReadyToRunHelperId helperId, object target, TypeSystemEntity dictionaryOwner)
            : base(factory, helperId, target, dictionaryOwner)
        {
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            // this code for this node is written out in ....
            return new ObjectData(null, null, 0, null);
        }
    }
}
