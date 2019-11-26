using System;
using System.Collections.Generic;
using System.Linq;

namespace CurlUnity
{
    public class CurlMulti : IDisposable
    {
        public delegate void MultiPerformCallback(CURLE result, CurlMulti multi);
        
        private IntPtr multiPtr;
        private CurlShare share;
        private Dictionary<IntPtr, CurlEasy> workingEasies = new Dictionary<IntPtr, CurlEasy>();

        private volatile bool started = false;

        public CurlMulti(IntPtr ptr = default)
        {
            if (ptr != IntPtr.Zero)
            {
                multiPtr = ptr;
            }
            else
            {
                multiPtr = Lib.curl_multi_init();
            }

            Lib.curl_multi_setopt_int(multiPtr, CURLMOPT.PIPELINING, (long)CURLPIPE.MULTIPLEX);

            share = new CurlShare();
            share.SetOpt(CURLSHOPT.SHARE, (long)CURLLOCKDATA.SSL_SESSION);
        }

        internal void CleanUp()
        {
            if (multiPtr != IntPtr.Zero)
            {
                if (CurlMultiUpdater.Instance != null) CurlMultiUpdater.Instance.RemoveMulti(this);
                Lib.curl_multi_cleanup(multiPtr);
                multiPtr = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Abort();
        }

        public void Abort()
        {
            if (multiPtr != IntPtr.Zero)
            {
                CurlEasy[] easies = null;

                lock (workingEasies)
                {
                    easies = workingEasies.Values.ToArray();
                }

                foreach (var easy in easies)
                {
                    easy.Abort();
                }

                CleanUp();
            }
        }

        public void Start()
        {
            if (started)
                return;

            var workingCount = workingEasies.Count;
            if (workingCount > 0 && CurlMultiUpdater.Instance != null)
            {
                started = true;
                CurlMultiUpdater.Instance.AddMulti(this);
            }
        }

        public void Stop()
        {
            if (!started)
                return;

            started = false;

            {
                CurlEasy[] easies = null;

                lock (workingEasies)
                {
                    easies = workingEasies.Values.ToArray();
                }

                foreach (var easy in easies)
                {
                    easy.Abort();
                }
            }

            if (CurlMultiUpdater.Instance != null)
            {
                CurlMultiUpdater.Instance.RemoveMulti(this);
            }
        }

        internal void AddEasy(CurlEasy easy)
        {
            lock (this)
            {
                var wc = workingEasies.Count;
                var res = Lib.curl_multi_add_handle(multiPtr, (IntPtr)easy);
                CurlLog.Assert(res == CURLM.OK, $"AddEasy {easy.uri} {res} {wc}");
                easy.SetOpt(CURLOPT.SHARE, (IntPtr)share);
            }

            lock (workingEasies)
            {
                workingEasies[(IntPtr)easy] = easy;
            }
        }

        internal void RemoveEasy(CurlEasy easy)
        {
            lock (this)
            {
                var res = Lib.curl_multi_remove_handle(multiPtr, (IntPtr)easy);
                CurlLog.Assert(res == CURLM.OK, $"RemoveEasy {res}");
            }

            lock (workingEasies)
            {
                workingEasies.Remove((IntPtr)easy);
            }
        }

        internal int Perform()
        {
            if (!started)
                return 0;

            long running = 0;

            if (multiPtr != IntPtr.Zero)
            {
                lock (this)
                {
                    var res = Lib.curl_multi_perform(multiPtr, ref running);
                    CurlLog.Assert(res == CURLM.OK, $"Perform {res}");
                }

                while (true)
                {
                    long index = 0;
                    var msgPtr = Lib.curl_multi_info_read(multiPtr, ref index);
                    if (msgPtr != IntPtr.Zero)
                    {
                        var msg = (CurlMsg)msgPtr;
                        if (msg.message == CURLMSG.DONE)
                        {
                            CurlEasy easy = null;

                            lock (workingEasies)
                            {
                                workingEasies.TryGetValue(msg.easyPtr, out easy);
                            }

                            if (easy != null)
                            {
                                RemoveEasy(easy);
                                easy.OnMultiPerform(msg.result, this);
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return (int)running;
        }

        internal void SetupLock(bool on)
        {
            share.SetupLock(on);
        }

        public static explicit operator IntPtr(CurlMulti multi)
        {
            return multi.multiPtr;
        }

        public static explicit operator CurlMulti(IntPtr ptr)
        {
            return new CurlMulti(ptr);
        }
    }
}