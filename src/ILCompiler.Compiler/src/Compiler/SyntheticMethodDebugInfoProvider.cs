﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Debug information provider that generates IL assembly listing for compiler-generated
    /// IL stubs.
    /// </summary>
    public class SyntheticMethodDebugInfoProvider : DebugInformationProvider, IDisposable
    {
        private readonly string _fileName;
        private readonly TextWriter _tw;
        private int _currentLine;
        private Dictionary<MethodDesc, MethodDebugInformation> _generatedInfos = new Dictionary<MethodDesc, MethodDebugInformation>();

        public SyntheticMethodDebugInfoProvider(string fileName)
        {
            _fileName = fileName;
            _tw = new StreamWriter(File.OpenWrite(fileName));
        }

        public override MethodDebugInformation GetDebugInfo(MethodIL methodIL)
        {
            MethodIL definitionIL = methodIL.GetMethodILDefinition();
            if (definitionIL is EcmaMethodIL)
                return methodIL.GetDebugInfo();

            // One file per compilation model has obvious stability problems in multithreaded
            // compilation. This would be fixable by e.g. generating multiple files, or preparing
            // the file after the IL scanning phase. Since this is a non-shipping feature,
            // we're going to ignore the stability concern here.
            lock (_generatedInfos)
            {
                if (!_generatedInfos.TryGetValue(definitionIL.OwningMethod, out MethodDebugInformation debugInfo))
                {
                    debugInfo = GetDebugInformation(definitionIL);
                    _generatedInfos.Add(definitionIL.OwningMethod, debugInfo);
                }

                return debugInfo;
            }
        }

        private MethodDebugInformation GetDebugInformation(MethodIL methodIL)
        {
            MethodDesc owningMethod = methodIL.OwningMethod;
            var disasm = new ILDisassember(methodIL);
            var fmt = new ILDisassember.ILTypeNameFormatter(null);

            ArrayBuilder<ILSequencePoint> sequencePoints = new ArrayBuilder<ILSequencePoint>();

            _tw.Write(".method ");
            // TODO: accessibility, specialname, calling conventions etc.
            if (!owningMethod.Signature.IsStatic)
                _tw.Write("instance ");
            _tw.Write(fmt.FormatName(owningMethod.Signature.ReturnType));
            _tw.Write(" ");
            _tw.Write(owningMethod.Name);
            if (owningMethod.HasInstantiation)
            {
                _tw.Write("<");
                for (int i = 0; i < owningMethod.Instantiation.Length; i++)
                {
                    if (i != 0)
                        _tw.Write(", ");
                    _tw.Write(fmt.FormatName(owningMethod.Instantiation[i]));
                }
                _tw.Write(">");
            }
            _tw.Write("(");
            for (int i = 0; i < owningMethod.Signature.Length; i++)
            {
                if (i != 0)
                    _tw.Write(", ");
                _tw.Write(fmt.FormatName(owningMethod.Signature[i]));
            }
            _tw.WriteLine(") cil managed"); _currentLine++;

            _tw.WriteLine("{"); _currentLine++;

            _tw.Write("  // Code size: ");
            _tw.Write(disasm.CodeSize);
            _tw.WriteLine(); _currentLine++;
            _tw.Write("  .maxstack ");
            _tw.Write(methodIL.MaxStack);
            _tw.WriteLine(); _currentLine++;

            LocalVariableDefinition[] locals = methodIL.GetLocals();
            if (locals != null && locals.Length > 0)
            {
                _tw.Write("  .locals ");
                if (methodIL.IsInitLocals)
                    _tw.Write("init ");

                _tw.Write("(");

                for (int i = 0; i < locals.Length; i++)
                {
                    if (i != 0)
                    {
                        _tw.WriteLine(","); _currentLine++;
                        _tw.Write("      ");
                    }
                    _tw.Write(fmt.FormatName(locals[i].Type));
                    _tw.Write(" ");
                    if (locals[i].IsPinned)
                        _tw.Write("pinned ");
                    _tw.Write("V_");
                    _tw.Write(i);
                }
                _tw.WriteLine(")"); _currentLine++;
            }
            _tw.WriteLine(); _currentLine++;

            while (disasm.HasNextInstruction)
            {
                _currentLine++;

                int offset = disasm.Offset;
                _tw.WriteLine(disasm.GetNextInstruction());
                sequencePoints.Add(new ILSequencePoint(offset, _fileName, _currentLine));
            }

            _tw.WriteLine("}"); _currentLine++;
            _tw.WriteLine(); _currentLine++;

            return new SyntheticMethodDebugInformation(sequencePoints.ToArray());
        }

        public void Dispose()
        {
            _tw.Dispose();
        }

        private class SyntheticMethodDebugInformation : MethodDebugInformation
        {
            private readonly ILSequencePoint[] _sequencePoints;

            public SyntheticMethodDebugInformation(ILSequencePoint[] sequencePoints)
            {
                _sequencePoints = sequencePoints;
            }

            public override IEnumerable<ILSequencePoint> GetSequencePoints()
            {
                return _sequencePoints;
            }
        }
    }
}
