using System;
using System.Collections.Generic;
using Mono.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Describes a value type or class. It usually has fields and methods.
    /// </summary>
    class Class
    {
        internal bool IsEmitted;
        internal bool MethodCompiled;

        public Class(Type type)
        {
            Type = type;
            Fields = new Dictionary<FieldDefinition, Field>();
            VirtualTable = new List<Function>();
            Functions = new List<Function>();
            Interfaces = new HashSet<Class>();
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public Type Type { get; private set; }

        public Dictionary<FieldDefinition, Field> Fields { get; private set; }

        public List<Function> VirtualTable { get; private set; }

        public List<Function> Functions { get; private set; }

        public HashSet<Class> Interfaces { get; private set; }

        /// <summary>
        /// Gets or sets the parent class.
        /// </summary>
        /// <value>
        /// The parent class.
        /// </value>
        public Class BaseType { get; internal set; }

        /// <summary>
        /// Gets or sets the RTTI global variable, which will contain vtable, IMT, static fields, etc...
        /// </summary>
        /// <value>
        /// The generated RTTI global variable.
        /// </value>
        public ValueRef GeneratedRuntimeTypeInfoGlobal { get; internal set; }

        /// <summary>
        /// Gets or sets the depth of this class in type hierarchy.
        /// </summary>
        /// <value>
        /// The depth of this class in type hierarchy.
        /// </value>
        public int Depth { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Type.ToString();
        }
    }
}