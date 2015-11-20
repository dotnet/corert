// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    public class StringIndirectionNode : EmbeddedObjectNode, ISymbolNode
    {
        public string _data;

        public StringIndirectionNode(string data)
        {
            base.Offset = 1; // 1 is not a valid offset, so when the wrapper object emitter sets offsets, it will become more reasonable
            _data = data;
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
                if (base.Offset != 1)
                    return NodeFactory.NameMangler.CompilationUnitPrefix + "__str" + base.Offset.ToString(CultureInfo.InvariantCulture);
                else
                    return NodeFactory.NameMangler.CompilationUnitPrefix + "__str" + _data;
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return base.Offset;
            }
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            dataBuilder.RequirePointerAlignment();

            StringDataNode stringDataNode = factory.StringData(_data);
            if (!relocsOnly)
                stringDataNode.SetId(base.Offset);

            dataBuilder.EmitPointerReloc(stringDataNode);
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[] { new DependencyListEntry(context.StringData(_data), "string contents") };
        }

        protected override void OnMarked(NodeFactory context)
        {
            context.StringTable.AddEmbeddedObject(this);
        }
    }
}
