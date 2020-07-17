// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;

using Internal.Reflection.Core.NonPortable;

namespace Internal.Reflection
{
    internal sealed class RuntimeAssembly : Assembly, IRuntimeImplemented
    {
        private readonly RuntimeTypeHandle _moduleType;

        public RuntimeAssembly(RuntimeTypeHandle moduleType)
        {
            _moduleType = moduleType;
        }

        public override bool Equals(object o)
        {
            return o is RuntimeAssembly other && other._moduleType.Equals(_moduleType);
        }

        public override int GetHashCode()
        {
            return _moduleType.GetHashCode();
        }

        public override IEnumerable<CustomAttributeData> CustomAttributes => new List<CustomAttributeData>();
    }
}
