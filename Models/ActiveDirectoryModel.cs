using System;
using System.Collections.Generic;
using System.Linq;
using System.DirectoryServices;
using System.Web;
// test
namespace COMAPI.Models
{
    public class ActiveDirectoryModel : BaseModel
    {
        public DirectoryEntry root = new DirectoryEntry();
        System.Guid rootGuid;
        public string domainPath = "";

        readonly bool actionsEnabled = int.Parse(System.Web.Configuration.WebConfigurationManager.AppSettings["isActionEnabled"]) == 1;

        public ActiveDirectoryModel()
        {
            Dictionary<string, string> con = ParseConnectionString("AD");
            domainPath = "DC=" + con["domain"].Replace(".", ",DC=");
            root.Path = "LDAP://" + con["domain"] + "/" + domainPath;
            root.Username = null; // con["username"];
            root.Password = null; //con["password"];
            root.AuthenticationType = AuthenticationTypes.Secure;
            rootGuid = root.Guid;
        }

        public ActiveDirectoryModel(string baseOU)
        {
            Dictionary<string, string> con = ParseConnectionString("AD");
            domainPath = "DC=" + con["domain"].Replace(".", ",DC=");
            root.Path = "LDAP://" + con["domain"] + "/" + baseOU + "," + domainPath;
            root.Username = null; // con["username"];
            root.Password = null; //con["password"];
            root.AuthenticationType = AuthenticationTypes.Secure;
            rootGuid = root.Guid;
        }

        #region Find Users & Objects
        //find user
        public DirectoryEntry FindUser(string filter)
        {
            if (filter.Contains('='))
            {
                try { return FindObject("(&(objectClass=user)(" + filter.Trim() + "))"); }
                catch { return null; }
            }
            else if (filter.Contains('@'))
            {
                try { return FindObject("(&(objectClass=user)(cn=" + filter.Split('@')[0].Trim() + "))"); }
                catch { return null; }
            }
            else
            {
                try { return FindObject("(&(objectClass=user)(cn=" + filter.Trim() + "))"); }
                catch { return null; }
            }
        }

        public DirectoryEntry FindComputer(string name)
        {
            DirectorySearcher ds = new DirectorySearcher(root);
            ds.Filter = "cn=" + name;
            ds.SearchScope = SearchScope.Subtree;
            try { return ds.FindOne().GetDirectoryEntry(); }
            catch
            {
                return null;
            }
        }

        //find object
        public DirectoryEntry FindObject(string filter)
        {
            //if (!filter.Contains('=')) { filter = "cn=" + filter; }
            DirectorySearcher ds = new DirectorySearcher(root);
            ds.Filter = filter;
            ds.SearchScope = SearchScope.Subtree;
            try { return ds.FindOne().GetDirectoryEntry(); }
            catch
            {
                return null;
            }
        }

        //find all users (w/ filter)
        public SearchResultCollection FindUserCollection(string filter)
        {
            DirectorySearcher ds = new DirectorySearcher(root);
            try
            {
                if (filter.Length > 0) { ds.Filter = "(&(objectClass=user)(" + filter + "))"; }
                else { ds.Filter = "(&(objectClass=user)(objectCategory=Person))"; }
            }
            catch { return null; }
            ds.SearchScope = SearchScope.Subtree;
            try { return ds.FindAll(); }
            catch { return null; }
        }

        //find all objects (w/ filter)
        public SearchResultCollection FindObjectCollection(string filter)
        {
            DirectorySearcher ds = new DirectorySearcher(root);
            ds.PageSize = 3000;
            try
            {
                if (filter.Length > 0) { ds.Filter = "(" + filter + ")"; }
            }
            catch { return null; }
            ds.SearchScope = SearchScope.Subtree;
            try { return ds.FindAll(); }
            catch { return null; }
        }

        public bool ObjectExists(string filter)
        {
            DirectorySearcher ds = new DirectorySearcher(root);
            ds.Filter = filter;
            ds.SearchScope = SearchScope.Subtree;
            try
            {
                if (ds.FindAll().Count > 0) { return true; }
            }
            catch { }
            return false;
        }

        //find computer

        //find all computers (w/ filter)
        #endregion

