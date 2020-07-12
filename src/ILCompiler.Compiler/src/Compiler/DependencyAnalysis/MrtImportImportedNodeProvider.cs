// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class ImportExportOrdinals
    {
        public bool isImport;
        public uint tlsIndexOrdinal;
        public ReadOnlyDictionary<TypeDesc, uint> typeOrdinals;
        public ReadOnlyDictionary<TypeDesc, uint> nonGcStaticOrdinals;
        public ReadOnlyDictionary<TypeDesc, uint> gcStaticOrdinals;
        public ReadOnlyDictionary<TypeDesc, uint> tlsStaticOrdinals;
        public ReadOnlyDictionary<MethodDesc, uint> methodOrdinals;
        public ReadOnlyDictionary<MethodDesc, uint> unboxingStubMethodOrdinals;
        public ReadOnlyDictionary<MethodDesc, uint> methodDictionaryOrdinals;
    }

    public class MrtImportImportedNodeProvider : ImportedNodeProvider
    {
        private readonly KeyValuePair<MrtProcessedImportAddressTableNode, ImportExportOrdinals>[] _importOrdinals;

        public MrtImportImportedNodeProvider(TypeSystemContext context, KeyValuePair<string, ImportExportOrdinals>[] ordinals)
        {
            Dictionary<string, MrtProcessedImportAddressTableNode> importAddressTables = new Dictionary<string, MrtProcessedImportAddressTableNode>();
            _importOrdinals = new KeyValuePair<MrtProcessedImportAddressTableNode, ImportExportOrdinals>[ordinals.Length];

            for (int i = 0; i < ordinals.Length; i++)
            {
                string symbolName = "__imp_" + ordinals[i].Key + "ExportAddressTable";
                MrtProcessedImportAddressTableNode importTable = null;
                if (!importAddressTables.TryGetValue(symbolName, out importTable))
                {
                    importTable = new MrtProcessedImportAddressTableNode(symbolName, context);
                    importAddressTables.Add(symbolName, importTable);
                }

                _importOrdinals[i] = new KeyValuePair<MrtProcessedImportAddressTableNode, ImportExportOrdinals>(importTable, ordinals[i].Value);
            }
        }

        private bool LookupInImportExportOrdinals<TType>(
            Func<ImportExportOrdinals, ReadOnlyDictionary<TType, uint>> getOrdinalDictionary,
            TType lookup,
            MrtImportNode node)
        {
            uint ordinal = 0;
            MrtProcessedImportAddressTableNode importTable = null;
            foreach (KeyValuePair<MrtProcessedImportAddressTableNode, ImportExportOrdinals> ordinalGroup in _importOrdinals)
            {
                if (getOrdinalDictionary(ordinalGroup.Value).TryGetValue(lookup, out ordinal))
                {
                    importTable = ordinalGroup.Key;
                    break;
                }
            }

            if (importTable == null)
                throw new ArgumentException();

            node.InitializeImport(importTable, (int)ordinal);
            return true;
        }

        public override IEETypeNode ImportedEETypeNode(NodeFactory factory, TypeDesc type)
        {
            var node = new MrtImportedEETypeSymbolNode(type);
            LookupInImportExportOrdinals((ImportExportOrdinals importOrdinals) => importOrdinals.typeOrdinals, type, node);
            return node;
        }

        public override ISortableSymbolNode ImportedGCStaticNode(NodeFactory factory, MetadataType type)
        {
            MrtImportNode node = new MrtImportedGCStaticSymbolNode(type);
            LookupInImportExportOrdinals((ImportExportOrdinals importOrdinals) => importOrdinals.gcStaticOrdinals, type, node);
            return node;
        }

        public override ISortableSymbolNode ImportedNonGCStaticNode(NodeFactory factory, MetadataType type)
        {
            MrtImportNode node = new MrtImportedNonGCStaticSymbolNode(type);
            LookupInImportExportOrdinals((ImportExportOrdinals importOrdinals) => importOrdinals.nonGcStaticOrdinals, type, node);
            return node;
        }

        public override ISortableSymbolNode ImportedThreadStaticOffsetNode(NodeFactory factory, MetadataType type)
        {
            MrtImportNode node = new MrtImportedThreadStaticOffsetSymbolNode(type);
            LookupInImportExportOrdinals((ImportExportOrdinals importOrdinals) => importOrdinals.tlsStaticOrdinals, type, node);
            return node;
        }

        public override ISortableSymbolNode ImportedThreadStaticIndexNode(NodeFactory factory, MetadataType type)
        {
            return factory.ExternSymbol("__imp__tls_index_SharedLibrary");
        }

        public override ISortableSymbolNode ImportedTypeDictionaryNode(NodeFactory factory, TypeDesc type)
        {
            // When using this style of imported symbol, this symbol should never be imported
            throw new NotSupportedException();
        }

        public override ISortableSymbolNode ImportedMethodDictionaryNode(NodeFactory factory, MethodDesc method)
        {
            MrtImportNode node = new MrtImportedMethodDictionarySymbolNode(method);

            LookupInImportExportOrdinals((ImportExportOrdinals importOrdinals) => importOrdinals.methodDictionaryOrdinals, method, node);
            return node;
        }

        public override IMethodNode ImportedMethodCodeNode(NodeFactory factory, MethodDesc method, bool unboxingStub)
        {
            IMethodNode node;
            if (unboxingStub)
            {
                var newUnboxingNode = new MrtImportedUnboxingMethodCodeSymbolNode(method);
                LookupInImportExportOrdinals((ImportExportOrdinals importOrdinals) => importOrdinals.unboxingStubMethodOrdinals, method, newUnboxingNode);
                node = newUnboxingNode;
            }
            else
            {
                var newCodeNode = new MrtImportedMethodCodeSymbolNode(method);
                LookupInImportExportOrdinals((ImportExportOrdinals importOrdinals) => importOrdinals.methodOrdinals, method, newCodeNode);
                node = newCodeNode;
            }

            // return jump stub to code instead of code directly. Logic that is aware, and can itself do an indirect jump is responsible for looking through the method entrypoint
            return new RuntimeDecodableJumpStubNode(node);
        }
    }
}
