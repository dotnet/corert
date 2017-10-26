// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        private unsafe void FinishInitialization(string textInfoName)
        {
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
                for (int i = 0; i < s.Length; i++)
                {
                    buffer[i] = ('a' <= s[i] && s[i] <= 'z') ? (char)(s[i] - 0x20) : s[i];
                }
            }
            else
            {
                for (int i = 0; i < s.Length; i++)
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
