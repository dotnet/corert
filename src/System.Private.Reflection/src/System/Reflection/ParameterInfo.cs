// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  ParameterInfo
**
==============================================================*/

using global::System;
using global::System.Collections.Generic;

namespace System.Reflection
{
    public class ParameterInfo
    {
        protected ParameterInfo()
        {
        }

        public virtual ParameterAttributes Attributes
        {
            get
            {
                return default(ParameterAttributes);
            }
        }

        public virtual IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual Object DefaultValue
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual bool HasDefaultValue
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }


        public bool IsIn
        {
            get
            {
                return ((Attributes & ParameterAttributes.In) != 0);
            }
        }

        public bool IsOptional
        {
            get
            {
                return ((Attributes & ParameterAttributes.Optional) != 0);
            }
        }

        public bool IsOut
        {
            get
            {
                return ((Attributes & ParameterAttributes.Out) != 0);
            }
        }

        public bool IsRetval
        {
            get
            {
                return ((Attributes & ParameterAttributes.Retval) != 0);
            }
        }


        public virtual MemberInfo Member
        {
            get
            {
                return null;
            }
        }

        public virtual String Name
        {
            get
            {
                return null;
            }
        }

        public virtual Type ParameterType
        {
            get
            {
                return null;
            }
        }

        public virtual int Position
        {
            get
            {
                return 0;
            }
        }
    }
}

