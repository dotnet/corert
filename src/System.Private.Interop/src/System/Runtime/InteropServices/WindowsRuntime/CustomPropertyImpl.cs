// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security;
using Windows.UI.Xaml.Data;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    //
    // ICustomProperty implementation - basically a wrapper of PropertyInfo
    //
    // MCG emits references to this internal type into generated interop code, so we apply the [DependencyReductionRoot]
    // attribute to it so that it survives initial tree shaking.
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    internal sealed class CustomPropertyImpl : ICustomProperty
    {
        private PropertyInfo m_property;

        /// <summary>
        /// True if the property is a collection indexer that may not have metadata
        /// </summary>
        private bool m_isIndexerWithoutMetadata;

        /// <summary>
        /// Type that contains an indexer that doesn't have metadata
        /// </summary>
        private Type m_indexerContainingType;

        //
        // Constructor
        //
        public CustomPropertyImpl(PropertyInfo propertyInfo, bool isIndexerWithoutMetadata = false, Type indexerContainingType = null)
        {
            m_property = propertyInfo;
            m_isIndexerWithoutMetadata = isIndexerWithoutMetadata;
            m_indexerContainingType = indexerContainingType;
        }

        //
        // ICustomProperty interface implementation
        //

        public string Name
        {
            get
            {
                if (m_isIndexerWithoutMetadata)
                {
                    return "Item";
                }

                return m_property.Name;
            }
        }

        public bool CanRead
        {
            get
            {
                return m_isIndexerWithoutMetadata || (m_property.GetMethod != null && m_property.GetMethod.IsPublic);
            }
        }

        public bool CanWrite
        {
            get
            {
                return m_isIndexerWithoutMetadata || (m_property.SetMethod != null && m_property.SetMethod.IsPublic);
            }
        }

        /// <summary>
        /// Verify caller has access to the getter/setter property
        /// </summary>
        private void CheckAccess(bool getValue)
        {
            if (m_isIndexerWithoutMetadata)
            {
                return;
            }

            MethodInfo accessor = getValue ? m_property.GetMethod : m_property.SetMethod;

            if (accessor == null)
                throw new ArgumentException(getValue ? SR.Arg_GetMethNotFnd : SR.Arg_SetMethNotFnd);

            if (!accessor.IsPublic)
            {
                Exception ex = new MemberAccessException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        SR.Arg_MethodAccessException_WithMethodName,
                        accessor.ToString(),
                        accessor.DeclaringType.FullName)
                    );

                // We need to make sure it has the same HR that we were returning in desktop
                InteropExtensions.SetExceptionErrorCode(ex, __HResults.COR_E_METHODACCESS);
                throw ex;
            }
        }

        internal static void LogDataBindingError(string propertyName, Exception ex)
        {
            string message = SR.Format(SR.CustomPropertyProvider_DataBindingError, propertyName, ex.Message);

            // Don't call Debug.WriteLine, since it only does anything in DEBUG builds.  Instead, we call OutputDebugString directly.
            ExternalInterop.OutputDebugString(message);
        }

        public object GetValue(object target)
        {
            CheckAccess(getValue: true);

            if (m_isIndexerWithoutMetadata)
            {
                throw new TargetParameterCountException();
            }

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
            CheckAccess(getValue: false);

            if (m_isIndexerWithoutMetadata)
            {
                throw new TargetParameterCountException();
            }

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
                if (m_isIndexerWithoutMetadata)
                {
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

                return m_property.PropertyType;
            }
        }

        // Unlike normal .Net, Jupiter properties can have at most one indexer parameter. A null
        // indexValue here means that the property has an indexer argument and its value is null.
        public object GetIndexedValue(object target, object index)
        {
            CheckAccess(getValue: true);
            object unwrappedTarget = UnwrapTarget(target);

            // We might not have metadata for the accessor, but we can try some common collections
            if (m_isIndexerWithoutMetadata)
            {
                IDictionary dictionary = unwrappedTarget as IDictionary;
                if (dictionary != null)
                {
                    return dictionary[index];
                }
                IList list = unwrappedTarget as IList;
                if (list != null)
                {
                    if (index is int)
                    {
                        return list[(int)index];
                    }
                }
            }

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

        // Unlike normal .Net, Jupiter properties can have at most one indexer parameter. A null
        // indexValue here means that the property has an indexer argument and its value is null.
        public void SetIndexedValue(object target, object value, object index)
        {
            CheckAccess(getValue: false);
            object unwrappedTarget = UnwrapTarget(target);

            // We might not have metadata for the accessor, but we can try some common collections
            IDictionary dictionary = unwrappedTarget as IDictionary;
            if (dictionary != null)
            {
                dictionary[index] = value;
            }
            IList list = unwrappedTarget as IList;
            if (list != null)
            {
                if (index is int)
                {
                    list[(int)index] = value;
                }
            }

            try
            {
                m_property.SetValue(unwrappedTarget, value, new object[] { index });
            }
            catch (MemberAccessException ex)
            {
                LogDataBindingError(Name, ex);
                throw;
            }
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
                object winrtUnboxed = McgModuleManager.UnboxIfBoxed(target);
                if (winrtUnboxed != null)
                    return winrtUnboxed;
            }

            return target;
        }
    }
}
