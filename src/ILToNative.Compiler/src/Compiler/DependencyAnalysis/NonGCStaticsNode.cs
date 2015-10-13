// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Internal.TypeSystem;

namespace ILToNative.DependencyAnalysis
{
    class NonGCStaticsNode : ObjectNode, ISymbolNode
    {
        MetadataType _type;

        public NonGCStaticsNode(MetadataType type)
        {
            _type = type;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override string Section
        {
            get
            {
                return "data";
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "__NonGCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectData data = new ObjectData(new byte[_type.NonGCStaticFieldSize],
                                             Array.Empty<Relocation>(),
                                             _type.NonGCStaticFieldAlignment,
                                             new ISymbolNode[] { this });
            return data;
        }
    }
}
