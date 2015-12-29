// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

//

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
    public sealed class TypeIdentifierAttribute : Attribute
    {
        public TypeIdentifierAttribute() { }
        public TypeIdentifierAttribute(string scope, string identifier) { Scope_ = scope; Identifier_ = identifier; }

        public String Scope { get { return Scope_; } }
        public String Identifier { get { return Identifier_; } }

        internal String Scope_;
        internal String Identifier_;
    }
}
