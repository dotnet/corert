// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Thrown when a manual marshalling method is called, but the type was not found
    /// by static analysis or in the rd.xml file.
    /// </summary>
    class MissingInteropDataException : Exception
    {
        public Type MissingType { get; private set; }

#if ENABLE_WINRT
        public MissingInteropDataException(string resourceFormat, Type pertainantType)
           : base(SR.Format(resourceFormat,
           Internal.Reflection.Execution.PayForPlayExperience.MissingMetadataExceptionCreator.ComputeUsefulPertainantIfPossible(pertainantType)))
        {
            MissingType = pertainantType;
        }
#else
        public MissingInteropDataException(string resourceFormat, Type pertainantType): 
            base(SR.Format(resourceFormat, pertainantType.Name))
        {
            MissingType = pertainantType;
        }
#endif //ENABLE_WINRT
    }
}
