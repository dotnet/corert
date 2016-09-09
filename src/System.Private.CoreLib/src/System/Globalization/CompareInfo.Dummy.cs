// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Globalization
{
    public partial class CompareInfo
    {
        internal unsafe CompareInfo(CultureInfo culture)
        {
            _name = culture.m_name;
            _sortName = culture.SortName;
        }

        internal unsafe static int IndexOfOrdinal(string source, string value, int startIndex, int count, bool ignoreCase)
        {
            fixed (char *pSource = source) fixed (char *pValue = value)
            {
                char *pSrc = &pSource[startIndex];
                int index = FindStringOrdinal(pSrc, count, pValue, value.Length, FindStringOptions.Start, ignoreCase);
                if (index >= 0)
                {
                    return index + startIndex;
                }
                return -1;
            }
        }

        internal unsafe static int LastIndexOfOrdinal(string source, string value, int startIndex, int count, bool ignoreCase)
        {
            fixed (char *pSource = source) fixed (char *pValue = value)
            {
                char *pSrc = &pSource[startIndex - count + 1];
                int index = FindStringOrdinal(pSrc, count, pValue, value.Length, FindStringOptions.End, ignoreCase);
                if (index >= 0)
                {
                    return index + startIndex - (count - 1);
                }
                return -1;
            }
        }

        private unsafe bool StartsWith(string source, string prefix, CompareOptions options)
        {
            fixed (char *pSource = source) fixed (char *pValue = prefix)
            {
                return FindStringOrdinal(pSource, source.Length, pValue, prefix.Length, FindStringOptions.StartsWith,
                                         (options & CompareOptions.IgnoreCase) != 0) >= 0;
            }
        }

        private unsafe bool EndsWith(string source, string suffix, CompareOptions options)
        {
            fixed (char *pSource = source) fixed (char *pValue = suffix)
            {
                return FindStringOrdinal(pSource, source.Length, pValue, suffix.Length, FindStringOptions.EndsWith,
                                         (options & CompareOptions.IgnoreCase) != 0) >= 0;
            }
        }

        private unsafe int IndexOfCore(string source, string value, int startIndex, int count, CompareOptions options)
        {
            return IndexOfOrdinal(source, value, startIndex, count, (options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0);
        }

        private unsafe int LastIndexOfCore(string source, string value, int startIndex, int count, CompareOptions options)
        {
            return LastIndexOfOrdinal(source, value, startIndex, count, (options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0);
        }

        private unsafe int GetHashCodeOfStringCore(string source, CompareOptions options)
        {
            bool ignoreCase = (options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0;

            if (ignoreCase)
            {
                return source.ToUpper().GetHashCode();
            }

            return source.GetHashCode();
        }

        private unsafe int CompareString(string string1, int offset1, int length1, string string2, int offset2, int length2, CompareOptions options)
        {
            fixed (char *pStr1 = string1) fixed (char *pStr2 = string2)
            {
                char *pString1 = &pStr1[offset1];
                char *pString2 = &pStr2[offset2];

                return CompareString(pString1, length1, pString2, length2, options);
            }
        }

        private static unsafe int CompareStringOrdinalIgnoreCase(char* string1, int count1, char* string2, int count2)
        {
            return CompareString(string1, count1, string2, count2, 0);
        }

        private static unsafe int CompareString(char *pString1, int length1, char *pString2, int length2, CompareOptions options)
        {
            bool ignoreCase = (options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0;
            int index = 0;

            if (ignoreCase)
            {
                while ( index < length1 &&
                        index < length2 &&
                        ToUpper(pString1[index]) == ToUpper(pString2[index]))
                {
                    index++;
                }
            }
            else
            {
                while ( index < length1 &&
                        index < length2 &&
                        pString1[index] == pString2[index])
                {
                    index++;
                }
            }

            if (index >= length1)
            {
                if (index >= length2)
                {
                    return 0;
                }
                return -1;
            }

            if (index >= length2)
            {
                return 1;
            }

            return ignoreCase ? ToUpper(pString1[index]) - ToUpper(pString2[index]) : pString1[index] - pString2[index];
        }


        private unsafe static int FindStringOrdinal(char *source, int sourceCount, char *value,int valueCount, FindStringOptions option, bool ignoreCase)
        {
            int ctrSource = 0;  // index value into source
            int ctrValue = 0;   // index value into value
            char sourceChar;    // Character for case lookup in source
            char valueChar;     // Character for case lookup in value
            int lastSourceStart;

            Debug.Assert(source != null);
            Debug.Assert(value != null);
            Debug.Assert(sourceCount>= 0);
            Debug.Assert(valueCount >= 0);

            if(valueCount == 0)
            {
                switch (option)
                {
                    case FindStringOptions.StartsWith:
                    case FindStringOptions.Start:
                        return(0);

                    case FindStringOptions.EndsWith:
                    case FindStringOptions.End:
                        return(sourceCount);

                    default:
                        return -1;
                }
            }

            if(sourceCount < valueCount)
            {
                return -1;
            }

            switch (option)
            {
                case FindStringOptions.StartsWith:
                {
                    if (ignoreCase)
                    {
                        for (ctrValue = 0; ctrValue < valueCount; ctrValue++)
                        {
                            sourceChar = ToUpper(source[ctrValue]);
                            valueChar  = ToUpper(value[ctrValue]);

                            if (sourceChar != valueChar)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (ctrValue = 0; ctrValue < valueCount; ctrValue++)
                        {
                                sourceChar = source[ctrValue];
                                valueChar  = value[ctrValue];

                                if (sourceChar != valueChar)
                                {
                                    break;
                                }
                        }
                    }

                    if (ctrValue == valueCount)
                    {
                        return 0;
                    }
                }
                break;

                case FindStringOptions.Start:
                {
                    lastSourceStart = sourceCount - valueCount;
                    if (ignoreCase)
                    {
                        char firstValueChar = ToUpper(value[0]);
                        for (ctrSource = 0; ctrSource <= lastSourceStart; ctrSource++)
                        {
                            sourceChar = ToUpper(source[ctrSource]);
                            if (sourceChar != firstValueChar)
                            {
                                continue;
                            }

                            for (ctrValue = 1; ctrValue < valueCount; ctrValue++)
                            {
                                sourceChar = ToUpper(source[ctrSource + ctrValue]);
                                valueChar  = ToUpper(value[ctrValue]);

                                if (sourceChar != valueChar)
                                {
                                    break;
                                }
                            }

                            if (ctrValue == valueCount)
                            {
                                return ctrSource;
                            }
                        }
                    }
                    else
                    {
                        char firstValueChar = value[0];
                        for (ctrSource = 0; ctrSource <= lastSourceStart; ctrSource++)
                        {
                            sourceChar = source[ctrSource];
                            if (sourceChar != firstValueChar)
                            {
                                continue;
                            }

                            for (ctrValue = 1; ctrValue < valueCount; ctrValue++)
                            {
                                sourceChar = source[ctrSource + ctrValue];
                                valueChar  = value[ctrValue];

                                if (sourceChar != valueChar)
                                {
                                    break;
                                }
                            }

                            if (ctrValue == valueCount)
                            {
                                return ctrSource;
                            }
                        }
                    }
                }
                break;

                case FindStringOptions.EndsWith:
                {
                    lastSourceStart = sourceCount - valueCount;
                    if (ignoreCase)
                    {
                        for (ctrSource = lastSourceStart, ctrValue = 0; ctrValue < valueCount; ctrSource++,ctrValue++)
                        {
                            sourceChar = ToUpper(source[ctrSource]);
                            valueChar  = ToUpper(value[ctrValue]);

                            if (sourceChar != valueChar)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (ctrSource = lastSourceStart, ctrValue = 0; ctrValue < valueCount; ctrSource++,ctrValue++)
                        {
                            sourceChar = source[ctrSource];
                            valueChar  = value[ctrValue];

                            if (sourceChar != valueChar)
                            {
                                break;
                            }
                        }
                    }

                    if (ctrValue == valueCount)
                    {
                        return sourceCount - valueCount;
                    }
                }
                break;

                case FindStringOptions.End:
                {
                    lastSourceStart = sourceCount - valueCount;
                    if (ignoreCase)
                    {
                        char firstValueChar = ToUpper(value[0]);
                        for (ctrSource = lastSourceStart; ctrSource >= 0; ctrSource--)
                        {
                            sourceChar = ToUpper(source[ctrSource]);
                            if(sourceChar != firstValueChar)
                            {
                                continue;
                            }
                            for (ctrValue = 1; ctrValue < valueCount; ctrValue++)
                            {
                                sourceChar = ToUpper(source[ctrSource + ctrValue]);
                                valueChar  = ToUpper(value[ctrValue]);

                                if (sourceChar != valueChar)
                                {
                                    break;
                                }
                            }

                            if (ctrValue == valueCount)
                            {
                                return ctrSource;
                            }
                        }
                    } else {
                        char firstValueChar = value[0];
                        for (ctrSource = lastSourceStart; ctrSource >= 0; ctrSource--)
                        {
                            sourceChar = source[ctrSource];
                            if(sourceChar != firstValueChar)
                            {
                                continue;
                            }

                            for (ctrValue = 1; ctrValue < valueCount; ctrValue++)
                            {
                                sourceChar = source[ctrSource + ctrValue];
                                valueChar  = value[ctrValue];

                                if (sourceChar != valueChar)
                                {
                                    break;
                                }
                            }

                            if (ctrValue == valueCount)
                            {
                                return ctrSource;
                            }
                        }
                    }
                }
                break;

                default:
                    return -1;
            }

            return -1;
        }

        private static char ToUpper(char c)
        {
            return ('a' <= c && c <= 'z') ? (char)(c - 0x20) : c;
        }

        private enum FindStringOptions
        {
            Start,
            StartsWith,
            End,
            EndsWith,
        }
    }
}
