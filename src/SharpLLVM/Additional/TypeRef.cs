namespace SharpLLVM
{
    public partial struct TypeRef
    {
        public override string ToString()
        {
            return LLVM.PrintTypeToString(this);
        }
    }
}