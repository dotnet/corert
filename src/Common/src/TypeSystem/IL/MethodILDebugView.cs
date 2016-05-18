// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Text;

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
                byte[] ilBytes = _methodIL.GetILBytes() ?? Array.Empty<byte>();

                StringBuilder sb = new StringBuilder();

                sb.Append("// Code size: ");
                sb.Append(ilBytes.Length);
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
                        sb.Append(locals[i].Type.ToString());
                        sb.Append(" ");
                        if (locals[i].IsPinned)
                            sb.Append("pinned ");
                        sb.Append("V_");
                        sb.Append(i);
                    }
                    sb.AppendLine(")");
                }
                sb.AppendLine();

                int offset = 0;

                Func<int, string> resolver = token => _methodIL.GetObject(token).ToString();

                while (offset < ilBytes.Length)
                {
                    sb.Append(ILDisassember.FormatOffset(offset));
                    sb.Append(": ");
                    sb.AppendLine(ILDisassember.Disassemble(resolver, ilBytes, ref offset));
                }

                return sb.ToString();
            }
        }
    }
}
