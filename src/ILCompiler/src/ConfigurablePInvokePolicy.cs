// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.RegularExpressions;

using Internal.IL;

namespace ILCompiler
{
    class ConfigurablePInvokePolicy : PInvokeILEmitterConfiguration
    {
        private readonly Regex[] _libraryNameRegices;

        public ConfigurablePInvokePolicy(IEnumerable<string> pinvokeNames)
        {
            List<Regex> regices = new List<Regex>();
            foreach (string pinvokeName in pinvokeNames)
            {
                regices.Add(new Regex(pinvokeName));
            }
            _libraryNameRegices = regices.ToArray();
        }

        public override bool GenerateDirectCall(string libraryName, string methodName)
        {
            foreach (Regex regex in _libraryNameRegices)
            {
                if (regex.IsMatch(libraryName))
                    return true;
            }

            return false;
        }
    }
}
