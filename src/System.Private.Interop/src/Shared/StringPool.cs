// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Commpressed collection of strings represented by two byte arrays + one ushort array
    ///    m_typeNamespaces are shared substrings, limited to 128 strings, each represented by an index. Currently only namespaces are stored. E.g. "System.Runtime."
    ///    m_typeNames are compressed strings, each represented by an index. Within m_typeNames, [0x80 .. 0xFF] bytes are used to represented shared sub strings.
    ///    m_indices array maps index to start position
    ///
    /// All strings are zero-terminated. Tail reusing is possible, but not implemented by MCG.
    /// If a string in m_typeNames start with 0x01, it's stored as complete UNICODE string (0x01 + two bytes for each char + two 0 bytes). lsb byte goes first.
    ///
    /// Functions:
    ///   1. GetString converts compressed string represented by an index to original System.String
    ///   2. StableStringHash computes hash code without decoding to System.String
    ///   3. IsStringEqual compares compressed string represented by an index with System.String
    ///
    /// TODO:
    ///   1. More string reuse
    /// </summary>
    internal class StringPool
    {
        byte[] m_typeNamespaces;       // Big byte array of all class/interface namespaces
        byte[] m_typeNames;            // Big byte array of all class/interface/value-type names
        UInt16[] m_indices;              // Map from >=0x80 bytes to first char in m_typeNamespaces array

        internal StringPool(
            byte[] typeNamespaces,
            byte[] typeNames,
            UInt16[] indices)
        {
            m_typeNamespaces = typeNamespaces;
            m_typeNames = typeNames;
            m_indices = indices;
        }

        internal const byte Escape_Start = 0x80;
        internal const byte Unicode_Mark = 0x01;    // If first byte is 0x01, whole string is UNICODE: two bytes for each char + two 0 bytes

        /// <summary>
        /// Convert string represented by an index back to original form, by expanding Unicode private use area characters to namespace names
        /// </summary>
        internal unsafe string GetString(UInt32 nameIdx)
        {
            Debug.Assert((nameIdx >= 0) && (nameIdx < m_typeNames.Length));

            fixed (byte* pNs = m_typeNamespaces)
            fixed (byte* pN = m_typeNames)
            {
                int len = 0;

                bool unicode = false;

                if (pN[nameIdx] == Unicode_Mark) // Check for UNICODE mark
                {
                    unicode = true;
                    nameIdx++;
                }

                // Calculate final string length
                for (byte* p = pN + nameIdx; ; p++)
                {
                    int c = *p;

                    if (unicode)
                    {
                        c |= (*(++p)) << 8; // read the 2nd byte, forming a completer UTF16 char
                    }

                    if (c == 0)
                        break;

                    if (!unicode && (c >= Escape_Start)) // If not UNICODE mode, for char in [0x80 .. 0xFF] range read substring from m_typeNamespace array
                    {
                        int namespaceIndex = m_indices[c - Escape_Start];

                        Debug.Assert((namespaceIndex >= 0) && (namespaceIndex < m_typeNamespaces.Length));

                        for (byte* q = pNs + namespaceIndex; *q != 0; q++)
                        {
                            len++;
                        }
                    }
                    else
                    {
                        len++;
                    }
                }

                // Allocate string, TODO make FastAllocString accessible
                string result = new string(' ', len);

                // Fill characters
                fixed (char* pResult = result)
                {
                    char* pDest = pResult;

                    for (byte* p = pN + nameIdx; ; p++)
                    {
                        int c = *p;

                        if (unicode)
                        {
                            c |= (*(++p)) << 8;
                        }

                        if (c == 0)
                            break;

                        if (!unicode && (c >= Escape_Start))
                        {
                            int namespaceIndex = m_indices[c - Escape_Start];

                            Debug.Assert((namespaceIndex >= 0) && (namespaceIndex < m_typeNamespaces.Length));

                            for (byte* q = pNs + namespaceIndex; *q != 0; q++)
                            {
                                *pDest++ = (char)*q;
                                len++;
                            }
                        }
                        else
                        {
                            *pDest++ = (char)c;
                        }
                    }
                }

                return result;
            }
        }

        const int Hash_Init = 5381;

        /// <summary>
        /// Should be inlined
        /// </summary>
        internal static int HashAccumulate(int hash, char val)
        {
            return ((hash << 5) + hash) ^ val;
        }

        /// <summary>
        /// This version needs to be the same as in StableStringHash(int nameIdx)
        /// </summary>
        internal static int StableStringHash(string str)
        {
            int hash = Hash_Init;

            if (str != null)
            {
                hash = HashAccumulate(hash, (char)0); // Make null and "" different

                for (int i = 0; i < str.Length; i++)
                {
                    hash = HashAccumulate(hash, str[i]);
                }
            }

            return hash & 0x7FFFFFFF;
        }

        /// <summary>
        /// Compute hash code same as StableStringHash(System.String)
        /// </summary>
        /// <param name="nameIdx"></param>
        /// <returns></returns>
        internal unsafe int StableStringHash(UInt32 nameIdx)
        {
            int hash = Hash_Init;

            fixed (byte* pNs = m_typeNamespaces)
            fixed (byte* pN = m_typeNames)
            {
                hash = HashAccumulate(hash, (char)0);

                bool unicode = false;

                if (pN[nameIdx] == Unicode_Mark)
                {
                    unicode = true;
                    nameIdx++;
                }

                for (byte* p = pN + nameIdx; ; p++)
                {
                    int c = *p;

                    if (unicode)
                    {
                        c |= (*(++p)) << 8;
                    }

                    if (c == 0)
                    {
                        break;
                    }

                    if (!unicode && c >= Escape_Start)
                    {
                        int namespaceIndex = m_indices[c - Escape_Start];

                        Debug.Assert((namespaceIndex >= 0) && (namespaceIndex < m_typeNamespaces.Length));

                        for (byte* q = pNs + namespaceIndex; *q != 0; q++)
                        {
                            hash = HashAccumulate(hash, (char)*q);
                        }
                    }
                    else
                    {
                        hash = HashAccumulate(hash, (char)c);
                    }
                }
            }

            return hash & 0x7FFFFFFF;
        }

        /// <summary>
        /// Check if name is the same as encoded string represented by nameIdx
        /// </summary>
        internal unsafe bool IsStringEqual(string name, UInt32 nameIdx)
        {
            Debug.Assert(nameIdx < m_typeNames.Length);

            fixed (char* pNameStart = name)
            fixed (byte* pNBlob = m_typeNames, pNsBlob = m_typeNamespaces)
            {
                bool unicode = false;

                if (pNBlob[nameIdx] == Unicode_Mark)
                {
                    unicode = true;
                    nameIdx++;
                }

                char* pName = pNameStart;
                byte* pN = pNBlob + nameIdx;

                for (; ; pN++)
                {
                    int c = *pN;

                    if (unicode)
                    {
                        c |= (*(++pN)) << 8;
                    }

                    if (c == 0)
                        break;

                    if (!unicode && (c >= Escape_Start))
                    {
                        byte* pNs = pNsBlob + m_indices[c - Escape_Start];

                        for (; ; pNs++, pName++)
                        {
                            byte d = *pNs;

                            if (d == 0)
                                break;

                            if (d != *pName)
                                goto NoMatch;
                        }
                    }
                    else
                    {
                        if (c != *pName)
                            goto NoMatch;

                        pName++;
                    }
                }

                if (*pName != '\0')
                    goto NoMatch;
            }

            Debug.Assert(GetString(nameIdx) == name, "IsStringEqual returned a bad result");

            return true;

        NoMatch:
            Debug.Assert(GetString(nameIdx) != name, "IsStringEqual returned a bad result");

            return false;
        }
    }
}
