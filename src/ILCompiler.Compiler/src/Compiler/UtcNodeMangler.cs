// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;
using System.Diagnostics;

namespace ILCompiler
{
    //
    // The naming format of these names is known to the debugger
    // 
    public class UtcNodeMangler : WindowsNodeMangler
    {
        public override string MethodGenericDictionary(MethodDesc method)
        {
            return GenericDictionaryNamePrefix + ((UTCNameMangler)NameMangler).GetMangledMethodNameForDictionary(method);
        }
    }
}
