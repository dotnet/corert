﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
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

        public override string Section
        {
            get
            {
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

        string ISymbolNode.MangledName
        {
            get
            {
                if (_id.HasValue)
                    return "__str_table_entry_" + _id.Value.ToString(CultureInfo.InvariantCulture);
                else
                    return "__str_table_entry_" + _data;
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
            Encoding encoding = UTF8Encoding.UTF8;

            ObjectDataBuilder objDataBuilder = new ObjectDataBuilder(factory);
            AsmStringWriter stringWriter = new AsmStringWriter((byte b) =>objDataBuilder.EmitByte(b));
            stringWriter.WriteString(_data);
            objDataBuilder.DefinedSymbols.Add(this);

            return objDataBuilder.ToObjectData();
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
    }
}
