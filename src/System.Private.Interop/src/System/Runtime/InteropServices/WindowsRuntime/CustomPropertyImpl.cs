// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Diagnostics;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    //
    // ICustomProperty implementation - basically a wrapper of PropertyInfo
    //
    // MCG emits references to this internal type into generated interop code, so we apply the [DependencyReductionRoot]
    // attribute to it so that it survives initial tree shaking.
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    [McgInternalTypeAttribute]
    internal sealed class CustomPropertyImpl : global::Windows.UI.Xaml.Data.ICustomProperty
    {
        /// <summary>
        /// The PropertyInfo for reflection
        /// It might be null if m_supportIndexerWithoutMetadata is true
        /// </summary>
        private PropertyInfo m_property;

        /// <summary>
        /// True if the property is a collection indexer that may not have metadata
        /// Typically a IList/IDictionary
        /// This is attempted as a fallback if metadata is not available or accessor is gone
        /// </summary>
        private bool m_supportIndexerWithoutMetadata;

        /// <summary>
        /// Type that contains an indexer that doesn't have metadata
        /// </summary>
        private Type m_indexerContainingType;

        //
        // Constructor
        //
        public CustomPropertyImpl(PropertyInfo propertyInfo, bool supportIndexerWithoutMetadata, Type indexerContainingType = null)
        {
            m_property = propertyInfo;
            m_supportIndexerWithoutMetadata = supportIndexerWithoutMetadata;
            m_indexerContainingType = indexerContainingType;
        }

        //
        // ICustomProperty interface implementation
        //

        public string Name
        {
            get
            {
                if (m_property != null)
                {
                    return m_property.Name;
                }

                Debug.Assert(m_supportIndexerWithoutMetadata);

                return "Item";
            }
        }

        public bool CanRead
        {
            get
            {
                return m_supportIndexerWithoutMetadata || (m_property.GetMethod != null && m_property.GetMethod.IsPublic);
            }
        }

        public bool CanWrite
        {
            get
            {
                return m_supportIndexerWithoutMetadata || (m_property.SetMethod != null && m_property.SetMethod.IsPublic);
            }
        }

        /// <summary>
        /// Verify caller has access to the getter/setter property
        /// Return true if we can use reflection to access the property. False otherwise.
        /// </summary>
        private bool CheckAccess(bool getValue)
        {
            //
            // If no property, there is nothing to check against
            // We'll use IList/IDictionary to access them
            //
            if (m_property == null)
            {
                Debug.Assert(m_supportIndexerWithoutMetadata);

                return false;
            }

            MethodInfo accessor = getValue ? m_property.GetMethod : m_property.SetMethod;

            if (accessor == null)
            {
                if (m_supportIndexerWithoutMetadata)
                {
                    return false;
                }
                else
                {
                    throw new ArgumentException(getValue ? SR.Arg_GetMethNotFnd : SR.Arg_SetMethNotFnd);
                }
            }

            if (!accessor.IsPublic)
            {
                if (m_supportIndexerWithoutMetadata)
                {
                    return false;
                }
                else
                {
                    Exception ex = new MemberAccessException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        SR.Arg_MethodAccessException_WithMethodName,
                        accessor.ToString(),
                        accessor.DeclaringType.FullName)
                    );

                    // We need to make sure it has the same HR that we were returning in desktop
                    ex.HResult = __HResults.COR_E_METHODACCESS;
                    throw ex;
                }
            }

            return true;
        }

        internal static void LogDataBindingError(string propertyName, Exception ex)
        {
            string message = SR.Format(SR.CustomPropertyProvider_DataBindingError, propertyName, ex.Message);

            // Don't call Debug.WriteLine, since it only does anything in DEBUG builds.  Instead, we call OutputDebugString directly.
            ExternalInterop.OutputDebugString(message);
        }

        public object GetValue(object target)
        {
            // This means you are calling GetValue on a index property
            if (m_supportIndexerWithoutMetadata)
            {
                throw new TargetParameterCountException();
            }

            CheckAccess(getValue: true);

            try
            {
                return m_property.GetValue(UnwrapTarget(target));
            }
            catch (MemberAccessException ex)
            {
                LogDataBindingError(Name, ex);
                throw;
            }
        }

        public void SetValue(object target, object value)
        {
            // This means you are calling SetValue on a index property
            if (m_supportIndexerWithoutMetadata)
            {
                throw new TargetParameterCountException();
            }

            CheckAccess(getValue: false);

            try
            {
                m_property.SetValue(UnwrapTarget(target), value);
            }
            catch (MemberAccessException ex)
            {
                LogDataBindingError(Name, ex);
                throw;
            }
        }

        public Type Type
        {
            get
            {
                if (m_property != null)
                {
                    return m_property.PropertyType;
                }

                //
                // Calculate the property type from IList/IDictionary
                //
                Debug.Assert(m_supportIndexerWithoutMetadata);

                // The following calls look like reflection, but don't require metadata,
                // so they work on all types.
                Type indexType = null;
                TypeInfo containingTypeInfo = m_indexerContainingType.GetTypeInfo();
                foreach (Type itf in containingTypeInfo.ImplementedInterfaces)
                {
                    if (!itf.IsConstructedGenericType)
                    {
                        continue;
                    }

                    Type genericItf = itf.GetGenericTypeDefinition();
                    if (genericItf.Equals(typeof(IList<>)))
                    {
                        Type listArg = itf.GenericTypeArguments[0];
                        if (indexType != null && !indexType.Equals(listArg))
                        {
                            throw new MissingMetadataException();
                        }
                        indexType = listArg;
                    }
                    else if (genericItf.Equals(typeof(IDictionary<,>)))
                    {
                        Type dictionaryArg = itf.GenericTypeArguments[1];
                        if (indexType != null && !indexType.Equals(dictionaryArg))
                        {
                            throw new MissingMetadataException();
                        }
                        indexType = dictionaryArg;
                    }
                }

                if (indexType == null)
                {
                    throw new MissingMetadataException();
                }

                return indexType;
            }
        }

        // Unlike normal .Net, Jupiter properties can have at most one indexer parameter. A null
        // indexValue here means that the property has an indexer argument and its value is null.
        public object GetIndexedValue(object target, object index)
        {
            bool supportPropertyAccess = CheckAccess(getValue: true);
            object unwrappedTarget = UnwrapTarget(target);

            //
            // If we can use reflection, go with reflection
            // If it fails, we would simply throw and not fallback to IList/IDictionary
            //
            if (m_property != null && supportPropertyAccess)
            {
                try
                {
                    return m_property.GetValue(unwrappedTarget, new object[] { index });
                }
                catch (MemberAccessException ex)
                {
                    LogDataBindingError(Name, ex);
                    throw;
                }
            }

            //
            // If reflection is not supported, fallback to IList/IDictionary indexer
            // If fallback is not possible, we would've failed earlier
            //
            Debug.Assert(m_supportIndexerWithoutMetadata);

            IDictionary dictionary = unwrappedTarget as IDictionary;
            if (dictionary != null)
            {
                return dictionary[index];
            }

            IList list = (IList) unwrappedTarget;
            return list[Convert.ToInt32(index)];
        }

        // Unlike normal .Net, Jupiter properties can have at most one indexer parameter. A null
        // indexValue here means that the property has an indexer argument and its value is null.
        public void SetIndexedValue(object target, object value, object index)
        {
            bool supportPropertyAccess = CheckAccess(getValue: false);
            object unwrappedTarget = UnwrapTarget(target);

            //
            // If we can use reflection, go with reflection
            // If it fails, we would simply throw and not fallback to IList/IDictionary
            //
            if (m_property != null && supportPropertyAccess)
            {
                try
                {
                    m_property.SetValue(unwrappedTarget, value, new object[] { index });
                    return;
                }
                catch (MemberAccessException ex)
                {
                    LogDataBindingError(Name, ex);
                    throw;
                }
            }

            //
            // If reflection is not supported, fallback to IList/IDictionary indexer
            // If fallback is not possible, we would've failed earlier
            //
            Debug.Assert(m_supportIndexerWithoutMetadata);

            IDictionary dictionary = unwrappedTarget as IDictionary;
            if (dictionary != null)
            {
                dictionary[index] = value;
                return;
            }

            IList list = (IList) unwrappedTarget;
            list[Convert.ToInt32(index)] = value;
        }

        public static object UnwrapTarget(object target)
        {
            //
            // If target is a managed wrapper, we should unwrap it and use its target for data binding
            // For example, you don't want to data bind against a KeyValuePairImpl<K, V> - you want the real
            // KeyValuePair<K, V>
            //
            object newTarget = McgComHelpers.UnboxManagedWrapperIfBoxed(target);
            if (newTarget != target)
                return newTarget;

            if (target is __ComObject)
            {
                object winrtUnboxed = McgMarshal.UnboxIfBoxed(target);
                if (winrtUnboxed != null)
                    return winrtUnboxed;
            }

            return target;
        }
    }
}
