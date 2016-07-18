// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Text;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL
{
    internal sealed class MethodILDebugView
    {
        private readonly MethodIL _methodIL;

        public MethodILDebugView(MethodIL methodIL)
        {
            _methodIL = methodIL;
        }

        public string Disassembly
        {
            get
            {
                ILDisassember disasm = new ILDisassember(_methodIL);

                StringBuilder sb = new StringBuilder();

                sb.Append("// Code size: ");
                sb.Append(disasm.CodeSize);
                sb.AppendLine();
                sb.Append(".maxstack ");
                sb.Append(_methodIL.MaxStack);
                sb.AppendLine();

                LocalVariableDefinition[] locals = _methodIL.GetLocals();
                if (locals != null && locals.Length > 0)
                {
                    sb.Append(".locals ");
                    if (_methodIL.IsInitLocals)
                        sb.Append("init ");

                    sb.Append("(");

                    for (int i = 0; i < locals.Length; i++)
                    {
                        if (i != 0)
                        {
                            sb.AppendLine(",");
                            sb.Append(' ', 4);
                        }
                        disasm.AppendType(sb, locals[i].Type);
                        sb.Append(" ");
                        if (locals[i].IsPinned)
                            sb.Append("pinned ");
                        sb.Append("V_");
                        sb.Append(i);
                    }
                    sb.AppendLine(")");
                }
                sb.AppendLine();

                while (disasm.HasNextInstruction)
                {
                    sb.AppendLine(disasm.GetNextInstruction());
                }

                return sb.ToString();
            }
        }
    }
}
