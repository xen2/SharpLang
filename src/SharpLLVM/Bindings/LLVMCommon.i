%define REF_CLASS(TYPE, CSTYPE)
    typedef struct TYPE { } TYPE;
    %typemap(cstype) TYPE* "out $csclassname"
    %typemap(csin) TYPE* "out $csinput.Value"
    %typemap(csin) TYPE "$csinput.Value"
    %typemap(imtype) TYPE "System.IntPtr"
    %typemap(imtype) TYPE* "out System.IntPtr"

    // Arrays (ptr)
    %typemap(csin, pre="    fixed (CSTYPE* swig_ptrTo_$csinput = $csinput)")
                     (TYPE *ARRAY) "(System.IntPtr)swig_ptrTo_$csinput"
    %typemap(cstype) (TYPE *ARRAY) "CSTYPE[]"
    %typemap(imtype) (TYPE *ARRAY) "System.IntPtr"

    // Arrays (ptr + count)
    %typemap(in) (TYPE *ARRAY, unsigned ARRAYSIZE) "$1 = $1_data; $2 = $input;"
    %typemap(ctype) (TYPE *ARRAY, unsigned ARRAYSIZE) "void* $1_data, unsigned int"
    %typemap(csin, pre="    fixed (CSTYPE* swig_ptrTo_$csinput = $csinput)")
                     (TYPE *ARRAY, unsigned ARRAYSIZE) "(System.IntPtr)swig_ptrTo_$csinput, (uint)$csinput.Length"
    %typemap(imtype) (TYPE *ARRAY, unsigned ARRAYSIZE) "System.IntPtr $1_data, uint"
    %typemap(cstype) (TYPE *ARRAY, unsigned ARRAYSIZE) "CSTYPE[]"
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
%typemap(imtype) char** "out string"
