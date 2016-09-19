// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.DeveloperExperience.StackTrace
{
    internal class StackTraceMethodDescriptor
    {
        private uint leafId;
        private uint declTypeDescId;
        private uint[] argTypeDescIds;
        private uint[] genericArgTypeDescIds;

        public StackTraceTrieNode name;
        public StackTraceTypeDescriptor declType;
        public StackTraceTypeDescriptor[] argTypes;
        public StackTraceTypeDescriptor[] genericTypes;

        private unsafe StackTraceMethodDescriptor(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint leafId, uint declTypeDescId, uint[] argTypeDescIds, uint[] genericArgTypeDescIds)
        {
            this.leafId = leafId;
            this.declTypeDescId = declTypeDescId;
            this.argTypeDescIds = argTypeDescIds;
            this.genericArgTypeDescIds = genericArgTypeDescIds;

            // Creates the object references.
            this.name = StackTraceTrieNode.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.trieNodeOffsets[(int)this.leafId - 1]);
            this.declType = StackTraceTypeDescriptor.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.typeOffsets[(int)this.declTypeDescId]);

            this.argTypes = new StackTraceTypeDescriptor[argTypeDescIds.Length];
            for (int i = 0; i < argTypeDescIds.Length; i++)
            {
                this.argTypes[i] = StackTraceTypeDescriptor.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.typeOffsets[(int)argTypeDescIds[i]]);
            }

            this.genericTypes = new StackTraceTypeDescriptor[genericArgTypeDescIds.Length];
            for (int i = 0; i < genericArgTypeDescIds.Length; i++)
            {
                this.genericTypes[i] = StackTraceTypeDescriptor.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.typeOffsets[(int)genericArgTypeDescIds[i]]);
            }
        }

        public unsafe static StackTraceMethodDescriptor CreateFromBuffer(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint offset)
        {
            uint bytesRead;
            return ReadFromBuffer(stackTraceBlobIndex, buffer, offset, out bytesRead);
        }

        public static unsafe uint CalculateSize(byte* buffer, uint offset)
        {
            uint bytesRead = 0;
            ReadFromBuffer(null, buffer, offset, out bytesRead);
            return bytesRead;
        }

        public override string ToString()
        {
            string genericParams = (genericTypes.Length == 0) ? "" : "<" + string.Join(",", (object[])this.genericTypes) + ">";
            return declType.ToString() + "." + name.ToString() + genericParams + "(" + string.Join(",", (object[])this.argTypes) + ")";
        }

        private unsafe static StackTraceMethodDescriptor ReadFromBuffer(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint offset, out uint bytesRead)
        {
            bool constructDescriptor = stackTraceBlobIndex != null;
            byte* pMyBuffer = buffer + offset;
            bytesRead = 0;

            uint leafId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
            uint declTypeId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);

            // un-concatenates the two sizes placed in this byte.
            uint numArgsAndGenericArgs = StackTraceDecoder.DecodeByte(pMyBuffer + bytesRead, ref bytesRead);
            uint numArgs = numArgsAndGenericArgs >> 4;
            uint numGenerics = numArgsAndGenericArgs & 0x0F;

            if (numArgs == 15)
            {
                numArgs += StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
            }

            if (numGenerics == 15)
            {
                numGenerics += StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
            }

            uint[] argsTypeIdsList = null;
            uint[] genericTypeIdsList = null;

            if (constructDescriptor)
            {
                argsTypeIdsList = new uint[numArgs];
                genericTypeIdsList = new uint[numGenerics];
            }

            for (uint i = 0; i < numArgs; i++)
            {
                uint argsTypeId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
                if (constructDescriptor)
                {
                    argsTypeIdsList[i] = argsTypeId;
                }
            }

            for (uint i = 0; i < numGenerics; i++)
            {
                uint genericTypeId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
                if (constructDescriptor)
                {
                    genericTypeIdsList[i] = genericTypeId;
                }
            }

            if (constructDescriptor)
            {
                return new StackTraceMethodDescriptor(
                        stackTraceBlobIndex,
                        buffer,
                        leafId,
                        declTypeId,
                        argsTypeIdsList,
                        genericTypeIdsList
                    );
            }
            else
            {
                return null;
            }
        }
    }
}