#ifndef SHARPLLVM_DEBUGINFO
#define SHARPLLVM_DEBUGINFO

#include <llvm/IR/DIBuilder.h>

typedef llvm::DIBuilder* LLVMDIBuilderRef;
typedef llvm::MDNode* LLVMDIDescriptor;

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
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateFile(
	LLVMDIBuilderRef Builder,
	const char* Filename,
	const char* Directory);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateSubroutineType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor File,
	LLVMDIDescriptor ParameterTypes);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateFunction(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	const char* LinkageName,
	LLVMDIDescriptor File,
	unsigned LineNo,
	LLVMDIDescriptor Ty,
	bool isLocalToUnit,
	bool isDefinition,
	unsigned ScopeLine,
	unsigned Flags,
	bool isOptimized,
	LLVMValueRef Fn,
	LLVMDIDescriptor TParam,
	LLVMDIDescriptor Decl);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateBasicType(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Encoding);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreatePointerType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor PointeeTy,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	const char* Name);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateForwardDecl(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	const char* Name,
	LLVMDIDescriptor Scope,
	LLVMDIDescriptor File,
	unsigned Line,
	unsigned RuntimeLang,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	const char* UniqueId);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateStructType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMDIDescriptor DerivedFrom,
	LLVMDIDescriptor Elements,
	unsigned RunTimeLang,
	LLVMDIDescriptor VTableHolder,
	const char *UniqueId);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateClassType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMDIDescriptor DerivedFrom,
	LLVMDIDescriptor Elements,
	LLVMDIDescriptor VTableHolder,
	LLVMDIDescriptor TemplateParms,
	const char *UniqueId);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateMemberType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNo,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMDIDescriptor Ty);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateLexicalBlock(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	LLVMDIDescriptor File,
	unsigned Line,
	unsigned Col,
	unsigned Discriminator);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateExpression(LLVMDIBuilderRef Builder, int64_t* Addresses, unsigned NumAddresses);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateGlobalVariable(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Context,
	const char* Name,
	const char* LinkageName,
	LLVMDIDescriptor File,
	unsigned LineNo,
	LLVMDIDescriptor Ty,
	bool isLocalToUnit,
	LLVMValueRef Val,
	LLVMDIDescriptor Decl = NULL);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateLocalVariable(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNo,
	LLVMDIDescriptor Ty,
	bool AlwaysPreserve,
	unsigned Flags,
	unsigned ArgNo);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateArrayType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMDIDescriptor Ty,
	LLVMDIDescriptor Subscripts);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateVectorType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMDIDescriptor Ty,
	LLVMDIDescriptor Subscripts);
extern "C" LLVMDIDescriptor LLVMDIBuilderGetOrCreateSubrange(
	LLVMDIBuilderRef Builder,
	int64_t Lo,
	int64_t Count);
extern "C" LLVMDIDescriptor LLVMDIBuilderGetOrCreateArray(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor* Ptr,
	unsigned Count);
extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareAtEnd(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMDIDescriptor VarInfo,
	LLVMDIDescriptor Expr,
	LLVMBasicBlockRef InsertAtEnd);
extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareBefore(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMDIDescriptor VarInfo,
	LLVMDIDescriptor Expr,
	LLVMValueRef InsertBefore);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateEnumerator(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t Val);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateEnumerationType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	LLVMDIDescriptor Elements,
	LLVMDIDescriptor ClassType);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateUnionType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMDIDescriptor Elements,
	unsigned RunTimeLang,
	const char* UniqueId);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateTemplateTypeParameter(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor Ty,
	LLVMDIDescriptor File,
	unsigned LineNo,
	unsigned ColumnNo);
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateNameSpace(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNo);
extern "C" void LLVMDICompositeTypeSetTypeArray(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor* CompositeType,
	LLVMDIDescriptor TypeArray);
extern "C" void LLVMAddModuleFlag(LLVMModuleRef M,
	const char *name,
	uint32_t value);

extern "C" LLVMValueRef LLVMDIMetadataAsValue(LLVMDIDescriptor Value);

extern "C" char* LLVMDIPrintDescriptorToString(LLVMDIDescriptor Value);

extern "C" LLVMDIDescriptor LLVMDICreateDebugLocation(unsigned Line, unsigned Col, LLVMDIDescriptor Scope, LLVMDIDescriptor InlinedAt);

extern "C" uint32_t LLVMDIGetDebugMetadataVersion();

#endif
