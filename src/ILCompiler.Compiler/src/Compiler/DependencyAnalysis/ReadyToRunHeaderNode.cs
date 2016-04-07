// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Runtime;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class ReadyToRunHeaderNode : ObjectNode, ISymbolNode
    {
        struct HeaderItem
        {
            public HeaderItem(ReadyToRunSectionType id, ObjectNode node, ISymbolNode startSymbol, ISymbolNode endSymbol)
            {
                Id = id;
                Node = node;
                StartSymbol = startSymbol;
                EndSymbol = endSymbol;
            }

            readonly public ReadyToRunSectionType Id;
            readonly public ObjectNode Node;
            readonly public ISymbolNode StartSymbol;
            readonly public ISymbolNode EndSymbol;
        }

        List<HeaderItem> _items = new List<HeaderItem>();
        TargetDetails _target;

        public ReadyToRunHeaderNode(TargetDetails target)
        {
            _target = target;
        }

        internal void Add(ReadyToRunSectionType id, ObjectNode node, ISymbolNode startSymbol, ISymbolNode endSymbol = null)
        {
            _items.Add(new HeaderItem(id, node, startSymbol, endSymbol));
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return NodeFactory.NameMangler.CompilationUnitPrefix + "__ReadyToRunHeader";
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
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

        public int Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);
            builder.Alignment = factory.Target.PointerSize;
            builder.DefinedSymbols.Add(this);

            _items.Sort((x, y) => Comparer<int>.Default.Compare((int)x.Id, (int)y.Id));

            // ReadyToRunHeader.Magic
            builder.EmitInt((int)(ReadyToRunHeaderConstants.Signature));

            // ReadyToRunHeader.MajorVersion
            builder.EmitShort((short)(ReadyToRunHeaderConstants.CurrentMajorVersion));
            builder.EmitShort((short)(ReadyToRunHeaderConstants.CurrentMinorVersion));

            // ReadyToRunHeader.Flags
            builder.EmitInt(0);

            // ReadyToRunHeader.NumberOfSections
            var sectionCountReservation = builder.ReserveShort();

            // ReadyToRunHeader.EntrySize
            builder.EmitByte((byte)(8 + 2 * factory.Target.PointerSize));

            // ReadyToRunHeader.EntryType
            builder.EmitByte(1);

            int count = 0;
            foreach (var item in _items)
            {
                // Skip empty entries
                if (item.Node.ShouldSkipEmittingObjectNode(factory))
                    continue;

                builder.EmitInt((int)item.Id);

                ModuleInfoFlags flags = 0;
                if (item.EndSymbol != null)
                {
                    flags |= ModuleInfoFlags.HasEndPointer;
                }
                builder.EmitInt((int)flags);

                builder.EmitPointerReloc(item.StartSymbol);

                if (item.EndSymbol != null)
                {
                    builder.EmitPointerReloc(item.EndSymbol);
                }
                else
                {
                    builder.EmitZeroPointer();
                }

                count++;
            }
            builder.EmitShort(sectionCountReservation, checked((short)count));

            return builder.ToObjectData();
        }
    }
}
