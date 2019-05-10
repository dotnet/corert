// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ImportSectionsTableNode : ArrayOfEmbeddedDataNode<ImportSectionNode>
    {   
        public ImportSectionsTableNode(TargetDetails target)
            : base("ImportSectionsTableStart", "ImportSectionsTableEnd", null)
        {
        }
        
        protected override void GetElementDataForNodes(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            builder.RequireInitialPointerAlignment();
            int index = 0;
            foreach (ImportSectionNode node in NodesList)
            {
                if (!relocsOnly && !node.ShouldSkipEmittingTable(factory))
                {
                    node.InitializeOffsetFromBeginningOfArray(builder.CountBytes);
                    node.InitializeIndexFromBeginningOfArray(index++);
                }

                node.EncodeData(ref builder, factory, relocsOnly);
                if (node is ISymbolDefinitionNode symbolDef)
                {
                    builder.AddSymbol(symbolDef);
                }
            }
        }

        public override int ClassCode => 787556329;
    }
}
