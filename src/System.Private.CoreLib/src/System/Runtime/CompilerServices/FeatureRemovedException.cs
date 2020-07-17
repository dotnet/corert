// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    // Exception to be thrown when a feature was removed during publishing.
    internal sealed class FeatureRemovedException : Exception
    {
        public string FeatureName { get; }

        public FeatureRemovedException(string featureName)
        {
            FeatureName = featureName;
        }

        public override string Message
        {
            get
            {
                return SR.Format(SR.FeatureRemoved_Message, FeatureName);
            }
        }
    }
}
