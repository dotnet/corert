// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Thrown when a manual marshalling method is called, but the type was not found
    /// by static analysis or in the rd.xml file.
    /// </summary>
    public class MissingInteropDataException : Exception
    {
        public Type MissingType { get; private set; }

        public MissingInteropDataException(string resourceFormat, Type pertainantType)
            : base()
        {
            MissingType = pertainantType;
        }
    }
}
