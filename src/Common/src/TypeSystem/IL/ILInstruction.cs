namespace Internal.IL
{
    public class ILInstruction
    {
        public int Offset { get; private set; }
        public ILOpcode OpCode { get; private set; }
        public object Operand { get; private set; }

        public ILInstruction(int offset, ILOpcode opCode, object operand)
        {
            Offset = offset;
            OpCode = opCode;
            Operand = operand;
        }
    }
}
