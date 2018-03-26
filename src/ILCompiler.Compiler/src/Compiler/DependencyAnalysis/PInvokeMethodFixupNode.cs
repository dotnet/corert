// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a single PInvoke MethodFixupCell as defined in the core library.
    /// </summary>
    public class PInvokeMethodFixupNode : ObjectNode, ISymbolDefinitionNode
    {
        private string _moduleName;
        private string _entryPointName;
        private bool _exactMatchOnly;
        private CharSet _charSet;

        public PInvokeMethodFixupNode(string moduleName, string entryPointName, bool exactSpelling, CharSet charSet)
        {
            _moduleName = moduleName;
            _entryPointName = entryPointName;
            _exactMatchOnly = exactSpelling;
            _charSet = charSet;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__pinvoke_");
            sb.Append(_moduleName);
            sb.Append("__");
            sb.Append(_entryPointName);
            if(!_exactMatchOnly)
            {
                sb.Append("_");
                sb.Append(_charSet.ToString());
            }
        }
        public int Offset => 0;
        public override bool IsShareable => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);

            //
            // Emit a MethodFixupCell struct
            //

            // Address (to be fixed up at runtime)
            builder.EmitZeroPointer();

            // Entry point name
            if (factory.Target.IsWindows && _entryPointName.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                // Windows-specific ordinal import
                // CLR-compatible behavior: Strings that can't be parsed as a signed integer are treated as zero.
                int entrypointOrdinal;
                if (!int.TryParse(_entryPointName.Substring(1), out entrypointOrdinal))
                    entrypointOrdinal = 0;

                // CLR-compatible behavior: Ordinal imports are 16-bit on Windows. Discard rest of the bits.
                builder.EmitNaturalInt((ushort)entrypointOrdinal);
            }
            else
            {
                // Import by name
                builder.EmitPointerReloc(factory.ConstantUtf8String(_entryPointName));
            }

            // Module fixup cell
            builder.EmitPointerReloc(factory.PInvokeModuleFixup(_moduleName));

            builder.EmitInt(_exactMatchOnly ? 0 : (int)_charSet);

            return builder.ToObjectData();
        }

        protected internal override int ClassCode => -1592006940;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            var exactMatchCompare = _exactMatchOnly.CompareTo(((PInvokeMethodFixupNode)other)._exactMatchOnly);
            if (exactMatchCompare != 0)
                return exactMatchCompare;

            var charSetCompare = ((int)_charSet).CompareTo((int)((PInvokeMethodFixupNode)other)._charSet);

            if (charSetCompare != 0)
                return charSetCompare;

            var moduleCompare = string.Compare(_moduleName, ((PInvokeMethodFixupNode)other)._moduleName);
            if (moduleCompare != 0)
                return moduleCompare;

            return string.Compare(_entryPointName, ((PInvokeMethodFixupNode)other)._entryPointName);
        }
    }
}
