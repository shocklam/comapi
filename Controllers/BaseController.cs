using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace COMAPI.Controllers
{
    public class BaseController : ApiController
    {
        //just defining some functions to be used in other controllers
        public int isLogEnabled = int.Parse(System.Web.Configuration.WebConfigurationManager.AppSettings["isLogEnabled"]);

        public void Trace(string info, int traceLevel = 1, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
        {
            System.Diagnostics.Trace.WriteLine(info);
            if (isLogEnabled < traceLevel) { return; }
            try
            {
                using (System.IO.StreamWriter outfile = new System.IO.StreamWriter(System.Web.Configuration.WebConfigurationManager.AppSettings["LogPath"], true))
                {
                    outfile.WriteLine(callerName + "\t" + DateTime.Now.ToString() + '\t' + info);
                }
            }
            catch { }
        }

    }
}
