// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;

using Internal.Text;

using ILCompiler.DependencyAnalysis;

using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

namespace ILCompiler
{
    public class RyuJitObjectDumper : ObjectDumper
    {
        public RyuJitObjectDumper(string fileName) : base(fileName) {}

        public override void DumpObjectNode(NameMangler mangler, ObjectNode node, ObjectData objectData)
        {
            base.DumpObjectNode(mangler, node, objectData);

            string name = null;
            var symbolNode = node as ISymbolNode;
            if (symbolNode != null)
            {
                Utf8StringBuilder sb = new Utf8StringBuilder();
                symbolNode.AppendMangledName(mangler, sb);
                name = sb.ToString();
            }

            var nodeWithCodeInfo = node as INodeWithCodeInfo;
            if (nodeWithCodeInfo != null)
            {
                _writer.WriteStartElement("GCInfo");
                _writer.WriteAttributeString("Name", name);
                _writer.WriteAttributeString("Length", nodeWithCodeInfo.GCInfo.Length.ToStringInvariant());
                _writer.WriteAttributeString("Hash", HashData(nodeWithCodeInfo.GCInfo));
                _writer.WriteEndElement();

                if (nodeWithCodeInfo.EHInfo != null)
                {
                    _writer.WriteStartElement("EHInfo");
                    _writer.WriteAttributeString("Name", name);
                    _writer.WriteAttributeString("Length", nodeWithCodeInfo.EHInfo.Data.Length.ToStringInvariant());
                    _writer.WriteAttributeString("Hash", HashData(nodeWithCodeInfo.EHInfo.Data));
                    _writer.WriteEndElement();
                }
            }
        }
    }
}
