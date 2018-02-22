// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modelled as a type in the
    /// DependencyAnalysis infrastructure during compilation. An "ImportedEETypeSymbolNode"
    /// will not be present in the final linked binary and instead referenced through
    /// an import table mechanism.
    /// </summary>
    public sealed class MrtImportedEETypeSymbolNode : MrtImportWithTypeSymbol, IEETypeNode
    {
        public MrtImportedEETypeSymbolNode(TypeDesc type) : base(type) { }
        protected override sealed string GetNonImportedName(NameMangler nameMangler) => nameMangler.NodeMangler.EEType(Type);
    }

    public sealed class MrtImportedGCStaticSymbolNode : MrtImportWithTypeSymbol
    {
        public MrtImportedGCStaticSymbolNode(TypeDesc type) : base(type) { }
        protected override sealed string GetNonImportedName(NameMangler nameMangler) => GCStaticsNode.GetMangledName(Type, nameMangler);
    }

    public sealed class MrtImportedNonGCStaticSymbolNode : MrtImportWithTypeSymbol
    {
        public MrtImportedNonGCStaticSymbolNode(TypeDesc type) : base(type) { }
        protected override sealed string GetNonImportedName(NameMangler nameMangler) => NonGCStaticsNode.GetMangledName(Type, nameMangler);
    }

    public sealed class MrtImportedThreadStaticOffsetSymbolNode : MrtImportWithTypeSymbol
    {
        public MrtImportedThreadStaticOffsetSymbolNode(TypeDesc type) : base(type) { }
        protected override sealed string GetNonImportedName(NameMangler nameMangler) => ThreadStaticsOffsetNode.GetMangledName(nameMangler, Type);
    }

    public sealed class MrtImportedMethodDictionarySymbolNode : MrtImportWithMethodSymbol
    {
        public MrtImportedMethodDictionarySymbolNode(MethodDesc method) : base(method) { }
        protected override sealed string GetNonImportedName(NameMangler nameMangler) => nameMangler.NodeMangler.MethodGenericDictionary(Method);
    }

    public sealed class MrtImportedMethodCodeSymbolNode : MrtImportWithMethodSymbol, IMethodNode
    {
        public MrtImportedMethodCodeSymbolNode(MethodDesc method) : base(method) { }
        protected override sealed string GetNonImportedName(NameMangler nameMangler) => nameMangler.GetMangledMethodName(Method).ToString();
    }

    public sealed class MrtImportedUnboxingMethodCodeSymbolNode : MrtImportWithMethodSymbol, IMethodNode
    {
        public MrtImportedUnboxingMethodCodeSymbolNode(MethodDesc method) : base(method) { }
        protected override sealed string GetNonImportedName(NameMangler nameMangler) => UnboxingStubNode.GetMangledName(nameMangler, Method);
    }

    public abstract class MrtImportWithTypeSymbol : MrtImportNode
    {
        private TypeDesc _type;

        public MrtImportWithTypeSymbol(TypeDesc type)
        {
            _type = type;
        }

        public TypeDesc Type => _type;
    }

    public abstract class MrtImportWithMethodSymbol : MrtImportNode
    {
        private MethodDesc _method;

        public MrtImportWithMethodSymbol(MethodDesc method)
        {
            _method = method;
        }

        public MethodDesc Method => _method;

    }

    public abstract class MrtImportNode : SortableDependencyNode, ISymbolDefinitionNode, ISortableSymbolNode
    {
        private const int InvalidOffset = int.MinValue;

        private int _offset;
        private MrtProcessedImportAddressTableNode _importTable;
        public int Ordinal { get; private set; }

        public IHasStartSymbol ContainingNode => _importTable;

        public MrtImportNode()
        {
            _offset = InvalidOffset;
        }

        public void InitializeImport(MrtProcessedImportAddressTableNode importTable, int ordinal)
        {
            Debug.Assert(_importTable == null);
            Debug.Assert(importTable != null);
            Ordinal = ordinal;
            _importTable = importTable;
        }

        int ISymbolNode.Offset => 0;
        int ISymbolDefinitionNode.Offset => OffsetFromBeginningOfArray;

        protected override sealed string GetName(NodeFactory factory)
        {
            string prefix = "MrtImport " + Ordinal.ToStringInvariant() + " __mrt__";
            return prefix + GetNonImportedName(factory.NameMangler);
        }

        protected abstract string GetNonImportedName(NameMangler nameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__mrt__").Append(nameMangler.CompilationUnitPrefix).Append(GetNonImportedName(nameMangler));
        }

        public bool RepresentsIndirectionCell => true;

        public int OffsetFromBeginningOfArray
        {
            get
            {
                if (_offset == InvalidOffset)
                    throw new InvalidOperationException();

                Debug.Assert(_offset != InvalidOffset);
                return _offset;
            }
        }

        internal void InitializeOffsetFromBeginningOfArray(int offset)
        {
            Debug.Assert(_offset == InvalidOffset || _offset == offset);
            _offset = offset;
        }

        public bool IsShareable => false;
        public sealed override bool InterestingForDynamicDependencyAnalysis => false;
        public sealed override bool HasDynamicDependencies => false;
        public sealed override bool HasConditionalStaticDependencies => false;

        public sealed override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public sealed override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            MrtImportNode otherImportNode = (MrtImportNode)other;

            int result = string.CompareOrdinal(_importTable.ExportTableToImportSymbol, otherImportNode._importTable.ExportTableToImportSymbol);
            if (result != 0)
                return result;

            return Ordinal - otherImportNode.Ordinal;
        }

        protected internal override int ClassCode => 2017985192;

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_importTable, "Import table") };
        }

        public override bool StaticDependenciesAreComputed => true;

        protected override void OnMarked(NodeFactory factory)
        {
            // We don't want the child in the parent collection unless it's necessary.
            // Only when this node gets marked, the parent node becomes the actual parent.
            _importTable.AddNode(this);
        }

        int ISortableSymbolNode.CompareToImpl(ISortableSymbolNode other, CompilerComparer comparer)
        {
            return CompareToImpl((SortableDependencyNode)other, comparer);
        }

        int ISortableSymbolNode.ClassCode => this.ClassCode;

        void ISymbolNode.AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            AppendMangledName(nameMangler, sb);
        }
    }

}
