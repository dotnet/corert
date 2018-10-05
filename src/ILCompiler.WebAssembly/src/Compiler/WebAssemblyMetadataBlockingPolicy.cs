using Internal.TypeSystem;

namespace ILCompiler.Compiler
{
    public class WebAssemblyMetadataBlockingPolicy: MetadataBlockingPolicy
    {
        public override bool IsBlocked(MetadataType type)
        {
            return false;
        }

        public override bool IsBlocked(MethodDesc method)
        {
            return true;
        }

        public override bool IsBlocked(FieldDesc field)
        {
            return true;
        }
    }
}
