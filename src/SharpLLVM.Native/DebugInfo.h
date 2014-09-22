#ifndef SHARPLLVM_DEBUGINFO
#define SHARPLLVM_DEBUGINFO

#include <llvm/IR/DIBuilder.h>

typedef llvm::DIBuilder* LLVMDIBuilderRef;

extern "C" LLVMDIBuilderRef LLVMDIBuilderCreate(LLVMModuleRef M);
extern "C" void LLVMDIBuilderDispose(LLVMDIBuilderRef Builder);
extern "C" void LLVMDIBuilderFinalize(LLVMDIBuilderRef Builder);
extern "C" void LLVMDIBuilderCreateCompileUnit(
	LLVMDIBuilderRef Builder,
	unsigned Lang,
	const char* File,
	const char* Dir,
	const char* Producer,
	bool isOptimized,
	const char* Flags,
	unsigned RuntimeVer,
	const char* SplitName);
extern "C" LLVMValueRef LLVMDIBuilderCreateFile(
	LLVMDIBuilderRef Builder,
	const char* Filename,
	const char* Directory);
extern "C" LLVMValueRef LLVMDIBuilderCreateSubroutineType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef File,
	LLVMValueRef ParameterTypes);
extern "C" LLVMValueRef LLVMDIBuilderCreateFunction(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	const char* LinkageName,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	bool isLocalToUnit,
	bool isDefinition,
	unsigned ScopeLine,
	unsigned Flags,
	bool isOptimized,
	LLVMValueRef Fn,
	LLVMValueRef TParam,
	LLVMValueRef Decl);
extern "C" LLVMValueRef LLVMDIBuilderCreateBasicType(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Encoding);
extern "C" LLVMValueRef LLVMDIBuilderCreatePointerType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef PointeeTy,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	const char* Name);
extern "C" LLVMValueRef LLVMDIBuilderCreateStructType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMValueRef DerivedFrom,
	LLVMValueRef Elements,
	unsigned RunTimeLang,
	LLVMValueRef VTableHolder,
	const char *UniqueId);
extern "C" LLVMValueRef LLVMDIBuilderCreateClassType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMValueRef DerivedFrom,
	LLVMValueRef Elements,
	LLVMValueRef VTableHolder,
	LLVMValueRef TemplateParms,
	const char *UniqueId);
extern "C" LLVMValueRef LLVMDIBuilderCreateMemberType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNo,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMValueRef Ty);
extern "C" LLVMValueRef LLVMDIBuilderCreateLexicalBlock(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	LLVMValueRef File,
	unsigned Line,
	unsigned Col,
	unsigned Discriminator);
extern "C" LLVMValueRef LLVMDIBuilderCreateStaticVariable(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Context,
	const char* Name,
	const char* LinkageName,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	bool isLocalToUnit,
	LLVMValueRef Val,
	LLVMValueRef Decl = NULL);
extern "C" LLVMValueRef LLVMDIBuilderCreateLocalVariable(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	bool AlwaysPreserve,
	unsigned Flags,
	unsigned ArgNo);
extern "C" LLVMValueRef LLVMDIBuilderCreateArrayType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMValueRef Ty,
	LLVMValueRef Subscripts);
extern "C" LLVMValueRef LLVMDIBuilderCreateVectorType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMValueRef Ty,
	LLVMValueRef Subscripts);
extern "C" LLVMValueRef LLVMDIBuilderGetOrCreateSubrange(
	LLVMDIBuilderRef Builder,
	int64_t Lo,
	int64_t Count);
extern "C" LLVMValueRef LLVMDIBuilderGetOrCreateArray(
	LLVMDIBuilderRef Builder,
	LLVMValueRef* Ptr,
	unsigned Count);
extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareAtEnd(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMValueRef VarInfo,
	LLVMBasicBlockRef InsertAtEnd);
extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareBefore(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMValueRef VarInfo,
	LLVMValueRef InsertBefore);
extern "C" LLVMValueRef LLVMDIBuilderCreateEnumerator(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t Val);
extern "C" LLVMValueRef LLVMDIBuilderCreateEnumerationType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	LLVMValueRef Elements,
	LLVMValueRef ClassType);
extern "C" LLVMValueRef LLVMDIBuilderCreateUnionType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMValueRef Elements,
	unsigned RunTimeLang,
	const char* UniqueId);
extern "C" LLVMValueRef LLVMDIBuilderCreateTemplateTypeParameter(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef Ty,
	LLVMValueRef File,
	unsigned LineNo,
	unsigned ColumnNo);
extern "C" LLVMValueRef LLVMDIBuilderCreateOpDeref(LLVMTypeRef IntTy);
extern "C" LLVMValueRef LLVMDIBuilderCreateOpPlus(LLVMTypeRef IntTy);
extern "C" LLVMValueRef LLVMDIBuilderCreateComplexVariable(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	LLVMValueRef Scope,
	const char *Name,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	LLVMValueRef* AddrOps,
	unsigned AddrOpsCount,
	unsigned ArgNo);
extern "C" LLVMValueRef LLVMDIBuilderCreateNameSpace(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNo);
extern "C" void LLVMDICompositeTypeSetTypeArray(
	LLVMValueRef CompositeType,
	LLVMValueRef TypeArray);
extern "C" void LLVMAddModuleFlag(LLVMModuleRef M,
	const char *name,
	uint32_t value);

#endif
