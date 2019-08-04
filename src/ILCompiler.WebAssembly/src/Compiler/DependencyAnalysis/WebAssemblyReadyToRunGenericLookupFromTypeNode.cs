using System.Collections.Generic;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class WebAssemblyReadyToRunGenericLookupFromTypeNode : ReadyToRunGenericLookupFromTypeNode
    {
        private ReadyToRunHelperId _helperId;

        public WebAssemblyReadyToRunGenericLookupFromTypeNode(NodeFactory factory, ReadyToRunHelperId helperId, object target, TypeSystemEntity dictionaryOwner)
            : base(factory, helperId, target, dictionaryOwner)
        {
            this._helperId = helperId;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            // this code for this node is written out in ....
            return new ObjectData(new byte[0], new Relocation[0], 1, new ISymbolDefinitionNode[0]);
        }

//        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
//        {
//            switch (_helperId)
//            {
//                case ReadyToRunHelperId.GetGCStaticBase:
//                case ReadyToRunHelperId.GetThreadStaticBase:
//                case ReadyToRunHelperId.GetNonGCStaticBase:
//                case ReadyToRunHelperId.DelegateCtor:
//                    var deps = new List<CombinedDependencyListEntry>();
//                    deps.AddRange(base.GetConditionalStaticDependencies(factory));
//                    IMethodNode helperNode = (IMethodNode)factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);
//
//                    deps.Add(new CombinedDependencyListEntry(new WebAssemblyMethodBodyNode(helperNode.Method), this, "code emitted too late to add through normal path"));
//                    return deps;
//                default:
//                    return base.GetConditionalStaticDependencies(factory);
//        
//            }
//        }
    }
}
