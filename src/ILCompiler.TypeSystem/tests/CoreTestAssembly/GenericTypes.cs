// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace GenericTypes
{
    /// <summary>
    /// Generic class to be used for testing.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GenericClass<T>
    {
        /// <summary>
        /// Purpose is to manipulate a method involving a generic parameter in its return type.
        /// </summary>
        public virtual T Foo()
        {
            return default(T);
        }
        /// <summary>
        /// Purpose is to manipulate a method involving a generic parameter in its parameter list.
        /// </summary>
        public void Bar(T a)
        {
        }
    }

}