        #region Object Creation and Updating
        public DirectoryEntry CreateObject(string name, string parentOU, string objectClass, string san = "", string description = "", string info = "", string mail = "", string location = "")
        {
            // the 'name' can be CN=name or just name
            if (name.IndexOf("CN=") != 0) { name = "CN=" + name; }

            DirectoryEntry ou = this.FindObject("distinguishedName=" + parentOU);
            //if the parent OU doesn't exist, the we should build it!
            if (ou == null)
            {
                try
                {
                    Trace("Parent (" + parentOU + ") doesn't exist for " + name);
                    string child = parentOU.Split(',')[0];
                    DirectoryEntry newOu = CreateObject(child, parentOU.Replace(child + ",", ""), "organizationalUnit");
                    ou = newOu;
                }
                catch (Exception ex)
                {
                    Trace("Parent object not created: " + ex.Message);
                }
            }

            Trace("Creating:" + name + "," + parentOU + "/" + san + "/");
            DirectoryEntry newObject = ou.Children.Add(name, objectClass);

            //if (san.Length != 0 && san.Contains("$") == false && objectClass == "computer") { san = san + "$"; }
            if (san != "") { newObject.Properties["sAMAccountName"].Value = san; }
            if (actionsEnabled) { newObject.CommitChanges(); }

            if (description != "") { newObject.Properties["description"].Value = description; }
            if (info != "") { newObject.Properties["info"].Value = info; }
            if (location != "") { newObject.Properties["location"].Value = location; }
            if (actionsEnabled)
            {
                newObject.CommitChanges();
            }

            try
            {
                if (objectClass == "computer")
                {
                    newObject.InvokeSet("userAccountControl", 4128);
                    if (actionsEnabled)
                    {
                        newObject.CommitChanges();
                    }
                }
            }
            catch (Exception ex) { Trace("Error setting userAccountControl on: " + name + " -- " + ex.Message); }

 
            Trace(name + " Created in: " + parentOU);
            return newObject;
        }

        public DirectoryEntry CreateComputer(string name, string parentOU, string description, string location)
        {
            DirectoryEntry ou = this.FindObject("distinguishedName=" + parentOU);
            //if the parent OU doesn't exist, the we should build it!
            if (ou == null)
            {
                try
                {
                    Trace("Parent (" + parentOU + ") doesn't exist for " + name);
                    string child = parentOU.Split(',')[0];
                    DirectoryEntry newOu = CreateObject(child, parentOU.Replace(child + ",", ""), "organizationalUnit");
                    ou = newOu;
                }
                catch (Exception ex)
                {
                    Trace("Parent object not created: " + ex.Message);
                }
            }
            //create teh computer object
            Trace("Creating:" + name + "," + parentOU);

            //due to some security BS, you have to create the computer in the default computers container then move it
            DirectoryEntry computerBaseOu = new DirectoryEntry("LDAP://ad.uc.edu/OU=Computers,DC=ad,DC=uc,DC=edu");

            //create the computer and set the sAMAccountname and enable it
            DirectoryEntry newObject = computerBaseOu.Children.Add("CN=" + name, "computer");
            newObject.InvokeSet("sAMAccountName", name + "$");
            newObject.InvokeSet("userAccountControl", 4128);
            newObject.CommitChanges();

            //once created, you move it to the proper container
            try
            {
                newObject.MoveTo(ou);
                newObject.CommitChanges();
            }
            catch { Trace("Couldn't move " + name + " to " + parentOU); }

            //then set the description and location if needed
            try
            {
                if (description != null) { newObject.InvokeSet("description", description); }
                if (location != null) { newObject.InvokeSet("location", location); }
                newObject.CommitChanges();

            }
            catch { Trace("Couldn't update location/description"); }

            return newObject;
        }

