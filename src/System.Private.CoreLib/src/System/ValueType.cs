// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
