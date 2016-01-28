// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  CustomAttributeData
**
==============================================================*/

using global::System;
using global::System.Collections.Generic;

namespace System.Reflection
{
    public class CustomAttributeData
    {
        protected CustomAttributeData()
        {
        }

        public virtual Type AttributeType
        {
            get
            {
                throw new NullReferenceException(); // Match the desktop's (not very slick) default behavior)
            }
        }

        public virtual IList<CustomAttributeTypedArgument> ConstructorArguments
        {
            get
            {
                throw new NullReferenceException(); // Match the desktop's (not very slick) default behavior)
            }
        }

        public virtual IList<CustomAttributeNamedArgument> NamedArguments
        {
            get
            {
                return null; // This one makes sense at least...
            }
        }
    }
}

