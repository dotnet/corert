// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.IL;

namespace Internal.TypeSystem
{
    public abstract partial class TypeSystemContext
    {
        private class DelegateInfoHashtable : LockFreeReaderHashtable<TypeDesc, DelegateInfo>
        {
            private enum CoreLibSupportLevel { Unknown, Supported, Unsupported }

            private CoreLibSupportLevel _supportLevel;

            protected override int GetKeyHashCode(TypeDesc key)
            {
                return key.GetHashCode();
            }
            protected override int GetValueHashCode(DelegateInfo value)
            {
                return value.Type.GetHashCode();
            }
            protected override bool CompareKeyToValue(TypeDesc key, DelegateInfo value)
            {
                return Object.ReferenceEquals(key, value.Type);
            }
            protected override bool CompareValueToValue(DelegateInfo value1, DelegateInfo value2)
            {
                return Object.ReferenceEquals(value1.Type, value2.Type);
            }
            protected override DelegateInfo CreateValueFromKey(TypeDesc key)
            {
                if (_supportLevel == CoreLibSupportLevel.Unknown)
                {
                    // Check if the core library supports dynamic invoke.
                    _supportLevel = DelegateInfo.SupportsDynamicInvoke(key.Context) ?
                        CoreLibSupportLevel.Supported : CoreLibSupportLevel.Unsupported;
                }

                DelegateFeature supportedFeatures = _supportLevel == CoreLibSupportLevel.Supported ?
                    DelegateFeature.DynamicInvoke | DelegateFeature.ObjectArrayThunk : 0;


                return new DelegateInfo(key, supportedFeatures);
            }
        }

        private DelegateInfoHashtable _delegateInfoHashtable = new DelegateInfoHashtable();

        public DelegateInfo GetDelegateInfo(TypeDesc delegateType)
        {
            return _delegateInfoHashtable.GetOrCreateValue(delegateType);
        }
    }
}
