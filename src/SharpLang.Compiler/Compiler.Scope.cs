using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private void PrepareScopes(FunctionCompilerContext functionContext)
        {
            var function = functionContext.Function;
            var methodReference = function.MethodReference;
            var body = functionContext.Body;

            // Add root scope variables
            // Note: could be null
            var newScope = new Scope(body.Scope);
            functionContext.Scopes.Add(newScope);
            
            // Update debug information
            var startSequencePoint = body.Instructions[0].SequencePoint;
            if (startSequencePoint != null)
            {
                var url = startSequencePoint.Document.Url;
                var line = startSequencePoint.StartLine;
                functionContext.DebugFile = LLVM.DIBuilderCreateFile(debugBuilder, Path.GetFileName(url), Path.GetDirectoryName(url));
                var functionParameterTypes = LLVM.DIBuilderGetOrCreateArray(debugBuilder, new ValueRef[0]);
                var functionType = LLVM.DIBuilderCreateSubroutineType(debugBuilder, functionContext.DebugFile, functionParameterTypes);
                newScope.GeneratedScope = LLVM.DIBuilderCreateFunction(debugBuilder, functionContext.DebugFile, methodReference.FullName, LLVM.GetValueName(function.GeneratedValue),
                    functionContext.DebugFile, (uint)line, functionType,
                    false, true, (uint)line, 0, false, function.GeneratedValue, ValueRef.Empty, ValueRef.Empty);
            }

            SetupDebugLocation(body.Instructions[0], newScope);
            if (body.Scope != null)
            {
                EnterScope(functionContext, newScope);
            }
            else
            {
                // Emit locals (if no scopes)
                for (int index = 0; index < body.Variables.Count; index++)
                {
                    var variable = functionContext.Locals[index];
                    var variableName = body.Variables[index].Name;

                    EmitDebugVariable(functionContext, body.Instructions[0], newScope.GeneratedScope, variable, DW_TAG.auto_variable, variableName);
                }
            }

            // Emit args
            for (int index = 0; index < function.ParameterTypes.Length; index++)
            {
                var arg = functionContext.Arguments[index];

                var argName = LLVM.GetValueName(arg.Value);

                EmitDebugVariable(functionContext, body.Instructions[0], newScope.GeneratedScope, arg, DW_TAG.arg_variable, argName, index + 1);
            }
        }

        private void ProcessScopes(FunctionCompilerContext functionContext, Instruction instruction)
        {
            var scopes = functionContext.Scopes;

            // Exit finished scopes
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                var scope = scopes[index];
                if (scope.Source != null && instruction.Offset > scope.Source.End.Offset)
                    scopes.RemoveAt(index);
                else
                    break;
            }

            var lastScope = scopes[scopes.Count - 1];
            bool foundNewScope = true;
            while (foundNewScope)
            {
                foundNewScope = false;
                if (lastScope.Source != null && lastScope.Source.HasScopes)
                {
                    foreach (var childScope in lastScope.Source.Scopes)
                    {
                        if (instruction == childScope.Start)
                        {
                            lastScope = CreateScope(functionContext, lastScope, childScope);
                            scopes.Add(lastScope);

                            EnterScope(functionContext, lastScope);

                            foundNewScope = true;
                            break;
                        }
                    }
                }
            }

            SetupDebugLocation(instruction, lastScope);
        }

        private void SetupDebugLocation(Instruction instruction, Scope lastScope)
        {
            var sequencePoint = instruction.SequencePoint;
            if (sequencePoint != null)
            {
                var line = sequencePoint.StartLine;
                var column = sequencePoint.StartColumn;
                var debugLoc = LLVM.MDNodeInContext(context,
                    new[]
                    {
                        LLVM.ConstInt(int32Type, (ulong) line, true), LLVM.ConstInt(int32Type, (ulong) column, true),
                        lastScope.GeneratedScope, ValueRef.Empty
                    });
                LLVM.SetCurrentDebugLocation(builder, debugLoc);
            }
        }

        private Scope CreateScope(FunctionCompilerContext functionContext, Scope parentScope, Mono.Cecil.Cil.Scope cecilScope)
        {
            var newScope = new Scope(cecilScope);
            var sequencePoint = newScope.Source.Start.SequencePoint;
            if (sequencePoint != null)
            {
                newScope.GeneratedScope = LLVM.DIBuilderCreateLexicalBlock(debugBuilder, parentScope.GeneratedScope,
                    functionContext.DebugFile,
                    (uint) sequencePoint.StartLine, (uint) sequencePoint.StartColumn, 0);
            }

            return newScope;
        }

        private void EnterScope(FunctionCompilerContext functionContext, Scope newScope)
        {
            if (newScope.Source != null)
            {
                SetupDebugLocation(newScope.Source.Start, newScope);
                if (newScope.Source.HasVariables)
                {
                    foreach (var local in newScope.Source.Variables)
                    {
                        var variable = functionContext.Locals[local.Index];
                        var variableName = local.Name;

                        EmitDebugVariable(functionContext, newScope.Source.Start, newScope.GeneratedScope, variable, DW_TAG.auto_variable, variableName);
                    }
                }
            }
        }

        private void EmitDebugVariable(FunctionCompilerContext functionContext, Instruction start, ValueRef generatedScope, StackValue variable, DW_TAG dwarfType, string variableName, int argIndex = 0)
        {
            var sequencePoint = start.SequencePoint;
            var debugType = CreateDebugType(functionContext, variable.Type);

            // TODO: Detect where variable is actually declared (first use of local?)
            var debugLocalVariable = LLVM.DIBuilderCreateLocalVariable(debugBuilder,
                (uint)dwarfType,
                generatedScope, variableName, functionContext.DebugFile, sequencePoint != null ? (uint)sequencePoint.StartLine : 0, debugType,
                true, 0, (uint)argIndex);

            var debugVariableDeclare = LLVM.DIBuilderInsertDeclareAtEnd(debugBuilder, variable.Value, debugLocalVariable,
                LLVM.GetInsertBlock(builder));
            LLVM.SetInstDebugLocation(builder, debugVariableDeclare);
        }

        public enum DW_TAG
        {
            auto_variable = 0x100,
            arg_variable = 0x101,
        }

        public enum DW_ATE
        {
            Boolean = 0x02,
            Float = 0x04,
            Signed = 0x05,
            Unsigned = 0x07,
        }

        private ValueRef CreateDebugType(FunctionCompilerContext functionContext, Type type)
        {
            var size = LLVM.ABISizeOfType(targetData, type.DefaultType) * 8;
            var align = LLVM.ABIAlignmentOfType(targetData, type.DefaultType) * 8;

            switch (type.TypeReference.MetadataType)
            {
                case MetadataType.Boolean:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "bool", size, align, (uint)DW_ATE.Boolean);
                case MetadataType.SByte:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "sbyte", size, align, (uint)DW_ATE.Signed);
                case MetadataType.Byte:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "byte", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Int16:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "short", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UInt16:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "ushort", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Int32:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "int", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UInt32:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "uint", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Int64:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "long", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UInt64:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "ulong", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Single:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "float", size, align, (uint)DW_ATE.Float);
                case MetadataType.Double:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "double", size, align, (uint)DW_ATE.Float);
                case MetadataType.Char:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "char", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.IntPtr:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "IntPtr", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UIntPtr:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "UIntPtr", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Pointer:
                    var elementType = GetType(((PointerType)type.TypeReference).ElementType);
                    return LLVM.DIBuilderCreatePointerType(debugBuilder, CreateDebugType(functionContext, elementType), size, align, type.TypeReference.Name);
                default:
                    // For now, let's have a fallback since lot of types are not supported yet.
                    return CreateDebugType(functionContext, intPtr);
            }
        }
    }
}
