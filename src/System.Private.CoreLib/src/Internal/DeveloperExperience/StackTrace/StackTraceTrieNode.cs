// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.DeveloperExperience.StackTrace
{
    internal class StackTraceTrieNode
    {
        private uint parentId;
        private uint stringId;

        public StackTraceTrieNode parent;
        public string name;

        private unsafe StackTraceTrieNode(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint parentId, uint stringId)
        {
            this.parentId = parentId;
            this.stringId = stringId;

            // Creates the objects from the ids.
            this.parent = (this.parentId == 0) ? null : StackTraceTrieNode.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.trieNodeOffsets[(int)this.parentId - 1]);
            this.name = StackTraceStringDescriptor.CreateString(buffer, stackTraceBlobIndex.stringOffsets[(int)this.stringId]);
        }

        public unsafe static StackTraceTrieNode CreateFromBuffer(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint offset)
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
            if (parent == null)
            {
                return name;
            }
            else
            {
                return this.parent.ToString() + name;
            }
        }

        private static unsafe StackTraceTrieNode ReadFromBuffer(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint offset, out uint bytesRead)
        {
            bool constructDescriptor = stackTraceBlobIndex != null;
            bytesRead = 0;
            byte* pMyBuffer = buffer + offset;

            uint parentId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
            uint stringId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);

            if (constructDescriptor)
            {
                return new StackTraceTrieNode(
                        stackTraceBlobIndex,
                        buffer,
                        parentId,
                        stringId
                    );
            }
            else
            {
                return null;
            }
        }
    }
}