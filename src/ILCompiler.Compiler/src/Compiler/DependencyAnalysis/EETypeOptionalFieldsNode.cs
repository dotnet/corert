// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ILCompiler.DependencyAnalysisFramework;
using Internal.Runtime;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    internal class EETypeOptionalFieldsNode : ObjectNode, ISymbolNode
    {
        EETypeOptionalFieldsBuilder _fieldBuilder = new EETypeOptionalFieldsBuilder();
        TargetDetails _target;

        public EETypeOptionalFieldsNode(EETypeOptionalFieldsBuilder fieldBuilder, TargetDetails target)
        {
            _fieldBuilder = fieldBuilder;
            _target = target;
        }

        public override string Section
        {
            get
            {
                if (_target.IsWindows)
                    return "rdata";
                else
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

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "optionalfields_" + _fieldBuilder.ToString();
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return !_fieldBuilder.IsAtLeastOneFieldUsed();
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.RequirePointerAlignment();
            objData.DefinedSymbols.Add(this);

            if (!relocsOnly)
            {
                objData.EmitBytes(_fieldBuilder.GetBytes());
            }
            
            return objData.ToObjectData();
        }
    }
}
