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
        private PInvokeFlags _flags;

        public PInvokeMethodFixupNode(string moduleName, string entryPointName, PInvokeFlags flags)
        {
            _moduleName = moduleName;
            _entryPointName = entryPointName;
            _flags = flags;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__pinvoke_");
            sb.Append(_moduleName);
            sb.Append("__");
            sb.Append(_entryPointName);
            if(!_flags.ExactSpelling)
            {
                sb.Append("__");
                sb.Append(_flags.CharSet.ToString());
            }
            sb.Append("__");
            sb.Append(((int)_flags.Attributes).ToString());
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

            builder.EmitInt(_flags.ExactSpelling ? 0 : (int)_flags.CharSet);

            return builder.ToObjectData();
        }

        public override int ClassCode => -1592006940;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            var flagsCompare = _flags.CompareTo(((PInvokeMethodFixupNode)other)._flags);
            if (flagsCompare != 0)
                return flagsCompare;

            var moduleCompare = string.Compare(_moduleName, ((PInvokeMethodFixupNode)other)._moduleName);
            if (moduleCompare != 0)
                return moduleCompare;

            return string.Compare(_entryPointName, ((PInvokeMethodFixupNode)other)._entryPointName);
        }
    }
}
