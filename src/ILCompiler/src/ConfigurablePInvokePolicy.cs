// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.IL;

namespace ILCompiler
{
    class ConfigurablePInvokePolicy : PInvokeILEmitterConfiguration
    {
        public ConfigurablePInvokePolicy(IEnumerable<string> pinvokeNames)
        {
            throw new NotImplementedException();
        }

        public override bool GenerateDirectCall(string libraryName, string methodName)
        {
            throw new NotImplementedException();
        }
    }
}
