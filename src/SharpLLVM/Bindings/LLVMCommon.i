%define REF_ARRAY(TYPE, CSTYPE)
    // Arrays (ptr)
    %typemap(csin, pre="    fixed (CSTYPE* swig_ptrTo_$csinput = $csinput)")
                     (TYPE *ARRAY) "(System.IntPtr)swig_ptrTo_$csinput"
    %typemap(cstype) (TYPE *ARRAY) "CSTYPE[]"
    %typemap(imtype) (TYPE *ARRAY) "System.IntPtr"

    // Arrays (ptr + count)
    %typemap(in) (TYPE *ARRAY, unsigned ARRAYSIZE) "$1 = (TYPE*)$1_data; $2 = $input;"
    %typemap(ctype) (TYPE *ARRAY, unsigned ARRAYSIZE) "void* $1_data, unsigned int"
    %typemap(csin, pre="    fixed (CSTYPE* swig_ptrTo_$csinput = $csinput)")
                     (TYPE *ARRAY, unsigned ARRAYSIZE) "(System.IntPtr)swig_ptrTo_$csinput, (uint)$csinput.Length"
    %typemap(imtype) (TYPE *ARRAY, unsigned ARRAYSIZE) "System.IntPtr $1_data, uint"
    %typemap(cstype) (TYPE *ARRAY, unsigned ARRAYSIZE) "CSTYPE[]"

	// Arrays (count + const array)
    %typemap(in) (unsigned ARRAYSIZE, const TYPE ARRAY[]) "$1 = $1_count; $2 = (TYPE*)$input;"
    %typemap(ctype) (unsigned ARRAYSIZE, const TYPE ARRAY[]) "unsigned int $1_count, void*"
    %typemap(csin, pre="    fixed (CSTYPE* swig_ptrTo_$csinput = $csinput)")
                     (unsigned ARRAYSIZE, const TYPE ARRAY[]) "(uint)$csinput.Length, (System.IntPtr)swig_ptrTo_$csinput"
    %typemap(imtype) (unsigned ARRAYSIZE, const TYPE ARRAY[]) "uint $1_count, System.IntPtr"
    %typemap(cstype) (unsigned ARRAYSIZE, const TYPE ARRAY[]) "CSTYPE[]"
%enddef

%define REF_CLASS(TYPE, CSTYPE)
    typedef struct TYPE { } TYPE;
    %typemap(cstype) TYPE* "out $csclassname"
    %typemap(csin) TYPE* "out $csinput.Value"
    %typemap(csin) TYPE "$csinput.Value"
    %typemap(imtype) TYPE "System.IntPtr"
    %typemap(imtype) TYPE* "out System.IntPtr"

    REF_ARRAY(TYPE, CSTYPE)
%enddef

%nodefault;
%typemap(out) SWIGTYPE %{ $result = $1; %}
%typemap(in) SWIGTYPE %{ $1 = ($1_ltype)$input; %}
%typemap(csinterfaces) SWIGTYPE "System.IEquatable<$csclassname>"
%typemap(csclassmodifiers) SWIGTYPE "public partial struct"
%typemap(csbody) SWIGTYPE %{
    public $csclassname(global::System.IntPtr cPtr)
    {
        Value = cPtr;
    }

    public System.IntPtr Value;
    
    public bool Equals($csclassname other)
    {
        return Value.Equals(other.Value);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        return obj is $csclassname && Equals(($csclassname)obj);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==($csclassname left, $csclassname right)
    {
        return left.Equals(right);
    }

    public static bool operator !=($csclassname left, $csclassname right)
    {
        return !left.Equals(right);
    }%}
%typemap(csout, excode=SWIGEXCODE) SWIGTYPE {
    $&csclassname ret = new $&csclassname($imcall);$excode
    return ret;
  }
%typemap(csdestruct) SWIGTYPE;
%typemap(csfinalize) SWIGTYPE;


%typemap(cstype) char** "out string"
%typemap(csin) char** "out $csinput"
%typemap(imtype, inattributes="[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStr)]") char** "out string"
%typemap(argout) char**
%{ 
	if (*$1 != NULL) *$1 = SWIG_csharp_string_callback(*$1);
%}

%typemap(cstype) size_t* "out System.IntPtr"
%typemap(csin) size_t* "out $csinput"
%typemap(imtype) size_t* "out System.IntPtr"
