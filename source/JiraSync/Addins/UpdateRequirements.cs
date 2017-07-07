using JiraService.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tricentis.TCAddOns;
using Tricentis.TCAPIObjects.Objects;
using Tricentis.TCCore.CredentialManager;

namespace JiraSync.Addins
{
    public class UpdateRequirements : TCAddOnTask
    {
        public override string Name => "Update Requirements";

        public override Type ApplicableType => typeof(RequirementSet);

        public override bool RequiresChangeRights => true;

        public override TCObject Execute(TCObject requirementSet, TCAddOnTaskContext taskContext)
        {
            RequirementSet rs = (RequirementSet)requirementSet;
            JiraConfig config = rs.GetJiraConfig();
            
            #region Setup
            if (config == null)
            {
                string url = taskContext.GetStringValue("Jira Instance URL: ", false);
                string jqlValue = taskContext.GetStringValue("JQL Filter for requirements: ", false);
                config = new JiraConfig
                {
                    baseURL = url,
                    jqlFilter = jqlValue,
                    fieldMaps = new List<FieldMap>()
                {
                    new FieldMap {direction = Direction.jira_to_tosca, jiraJsonPath="$.fields.summary", toscaField="Name" }
                }
                };
                rs.SaveConfig(config);
            }
            string username, password;
            if (CredentialManager.Instance.Credentials.Any(x => x.BaseURL == config.baseURL))
            {
                Credential credential = CredentialManager.Instance.Credentials.First(x => x.BaseURL == config.baseURL);
                username = credential.Username;
                password = credential.Password;
            }
            else
            {
                username = taskContext.GetStringValue("Jira Username", false);
                password = taskContext.GetStringValue("Jira Password", true);
                CredentialManager.Instance.StoreOrUpdateCredential(new Credential
                {
                    BaseURL = config.baseURL,
                    Description = "Created by Jira Config",
                    Username = username,
                    Password = password
                });
            }
            #endregion

            var jira = new JiraService.Jira(config.baseURL, username, password);
            var issueService = jira.GetIssueService();
            String startTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            string jql = config.jqlFilter;
            JiraService.Issue.Issue[] issues = null;
            Task<JiraService.Issue.Issue[]> issueTask = null;
            try
            {
                issueTask = issueService.SearchAsync(jql);
                while (!issueTask.IsCompleted)
                {
                    taskContext.ShowStatusInfo($"Gettign issues for JQL: {jql}");
                    System.Threading.Thread.Sleep(100);
                }
                //order the issues so that subtasks are not created before parent tasks
                issues = issueTask.Result.OrderBy(x => x.fields.project.name).ThenBy(x => x.id).ToArray();
                taskContext.ShowStatusInfo("Creating Requirements");
            }
            catch (Exception e)
            {
                taskContext.ShowStatusInfo($"Error synchronising: {e.Message}");
            }
            HashSet<string> updatedItems = new HashSet<string>();
            if (issues != null)
            {
                foreach (var issue in issues)
                {
                    var req = CreateOrUpdateRequirement(rs, config, issue);
                    updatedItems.Add(req.UniqueId);
                }

                // Prompt status
                taskContext.ShowMessageBox("Jira Sync", issues.Length.ToString() + " requirements have been synchronised.");
            }
            var 
            return null;
        }

        private Requirement CreateOrUpdateRequirement(RequirementSet rs, JiraConfig config, JiraService.Issue.Issue issue)
        {
            Requirement req = FindRequirementForIssue(rs, issue.key);
            if (req == null)
                req = rs.CreateRequirement();
            //Move requirement to parent
            string parentKey = (issue.fields.parent == null ? null : issue.fields.parent.key);
            if (config.parentLocatorPropertyOverride != null)
            {
                string parentKeyOverride = issue.GetValueByPath(config.parentLocatorPropertyOverride);
                if (parentKeyOverride != null)
                    parentKey = parentKeyOverride;
            }
            if (parentKey != null)
            {
                Requirement parent = FindRequirementForIssue(rs, parentKey);
                if (parent != null)
                    parent.Move(req);
            }
            try
            {
                req.SetAttibuteValue(Global.JiraTicket, issue.key);
            }
            catch (Exception)
            {

                throw new Exception("Please prepare project for integration (available from context menu at project level) and then try again");
            }
            //req.SetAttibuteValue(Global.JiraDefectID, issue.id);
            foreach (var fieldMap in config.fieldMaps)
            {
                string jiraValue = issue.GetValueByPath(fieldMap.jiraJsonPath);
                try
                {
                    req.SetAttibuteValue(fieldMap.toscaField, jiraValue);
                }
                catch (Exception)
                {
                    //NOM NOM NOM: Tasty tasty exceptions
                }
            }
            return req;
        }


        private Requirement FindRequirementForIssue(RequirementSet rs, string issueKey)
        {
            Requirement req = rs.Search($"=>SUBPARTS:Requirement[{Global.JiraTicket}==\"{issueKey}\"]").FirstOrDefault() as Requirement;
            return req;
        }
    }
}
