using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;

namespace COMAPI.Models
{

    public class BaseModel
    {
        public int isLogEnabled = int.Parse(System.Web.Configuration.WebConfigurationManager.AppSettings["isLogEnabled"]);

        public void Trace(string info, int traceLevel = 1, [CallerMemberName] string callerName = "")
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

        public Dictionary<string, string> ParseConnectionString(string Key)
        {
            Dictionary<string, string> output = new Dictionary<string, string>();
            string connectionString = System.Web.Configuration.WebConfigurationManager.ConnectionStrings[Key].ConnectionString;

            foreach (string item in connectionString.Split(';'))
            {
                output.Add(item.Split('=')[0], item.Split('=')[1]);
            }
            return output;
        }


    }
}