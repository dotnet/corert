using System;
using System.Collections.Generic;
using System.Text;

namespace Internal.TypeSystem
{
    public static class TypeDescExtensiosn
    {
        public static TypeDesc ResolveSignatureVariable(this TypeDesc t, MethodDesc method)
        {
            if (t.IsSignatureVariable)
            {
                SignatureVariable sigVar = t as SignatureVariable;
                if (sigVar.IsMethodSignatureVariable)
                    return method.Instantiation[sigVar.Index];
                else
                    return method.OwningType.Instantiation[sigVar.Index];
            }
            return t;
        }
    }
}
