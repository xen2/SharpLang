namespace System
{
    /// <summary>
    /// Shared <see cref="Type"/> implementation for array types.
    /// </summary>
    class SharpLangTypeArray : SharpLangTypeElement
    {
        private int rank;

        unsafe public SharpLangTypeArray(SharpLangEEType* eeType, SharpLangType elementType, int rank) : base(eeType, elementType)
        {
            this.rank = rank;
        }

        public override int GetArrayRank()
        {
            return rank;
        }

        protected override string NameSuffix
        {
            get { return "[]"; }
        }

        protected override bool IsArrayImpl()
        {
            return true;
        }
    }
}