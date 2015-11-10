// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace ILToNative.DependencyAnalysis
{
    public abstract class ObjectNodeWithFrameInfo : ObjectNode
    {
        FrameInfo[] _frameInfos;

        public void SetFrameInfos(FrameInfo[] frameInfos)
        {
            Debug.Assert(_frameInfos == null);
            _frameInfos = frameInfos;
        }

        public FrameInfo[] GetFrameInfos()
        {
            return _frameInfos;
        }
    }
}
