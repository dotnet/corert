// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Internal.Runtime;
using Internal.NativeFormat;
using System.Diagnostics;
using System.Text;

namespace Internal.Runtime
{
    internal unsafe partial class EETypeOptionalFieldsBuilder
    {
        NativePrimitiveEncoder _encoder;
        OptionalField[] _rgFields = new OptionalField[(int)EETypeOptionalFieldsElement.Count];

        struct OptionalField
        {
            internal bool _fieldPresent;
            internal UInt32 _value;
        }

        internal EETypeOptionalFieldsBuilder() {}
        
        internal UInt32 GetFieldValue(EETypeOptionalFieldsElement eTag, UInt32 defaultValueIfNotFound)
        {
            return _rgFields[(int)eTag]._fieldPresent ? _rgFields[(int)eTag]._value : defaultValueIfNotFound;
        }

        internal void SetFieldValue(EETypeOptionalFieldsElement eTag, UInt32 value)
        {
            _rgFields[(int)eTag]._fieldPresent = true;
            _rgFields[(int)eTag]._value = value;
        }

        internal void ClearField(EETypeOptionalFieldsElement eTag)
        {
            _rgFields[(int)eTag]._fieldPresent = false;
        }

        private int Encode()
        {
            EETypeOptionalFieldsElement eLastTag = EETypeOptionalFieldsElement.Count;

            for (EETypeOptionalFieldsElement eTag = 0; eTag < EETypeOptionalFieldsElement.Count; eTag++)
                eLastTag = _rgFields[(int)eTag]._fieldPresent ? eTag : eLastTag;

            if (eLastTag == EETypeOptionalFieldsElement.Count)
                return 0;

            _encoder = new NativePrimitiveEncoder();
            _encoder.Init();

            for (EETypeOptionalFieldsElement eTag = 0; eTag < EETypeOptionalFieldsElement.Count; eTag++)
            {
                if (!_rgFields[(int)eTag]._fieldPresent)
                    continue;

                _encoder.WriteByte((byte)((byte)eTag | (eTag == eLastTag ? 0x80 : 0)));
                _encoder.WriteUnsigned(_rgFields[(int)eTag]._value);
            }

            return _encoder.Size;
        }

        public byte[] GetBytes()
        {
            Debug.Assert(IsAtLeastOneFieldUsed());
            if (_encoder.Size == 0)
            {
                Encode();
            }

            return _encoder.GetBytes();
        }

        public bool IsAtLeastOneFieldUsed()
        {
            for (int i = 0; i < (int)EETypeOptionalFieldsElement.Count; i++)
            {
                if (_rgFields[i]._fieldPresent)
                    return true;
            }

            return false;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < (int)EETypeOptionalFieldsElement.Count; i++)
            {
                sb.Append(_rgFields[i]._value.ToString());

                if (i != (int)EETypeOptionalFieldsElement.Count - 1)
                {
                    sb.Append("_");
                }
            }

            return sb.ToString();
        }
    }
}
