// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// #define DUMP_STACKTRACE_DECODE // uncomment this to print the stack trace blob's decoded bytes to the debug console.

using System.Diagnostics;

namespace Internal.DeveloperExperience.StackTrace
{
    internal class StackTraceBlobIndex
    {
        private const int STACK_TRACE_MAGIC_WORD_INDEX = 0;
        private const int STACK_TRACE_METHOD_DESCRIPTOR_COUNT_INDEX = 1;
        private const int STACK_TRACE_TYPE_DESCRIPTOR_COUNT_INDEX = 2;
        private const int STACK_TRACE_STRING_COUNT_INDEX = 3;
        private const int STACK_TRACE_TRIE_NODE_COUNT_INDEX = 4;

        // This constant marks the beginning of the stack trace data embedded in the binary
        // Just in case we need to update this format, we can update the magic byte so that we 
        // know at runtime which version of the toolchain built this binary and decode the blob 
        // accordingly.
        private const uint STACK_TRACE_MAGIC_WORD = 0xC0;
        private const int INT_SIZE = sizeof(uint);
        private const int RVA_SIZE = INT_SIZE;

        // These lists are built up after initializing offsets. 
        // They are a mapping, from id (or list index) of the descriptor to the offset in the binary where that descriptor is located.
        public uint[] methodOffsets;
        public uint[] typeOffsets;
        public uint[] trieNodeOffsets;
        public uint[] stringOffsets;

        // Initializes all offset lists to be able to quickly find descriptors based on their ids.
        // The four lists, offsets.methodOffsets, offsets.typeOffsets, offsets.stringOffsets, and offsets.trieNodeOffsets are stored in memory,
        // and every method name lookup after these lists are stored is done in constant time after finding the method descriptor.
        public unsafe static StackTraceBlobIndex BuildStackTraceBlobIndex(byte* pBlob, uint cbBlob)
        {
            StackTraceBlobIndex stackTraceBlobIndex = new StackTraceBlobIndex();

            uint* pHeader = (uint*)pBlob;

            // The blob start with the magic word
            uint magicByte = pHeader[STACK_TRACE_MAGIC_WORD_INDEX];
            if (magicByte != STACK_TRACE_MAGIC_WORD)
            {
                return null;
            }

            // Then it stores the number of method descriptors
            uint methodCount = pHeader[STACK_TRACE_METHOD_DESCRIPTOR_COUNT_INDEX];

            // Then it stores the number of type descriptors
            uint typeDescCount = pHeader[STACK_TRACE_TYPE_DESCRIPTOR_COUNT_INDEX];

            // Then it stores the number of strings, and
            uint stringCount = pHeader[STACK_TRACE_STRING_COUNT_INDEX];

            // Finally, it stores the number of trie nodes
            uint trieNodeCount = pHeader[STACK_TRACE_TRIE_NODE_COUNT_INDEX];

            stackTraceBlobIndex.methodOffsets = new uint[(int)methodCount];
            stackTraceBlobIndex.typeOffsets = new uint[(int)typeDescCount];
            stackTraceBlobIndex.stringOffsets = new uint[(int)stringCount];
            stackTraceBlobIndex.trieNodeOffsets = new uint[(int)trieNodeCount];

            // Then it stores all the RVAs of the methods (i.e. methodCount * RVA_SIZE)

            // The method table starts right after the RVAs and counts
            uint tableOffset = methodCount * RVA_SIZE + 5 * INT_SIZE; // for the data we read above.
            byte* tablePtr = pBlob + tableOffset;

            // Keeps a running total of bytes read.
            uint bytesRead = 0;
            int i = 0;

            // Read all the method descriptors.
            for (i = 0; i < methodCount; i++)
            {
#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("BeginMethod(" + i + ")");
#endif
                stackTraceBlobIndex.methodOffsets[i] = tableOffset + bytesRead;
                bytesRead += StackTraceMethodDescriptor.CalculateSize(tablePtr, bytesRead);

#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("EndMethod");
#endif
            }

            // Read all the type descriptors.
            for (i = 0; i < typeDescCount; i++)
            {
#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("BeginType(" + i + ")");
#endif
                stackTraceBlobIndex.typeOffsets[i] = tableOffset + bytesRead;
                bytesRead += StackTraceTypeDescriptor.CalculateSize(tablePtr, bytesRead);
#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("EndType");
#endif
            }

            // Read all the strings.
            for (i = 0; i < stringCount; i++)
            {
#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("BeginString(" + i + ")");
#endif
                stackTraceBlobIndex.stringOffsets[i] = tableOffset + bytesRead;
                bytesRead += StackTraceStringDescriptor.CalculateSize(tablePtr, bytesRead);
#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("EndString");
#endif
            }

            // Read all the trie nodes.
            for (i = 0; i < trieNodeCount; i++)
            {
#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("BeginNode(" + i + ")");
#endif
                stackTraceBlobIndex.trieNodeOffsets[i] = tableOffset + bytesRead;
                bytesRead += StackTraceTrieNode.CalculateSize(tablePtr, bytesRead);
#if DUMP_STACKTRACE_DECODE
                Debug.WriteLine("EndNode");
#endif
            }

            Debug.Assert(bytesRead + tableOffset == cbBlob, "StackTraceBlob deserialization error");
            return stackTraceBlobIndex;
        }
    }
}