// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace ILCompiler.DependencyAnalysis
{
    public class StringDataNode : ObjectNode, ISymbolNode
    {
        public string _data;
        private int? _id;

        public StringDataNode(string data)
        {
            _data = data;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.ReadOnlyDataSection;
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
                if (!_id.HasValue)
                {
                    throw new InvalidOperationException("MangledName called before String Id was initialized.");
                }

                return NodeFactory.CompilationUnitPrefix + "__str_table_entry_" + _id.Value.ToStringInvariant();
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public void SetId(int id)
        {
            _id = id;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            byte[] objectData = Array.Empty<byte>();

            if (!relocsOnly)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(_data);

                var encoder = new Internal.NativeFormat.NativePrimitiveEncoder();
                encoder.Init();

                encoder.WriteUnsigned((uint)bytes.Length);
                foreach (var b in bytes)
                {
                    encoder.WriteByte(b);
                }

                objectData = encoder.GetBytes();
            }

            return new ObjectData(objectData, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
    }
}
