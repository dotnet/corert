// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Contracts;

namespace System.Globalization
{
    public partial class TextInfo
    {
        internal unsafe TextInfo(CultureData cultureData)
        {
            _cultureData = cultureData;
            _cultureName = _cultureData.CultureName;
            _textInfoName = _cultureData.STEXTINFO;
        }

        private unsafe string ChangeCase(string s, bool toUpper)
        {
            if (s.Length == 0)
            {
                return s;
            }

            char[] buffer = new char[s.Length];

            if (toUpper)
            {
                for (int i=0; i<s.Length; i++)
                {
                    buffer[i] = ('a' <= s[i] && s[i] <= 'z') ? (char)(s[i] - 0x20) : s[i];
                }
            }
            else
            {
                for (int i=0; i<s.Length; i++)
                {
                    buffer[i] = ('A' <= s[i] && s[i] <= 'Z') ? (char)(s[i] | 0x20) : s[i];
                }
            }

            return new string(buffer, 0, buffer.Length);
        }

        private unsafe char ChangeCase(char c, bool toUpper)
        {
            if (toUpper)
            {
                return ('a' <= c && c <= 'z') ? (char)(c - 0x20) : c;
            }

            return ('A' <= c && c <= 'Z') ? (char)(c | 0x20) : c;
        }
    }
}