        public DirectoryEntry CreateUser(string sn, string givenName, string san, string parentOU, string password, string location = "", Dictionary<string, string>[] otherProperties = null)
        {

            DirectoryEntry ou = this.FindObject("distinguishedName=" + parentOU);
            //if the parent OU doesn't exist, the we should build it!
            if (ou == null)
            {
                try
                {
                    Trace("Parent (" + parentOU + ") doesn't exist for " + san);
                    string child = parentOU.Split(',')[0];
                    DirectoryEntry newOu = CreateObject(child, parentOU.Replace(child + ",", ""), "organizationalUnit");
                    ou = newOu;
                }
                catch (Exception ex)
                {
                    Trace("Parent object not created: " + ex.Message);
                }
            }

            Trace("Creating:" + san + "," + parentOU);
            DirectoryEntry newObject = ou.Children.Add("CN=" + givenName + sn, "user");
            newObject.InvokeSet("sn", sn);
            newObject.InvokeSet("givenName", givenName);
            newObject.InvokeSet("sAMAccountName", san);
            newObject.CommitChanges();


            // newObject.InvokeSet("userAccountControl", 66048);
            //            newObject.CommitChanges();

            Trace(san + " Created in: " + parentOU);
            return newObject;
        }

        public DirectoryEntry MoveObject(string o, string destination)
        {
            Models.ActiveDirectoryModel adm = new Models.ActiveDirectoryModel();
            DirectoryEntry deObject = adm.FindObject("distinguishedName=" + o);

            return MoveObject(deObject, destination);
        }

        public DirectoryEntry MoveObject(DirectoryEntry de, string destination)
        {
            Models.ActiveDirectoryModel adm = new Models.ActiveDirectoryModel();
            DirectoryEntry destOU = adm.FindObject("distinguishedName=" + destination);
            de.MoveTo(destOU);
            if (actionsEnabled)
            {
                de.CommitChanges();
            }
            Trace(de.Name + " moved to " + destination);
            return de;
        }


        //takes either the DN or CN of an object and applies the dictionary list of values to it
        public DirectoryEntry UpdateObject(string dn, Dictionary<string, string> values)
        {
            DirectoryEntry de = null;

            if (dn.StartsWith("CN=")) { de = FindObject("distinguishedName=" + dn); }
            else { de = FindObject("CN=" + dn); }

            if (de != null) { return UpdateObject(de, values); }
            else { return null; }
        }

        //takes a DE object and sets each property based on the dictionary of values
        public DirectoryEntry UpdateObject(DirectoryEntry de, Dictionary<string, string> values)
        {
            foreach (KeyValuePair<string, string> item in values)
            {
                try
                {
                    if (item.Value.Trim().Length == 0)
                    {
                        de.Properties[item.Key].Clear();
                    }
                    else
                    {
                        de.InvokeSet(item.Key, item.Value);
                    }
                    if (actionsEnabled)
                    {
                        de.CommitChanges();
                    }
                }
                catch (Exception ex)
                {
                    Trace("Failed to update " + item.Key + ":" + ex.Message);
                }
            }

            return de;
        }


        public DirectoryEntry SetEnabled(string dn, bool enable)
        {
            DirectoryEntry de = null;

            if (dn.StartsWith("CN=")) { de = FindObject("distinguishedName=" + dn); }
            else { de = FindObject("CN=" + dn); }

            if (de != null) { return SetEnabled(de, enable); }
            else { return null; }
        }


        public DirectoryEntry SetEnabled(DirectoryEntry de, bool enable)
        {
            if (enable)
            {
                try
                {
                    de.Properties["userAccountControl"][0] = 4128;
                    de.CommitChanges();
                    return de;
                }
                catch (Exception ex)
                {
                    Trace("Error enabling computer: " + ex.Message);
                    return de;
                }
            }
            int oldVal = (int)de.Properties["userAccountControl"][0];
            int disable = 2;

            if (enable) { de.Properties["userAccountControl"][0] = (oldVal & ~disable); }
            else { de.Properties["userAccountControl"][0] = (oldVal | disable); }
            if (actionsEnabled)
            {
                de.CommitChanges();
            }

            return de;
        }
        #endregion

        #region Group Routines
        //create security group
        public bool CreateGroup(string groupName, string san, string parentOU, string desc = "", string info = "", string mail = "", string managedBy = "")
        {
            //try
            //{
            if (san == "" || san == null) { san = groupName.ToLower().Replace(" ", ""); }
            if (!groupName.Contains("CN=")) { groupName = "CN=" + groupName; }
            DirectoryEntry find = FindObject("distinguishedName=" + groupName + "," + parentOU);

            if (find != null) { return false; }
            DirectoryEntry de = CreateObject(groupName, parentOU, "group", san, desc, info, mail, "");
            try { if (managedBy != "") { de.InvokeSet("managedBy", managedBy); } }
            catch { }

            Trace("Group Created: " + groupName + " in " + parentOU);

            return true;
            //}
            //catch
            //{
            //    return false;
            //}
        }

