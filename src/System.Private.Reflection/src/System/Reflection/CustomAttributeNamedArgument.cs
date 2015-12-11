// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  CustomAttributeNamedArgument
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public struct CustomAttributeNamedArgument
    {
        public CustomAttributeNamedArgument(Type attributeType, String memberName, bool isField, CustomAttributeTypedArgument typedValue)
        {
            if (attributeType == null)
                throw new ArgumentNullException("attributeType");

            if (memberName == null)
                throw new ArgumentNullException("memberName");

            _memberName = memberName;
            _isField = isField;
            _typedValue = typedValue;
        }

        public String MemberName
        {
            get
            {
                return _memberName;
            }
        }

        public bool IsField
        {
            get
            {
                return _isField;
            }
        }

        public CustomAttributeTypedArgument TypedValue
        {
            get
            {
                return _typedValue;
            }
        }

        private String _memberName;
        private bool _isField;
        private CustomAttributeTypedArgument _typedValue;
    }
}

