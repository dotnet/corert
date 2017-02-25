// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Xml;

using Internal.Text;

using ILCompiler.DependencyAnalysis;

using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

namespace ILCompiler
{
    public class ObjectDumper : IObjectDumper
    {
        private readonly string _fileName;

        private XmlWriter _writer;

        public ObjectDumper(string fileName)
        {
            _fileName = fileName;
        }

        internal void Begin()
        {
            var settings = new XmlWriterSettings
            {
                CloseOutput = true,
                Indent = true,
            };

            _writer = XmlWriter.Create(File.CreateText(_fileName), settings);
            _writer.WriteStartElement("ObjectNodes");
        }

        void IObjectDumper.DumpObjectNode(NameMangler mangler, ObjectNode node, ObjectData objectData)
        {
            string name = null;

            _writer.WriteStartElement(node.GetType().Name.Replace('`', '_'));

            var symbolNode = node as ISymbolNode;
            if (symbolNode != null)
            {
                Utf8StringBuilder sb = new Utf8StringBuilder();
                symbolNode.AppendMangledName(mangler, sb);
                name = sb.ToString();
                _writer.WriteAttributeString("Name", name);
            }

            _writer.WriteAttributeString("Length", objectData.Data.Length.ToStringInvariant());

            _writer.WriteEndElement();

            var nodeWithCodeInfo = node as INodeWithCodeInfo;
            if (nodeWithCodeInfo != null)
            {
                _writer.WriteStartElement("GCInfo");
                _writer.WriteAttributeString("Name", name);
                _writer.WriteAttributeString("Length", nodeWithCodeInfo.GCInfo.Length.ToStringInvariant());
                _writer.WriteEndElement();

                if (nodeWithCodeInfo.EHInfo != null)
                {
                    _writer.WriteStartElement("EHInfo");
                    _writer.WriteAttributeString("Name", name);
                    _writer.WriteAttributeString("Length", nodeWithCodeInfo.EHInfo.Data.Length.ToStringInvariant());
                    _writer.WriteEndElement();
                }
            }
        }

        internal void End()
        {
            _writer.WriteEndElement();
            _writer.Dispose();
            _writer = null;
        }
    }
}
