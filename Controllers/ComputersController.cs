using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.DirectoryServices;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using COMAPI.Models;

namespace COMAPI.Controllers
{
    public class ComputersController : BaseController
    {
        private JCHIEntities db = new JCHIEntities();

        // GET: api/Computers
        public IQueryable<ComputerInfo> GetComputerInfoes()
        {
            return null;
        }

        // GET: api/Computers/d-it-0000
        [ResponseType(typeof(ComputerInfo))]
        public IHttpActionResult GetComputerInfo(string id)
        {
            id = id.ToUpper();
            using (var context = new JCHIEntities())
            {
                ComputerInfo ci;
                try
                {
                    ci = context.ComputerInfoes.Where(w => w.SN.ToUpper().Equals(id) || w.hostname.Contains(id)).OrderByDescending(o => o.TS).First();
                    Trace(ci.hostname + " found");
                }
                catch (Exception ex)
                {
                    Trace("Error finding " + id + ":" + ex.Message);
                    ci = context.ComputerInfoes.Find(id);
                }

                
                if (ci == null)
                {
                    try
                    {
                        ActiveDirectoryModel adm = new ActiveDirectoryModel();
                        DirectoryEntry deComp = adm.FindObject("(&(objectClass=Computer)(|(serialNumber=" + id + ")(name=*" + id + ")))");
                        ci = new ComputerInfo();
                        ci.hostname = deComp.Name;
                        ci.SN = deComp.Properties["serialNumber"].Value.ToString();
                        ci.info = "";
                        Trace("Found AD object for " + id );
                    }
                    catch (Exception ex)
                    {
                        Trace("Error finding AD object for " + id + ":" + ex.Message);
                        return NotFound();
                    }
                }

                return Ok(ci);
            }

        }

        public IHttpActionResult PutComputerAction(string host, string action, string data, string key)
        {
            Models.ActiveDirectoryModel adm = new ActiveDirectoryModel();

            //need some logic for each of the actions
            switch (action)
            {
                case "Upgrade":
                    //going to add this computer to the in-place upgrade list
                    string upgradeGroup = "CN=COM Computer In-Place,OU=IT,OU=Medicine,OU=Colleges,DC=ad,DC=uc,DC=edu";
                    adm.AddGroupMember(upgradeGroup, host);

                    break;
                case "Replace":
                    //adding this comptuer to the replacement list
                    //needs to have the computer that will be replacing it as the 'data'
                    string replaceGroup = "CN=COM Computer Replacements In-Progress,OU=IT,OU=Medicine,OU=Colleges,DC=ad,DC=uc,DC=edu";
                    adm.AddGroupMember(replaceGroup, host);
                    break;
                default:
                    break;
            }

            return Ok();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool ComputerInfoExists(string id)
        {
            return db.ComputerInfoes.Count(e => e.hostname == id) > 0;
        }

        public struct computer
        {
            string hostname, dn, description, notes, properties, info, SN, location, department;
        }
    }
}