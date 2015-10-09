// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
**
** Purpose: Enumerates the characters on a string.  skips range
**          checks.
**
**
============================================================*/

using System.Collections;
using System.Collections.Generic;

namespace System
{
    internal sealed class CharEnumerator : IEnumerator, IEnumerator<char>, IDisposable
    {
        private String _str;
        private int _index;
        private char _currentElement;

        internal CharEnumerator(String str)
        {
            _str = str;
            _index = -1;
        }

        //public Object Clone() {
        //    return MemberwiseClone();
        //}

        public bool MoveNext()
        {
            if (_index < (_str.Length - 1))
            {
                _index++;
                _currentElement = _str[_index];
                return true;
            }
            else
                _index = _str.Length;
            return false;
        }

        public void Dispose()
        {
            if (_str != null)
                _index = _str.Length;
            _str = null;
        }

        /// <internalonly/>
        Object IEnumerator.Current
        {
            get
            {
                if (_index == -1)
                    throw new InvalidOperationException(SR.InvalidOperation_EnumNotStarted);
                if (_index >= _str.Length)
                    throw new InvalidOperationException(SR.InvalidOperation_EnumEnded);

                return _currentElement;
            }
        }

        public char Current
        {
            get
            {
                if (_index == -1)
                    throw new InvalidOperationException(SR.InvalidOperation_EnumNotStarted);
                if (_index >= _str.Length)
                    throw new InvalidOperationException(SR.InvalidOperation_EnumEnded);
                return _currentElement;
            }
        }

        public void Reset()
        {
            _currentElement = (char)0;
            _index = -1;
        }
    }
}
