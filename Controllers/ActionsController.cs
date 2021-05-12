using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using COMAPI.Models;

namespace COMAPI.Controllers
{
    public class ActionsController : BaseController
    {
        // GET: api/Actions
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Actions/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/Actions
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/Actions/5
        public void Put(string id, [FromBody]ActionItem body)
        {
            //data needs to include new hostname, username, location

            try
            {
                Trace(id + "\t" + body.action + "\t" + body.data);
            }
            catch { }

            Models.ActiveDirectoryModel adm = new ActiveDirectoryModel();

            string replaceGroup = "CN=COM Computer Replacements In-Progress,OU=IT,OU=Medicine,OU=Colleges,DC=ad,DC=uc,DC=edu";
            string availableGroup = "CN=COM Unassigned Computers,OU=IT,OU=Medicine,OU=Colleges,DC=ad,DC=uc,DC=edu";
            string upgradeGroup = "CN=COM Computer In-Place,OU=IT,OU=Medicine,OU=Colleges,DC=ad,DC=uc,DC=edu";
            string surplusContainer = "OU=Surplus,OU=Disabled Objects,OU=Medicine,OU=Colleges,DC=ad,DC=uc,DC=edu";
            string disabledContainer = "OU=Disabled Objects,OU=Medicine,OU=Colleges,DC=ad,DC=uc,DC=edu";

            dynamic data;

            try
            {
                if (body.data != null)
                {
                    data = Newtonsoft.Json.JsonConvert.DeserializeObject(body.data.ToString());
                    Trace(data);
                }
                else
                {
                    data = null;
                }
            }
            catch
            {
                data = body.data;
            }

            DirectoryEntry comp = adm.FindObject("(&(objectClass=computer)(name=" + id + "))");

            if (comp == null)
            {
                Trace("Computer not found: " + id);
                return;
            }

            switch (body.action)
            {
                case "Update":
                    //sets values on the object to whatever is passed through.
                    if (body.key == 23654789)
                    {
                        try
                        {
                            JObject json = JObject.Parse(body.data.ToString());
                            Dictionary<string, string> props = new Dictionary<string, string>();

                            foreach (var item in json)
                            {
                                props.Add(item.Key, item.Value.ToString());
                            }

                            try
                            {
                                adm.UpdateObject(id, props);
                            }
                            catch (Exception ex)
                            {
                                Trace("Properties not updated: " + ex.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace("Properties not updated: " + ex.Message);
                        }
                    }
                    break;
                case "Upgrade":
                    //if they're asking for an in-place upgrade, just add the host to the upgrade list
                    
                    try
                    {
                        //verify the passed key is correct for this action
                        if (body.key == 789456123)
                        {
                            try
                            {
                                adm.AddGroupMember(upgradeGroup, id);
                            }
                            catch(Exception ex)
                            {
                                Trace(ex.Message);
                            }
                            
                        }
                    }
                    catch { }
                    break;
                case "Replace":
                    //need to have the computer that will be replacing it as the 'data'
                    //first thing - validate that the key matches

                    if (body.key != 741852963)
                    {
                        Trace("Invalid key passed: " + body.key);
                        break;
                    }

                    try
                    {
                        //do a little verification first
                        //parse the data into an object
                        string rep = "", contact = "", loc = "", deployed = "";

                        try
                        {

                            rep = data.replacement;
                            contact = data.contact;
                            loc = data.location;
                            deployed = data.deployedBy;
                        }
                        catch (Exception ex)
                        { 
                            Trace(ex.Message); 
                        }


                        //are both the id and the data valid systems?
                        DirectoryEntry old = adm.FindObject("(&(objectClass=computer)(name=" + id + "))");
                        DirectoryEntry replacement = adm.FindObject("(&(objectClass=computer)(name=" + data.replacement + "))");

                        try
                        {
                            Trace(old.Name.Substring(3).ToLower() + " replaced by " + replacement.Name.Substring(3).ToLower());
                            if (old.Name.Substring(3).ToLower() != id.ToLower() || replacement.Name.Substring(3).ToLower() != rep.ToLower())
                            {
                                Trace("Invalid hostname passed");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace("Hostname not found passed: " + ex.Message);
                            break;
                        }


                        //adding comptuer to the replacement list
                        try
                        {
                            adm.AddGroupMember(replaceGroup, id);
                        }
                        catch (Exception ex)
                        {
                            Trace("Error adding: " + ex.Message);
                        }

                        //remove the new computer from the available computers group
                        try
                        {
                            adm.RemoveGroupMember(availableGroup, replacement);
                        }
                        catch (Exception ex)
                        {
                            Trace("Error removing: " + ex.Message);
                        }

                        //set attributes on replacement computer.  check to see if the old one is listed as "*OFF*"
                        try
                        {
                            Dictionary<string,string> props = new Dictionary<string, string>();
                            props.Add("description", contact);
                            props.Add("location", loc);
                            try { props.Add("department", old.Properties["department"].Value.ToString()); }
                            catch { }
                            try { props.Add("info", old.Properties["description"].Value.ToString()); }
                            catch { }
                            props.Add("comment", "Deployment scheduled by: " + deployed + " on " + DateTime.Now.ToShortDateString());

                            try
                            {
                                if (old.Properties["Location"].Value.ToString().Contains("off"))
                                {
                                    props.Add("mobile", "yes");
                                }
                            }
                            catch { }

                            adm.UpdateObject(replacement, props);
                        }
                        catch (Exception ex)
                        {
                            Trace("Properties not updated: " + ex.Message);
                        }

                        //update the comments on the old computer
                        try
                        {
                            Dictionary<string, string> props = new Dictionary<string, string>();
                            props.Add("info", "Replace with: " + rep);
                            props.Add("comment", "Replacement scheduled by: " + deployed + " on " + DateTime.Now.ToShortDateString());

                            adm.UpdateObject(old, props);
                        }
                        catch { }

                        try
                        {
                            //add new computer to the same groups the old computer was in
                            foreach (DirectoryEntry group in adm.FindMemberOf(old))
                            {

                                //skip the COM Firewall Lockdown group - that one is just intended for old systems.
                                if (group.Properties["distinguishedName"].Value.ToString().Contains("OU=Medicine") && !group.Properties["distinguishedName"].Value.ToString().Contains("CN=COM Firewall Lockdown"))
                                {
                                    adm.AddGroupMember(group, replacement);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace("Error adding group: " + ex.Message);
                        }


                        //move the new computer to the right OU
                        try
                        {
                            string ou = old.Properties["distinguishedName"].Value.ToString().Replace(old.Name + ",", "");
                            adm.MoveObject(replacement, ou);
                        }
                        catch (Exception ex)
                        {
                            Trace("Error moving object: " + ex.Message);
                        }

                        LogAction(deployed, "Replacement", id, id + " replacement configured as " + rep);
                    }
                    catch (Exception ex2)
                    {
                        Trace(ex2.Message);
                    }

                    break;
                case "Complete":
                    //what to do when a computer is marked as complete.
                    if (body.key != 1592648)
                    {
                        Trace("Invalid key passed for Complete: " + body.key);
                        break;
                    }

                    try
                    {
                        //do a little verification first
                        //parse the data into an object
                        string type = "", rep = "", contact = "", tech = "", desc = "";

                        try
                        {
                            type = data.type;
                            tech = data.tech;
                            rep = data.replacement;
                            contact = data.contact;
                            desc = data.description;
                        }
                        catch (Exception ex)
                        {
                            Trace("Error setting variables: " + ex.Message);
                        }

                        //get the ad object for the computer
                        DirectoryEntry old = adm.FindObject("(&(objectClass=computer)(name=" + id + "))");


                        switch (type)
                        {
                            //if it's an in-place ugprade, remove it from the group, call it a day
                            case "In-Place Upgrade":
                                try
                                {
                                    adm.RemoveGroupMember(upgradeGroup, old);
                                    LogAction(tech.Split('@')[0], "In-Place Upgrade", id, id + " OS upgraded");
                                }
                                catch { }
                                break;
                            case "Replacement":
                                //is the replacement a valid system?
                                DirectoryEntry replacement = adm.FindObject("(&(objectClass=computer)(name=" + rep + "))");

                                try
                                {
                                    if (old.Name.Substring(3).ToLower() != id.ToLower() || replacement.Name.Substring(3).ToLower() != rep.ToLower())
                                    {
                                        Trace("Invalid hostname passed");
                                        break;
                                    }

                                    try
                                    {
                                        //disable the old computer but leave it in place
                                        adm.SetEnabled(old, false);

                                        //update the attributes
                                        Dictionary<string, string> props = new Dictionary<string, string>();
                                        props.Add("info", "Replaced with: " + rep + "\nOn: " + DateTime.Now.ToShortDateString() + "\nBy: " + tech.Split('@')[0]);
                                        props.Add("comment", "");

                                        adm.UpdateObject(old, props);

                                        LogAction(tech.Split('@')[0], "Replacement", id, id + " replaced by " + rep);
                                        Trace("Complete: " + old.Name.Substring(3).ToLower() + " replaced by " + replacement.Name.Substring(3).ToLower());
                                    }
                                    catch (Exception ex)
                                    {
                                        Trace(ex.Message);
                                    }

                                }
                                catch (Exception ex)
                                {
                                    Trace("Hostname not found passed: " + ex.Message);
                                    break;
                                }
                                break;
                            //if it's anything else, nothing really to do
                            default:
                                break;
                        }
                    }
                    catch { }

                    break;
                case "Surplus":
                    if (body.key != 147852)
                    {
                        Trace("Invalid key passed for Complete: " + body.key);
                        break;
                    }

                    Trace("Surplus: " + id + " by " + data.by);

                    //set the expiration on the computer object
                    try
                    {
                        string by = data.by;

                        if (by.Contains("@"))
                        {
                            by = by.Split('@')[0].ToLower();
                        }

                        if (by.Contains("("))
                        {
                            by = by.Split('(')[0].ToLower().Replace(")", "");
                        }

                        //disable the old computer
                        adm.SetEnabled(comp, false);

                        //move it to the surplus container
                        adm.MoveObject(comp, surplusContainer);

                        //update the attributes
                        Dictionary<string, string> props = new Dictionary<string, string>();
                        props.Add("info", "Surplus On: " + DateTime.Now.ToShortDateString() + "\nBy: " + by);
                        props.Add("comment", "");
                        //properties.Add("accountExpires", DateTime.Now.AddDays(30).ToShortDateString());

                        adm.UpdateObject(comp, props);


                        //add event log
                        LogAction(by, "Surplus", id, id + " marked for surplus.");
                        Trace("Complete: " + id + " surplused");
                    }
                    catch (Exception ex)
                    {
                        Trace(ex.Message);
                    }


                    

                    break;

                //disable will udpate description with details, disable the computer, move it to the disabled objects container, set expiration for 6 months
                case "Disable":
                    if (body.key != 654987321)
                    {
                        Trace("Invalid key passed for Complete: " + body.key);
                        break;
                    }

                    Trace("Disable: " + id + " by " + data.by);

                    //set the expiration on the computer object
                    try
                    {
                        string by = data.by;

                        if (by.Contains("@"))
                        {
                            by = by.Split('@')[0].ToLower();
                        }

                        if (by.Contains("("))
                        {
                            by = by.Split('(')[0].ToLower().Replace(")", "");
                        }

                        //disable the old computer
                        adm.SetEnabled(comp, false);

                        //move it to the surplus container
                        adm.MoveObject(comp, disabledContainer);

                        //update the attributes
                        Dictionary<string, string> props = new Dictionary<string, string>();
                        props.Add("info", "Disabled On: " + DateTime.Now.ToShortDateString() + "\nBy: " + by);
                        props.Add("comment", "");
                        //properties.Add("accountExpires", DateTime.Now.AddDays(30).ToShortDateString());

                        adm.UpdateObject(comp, props);


                        //add event log
                        LogAction(by, "Disabled", id, id + " disabled.");
                        Trace("Complete: " + id + " disabled");
                    }
                    catch (Exception ex)
                    {
                        Trace(ex.Message);
                    }

                    break;
                default:
                    break;

            }

        }

        // DELETE: api/Actions/5
        public void Delete(int id)
        {
        }

        public struct ActionItem
        {
            public string host, action;
            public object data;
            public int key;
        }

        public struct ReplacementData
        {
            public string host, user, location;
        }

        private void LogAction(string who, string action, string affectedObjects, string info)
        {
            Models.JCHIEntities ef = new JCHIEntities();

            EventLog el = new EventLog
            {
                Account = who,
                Action = action,
                Controller = "Flow",
                AffectedObjects = affectedObjects,
                Info = info,
                TS = DateTime.Now
            };

            try
            {
                ef.EventLogs.Add(el);
                ef.SaveChanges();
            }
            catch (Exception ex)
            {
                Trace("Could not save EventLog: " + ex.Message);
            }
        }
    }
}
