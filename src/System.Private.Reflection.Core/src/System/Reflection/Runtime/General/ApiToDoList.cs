// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file collects all of the Reflection apis that we're adding back for .NETCore 2.0, but haven't implemented yet.
// As we implement them, the apis should be moved out of this file and into the main source file for its containing class.
// Once we've implemented them all, this source file can be deleted.
//

using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeConstructorInfo
    {
        public sealed override RuntimeMethodHandle MethodHandle { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.FieldInfos
{
    internal abstract partial class RuntimeFieldInfo
    {
        public sealed override RuntimeFieldHandle FieldHandle { get { throw new NotImplementedException(); } }
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeMethodInfo
    {
        public sealed override RuntimeMethodHandle MethodHandle { get { throw new NotImplementedException(); } }
    }
}
