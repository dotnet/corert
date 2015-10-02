// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Internal.TypeSystem;

namespace ILToNative
{
    public enum ReadyToRunHelperId
    {
        NewHelper,
        VirtualCall,
        IsInstanceOf,
        CastClass,
        GetNonGCStaticBase,
    }

    class ReadyToRunHelper
    {
        Compilation _compilation;

        public ReadyToRunHelper(Compilation compilation, ReadyToRunHelperId id, Object target)
        {
            _compilation = compilation;

            this.Id = id;
            this.Target = target;
        }

        public ReadyToRunHelperId Id { get; private set; }
        public Object Target { get; private set; }

        public string MangledName
        {
            get
            {
                switch (this.Id)
                {
                    case ReadyToRunHelperId.NewHelper:
                        return "__NewHelper_" + _compilation.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.VirtualCall:
                        return "__VirtualCall_" + _compilation.GetMangledMethodName((MethodDesc)this.Target);
                    case ReadyToRunHelperId.IsInstanceOf:
                        return "__IsInstanceOf_" + _compilation.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.CastClass:
                        return "__CastClass_" + _compilation.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.GetNonGCStaticBase:
                        return "__GetNonGCStaticBase_" + _compilation.GetMangledTypeName((TypeDesc)this.Target);
                    default:
                        throw new NotImplementedException();
                }
            }            
        }
    }
}
