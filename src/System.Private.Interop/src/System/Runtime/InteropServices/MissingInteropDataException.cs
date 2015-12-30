// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Thrown when a manual marshalling method is called, but the type was not found
    /// by static analysis or in the rd.xml file.
    /// </summary>
    class MissingInteropDataException : Exception
    {
        public Type MissingType { get; private set; }

        public MissingInteropDataException(string message) : base(message) { }
#if ENABLE_WINRT
        public MissingInteropDataException(string resourceFormat, Type pertainantType)
           : base(SR.Format(resourceFormat,
           Internal.Reflection.Execution.PayForPlayExperience.MissingMetadataExceptionCreator.ComputeUsefulPertainantIfPossible(pertainantType)))
        {
            MissingType = pertainantType;
        }
#else
        public MissingInteropDataException(string resourceFormat, Type pertainantType)
        {
            MissingType = pertainantType;
        }
#endif //ENABLE_WINRT
    }
}
