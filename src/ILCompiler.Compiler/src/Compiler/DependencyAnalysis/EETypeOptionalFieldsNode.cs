// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public override ObjectNodeSection Section
        {
            get
            {
                if (_target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
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
            // Ensure that no duplicate EETypeOptionalFieldsNodes are emitted by letting the node Factory
            // pick a winner for each given EETypeOptionalFieldsBuilder
            if (factory.EETypeOptionalFields(_fieldBuilder) != this)
                return true;

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
