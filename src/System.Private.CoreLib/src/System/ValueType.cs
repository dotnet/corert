// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Base class for all value classes.
**
**
===========================================================*/

using Internal.Runtime.Augments;

namespace System
{
    // CONTRACT with Runtime
    // Place holder type for type hierarchy, Compiler/Runtime requires this class
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public abstract class ValueType
    {
        public override String ToString()
        {
            return this.GetType().ToString();
        }

        public override bool Equals(object obj)
        {
            return RuntimeAugments.Callbacks.ValueTypeEqualsUsingReflection(this, obj);
        }

        public override int GetHashCode()
        {
            return RuntimeAugments.Callbacks.ValueTypeGetHashCodeUsingReflection(this);
        }
    }
}
