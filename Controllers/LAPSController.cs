using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.DirectoryServices;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;
using System.Web.UI;
using COMAPI.Controllers;
using COMAPI.Models;

namespace PublicAPI.Controllers
{
    public class LAPSController : BaseController
    {


        // GET api/LAPS/<hostname>
        public IHttpActionResult Get(string id)
        {
            Trace("Returning LAPS for " + id);
            return Ok(GetPassword(id));
        }

        // POST api/<controller>
        public IHttpActionResult Post([FromBody] LapsRequest body)
        {
            Trace("LAP for " + body.hostname + " Reset: " + body.expire.ToString() + " Key: " + body.key.ToString());
            if (body.key != body.hostname.Length * 41 + body.expire)
            {
                Trace(body.hostname + " " + body.expire.ToString() + " key should be: " + (body.hostname.Length * 41 + body.expire).ToString());
                return BadRequest("Bad key");
            }

            JCHIEntities ef = new JCHIEntities();
            if (ef.SI_CheckIn.Where(w => w.Hostname == body.hostname && w.Username.EndsWith(body.by)).FirstOrDefault() == null)
            {
                return BadRequest("Invalid User");
            }

            if (body.expire > 0)
            {
                body.expire = body.expire > 180 ? 180 : body.expire;
                SetPasswordEpiry(body.hostname, DateTime.Now.AddDays(body.expire));
            }
            
            return Ok(GetPassword(body.hostname));
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }

        private LapsResponse GetPassword(string hostname)
        {
            ActiveDirectoryModel adc = new ActiveDirectoryModel();
            DirectoryEntry de = adc.FindComputer(hostname);
            LapsResponse output = new LapsResponse();

            //return Ok(de.Name);
            output.password = de.Properties["ms-Mcs-AdmPwd"].Value.ToString();
            try
            {
                object largeInteger = de.Properties["ms-Mcs-AdmPwdExpirationTime"].Value;
                var type = largeInteger.GetType();
                var highPart = (int)type.InvokeMember("HighPart", BindingFlags.GetProperty, null, largeInteger, null);
                var lowPart = (int)type.InvokeMember("LowPart", BindingFlags.GetProperty, null, largeInteger, null);
                var d = DateTime.FromFileTime((long)highPart << 32 | (uint)lowPart);
                Trace("Expriation: " + ((long)highPart << 32 | (uint)lowPart).ToString());
                output.expiration = d;
            }
            catch
            {
            }

            return output;
        }

        private bool SetPasswordEpiry(string hostname, DateTime date)
        {
            ActiveDirectoryModel adc = new ActiveDirectoryModel();
            DirectoryEntry de = adc.FindComputer(hostname);

            try
            {
                Trace("Existing expiration: " + (long)de.Properties["ms-Mcs-AdmPwdExpirationTime"].Value);
            }
            catch { }
            try
            {
                de.InvokeSet("ms-Mcs-AdmPwdExpirationTime", date.ToFileTime().ToString());
                de.CommitChanges();
                return true;
            }
            catch (Exception ex)
            {
                Trace("Error expiring password:" + ex.Message);
                return false;
            }

        }

        public struct LapsRequest
        {
            public string hostname, by;
            public int expire;
            public int key;
        }

        public struct LapsResponse
        {
            public string password;
            public DateTime expiration;
        }
    }
}