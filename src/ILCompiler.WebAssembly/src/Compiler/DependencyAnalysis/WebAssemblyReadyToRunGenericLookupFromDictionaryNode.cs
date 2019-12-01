using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class WebAssemblyReadyToRunGenericLookupFromDictionaryNode : ReadyToRunGenericLookupFromDictionaryNode
    {
        public WebAssemblyReadyToRunGenericLookupFromDictionaryNode(NodeFactory factory, ReadyToRunHelperId helperId, object target, TypeSystemEntity dictionaryOwner)
            : base(factory, helperId, target, dictionaryOwner)
        {
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            // this code for this node is written out in WebAssemblyObjectWriter.GetCodeForReadyToRunGenericHelper
            return new ObjectData(new byte[0], new Relocation[0], 1, new ISymbolDefinitionNode[0]);
        }
    }
}
