using System.Globalization;
using System.Reflection;

namespace System
{
    /// <summary>
    /// Base class for all <see cref="Type"/> that are exposed in SharpLang runtime.
    /// </summary>
    abstract class SharpLangType : TypeInfo, ISharpLangGenericContext
    {
        unsafe internal protected SharpLangEEType* EEType;

        unsafe public SharpLangType(SharpLangEEType* eeType)
        {
            this.EEType = eeType;
        }

        public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
        {
            throw new NotImplementedException();
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        protected override bool IsPrimitiveImpl()
        {
            throw new NotImplementedException();
        }

        protected override bool IsPointerImpl()
        {
            return false;
        }

        protected override bool IsCOMObjectImpl()
        {
            throw new NotImplementedException();
        }

        protected override bool IsByRefImpl()
        {
            return false;
        }

        protected override bool IsArrayImpl()
        {
            return false;
        }

        protected override bool HasElementTypeImpl()
        {
            return false;
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            throw new NotImplementedException();
        }

        protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotImplementedException();
        }

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotImplementedException();
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override Type GetNestedType(string name, BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            throw new NotImplementedException();
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override FieldInfo GetField(string name, BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
        {
            throw new NotImplementedException();
        }

        public override Type GetElementType()
        {
            return null;
        }

        public override Type[] GetInterfaces()
        {
            throw new NotImplementedException();
        }

        public override Type GetInterface(string name, bool ignoreCase)
        {
            throw new NotImplementedException();
        }

        public override Type UnderlyingSystemType
        {
            get { return this; }
        }

        public override Module Module
        {
            get { throw new NotImplementedException(); }
        }

        public override Guid GUID
        {
            get { throw new NotImplementedException(); }
        }

        public override string FullName
        {
            get
            {
                var name = Name;

                // Is it embedded in another type?
                var declaringType = DeclaringType;
                if (declaringType != null)
                    return declaringType.FullName + "+" + name;

                // Append namespace
                var @namespace = Namespace;
                if (@namespace != null)
                    return @namespace + "." + name;

                return name;
            }
        }

        public unsafe override RuntimeTypeHandle TypeHandle
        {
            get
            {
                if (EEType == null)
                    throw new PlatformNotSupportedException();

                return new RuntimeTypeHandle((IntPtr)EEType);
            }
        }

        public unsafe override Type BaseType
        {
            get
            {
                // TODO: Interface should return null too
                if (EEType->Base == null)
                    return null;

                return SharpLangModule.ResolveType(EEType->Base);
            }
        }

        public override bool IsGenericTypeDefinition
        {
            get { return false; }
        }

        public override string AssemblyQualifiedName
        {
            get
            {
                var fullName = FullName;
                if (fullName == null)
                    return null;

                return fullName + ", " + InternalAssemblyName;
            }
        }

        internal virtual string InternalAssemblyName { get { return null; } }

        public override Assembly Assembly
        {
            get { throw new NotImplementedException(); }
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public unsafe override Type MakePointerType()
        {
            return SharpLangModule.ResolveElementType(null, this, SharpLangEEType.Kind.Pointer);
        }

        public unsafe override Type MakeByRefType()
        {
            return SharpLangModule.ResolveElementType(null, this, SharpLangEEType.Kind.ByRef);
        }

        public unsafe override Type MakeArrayType()
        {
            return SharpLangModule.ResolveElementType(null, this, SharpLangEEType.Kind.Array);
        }
    }
}