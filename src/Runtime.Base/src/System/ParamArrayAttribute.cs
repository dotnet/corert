// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System
{
    // Attribute to indicate array of arguments for variable number of args.

    [AttributeUsage(AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    internal class ParamArrayAttribute : Attribute
    {
        public ParamArrayAttribute()
        {
        }
    }
}
