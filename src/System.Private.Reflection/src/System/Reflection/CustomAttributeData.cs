// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


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

