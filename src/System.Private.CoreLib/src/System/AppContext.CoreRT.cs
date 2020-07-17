// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using System.Runtime.ExceptionServices;

namespace System
{
    public static partial class AppContext
    {
        [RuntimeExport("OnFirstChanceException")]
        internal static void OnFirstChanceException(object e)
        {
            FirstChanceException?.Invoke(/* AppDomain */ null, new FirstChanceExceptionEventArgs((Exception)e));
        }
    }
}
