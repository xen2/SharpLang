using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public sealed class Compiler
    {
        private ModuleRef module;
        private ContextRef context;

        private Dictionary<TypeReference, Type> types = new Dictionary<TypeReference, Type>();
        private Dictionary<TypeDefinition, Class> classes = new Dictionary<TypeDefinition, Class>();
        private Dictionary<MethodDefinition, Function> functions = new Dictionary<MethodDefinition, Function>();

        public ModuleRef CompileAssembly(AssemblyDefinition assembly)
        {
            module = LLVM.ModuleCreateWithName(assembly.Name.Name);
            context = LLVM.GetModuleContext(module);

            foreach (var assemblyModule in assembly.Modules)
            {
                foreach (var type in assemblyModule.Types)
                {
                    CompileType(type);
                }
            }

            return module;
        }

        Class CompileClass(TypeDefinition typeDefinition, bool compileMethods)
        {
            Class @class;
            if (classes.TryGetValue(typeDefinition, out @class))
                return @class;

            // Process non-static fields
            var dataType = LLVM.StructCreateNamed(context, typeDefinition.FullName);

            var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

            foreach (var field in typeDefinition.Fields)
            {
                if (field.IsStatic)
                    continue;

                fieldTypes.Add(CompileType(field.FieldType).GeneratedType);
            }

            LLVM.StructSetBody(dataType, fieldTypes.ToArray(), false);

            @class = new Class(dataType);

            if (compileMethods)
            {
                // Process methods
                foreach (var method in typeDefinition.Methods)
                {
                    var function = CompileMethod(method);
                }
            }

            classes.Add(typeDefinition, @class);

            return @class;
        }

        Type CompileType(TypeReference typeReference)
        {
            Type type;
            if (types.TryGetValue(typeReference, out type))
                return type;

            var typeDefinition = typeReference as TypeDefinition;
            bool compileMethods = typeDefinition != null;
            if (typeDefinition == null)
            {
                typeDefinition = typeReference.Resolve();
            }

            // Implemented type
            var @class = CompileClass(typeDefinition, compileMethods);

            type = new Type(@class.Type);

            if (type == null)
            {
                throw new NotImplementedException();
            }

            types.Add(typeReference, type);

            return type;
        }

        Function CompileMethod(MethodDefinition method)
        {
            Function function;
            if (functions.TryGetValue(method, out function))
                return function;

            function = new Function();

            if (method.HasBody)
            {
                var body = method.Body;
                var builder = LLVM.CreateBuilderInContext(context);

                foreach (var instruction in body.Instructions)
                {
                    switch (instruction.OpCode.Code)
                    {
                        case Code.Ret:
                        {
                            if (method.ReturnType.MetadataType == MetadataType.Void)
                                LLVM.BuildRetVoid(builder);
                            else
                                throw new NotImplementedException("Opcode not implemented.");
                            break;
                        }
                        default:
                            throw new NotImplementedException("Opcode not implemented.");
                    }
                }
            }
            
            functions.Add(method, function);

            return function;
        }
    }
}