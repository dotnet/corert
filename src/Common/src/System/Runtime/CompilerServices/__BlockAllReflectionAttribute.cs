// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
  Providing a definition for __BlockAllReflectionAttribute in an assembly is a signal to the .NET Native toolchain 
  to remove the metadata for all APIs. This both reduces size and disables all reflection on those 
  APIs in libraries that include this.
*/

using System;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All)]
    internal class __BlockAllReflectionAttribute : Attribute { }
}
