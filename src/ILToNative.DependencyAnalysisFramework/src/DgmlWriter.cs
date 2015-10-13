// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace ILToNative.DependencyAnalysisFramework
{
    public class DgmlWriter : IDisposable, IDependencyAnalyzerLogEdgeVisitor, IDependencyAnalyzerLogNodeVisitor
    {
        XmlWriter _xmlWrite;
        bool _done = false;
        public DgmlWriter(XmlWriter xmlWrite)
        {
            _xmlWrite = xmlWrite;
            _xmlWrite.WriteStartDocument();
            _xmlWrite.WriteStartElement("DirectedGraph", "http://schemas.microsoft.com/vs/2009/dgml");
        }

        public void WriteNodesAndEdges(Action<Action<object>> nodeWriter, Action<Action<object, object, string>> edgeWriter)
        {

            _xmlWrite.WriteStartElement("Nodes");
            {
                nodeWriter(AddNode);
            }
            _xmlWrite.WriteEndElement();

            _xmlWrite.WriteStartElement("Links");
            {
                edgeWriter(AddReason);
            }
            _xmlWrite.WriteEndElement();
        }

        public static void WriteDependencyGraphToStream<DependencyContextType>(Stream stream, DependencyAnalyzerBase<DependencyContextType> analysis)
        {
            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Indent = true;
            writerSettings.IndentChars = " ";

            using (XmlWriter xmlWriter = XmlWriter.Create(stream, writerSettings))
            {
                using (DgmlWriter dgmlWriter = new DgmlWriter(xmlWriter))
                {
                    dgmlWriter.WriteNodesAndEdges((Action<Object> writeNode) =>
                    {
                        analysis.VisitLogNodes(dgmlWriter);
                    },
                    (Action<object, object, string> writeEdge) =>
                    {
                        analysis.VisitLogEdges(dgmlWriter);
                    }
                    );
                }
            }
        }

        public void Close()
        {
            if (!_done)
            {
                _done = true;
                _xmlWrite.WriteStartElement("Properties");
                {
                    _xmlWrite.WriteStartElement("Property");
                    _xmlWrite.WriteAttributeString("Id", "Label");
                    _xmlWrite.WriteAttributeString("Label", "Label");
                    _xmlWrite.WriteAttributeString("DataType", "String");
                    _xmlWrite.WriteEndElement();

                    _xmlWrite.WriteStartElement("Property");
                    _xmlWrite.WriteAttributeString("Id", "Reason");
                    _xmlWrite.WriteAttributeString("Label", "Reason");
                    _xmlWrite.WriteAttributeString("DataType", "String");
                    _xmlWrite.WriteEndElement();
                }
                _xmlWrite.WriteEndElement();

                _xmlWrite.WriteEndElement();
                _xmlWrite.WriteEndDocument();
            }
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        Dictionary<object, int> _nodeMappings = new Dictionary<object, int>();
        int _nodeNextId = 0;

        void AddNode(object node)
        {
            int nodeId = _nodeNextId++;
            Debug.Assert(!_nodeMappings.ContainsKey(node));

            _nodeMappings.Add(node, nodeId);

            _xmlWrite.WriteStartElement("Node");
            _xmlWrite.WriteAttributeString("Id", nodeId.ToString());
            _xmlWrite.WriteAttributeString("Label", node.ToString());
            _xmlWrite.WriteEndElement();
        }

        void AddReason(object nodeA, object nodeB, string reason)
        {
            _xmlWrite.WriteStartElement("Link");
            _xmlWrite.WriteAttributeString("Source", _nodeMappings[nodeA].ToString());
            _xmlWrite.WriteAttributeString("Target", _nodeMappings[nodeB].ToString());
            _xmlWrite.WriteAttributeString("Reason", reason);
            _xmlWrite.WriteEndElement();
        }

        void IDependencyAnalyzerLogEdgeVisitor.VisitEdge(DependencyNode nodeDepender, DependencyNode nodeDependedOn, string reason)
        {
            _xmlWrite.WriteStartElement("Link");
            _xmlWrite.WriteAttributeString("Source", _nodeMappings[nodeDepender].ToString());
            _xmlWrite.WriteAttributeString("Target", _nodeMappings[nodeDependedOn].ToString());
            _xmlWrite.WriteAttributeString("Reason", reason);
            _xmlWrite.WriteAttributeString("Stroke", "#FF0000");
            _xmlWrite.WriteEndElement();
        }

        void IDependencyAnalyzerLogEdgeVisitor.VisitEdge(string root, DependencyNode dependedOn)
        {
            AddReason(root, dependedOn, null);
        }

        void IDependencyAnalyzerLogNodeVisitor.VisitCombinedNode(Tuple<DependencyNode, DependencyNode> node)
        {
            AddNode(node);
        }

        HashSet<Tuple<DependencyNode, DependencyNode>> _combinedNodesEdgeVisited = new HashSet<Tuple<DependencyNode, DependencyNode>>();

        void IDependencyAnalyzerLogEdgeVisitor.VisitEdge(DependencyNode nodeDepender, DependencyNode nodeDependerOther, DependencyNode nodeDependedOn, string reason)
        {
            Tuple<DependencyNode, DependencyNode> combinedNode = new Tuple<DependencyNode, DependencyNode>(nodeDepender, nodeDependerOther);
            if (!_combinedNodesEdgeVisited.Contains(combinedNode))
            {
                _combinedNodesEdgeVisited.Add(combinedNode);

                _xmlWrite.WriteStartElement("Link");
                _xmlWrite.WriteAttributeString("Source", _nodeMappings[nodeDepender].ToString());
                _xmlWrite.WriteAttributeString("Target", _nodeMappings[combinedNode].ToString());
                _xmlWrite.WriteAttributeString("Reason", "Primary");
                _xmlWrite.WriteAttributeString("Stroke", "#00FF00");
                _xmlWrite.WriteEndElement();

                _xmlWrite.WriteStartElement("Link");
                _xmlWrite.WriteAttributeString("Source", _nodeMappings[nodeDependerOther].ToString());
                _xmlWrite.WriteAttributeString("Target", _nodeMappings[combinedNode].ToString());
                _xmlWrite.WriteAttributeString("Reason", "Secondary");
                _xmlWrite.WriteAttributeString("Stroke", "#00FF00");
                _xmlWrite.WriteEndElement();
            }

            _xmlWrite.WriteStartElement("Link");
            _xmlWrite.WriteAttributeString("Source", _nodeMappings[combinedNode].ToString());
            _xmlWrite.WriteAttributeString("Target", _nodeMappings[nodeDependedOn].ToString());
            _xmlWrite.WriteAttributeString("Reason", reason);
            _xmlWrite.WriteAttributeString("Stroke", "#0000FF");
            _xmlWrite.WriteEndElement();
        }

        void IDependencyAnalyzerLogNodeVisitor.VisitNode(DependencyNode node)
        {
            AddNode(node);
        }

        void IDependencyAnalyzerLogNodeVisitor.VisitRootNode(string rootName)
        {
            AddNode(rootName);
        }
    }
}
