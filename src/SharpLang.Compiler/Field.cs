using Mono.Cecil;

namespace SharpLang.CompilerServices
{
    class Field
    {
        public Field(FieldDefinition fieldDefinition, Class declaringClass, Type type, int structIndex)
        {
            FieldDefinition = fieldDefinition;
            DeclaringClass = declaringClass;
            Type = type;
            StructIndex = structIndex;
        }

        /// <summary>
        /// Gets the Cecil field definition.
        /// </summary>
        /// <value>
        /// The Cecil field definition.
        /// </value>
        public FieldDefinition FieldDefinition { get; private set; }

        public Class DeclaringClass { get; private set; }

        public Type Type { get; private set; }

        public int StructIndex { get; private set; }
    }
}