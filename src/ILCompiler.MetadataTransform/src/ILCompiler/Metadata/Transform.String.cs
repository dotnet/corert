// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat.Writer;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private Dictionary<string, ConstantStringValue> _strings = new Dictionary<string, ConstantStringValue>(StringComparer.Ordinal);

        private ConstantStringValue HandleString(string s)
        {
            if (s == null)
                return null;

            ConstantStringValue result;
            if (!_strings.TryGetValue(s, out result))
            {
                result = (ConstantStringValue)s;
                _strings.Add(s, result);
            }

            return result;
        }
    }
}