        //list groups object is member of 
        public List<DirectoryEntry> FindMemberOf(string DN)
        {
            return FindMemberOf(FindUser("(distinguishedName=" + DN + ")"));
        }

        //list groups DirectoryEntry is a member of
        public List<DirectoryEntry> FindMemberOf(DirectoryEntry Entry)
        {
            List<DirectoryEntry> ol = new List<DirectoryEntry>();

            try
            {
                PropertyCollection colProperties = Entry.Properties;
                PropertyValueCollection colPropertyValues = colProperties["memberOf"];
                foreach (string strGroup in colPropertyValues)
                {
                    try
                    {
                        DirectoryEntry de = FindObject("distinguishedName=" + strGroup);
                        //                string path = strGroup.Substring(0, strGroup.IndexOf(",DC="));
                        //                string cn = strGroup.Substring(0, strGroup.IndexOf(','));
                        ol.Add(de);
                    }
                    catch { }
                }
            }
            catch { }
            return ol;
        }

        //find all groups
        public SearchResultCollection FindGroupList(string filter, string startingOU = "")
        {
            DirectorySearcher ds;
            if (filter.Length > 0) { filter = "(&(objectClass=group)(" + filter + "))"; }
            else { filter = "(objectClass=group)"; }

            if (startingOU != "")
            {
                root.Path = root.Path.Replace(domainPath, startingOU + "," + domainPath);
                ds = new DirectorySearcher(root, filter);
            }
            else
            {
                ds = new DirectorySearcher(root, filter);
            }

            ds.SearchScope = SearchScope.Subtree;
            ds.PageSize = 5000;
            return ds.FindAll();
        }

        //find group members
        public SearchResultCollection FindGroupMembers(string group)
        {

            DirectorySearcher ds = new DirectorySearcher(root);
            ds.SearchScope = SearchScope.Subtree;
            //if it's a DN, we can just go from that
            if (group.Contains("CN="))
            {
                ds.Filter = "(&(|(objectClass=user)(objectClass=computer)(objectClass=group))(memberof=" + group + "))";
            }
            //if not, we need to find the DN
            else
            {
                ds.Filter = "(&(objectClass=group)(|(name=" + group + ")(sAMAccountName=" + group + ")))";
                ds.Filter = "(&(|(objectClass=user)(objectClass=computer)(objectClass=group))(memberof=" + ds.FindOne().GetDirectoryEntry().Properties["distinguishedName"].Value + "))";
            }

            return ds.FindAll();
        }

        //update group members

        //append group members
        int temp;
        public void AddGroupMember(string groupDN, string member)
        {
            DirectoryEntry deGroup = FindObject("(|(distinguishedName=" + groupDN + ")(cn=" + groupDN + "))");               //group to add to
            DirectoryEntry deObject = null;                                     //object to add

            if (deGroup == null)
            {
                throw new Exception("Group object not found");
            }

            //deGroup.Properties["member"].Add(user);
            if (member.Contains("CN="))
            {
                deGroup.Properties["member"].Add(member);
            }
            else
            {
                //if the "user" field is an email address, search for it by mail
                if (member.Contains("@") && member.Contains("."))
                {
                    deObject = FindObject("mail=" + member);
                }
                //if the "user" field is passed as an M number, check for that
                else if (int.TryParse(member.Substring(1), out temp))
                {
                    deObject = FindObject("uceduUCID=" + member);
                }
                //at this point, just seach the samaccountname or CN for the passed username
                else
                {
                    deObject = FindObject("sAMAccountName=" + member);
                    if (deObject == null) { deObject = FindObject("cn=" + member); }
                    if (deObject == null) { deObject = FindObject("sAMAccountName=" + member + "$"); }
                }

                if (deObject == null)
                {
                    throw new Exception("Member object not found");
                }

                if (deGroup.Properties["member"].Contains(deObject.Properties["distinguishedName"].Value.ToString()))
                {
                    throw new Exception("already in group");
                }
                deGroup.Properties["member"].Add(deObject.Properties["distinguishedName"].Value.ToString());
            }

            deGroup.CommitChanges();
            Trace("Added " + deObject.Name + " to " + groupDN);

        }

