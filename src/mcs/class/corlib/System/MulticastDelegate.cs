//
// System.MultiCastDelegate.cs
//
// Authors:
//   Miguel de Icaza (miguel@ximian.com)
//   Daniel Stodden (stodden@in.tum.de)
//
// (C) Ximian, Inc.  http://www.ximian.com
//

//
// Copyright (C) 2004 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;

namespace System
{
	[System.Runtime.InteropServices.ComVisible (true)]
	[Serializable]
	[StructLayout (LayoutKind.Sequential)]
	public abstract class MulticastDelegate : Delegate
	{
        // Note: maybe we could also reuse _target?
	    private Delegate[] invocationList;

		protected MulticastDelegate (object target, string method)
			: base (target, method)
		{
		}

		protected MulticastDelegate (Type target, string method)
			: base (target, method)
		{
		}
		
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData  (info, context);
		}


		// <remarks>
		//   Equals: two multicast delegates are equal if their base is equal
		//   and their invocations list is equal.
		// </remarks>
		public sealed override bool Equals (object obj)
		{
			if (!base.Equals (obj))
				return false;

            var delegate2 = obj as MulticastDelegate;

		    if (invocationList != null)
		    {
                // Compare invocation lists
		        if (delegate2.invocationList == null)
		            return false;

		        var invocationListLength = invocationList.Length;
		        if (invocationListLength != delegate2.invocationList.Length)
		            return false;

		        for (int i = 0; i < invocationListLength; ++i)
		        {
		            if (!invocationList[i].Equals(delegate2.invocationList))
		                return false;
		        }

		        return true;
		    }

            // Compare normal delegates
		    return base.Equals(obj);
		}

		//
		// FIXME: This could use some improvements.
		//
		public sealed override int GetHashCode ()
		{
			return base.GetHashCode ();
		}

		// <summary>
		//   Return, in order of invocation, the invocation list
		//   of a MulticastDelegate
		// </summary>
		public sealed override Delegate[] GetInvocationList ()
		{
		    if (invocationList != null)
		    {
		        var result = new Delegate[invocationList.Length];
                Array.Copy(result, invocationList, invocationList.Length);
		    }

            // Fallback to Delegate
		    return base.GetInvocationList();
		}

		// <summary>
		//   Combines this MulticastDelegate with the (Multicast)Delegate `follow'.
		//   This does _not_ combine with Delegates. ECMA states the whole delegate
		//   thing should have better been a simple System.Delegate class.
		//   Compiler generated delegates are always MulticastDelegates.
		// </summary>
		protected sealed override Delegate CombineImpl (Delegate follow)
		{
			if (this.GetType() != follow.GetType ())
				throw new ArgumentException (Locale.GetText ("Incompatible Delegate Types. First is {0} second is {1}.", this.GetType ().FullName, follow.GetType ().FullName));

			var followMulticast = follow as MulticastDelegate;
            var followInvocationList = followMulticast != null ? followMulticast.invocationList : null;
		    var followInvocationCount = followInvocationList != null ? followInvocationList.Length : 1;

		    Delegate[] newInvocationList;

		    int invocationCount = 1;
		    if (invocationList == null)
		    {
                newInvocationList = new Delegate[1 + followInvocationCount];
                newInvocationList[0] = this;
		    }
		    else
		    {
		        invocationCount = invocationList.Length;
                newInvocationList = new Delegate[invocationCount + followInvocationCount];
                for (int i = 0; i < invocationCount; ++i)
                    newInvocationList[i] = invocationList[i];
		    }

            if (followInvocationList == null)
            {
                newInvocationList[invocationCount] = follow;
            }
            else
            {
                newInvocationList[0] = this;
                for (int i = 0; i < followInvocationCount; ++i)
                    newInvocationList[i + invocationCount] = followInvocationList[i];
            }

		    return NewMulticast(newInvocationList);
		}
        
		protected sealed override Delegate RemoveImpl (Delegate removed)
		{
			if (removed == null)
				return this;

		    var removedMulticast = removed as MulticastDelegate;
		    if (removedMulticast == null || removedMulticast.invocationList == null)
		    {
		        if (invocationList == null)
		        {
                    // Both non multicast Delegate, so nothing left
		            if (base.Equals(removed))
		                return null;
		        }
		        else
		        {
                    // Remove from list
		            int invocationCount = invocationList.Length;
		            for (int i = invocationCount; --i >= 0;)
		            {
		                if (removed.Equals(invocationList[i]))
		                {
                            // Only two delegates?
                            // Returns the other one
		                    if (invocationCount == 2)
		                        return invocationList[1 - i];

		                    var newInvocationList = new Delegate[invocationCount - 1];
		                    
                            int j;
                            for (j = 0; j < i; j++)
                                newInvocationList[j] = invocationList[j];
                            for (j++; j < invocationCount; j++)
                                newInvocationList[j - 1] = invocationList[j];

                            return NewMulticast(newInvocationList);
		                }
		            }
		        }
		    }
		    else
		    {
		        var removedInvocationList = removedMulticast.invocationList;
                var removedInvocationCount = removedInvocationList.Length;

                // Removing a multicast delegate from a delegate wouldn't make sense, only consider the case
                // where we remove a multicast from a multicast
		        if (invocationList != null)
		        {
                    int invocationCount = invocationList.Length;

                    // Iterate over every possible range
                    // TODO: KPM?
		            for (int startIndex = invocationCount - removedInvocationCount; startIndex >= 0; --startIndex)
		            {
                        // Compare ranges
		                var equal = true;
		                for (int i = 0; i < removedInvocationCount; i++)
		                {
		                    if (!(invocationList[startIndex + i].Equals(removedInvocationList[i])))
		                    {
		                        equal = false;
		                        break;
		                    }
		                }

		                if (!equal)
                            continue; // Try startIndex - 1

                        // We have a match
		                var leftInvocationCount = invocationCount - removedInvocationCount;

                        // Nothing left?
                        if (leftInvocationCount == 0)
                            return null;

                        // Only one value left? (last or first)
                        if (leftInvocationCount == 1)
                            return invocationList[startIndex == 0 ? invocationCount - 1 : 0];

                        // General case
                        var newInvocationList = new Delegate[leftInvocationCount];

                        int j;
                        for (j = 0; j < startIndex; j++)
                            newInvocationList[j] = invocationList[j];
                        for (j += removedInvocationCount; j < invocationCount; j++)
                            newInvocationList[j - removedInvocationCount] = invocationList[j];

                        return NewMulticast(newInvocationList);
		            }
		        }
		    }

            // No changes
		    return this;
		}

		public static bool operator == (MulticastDelegate d1, MulticastDelegate d2)
		{
			if (d1 == null)
		    	return d2 == null;
		    		
			return d1.Equals (d2);
		}
		
		public static bool operator != (MulticastDelegate d1, MulticastDelegate d2)
		{
			if (d1 == null)
				return d2 != null;
		    	
			return !d1.Equals (d2);
		}

	    protected abstract IntPtr GetMulticastDispatchMethod();

	    private MulticastDelegate NewMulticast(Delegate[] newInvocationList)
	    {
	        var result = (MulticastDelegate)MemberwiseClone();

	        if (invocationList == null)
	        {
	            // Not a multicast yet, let's setup dispatch method
                result._methodPtr = GetMulticastDispatchMethod();
	            result._methodPtrAux = IntPtr.Zero;
	        }

            // Setup invocation list as target
            result._target = newInvocationList;
            result.invocationList = newInvocationList;

	        return result;
	    }
	}
}
