// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Collections.Generic;

namespace Internal.Reflection.Extensibility
{
    public abstract class ExtensibleType : Type
    {
        protected ExtensibleType()
        {
        }

        // TypeInfo/Type will undergo a lot of shakeup so we'll use this to project a 1.0-compatible viewpoint
        // on downward types so we can manage the switchover more easily.

        public override object[] GetCustomAttributes(bool inherit) { throw NotImplemented.ByDesign; }
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }
        public override bool IsDefined(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }
        public override Type ReflectedType { get { throw NotImplemented.ByDesign; } }
    }
}
