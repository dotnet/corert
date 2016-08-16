// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Why this file exists:
//
// Because the Reflection base types have so many overridable members, it becomes difficult to distinguish
// members we decided not to override vs. those we forgot to override. It would be nice if C# had a construct to 
// tell the reader (and Intellisense) that we've made an explicit decision *not* to override an inherited member, 
// but since it doesn't, we'll make do with this instead.
//
// In DEBUG builds, we'll add a base-delegating override so that it's clear we made an explicit decision
// to accept the base class's implemention. In RELEASE builds, we'll #if'd these out to avoid the extra metadata and runtime
// cost. That way, every overridable member is accounted for (i.e. the codebase should always be kept in a state
// where hitting "override" + SPACE never brings up additional suggestions in Intellisense.)
//
// To avoid introducing inadvertent inconsistencies between DEBUG and RELEASE behavior due to the fragile base class 
// problem, only do this for public or protected members that already exist on the public api type. Since we know 
// we'll never remove those members, we'll avoid the problem of "base" being compile-bound to something different
// from the runtime "base."
//

using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using Internal.Reflection.Extensibility;

namespace System.Reflection.Runtime.Assemblies
{
    internal sealed partial class RuntimeAssembly
    {
#if DEBUG
        public sealed override Type GetType(string name) => base.GetType(name);
        public sealed override bool IsDynamic => base.IsDynamic;
        public sealed override string ToString() => base.ToString();
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.CustomAttributes
{
    internal abstract partial class RuntimeCustomAttributeData
    {
#if DEBUG
        public sealed override bool Equals(object obj) => base.Equals(obj);
        public sealed override int GetHashCode() => base.GetHashCode();
#endif //DEBUG
    }
}

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
#if DEBUG
        public sealed override bool IsSubclassOf(Type c) => base.IsSubclassOf(c);
#endif //DEBUG
    }
}
