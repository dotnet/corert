// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Reflection
{
    public struct CustomAttributeNamedArgument
    {
        // This constructor is the one used by .Net Native as the current metadata format only contains the name and the "isField" value,
        // not the actual member. To keep .Net Native running as before, we'll use the name and isField as the principal data and 
        // construct the MemberInfo on demand.
        internal CustomAttributeNamedArgument(Type attributeType, string memberName, bool isField, CustomAttributeTypedArgument typedValue)
        {
            IsField = isField;
            MemberName = memberName;
            TypedValue = typedValue;
            _attributeType = attributeType;
            _lazyMemberInfo = null;
        }

        public CustomAttributeNamedArgument(MemberInfo memberInfo, object value)
        {
            if (memberInfo == null)
                throw new ArgumentNullException(nameof(memberInfo));

            Type type = null;
            FieldInfo field = memberInfo as FieldInfo;
            PropertyInfo property = memberInfo as PropertyInfo;

            if (field != null)
                type = field.FieldType;
            else if (property != null)
                type = property.PropertyType;
            else
                throw new ArgumentException(SR.Argument_InvalidMemberForNamedArgument);

            _lazyMemberInfo = memberInfo;
            _attributeType = memberInfo.DeclaringType;
            TypedValue = new CustomAttributeTypedArgument(type, value);
            IsField = field != null;
            MemberName = memberInfo.Name;
        }

        public CustomAttributeNamedArgument(MemberInfo memberInfo, CustomAttributeTypedArgument typedArgument)
        {
            if (memberInfo == null)
                throw new ArgumentNullException(nameof(memberInfo));

            _lazyMemberInfo = memberInfo;
            _attributeType = memberInfo.DeclaringType;
            TypedValue = typedArgument;
            IsField = memberInfo is FieldInfo;  // For compat with the desktop, there is no validation that a non-field member is a PropertyInfo.
            MemberName = memberInfo.Name;
        }

        public CustomAttributeTypedArgument TypedValue { get; }
        public bool IsField { get; }
        public string MemberName { get; }

        public MemberInfo MemberInfo
        {
            get
            {
                MemberInfo memberInfo = _lazyMemberInfo;
                if (memberInfo == null)
                {
                    if (IsField)
                        memberInfo = _attributeType.GetField(MemberName, BindingFlags.Public | BindingFlags.Instance);
                    else
                        memberInfo = _attributeType.GetProperty(MemberName, BindingFlags.Public | BindingFlags.Instance);

                    if (memberInfo == null)
                        throw RuntimeAugments.Callbacks.CreateMissingMetadataException(_attributeType);
                    _lazyMemberInfo = memberInfo;
                }
                return memberInfo;
            }
        }

        public override bool Equals(object obj) => obj == (object)this;
        public override int GetHashCode() => base.GetHashCode();
        public static bool operator ==(CustomAttributeNamedArgument left, CustomAttributeNamedArgument right) => left.Equals(right);
        public static bool operator !=(CustomAttributeNamedArgument left, CustomAttributeNamedArgument right) => !(left.Equals(right));

        public override string ToString()
        {
            // base.ToString() is a temporary implementation: this silly looking line officially tags this method as needing further work.
            if (string.Empty.Length > 0) throw new NotImplementedException();
            return base.ToString();
        }

        private readonly Type _attributeType;
        private volatile MemberInfo _lazyMemberInfo;
    }
}
