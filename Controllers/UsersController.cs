using Microsoft.Ajax.Utilities;
using COMAPI.Models;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Mvc;

namespace COMAPI.Controllers
{
    public class UsersController : BaseController
    {
        private JCHIEntities db = new JCHIEntities();
        public User Get(string id)
        {
            ActiveDirectoryModel adm = new ActiveDirectoryModel();
            if (id.Contains('@')) { id = id.Split('@')[0]; }
            DirectoryEntry deUser = adm.FindUser(id);

            if (deUser == null)
            {
                throw new Exception("User not found!");
            }

            User user;

            user.username = deUser.Properties["sAMAccountName"].Value.ToString();
            user.displayName = deUser.Properties["displayName"].Value.ToString();
            user.department = deUser.Properties["department"].Value.ToString();
            user.manager = "";
            try { user.manager = adm.FindUser(deUser.Properties["manager"].Value.ToString().Split(',')[0]).Properties["displayName"].Value.ToString(); }
            catch { }
            user.username = deUser.Properties["sAMAccountName"].Value.ToString();

            //string dn = deUser.Properties["departmentNumber"].Value.ToString();
            //going up the list of departments.
            user.departmentParents = "";
            user.departmentList = new List<string>();

            //adm = new ActiveDirectoryModel("OU=60004103,OU=60000292,OU=60000264,OU=60000531,OU=CMC");

            try
            {
                Trace("Query: &(cn=Orgs-*)(member=" + deUser.Properties["distinguishedName"].Value.ToString() + ")");
                SearchResultCollection dptGroups = adm.FindGroupList("&(cn=Orgs-*)(member=" + deUser.Properties["distinguishedName"].Value.ToString() + ")");

                user.departmentParents = dptGroups.Count.ToString();
                string tmp = "";

                foreach (SearchResult sr in dptGroups)
                {
                    tmp = sr.GetDirectoryEntry().Properties["distinguishedName"].Value.ToString();
                    tmp = tmp.Substring(tmp.IndexOf(',') + 1);
                    tmp = tmp.Substring(0, tmp.IndexOf(",OU=CMC"));
                    user.departmentParents += " " + tmp.Replace("OU=", "").Replace(",", "|");
                    user.departmentList.Add(sr.GetDirectoryEntry().Name.Replace("CN=Orgs-", "").Replace("-Members", "").Replace("-Affiliates", "") + "|" + sr.GetDirectoryEntry().Properties["Description"].Value.ToString().Replace("-Members", "").Replace("-Affiliates", ""));
                }


                //foreach (string dptNum in user.departmentParents.Split('|'))
                //{
                //    try
                //    {
                //        //Trace("Query: &(objectClass=Group)(cn=Orgs-" + dptNum.Replace("OU=", "") + "-Members)");
                //        //user.departmentRollup.Add(int.Parse(dptNum.Replace("OU=", "")), adm.FindObject("(&(objectClass=Group)(cn=Orgs-" + dptNum.Replace("OU=", "") + "-Members))").Properties["Description"].Value.ToString().Replace("-Members", ""));
                //        user.departmentList.Add(dptNum + "|" + adm.FindObject("(&(objectClass=Group)(cn=Orgs-" + dptNum + "-Members))").Properties["Description"].Value.ToString().Replace("-Members", ""));
                //    }
                //    catch (Exception ex)
                //    {
                //        Trace("Query: (&(objectClass=Group)(cn=Orgs-" + dptNum + "-Members))");
                //        Trace(ex.Message);
                //    }
                //}


            }
            catch (Exception ex)
            {
                Trace(ex.Message);
            }

            user.directReports = null;

            //foreach(SearchResult sr in adm.FindUserCollection("(&(objectClass=User)(manager=" + deUser.Properties["distinguishedName"].Value.ToString() + "))"))
            //{

            //}

            user.computers = null;

            

            return user;
        }

       

    }

    public struct User
    {
        public string username, displayName, department, manager, departmentParents;
        public string[] directReports;
        public List<string> departmentList;
        public List<string> computers;
    }

}
