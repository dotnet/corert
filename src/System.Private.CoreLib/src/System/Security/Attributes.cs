// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace System.Security
{
    // AllowPartiallyTrustedCallersAttribute:
    //  Indicates that the Assembly is secure and can be used by untrusted
    //  and semitrusted clients
    //  For v.1, this is valid only on Assemblies, but could be expanded to 
    //  include Module, Method, class
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    sealed public class AllowPartiallyTrustedCallersAttribute : System.Attribute
    {
        public AllowPartiallyTrustedCallersAttribute() { }
    }


    // SecurityCriticalAttribute
    //  Indicates that the decorated code or assembly performs security critical operations (e.g. Assert, "unsafe", LinkDemand, etc.)
    //  The attribute can be placed on most targets, except on arguments/return values.
    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Struct |
                    AttributeTargets.Enum |
                    AttributeTargets.Constructor |
                    AttributeTargets.Method |
                    AttributeTargets.Field |
                    AttributeTargets.Interface |
                    AttributeTargets.Delegate,
        AllowMultiple = false,
        Inherited = false)]
    sealed public class SecurityCriticalAttribute : System.Attribute
    {
        public SecurityCriticalAttribute() { }
    }

    // SecuritySafeCriticalAttribute: 
    // Indicates that the code may contain violations to the security critical rules (e.g. transitions from
    //      critical to non-public transparent, transparent to non-public critical, etc.), has been audited for
    //      security concerns and is considered security clean. Also indicates that the code is considered SecurityCritical.
    // At assembly-scope, all rule checks will be suppressed within the assembly and for calls made against the assembly.
    // At type-scope, all rule checks will be suppressed for members within the type and for calls made against the type.
    // At member level (e.g. field and method) the code will be treated as public - i.e. no rule checks for the members.

    [AttributeUsage(AttributeTargets.Class |
                    AttributeTargets.Struct |
                    AttributeTargets.Enum |
                    AttributeTargets.Constructor |
                    AttributeTargets.Method |
                    AttributeTargets.Field |
                    AttributeTargets.Interface |
                    AttributeTargets.Delegate,
        AllowMultiple = false,
        Inherited = false)]
    sealed public class SecuritySafeCriticalAttribute : System.Attribute
    {
        public SecuritySafeCriticalAttribute() { }
    }

    // SecurityTransparentAttribute:
    // Indicates the assembly contains only transparent code.
    // Security critical actions will be restricted or converted into less critical actions. For example,
    // Assert will be restricted, SuppressUnmanagedCode, LinkDemand, unsafe, and unverifiable code will be converted
    // into Full-Demands.

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    sealed public class SecurityTransparentAttribute : System.Attribute
    {
        public SecurityTransparentAttribute() { }
    }
}
