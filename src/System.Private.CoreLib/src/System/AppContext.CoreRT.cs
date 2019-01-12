// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;
using System.Runtime.ExceptionServices;

namespace System
{
    public static partial class AppContext
    {
#if PROJECTN
        // AppDomain lives in CoreFX, but some of this class's events need to pass in AppDomains, so people registering those
        // events need to first pass in an AppDomain that we stash here to pass back in the events.
        private static object s_appDomain;

        public static void SetAppDomain(object appDomain)
        {
            s_appDomain = appDomain;
        }

        [RuntimeExport("OnFirstChanceException")]
        internal static void OnFirstChanceException(object e)
        {
            FirstChanceException?.Invoke(s_appDomain, new FirstChanceExceptionEventArgs((Exception)e));
        }
#else
        [RuntimeExport("OnFirstChanceException")]
        internal static void OnFirstChanceException(object e)
        {
            FirstChanceException?.Invoke(/* AppDomain */ null, new FirstChanceExceptionEventArgs((Exception)e));
        }
#endif
    }
}
