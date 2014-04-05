namespace SharpLang.CompilerServices
{
    class Field
    {
        public Field(Class declaringClass, Type type, int structIndex)
        {
            DeclaringClass = declaringClass;
            Type = type;
            StructIndex = structIndex;
        }

        public Class DeclaringClass { get; private set; }

        public Type Type { get; private set; }

        public int StructIndex { get; private set; }
    }
}