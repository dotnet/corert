// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    //
    // This class exists for desktop compatibility. If one uses an api such as Type.GetProperty(string) to retrieve a member
    // from a base class, the desktop returns a special MemberInfo object that is blocked from seeing or invoking private
    // set or get methods on that property. That is, the type used to find the member is part of that member's object identity.
    //
    internal sealed class InheritedPropertyInfo : PropertyInfo
    {
        private readonly PropertyInfo _underlyingPropertyInfo;
        private readonly Type _reflectedType;

        internal InheritedPropertyInfo(PropertyInfo underlyingPropertyInfo, Type reflectedType)
        {
            // If the reflectedType is the declaring type, the caller should have used the original PropertyInfo.
            // This assert saves us from having to check this throughout.
            Debug.Assert(!(reflectedType.Equals(underlyingPropertyInfo.DeclaringType)), "reflectedType must be a proper base type of (and not equal to) underlyingPropertyInfo.DeclaringType.");

            _underlyingPropertyInfo = underlyingPropertyInfo;
            _reflectedType = reflectedType;
            return;
        }

        public sealed override PropertyAttributes Attributes
        {
            get { return _underlyingPropertyInfo.Attributes; }
        }

        public sealed override bool CanRead
        {
            get { return GetMethod != null; }
        }

        public sealed override bool CanWrite
        {
            get { return SetMethod != null; }
        }

        public sealed override ParameterInfo[] GetIndexParameters()
        {
            return _underlyingPropertyInfo.GetIndexParameters();
        }

        public sealed override Type PropertyType
        {
            get { return _underlyingPropertyInfo.PropertyType; }
        }

        public sealed override Type DeclaringType
        {
            get { return _underlyingPropertyInfo.DeclaringType; }
        }

        public sealed override String Name
        {
            get { return _underlyingPropertyInfo.Name; }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get { return _underlyingPropertyInfo.CustomAttributes; }
        }

        public sealed override bool Equals(Object obj)
        {
            InheritedPropertyInfo other = obj as InheritedPropertyInfo;
            if (other == null)
            {
                return false;
            }

            if (!(_underlyingPropertyInfo.Equals(other._underlyingPropertyInfo)))
            {
                return false;
            }

            if (!(_reflectedType.Equals(other._reflectedType)))
            {
                return false;
            }

            return true;
        }

        public sealed override int GetHashCode()
        {
            int hashCode = _reflectedType.GetHashCode();
            hashCode ^= _underlyingPropertyInfo.GetHashCode();
            return hashCode;
        }

        public sealed override Object GetConstantValue()
        {
            return _underlyingPropertyInfo.GetConstantValue();
        }

        public sealed override MethodInfo GetMethod
        {
            get
            {
                MethodInfo accessor = _underlyingPropertyInfo.GetMethod;
                return Filter(accessor);
            }
        }

        public sealed override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            if (GetMethod == null)
            {
                throw new ArgumentException(SR.Arg_GetMethNotFnd);
            }

            return _underlyingPropertyInfo.GetValue(obj, invokeAttr, binder, index, culture);
        }

        public sealed override Module Module
        {
            get { return _underlyingPropertyInfo.Module; }
        }

        public sealed override String ToString()
        {
            return _underlyingPropertyInfo.ToString();
        }

        public sealed override MethodInfo SetMethod
        {
            get
            {
                MethodInfo accessor = _underlyingPropertyInfo.SetMethod;
                return Filter(accessor);
            }
        }

        public sealed override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            if (SetMethod == null)
            {
                throw new ArgumentException(SR.Arg_SetMethNotFnd);
            }

            _underlyingPropertyInfo.SetValue(obj, value, invokeAttr, binder, index, culture);
        }

        public sealed override MemberTypes MemberType { get { return MemberTypes.Property; } }

        public sealed override MethodInfo[] GetAccessors(bool nonPublic)
        {
            MethodInfo[] accessors = _underlyingPropertyInfo.GetAccessors(nonPublic);
            MethodInfo[] survivors = new MethodInfo[accessors.Length];
            int numSurvivors = 0;
            for (int i = 0; i < accessors.Length; i++)
            {
                MethodInfo survivor = Filter(accessors[i]);
                if (survivor != null)
                {
                    survivors[numSurvivors++] = survivor;
                }
            }
            Array.Resize(ref survivors, numSurvivors);
            return survivors;
        }

        public sealed override MethodInfo GetGetMethod(bool nonPublic)
        {
            MethodInfo accessor = _underlyingPropertyInfo.GetGetMethod(nonPublic);
            return Filter(accessor);
        }

        public sealed override MethodInfo GetSetMethod(bool nonPublic)
        {
            MethodInfo accessor = _underlyingPropertyInfo.GetSetMethod(nonPublic);
            return Filter(accessor);
        }

        public sealed override object[] GetCustomAttributes(bool inherit)
        {
            return _underlyingPropertyInfo.GetCustomAttributes(inherit);
        }

        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return _underlyingPropertyInfo.GetCustomAttributes(attributeType, inherit);
        }

        public sealed override bool IsDefined(Type attributeType, bool inherit)
        {
            return _underlyingPropertyInfo.IsDefined(attributeType, inherit);
        }

        public sealed override Type ReflectedType
        {
            get { return _underlyingPropertyInfo.ReflectedType; }
        }

        public sealed override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return _underlyingPropertyInfo.GetCustomAttributesData();
        }

        public sealed override Type[] GetOptionalCustomModifiers()
        {
            return _underlyingPropertyInfo.GetOptionalCustomModifiers();
        }

        public sealed override object GetRawConstantValue()
        {
            return _underlyingPropertyInfo.GetRawConstantValue();
        }

        public sealed override Type[] GetRequiredCustomModifiers()
        {
            return _underlyingPropertyInfo.GetRequiredCustomModifiers();
        }

        public sealed override int MetadataToken
        {
            get { return _underlyingPropertyInfo.MetadataToken; }
        }

#if DEBUG
        public sealed override object GetValue(object obj, object[] index) 
        {
            return base.GetValue(obj, index);
        }

        public sealed override void SetValue(object obj, object value, object[] index)
        {
            base.SetValue(obj, value, index);
        }
#endif

        private MethodInfo Filter(MethodInfo accessor)
        {
            //
            // For desktop compat, hide inherited accessors that are marked private.
            //  
            //   Q: Why don't we also hide cross-assembly "internal" accessors?
            //   A: That inconsistency is also desktop-compatible.
            //
            if (accessor == null || accessor.IsPrivate)
            {
                return null;
            }

            return accessor;
        }
    }
}
 
