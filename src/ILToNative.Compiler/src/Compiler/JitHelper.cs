using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Internal.TypeSystem;

namespace ILToNative
{
    public enum JitHelperId
    {
        RngChkFail,
        AssignRef,
        CheckedAssignRef,
        Throw,
    }

    class JitHelper
    {
        Compilation _compilation;

        public JitHelper(Compilation compilation, JitHelperId id)
        {
            _compilation = compilation;

            this.Id = id;
        }

        public JitHelperId Id { get; private set; }

        public string MangledName
        {
            get
            {
                switch (this.Id)
                {
                    case JitHelperId.RngChkFail:
                        return "__range_check_fail";

                    case JitHelperId.AssignRef:
                    case JitHelperId.CheckedAssignRef:
                        return "WriteBarrier";

                    case JitHelperId.Throw:
                        return "__throw_exception";

                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }

}

