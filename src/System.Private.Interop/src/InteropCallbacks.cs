// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using System.Runtime.InteropServices;

namespace Internal.Interop
{
    /// <summary>
    /// Used by CoreFX for  async exception and COM weak reference support.
    /// </summary>
    public static class InteropCallbacks
    {
        public static bool ReportUnhandledError(this Exception ex)
        {
            return ExceptionHelpers.ReportUnhandledError(ex);
        }

        public static Exception AttachRestrictedErrorInfo(this Exception ex)
        {
            return ExceptionHelpers.AttachRestrictedErrorInfo(ex);
        }

        public static object GetCOMWeakReferenceTarget(object weakReference)
        {
            return COMWeakReferenceHelpers.GetTarget(weakReference);
        }

        public static void SetCOMWeakReferenceTarget(object weakReference, object target)
        {
            COMWeakReferenceHelpers.SetTarget(weakReference, target);
        }
    }
}
