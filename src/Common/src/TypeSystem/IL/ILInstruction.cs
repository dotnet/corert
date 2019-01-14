// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.IL
{
    public struct ILInstruction
    {
        public ILOpcode Opcode { get; private set; }
        public ILOperand Operand { get; private set; }

        public ILInstruction(ILOpcode opcode)
        {
            Opcode = opcode;
            Operand = default(ILOperand);
        }

        public ILInstruction(ILOpcode opcode, ILOperand operand)
        {
            Opcode = opcode;
            Operand = operand;
        }
    }
}
