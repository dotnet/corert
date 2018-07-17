// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
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

            foreach (ImportSectionNode node in NodesList)
            {
                if (!relocsOnly)
                    node.InitializeOffsetFromBeginningOfArray(builder.CountBytes);

                node.EncodeData(ref builder, factory, relocsOnly);
            }
        }

        protected override int ClassCode => 787556329;
    }
}
