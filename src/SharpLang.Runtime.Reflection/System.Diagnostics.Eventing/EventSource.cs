//
// EventSource.cs
//
// Authors:
//	Marek Safar  <marek.safar@gmail.com>
//
// Copyright (C) 2014 Xamarin Inc (http://www.xamarin.com)
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

namespace System.Diagnostics.Tracing
{
	public class EventSource : IDisposable
	{
        internal static uint s_currentPid;

		protected EventSource ()
		{
		}

		protected EventSource (bool throwOnEventWriteErrors)
		{
		}

		public bool IsEnabled ()
		{
			return false;
		}

		public bool IsEnabled (EventLevel level, EventKeywords keywords)
		{
			return false;
		}

		public void Dispose ()
		{
			Dispose (true);
		}

		protected virtual void Dispose (bool disposing)
		{			
		}

		protected virtual void OnEventCommand (EventCommandEventArgs command)
		{
		}

        protected void WriteEvent(int eventId, int arg1)
        {
        }

		protected void WriteEvent (int eventId, string arg1)
		{
		}

        protected void WriteEvent(int eventId, Guid arg1)
        {
        }

        protected void WriteEvent(int eventId, int arg1, int arg2)
        {
        }

        protected void WriteEvent(int eventId, int arg1, long arg2)
        {
        }

		protected void WriteEvent (int eventId, string arg1, int arg2)
		{
		}

		protected void WriteEvent (int eventId, int arg1, int arg2, int arg3)
		{
		}

        protected void WriteEvent(int eventId, int arg1, int arg2, long arg3)
        {
        }

		protected void WriteEvent (int eventId, string arg1, int arg2, int arg3)
		{
		}

        protected void WriteEvent(int eventId, string arg1, string arg2, string arg3)
        {
        }

        protected void WriteEvent(int eventId, string arg1, string arg2)
        {
        }

        protected unsafe void WriteEventCore(int eventId, int eventDataCount, EventSource.EventData* data)
        {
        }

	    protected unsafe void WriteEventWithRelatedActivityIdCore(int eventId, Guid* relatedActivityId, int eventDataCount, EventSource.EventData* data)
	    {
	    }

	    public static void SetCurrentThreadActivityId(Guid activityId)
	    {
	    }

	    public static void SetCurrentThreadActivityId(Guid activityId, out Guid oldActivityThatWillContinue)
	    {
	        oldActivityThatWillContinue = Guid.Empty;
	    }

        public struct EventData
        {
            public IntPtr DataPointer;
            internal int Size;
        }
	}

    class FrameworkEventSource : EventSource
    {
        public static readonly FrameworkEventSource Log = new FrameworkEventSource();

        public static class Keywords
        {
            public const EventKeywords Loader = (EventKeywords)0x0001; // This is bit 0
            public const EventKeywords ThreadPool = (EventKeywords)0x0002;
            public const EventKeywords NetClient = (EventKeywords)0x0004;
            //
            // This is a private event we do not want to expose to customers.  It is to be used for profiling
            // uses of dynamic type loading by ProjectN applications running on the desktop CLR
            //
            public const EventKeywords DynamicTypeUsage = (EventKeywords)0x0008;
            public const EventKeywords ThreadTransfer = (EventKeywords)0x0010;
        }

        public static bool IsInitialized { get { return false; } }

        public void ThreadTransferSend(long id, int kind, string info, bool multiDequeues)
        {
        }

        public void ThreadTransferSendObj(object id, int kind, string info, bool multiDequeues)
        {
        }

        public void ThreadTransferReceive(long id, int kind, string info)
        {
        }

        public void ThreadTransferReceiveObj(object id, int kind, string info)
        {
        }

        public void ThreadPoolEnqueueWorkObject(object workID)
        {
        }

        public void ThreadPoolDequeueWorkObject(object workID)
        {
        }
    }

    internal class ActivityTracker
    {
        private static ActivityTracker s_activityTrackerInstance = new ActivityTracker();
        public static ActivityTracker Instance { get { return s_activityTrackerInstance; } }

        public void Enable()
        {
        }
    }
}
