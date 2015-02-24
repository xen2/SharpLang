using System.Reflection;

namespace System
{
    /// <summary>
    /// <see cref="Type"/> implementation for generic type instantiations.
    /// </summary>
    class SharpLangTypeGeneric : SharpLangType
    {
        unsafe protected SharpLangEEType* eeType;
        private SharpLangTypeDefinition definition;
        private SharpLangType[] arguments;

        unsafe public SharpLangTypeGeneric(SharpLangEEType* eeType, SharpLangTypeDefinition definition, SharpLangType[] arguments) : base(eeType)
        {
            this.definition = definition;
            this.arguments = arguments;
        }

        public override string Name
        {
            get { return definition.Name; }
        }

        public override string Namespace
        {
            get { return definition.Namespace; }
        }

        public override string FullName
        {
            get
            {
                var result = definition.FullName + "[";

                for (int i = 0; i < arguments.Length; ++i)
                {
                    if (i > 0)
                        result += ",";

                    // TODO: Should be AssemblyQualifiedName
                    result += "[" + arguments[i].AssemblyQualifiedName + "]";
                }

                result += "]";

                return result;
            }
        }

        public override Module Module
        {
            get { return definition.Module; }
        }

        public override Assembly Assembly
        {
            get { return definition.Assembly; }
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            return definition.Attributes;
        }

        public override Type BaseType
        {
            get
            {
                // Test: Force going through ResolveBaseType
                //if (EEType != null)
                //    return base.BaseType;

                return definition.ResolveBaseType(this);
            }
        }

        public override bool IsGenericType
        {
            get { return true; }
        }

        public override bool ContainsGenericParameters
        {
            get
            {
                var genericArguments = GetGenericArguments();
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    if (genericArguments[i].ContainsGenericParameters)
                        return true;
                }

                return false;
            }
        }

        public override Type DeclaringType
        {
            get { return definition.ResolveDeclaringType(this); }
        }

        internal override string InternalAssemblyName
        {
            get { return definition.InternalAssemblyName; }
        }

        internal SharpLangType[] InternalArguments
        {
            get { return arguments; }
        }

        public override Type[] GetGenericArguments()
        {
            var result = new Type[arguments.Length];
            Array.Copy(arguments, result, arguments.Length);
            return result;
        }

        public override Type GetGenericTypeDefinition()
        {
            return definition;
        }
    }
}