// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Internal.TypeSystem;

namespace ILToNative.DependencyAnalysis
{
    class MethodCodeNode : ObjectNode, ISymbolNode
    {
        MethodDesc _method;
        ObjectData _methodCode;
        BlobNode _roData;

        public MethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
        }

        public void SetCode(ObjectData data)
        {
            Debug.Assert(_methodCode == null);
            _methodCode = data;
        }

        public void SetROData(BlobNode roData)
        {
            Debug.Assert(_roData == null);
            _roData = roData;
        }

        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }
        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override string Section
        {
            get
            {
                return "text";
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return _methodCode != null;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return NodeFactory.NameMangler.GetMangledMethodName(_method);
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
            return _methodCode;
        }
    }
}
