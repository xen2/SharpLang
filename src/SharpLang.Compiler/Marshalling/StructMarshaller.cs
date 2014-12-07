using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;

// TODO: Native types might be emitted multiple times
namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for non-blittable structure types.
    /// </summary>
    class StructMarshaller : Marshaller
    {
        private readonly TypeReference marshalledType;
        private List<StructField> fields;
        private TypeDefinition nativeType;
        private MethodReference managedToNativeMethod;
        private MethodReference nativeToManagedMethod;

        public StructMarshaller(TypeReference marshalledType)
        {
            this.marshalledType = marshalledType;
        }

        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            EnsureGenerateNativeType(context);

            return nativeType;
        }

        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            // Create marshalled type
            EnsureGenerateNativeType(context);

            if (true)
            {
                // Defer to method
                context.ManagedEmitters.Peek().EmitAddress(context.ILProcessor);
                context.NativeEmitters.Peek().EmitAddress(context.ILProcessor);
                context.ILProcessor.Emit(OpCodes.Call, managedToNativeMethod);
            }
            else
            {
                // TODO: If we decide to inline later (could avoid some allocations)
                EmitStructConvertCore(context, true);
            }
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            // Create marshalled type
            EnsureGenerateNativeType(context);

            if (true)
            {
                // Defer to method
                context.NativeEmitters.Peek().EmitAddress(context.ILProcessor);
                context.ManagedEmitters.Peek().EmitAddress(context.ILProcessor);
                context.ILProcessor.Emit(OpCodes.Call, nativeToManagedMethod);
            }
            else
            {
                // TODO: If we decide to inline later (could avoid some allocations)
                EmitStructConvertCore(context, false);
            }
        }

        public override void EmitStoreManagedToNative(MarshalCodeContext context)
        {
            EmitConvertManagedToNative(context);

            if (context.NativeEmitters.Count == 0)
                context.NativeEmitters.Peek().Emit(context.ILProcessor);
        }

        public override void EmitStoreNativeToManaged(MarshalCodeContext context)
        {
            EmitConvertNativeToManaged(context);

            if (context.ManagedEmitters.Count == 0)
                context.ManagedEmitters.Peek().Emit(context.ILProcessor);
        }

        private void EnsureGenerateNativeType(MarshalCodeContext context)
        {
            // Already done?
            if (fields != null)
                return;

            var corlib = context.Assembly.MainModule.Import(typeof(void)).Resolve().Module.Assembly;
            var voidType = context.Assembly.MainModule.Import(typeof(void));

            fields = new List<StructField>();

            var marshalledTypeDefinition = marshalledType.Resolve();

            // Create native type, with same fields (but using native types)
            var typeAttributes = marshalledTypeDefinition.Attributes;
            typeAttributes &= ~TypeAttributes.NestedPublic;
            nativeType = new TypeDefinition(marshalledType.Namespace, marshalledType.Name + "_Native", typeAttributes, marshalledTypeDefinition.BaseType);
            context.Assembly.MainModule.Types.Add(nativeType);

            // Add non-static fields
            foreach (var fieldDefinition in marshalledTypeDefinition.Fields)
            {
                if (fieldDefinition.IsStatic)
                    continue;

                var field = context.Assembly.MainModule.Import(fieldDefinition);

                var fieldType = ResolveGenericsVisitor.Process(marshalledType, field.FieldType);

                context.ManagedEmitters.Push(new FieldMarshalledObjectEmitter(field));

                var marshaller = FindMarshallerForType(fieldType, fieldDefinition.MarshalInfo);

                var nativeFieldType = context.Assembly.MainModule.Import(marshaller.GetNativeType(context));
                var nativeField = new FieldDefinition(field.Name, fieldDefinition.Attributes, nativeFieldType);
                nativeType.Fields.Add(nativeField);

                fields.Add(new StructField(field, fieldType, nativeField, nativeFieldType, marshaller));

                context.ManagedEmitters.Pop();
            }

            for (int i = 0; i < 2; ++i)
            {
                // Create Managed to Unmanaged wrapper (and opposite)
                var method = new MethodDefinition(i == 0 ? "MarshalManagedToNative" : "MarshalNativeToManaged", MethodAttributes.Static | MethodAttributes.Public, voidType);

                // Add method to type
                nativeType.Methods.Add(method);
                if (i == 0)
                    managedToNativeMethod = method;
                else
                    nativeToManagedMethod = method;

                var managedParameter = new ParameterDefinition("managedObj", ParameterAttributes.None, context.Assembly.MainModule.Import(new ByReferenceType(marshalledType)));
                var nativeParameter = new ParameterDefinition("nativeObj", ParameterAttributes.None, context.Assembly.MainModule.Import(new ByReferenceType(nativeType)));
                method.Parameters.Add(i == 0 ? managedParameter : nativeParameter);
                method.Parameters.Add(i == 0 ? nativeParameter : managedParameter);
                method.Parameters[1].Attributes |= ParameterAttributes.Out;

                var alternateContext = new MarshalCodeContext(context.Assembly, method, false);

                alternateContext.ManagedEmitters.Push(new ParameterMarshalledObjectEmitter(managedParameter));
                alternateContext.ManagedEmitters.Push(new ByReferenceMarshalledObjectEmitter());
                alternateContext.NativeEmitters.Push(new ParameterMarshalledObjectEmitter(nativeParameter));
                alternateContext.NativeEmitters.Push(new ByReferenceMarshalledObjectEmitter());

                EmitStructConvertCore(alternateContext, i == 0);

                alternateContext.ILProcessor.Emit(OpCodes.Ret);

                method.Body.UpdateInstructionOffsets();
            }
        }

        private void EmitStructConvertCore(MarshalCodeContext context, bool managedToNative)
        {
            foreach (var field in fields)
            {
                context.ManagedEmitters.Push(new FieldMarshalledObjectEmitter(field.Field));
                context.NativeEmitters.Push(new FieldMarshalledObjectEmitter(field.NativeField));

                // Marshall field
                if (managedToNative)
                    field.Marshaller.EmitStoreManagedToNative(context);
                else
                    field.Marshaller.EmitStoreNativeToManaged(context);

                context.NativeEmitters.Pop();
                context.ManagedEmitters.Pop();
            }
        }

        struct StructField
        {
            public FieldReference Field;
            public TypeReference FieldType;
            public FieldDefinition NativeField;
            public TypeReference NativeFieldType;
            public Marshaller Marshaller;

            public StructField(FieldReference field, TypeReference fieldType, FieldDefinition nativeField, TypeReference nativeFieldType, Marshaller marshaller)
            {
                Field = field;
                FieldType = fieldType;
                NativeField = nativeField;
                NativeFieldType = nativeFieldType;
                Marshaller = marshaller;
            }
        }
    }
}