        public void AddGroupMember(object group, object member)
        {
            DirectoryEntry deGroup = null, deMember = null;
            if (group.GetType() == typeof(DirectoryEntry)) { deGroup = (DirectoryEntry)group; }
            else if (group.GetType() == typeof(System.String)) { deGroup = FindObject("(&(objectClass=group)(|(distinguishedName=" + group.ToString() + ")(sAMAccountName=" + group.ToString() + ")))"); }
            else { throw new Exception("Invalid group object"); }

            if (member.GetType() == typeof(DirectoryEntry)) { deMember = (DirectoryEntry)member; }
            else if (member.GetType() == typeof(System.String))
            {
                if (member.ToString().Contains("CN="))
                {
                    deMember = FindObject("(distinguishedName=" + member.ToString() + ")");
                }
                else
                {
                    deMember = FindObject("(sAMAccountName=" + member.ToString() + ")");
                }
            }
            else { throw new Exception("Invalid member object"); }

            try
            {
                if (deGroup.Properties["member"].Contains(deMember.Properties["distinguishedName"].Value.ToString()))
                {
                    throw new Exception("already in group");
                }
                deGroup.Properties["member"].Add(deMember.Properties["distinguishedName"].Value.ToString());
                deGroup.CommitChanges();
                Trace("Added " + deMember.Name.ToString() + " to " + deGroup.Name.ToString());
            }
            catch (Exception ex)
            {
                Trace("Failed adding " + deMember.Name.ToString() + " to " + deGroup.Name.ToString() + ": " + ex.Message);
            }
        }

        //remove group member
        public void RemoveGroupMember(object group, object member)
        {
            DirectoryEntry deGroup = null, deMember = null;
            if (group.GetType() == typeof(DirectoryEntry)) { deGroup = (DirectoryEntry)group; }
            else if (group.GetType() == typeof(System.String)) { deGroup = FindObject("(&(objectClass=group)(|(distinguishedName=" + group.ToString() + ")(sAMAccountName=" + group.ToString() + ")))"); }
            else { return; }

            if (member.GetType() == typeof(DirectoryEntry)) { deMember = (DirectoryEntry)member; }
            else if (member.GetType() == typeof(System.String))
            {
                if (member.ToString().Contains("CN="))
                {
                    deMember = FindObject("(distinguishedName=" + member.ToString() + ")");
                }
                else
                {
                    deMember = FindObject("(sAMAccountName=" + member.ToString() + ")");
                }
            }
            else { return; }

            if (deGroup.Properties["member"].Contains(deMember.Properties["distinguishedName"].Value.ToString()))
            {
                deGroup.Properties["member"].Remove(member.ToString());
                deGroup.CommitChanges();
                Trace("Removed " + deMember.Name.ToString() + " from " + deGroup.Name.ToString());
            }
            else
            {
                throw new Exception("not a member of the group");
            }

            // FindGroupMembers(groupDN);
        }

        //clear group members
        public void ClearGroupMembers(object group)
        {
            DirectoryEntry deGroup;
            if (group.GetType() == typeof(DirectoryEntry)) { deGroup = (DirectoryEntry)group; }
            else if (group.GetType() == typeof(System.String)) { deGroup = FindObject("(&(objectClass=group)(|(distinguishedName=" + group.ToString() + ")(sAMAccountName=" + group.ToString() + ")))"); }
            else { return; }

            while (deGroup.Properties["member"].Count > 0)
            {
                deGroup.Properties["member"].Remove(deGroup.Properties["member"][0]);
                deGroup.CommitChanges();
            }
        }

        #endregion


        #region LocalAdminPassword

        public LocalAdminPasswordInfo GetLocalAdminPassword (string hostname, int daysToExpire, string by)
        {

        }

        public struct LocalAdminPasswordInfo
        {
            string password;
            DateTime expiration;
        }

        #endregion

    }


}