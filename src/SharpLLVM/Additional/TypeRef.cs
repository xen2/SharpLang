namespace SharpLLVM
{
    public partial struct TypeRef
    {
        public static readonly TypeRef Empty = new TypeRef();

        public override string ToString()
        {
            return LLVM.PrintTypeToString(this);
        }
    }
}