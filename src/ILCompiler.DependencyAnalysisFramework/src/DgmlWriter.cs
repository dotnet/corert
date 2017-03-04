// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysisFramework
{
    public class DgmlWriter
    {
        public static void WriteDependencyGraphToStream<DependencyContextType>(Stream stream, DependencyAnalyzerBase<DependencyContextType> analysis)
        {
            DgmlWriter<DependencyContextType>.WriteDependencyGraphToStream(stream, analysis);
        }
    }

    internal class DgmlWriter<DependencyContextType> : IDisposable, IDependencyAnalyzerLogEdgeVisitor, IDependencyAnalyzerLogNodeVisitor
    {
        private XmlWriter _xmlWrite;
        private bool _done = false;
        public DgmlWriter(XmlWriter xmlWrite)
        {
            _xmlWrite = xmlWrite;
            _xmlWrite.WriteStartDocument();
            _xmlWrite.WriteStartElement("DirectedGraph", "http://schemas.microsoft.com/vs/2009/dgml");
        }

        public void WriteNodesAndEdges(Action nodeWriter, Action edgeWriter)
        {
            _xmlWrite.WriteStartElement("Nodes");
            {
                nodeWriter();
            }
            _xmlWrite.WriteEndElement();

            _xmlWrite.WriteStartElement("Links");
            {
                edgeWriter();
            }
            _xmlWrite.WriteEndElement();
        }

        public static void WriteDependencyGraphToStream(Stream stream, DependencyAnalyzerBase<DependencyContextType> analysis)
        {
            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Indent = true;
            writerSettings.IndentChars = " ";

            using (XmlWriter xmlWriter = XmlWriter.Create(stream, writerSettings))
            {
                using (var dgmlWriter = new DgmlWriter<DependencyContextType>(xmlWriter))
                {
                    dgmlWriter.WriteNodesAndEdges(() =>
                    {
                        analysis.VisitLogNodes(dgmlWriter);
                    },
                    () =>
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

        private Dictionary<object, int> _nodeMappings = new Dictionary<object, int>();
        private int _nodeNextId = 0;

        private void AddNode(DependencyNode node)
        {
            AddNode(node, node.GetName());
        }

        private void AddNode(object node, string label)
        {
            int nodeId = _nodeNextId++;
            Debug.Assert(!_nodeMappings.ContainsKey(node));

            _nodeMappings.Add(node, nodeId);

            _xmlWrite.WriteStartElement("Node");
            _xmlWrite.WriteAttributeString("Id", nodeId.ToString());
            _xmlWrite.WriteAttributeString("Label", label);
            _xmlWrite.WriteEndElement();
        }

        private void AddReason(object nodeA, object nodeB, string reason)
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
            string label1 = node.Item1.GetName();
            string label2 = node.Item2.GetName();

            AddNode(node, string.Concat("(", label1, ", ", label2, ")"));
        }

        private HashSet<Tuple<DependencyNode, DependencyNode>> _combinedNodesEdgeVisited = new HashSet<Tuple<DependencyNode, DependencyNode>>();

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
            AddNode(rootName, rootName);
        }
    }
}
