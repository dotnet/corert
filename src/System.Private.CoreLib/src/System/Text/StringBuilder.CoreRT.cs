// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text
{
    public partial class StringBuilder
    {

        /// <summary>
        /// Calculate the new length for array allocation when marshalling StringBuilder in interop
        /// while taking current capacity into account
        /// This is needed to ensure compat with desktop CLR behavior
        /// </summary>
        internal int GetAllocationLength(int requiredLength)
        {
            int currentLength = Capacity;
            if (currentLength < requiredLength)
            {
                // round the current length to the nearest multiple of 2
                // that is >= the required length
                return (requiredLength + 1) & ~1;
            }

            return currentLength;
        }

        /// <summary>
        /// Throw away the current contents in this StringBuffer and replace it with a new char[]
        /// NOTE: The buffer in StringBuilder is *not* NULL-terminated.
        /// This is only called from MCG code
        /// </summary>
        internal unsafe void ReplaceBuffer(char* newBuffer)
        {
            int len = string.wcslen(newBuffer);

            // the '+1' is for back-compat with desktop CLR in terms of length calculation because desktop
            // CLR had '\0'
            char[] chunkChars = new char[GetAllocationLength(len + 1)];

            ThreadSafeCopy(newBuffer, chunkChars, 0, len);

            ReplaceBufferInternal(chunkChars, len);
        }

        /// <summary>
        /// Throw away the current contents in this StringBuffer and replace it with a new char[]
        /// NOTE: The buffer in StringBuilder is *not* NULL-terminated.
        /// This is only called from MCG code
        /// </summary>
        internal void ReplaceBuffer(char[] chunkCharsCandidate)
        {
            int len = chunkCharsCandidate.Length;
            int newLen = GetAllocationLength(len + 1);
            if (len == newLen)
            {
                ReplaceBufferInternal(chunkCharsCandidate, len);
            }
            else
            {
                char[] chunkChars = new char[GetAllocationLength(len + 1)];
                ThreadSafeCopy(chunkCharsCandidate, 0, chunkChars, 0, len);

                ReplaceBufferInternal(chunkChars, len);
            }
        }

        /// <summary>
        /// Replace the internal buffer with the specified length
        /// This is only called from MCG code
        /// </summary>
        private void ReplaceBufferInternal(char[] chunkChars, int length)
        {
            m_ChunkChars = chunkChars;
            m_ChunkOffset = 0;
            m_ChunkLength = length;
            m_ChunkPrevious = null;
        }

        /// <summary>
        /// Return buffer if single chunk, for MCG interop
        /// </summary>
        internal char[] GetBuffer(out int len)
        {
            len = 0;

            if (m_ChunkOffset == 0)
            {
                len = Length;

                return m_ChunkChars;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Copy StringBuilder's contents to the char*, and appending a '\0'
        /// The destination buffer must be big enough, and include space for '\0'
        /// NOTE: There is no guarantee the destination pointer has enough size, but we have no choice
        /// because the pointer might come from native code.
        /// </summary>
        internal unsafe void UnsafeCopyTo(char* destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int count = Length;

            destination[count] = '\0';

            StringBuilder chunk = this;
            int sourceEndIndex = count;
            int curDestIndex = count;
            while (count > 0)
            {
                int chunkEndIndex = sourceEndIndex - chunk.m_ChunkOffset;
                if (chunkEndIndex >= 0)
                {
                    if (chunkEndIndex > chunk.m_ChunkLength)
                        chunkEndIndex = chunk.m_ChunkLength;

                    int chunkCount = count;
                    int chunkStartIndex = chunkEndIndex - count;
                    if (chunkStartIndex < 0)
                    {
                        chunkCount += chunkStartIndex;
                        chunkStartIndex = 0;
                    }
                    curDestIndex -= chunkCount;
                    count -= chunkCount;

                    // SafeCritical: we ensure that chunkStartIndex + chunkCount are within range of m_chunkChars
                    // as well as ensuring that curDestIndex + chunkCount are within range of destination
                    ThreadSafeCopy(chunk.m_ChunkChars, chunkStartIndex, destination + curDestIndex, chunkCount);
                }
                chunk = chunk.m_ChunkPrevious;
            }
        }

        unsafe private static void ThreadSafeCopy(char[] source, int sourceIndex, char* destinationPtr, int count)
        {
            if (count > 0)
            {
                if ((uint)sourceIndex <= (uint)source.Length && (sourceIndex + count) <= source.Length)
                {
                    unsafe
                    {
                        fixed (char* sourcePtr = &source[sourceIndex])
                            string.wstrcpy(destinationPtr, sourcePtr, count);
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(sourceIndex), SR.ArgumentOutOfRange_Index);
                }
            }
        }
    }
}
