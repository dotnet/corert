// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    public class BlobNode : ObjectNode, ISymbolNode
    {
        private string _name;
        private string _section;
        private byte[] _data;
        private int _alignment;

        public BlobNode(string name, string section, byte[] data, int alignment)
        {
            _name = name;
            _section = section;
            _data = data;
            _alignment = alignment;
        }

        public override string Section
        {
            get
            {
                return _section;
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
                return _name;
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            return new ObjectData(_data, null, _alignment, new ISymbolNode[] { this });
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
    }
}
