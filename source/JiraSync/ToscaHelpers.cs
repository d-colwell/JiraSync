﻿using JiraService.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tricentis.TCAddOns;
using Tricentis.TCAPIObjects;
using Tricentis.TCAPIObjects.Objects;

namespace JiraSync
{
    public class ToscaHelpers
    {
        public static string[] GetPropertyNames(string type)
        {
            TCProject proj = TCAddOn.ActiveWorkspace.GetTCProject();
            TCObjectPropertiesDefinition propDef = proj.ObjectPropertiesDefinitions.FirstOrDefault(pd => pd.Name.ToLower() == type.ToLower());
            List<string> properties = new List<string>();
            if(propDef is Tricentis.TCAPIObjects.Objects.RequirementTypeDefinition)
            {
                var reqType = typeof(Requirement);
                properties.AddRange(GetAvailablePropertiesForType(reqType));
            }
            else if(type=="Issue")
            {
                var issType = typeof(Issue);
                properties.AddRange(GetAvailablePropertiesForType(issType));
            }
            properties.AddRange(propDef.GetPropertyNames());
            return properties.ToArray();
        }

        private static string[] GetAvailablePropertiesForType(Type type)
        {
            return type.GetProperties().Where(x => x.CanWrite && (!x.MemberType.GetType().IsClass || x.MemberType.GetType().IsPrimitive)).Select(x => x.Name).ToArray();
        }
        public static TCObjectPropertiesDefinition CreateCustomProperties(String objectToCreateOn, String propertyName)
        {
            TCProject proj = TCAddOn.ActiveWorkspace.GetTCProject();
            TCObjectPropertiesDefinition propDef = proj.ObjectPropertiesDefinitions.FirstOrDefault(pd => pd.Name.ToLower() == objectToCreateOn.ToLower());

            // Check if object exist
            if (propDef != null)
            {
                String[] props = propDef.GetPropertyNames();

                // Check if property already exists, create it otherwise
                if (!props.Contains(propertyName))
                {
                    TCObjectProperty newPrp = propDef.CreateProperty();
                    newPrp.Name = propertyName;
                }
            }
            else
                throw new Exception("Invalid object to create property definition");

            return propDef;
        }

        public static TCObjectPropertiesDefinition CreateIssuesProperties(String objectToCreateOn, String propertyName)
        {
            TCProject proj = TCAddOn.ActiveWorkspace.GetTCProject();
            TCObjectPropertiesDefinition issuesDef = proj.ObjectPropertiesDefinitions.FirstOrDefault(pd => pd.Name.ToLower() == objectToCreateOn.ToLower());

            if (issuesDef == null)
            {   // Create Issues property definition.
                TCComponentFolder tempFolder = (TCComponentFolder)proj.CreateComponentFolder();
                issuesDef = tempFolder.CreatePropertyDefinition("Issue");
                proj.Move(issuesDef);
                tempFolder.Delete(MsgBoxResult_OkCancel.Ok, MsgBoxResult_YesNo.Yes);
            }

            // Check if object exist
            if (issuesDef != null)
            {
                String[] props = issuesDef.GetPropertyNames();

                // Check if property already exists, create it otherwise
                if (!props.Contains(propertyName))
                {
                    TCObjectProperty newPrp = issuesDef.CreateProperty();
                    newPrp.Name = propertyName;
                }
            }

            return issuesDef;
        }

        public static TCVirtualFolder CreateVirtualFolder(TCObject obj, String virtualFolderName, String tql)
        {
            TCVirtualFolder vf = (TCVirtualFolder)obj.Search("->subparts:TCVirtualFolder[Name==\"" + virtualFolderName + "\"]").FirstOrDefault();

            // Create virtual folder if required
            if (vf == null)
            {
                vf = (obj as TCFolder).CreateVirtualFolder();
            }

            vf.Name = virtualFolderName;
            vf.Query = tql;
            vf.RefreshVirtualFolder();

            return vf;
        }
    }

    public static class Helpers
    {
        public static JiraConfig GetJiraConfig(this OwnedItem item)
        {
            if (!item.AttachedFiles.Any(f => f.Name == Global.JiraConfigAttachmentName))
                return null;
            OwnedFile file = item.AttachedFiles.First(x => x.Name == Global.JiraConfigAttachmentName);
            string fileContent = Encoding.Default.GetString(file.EmbeddedContent.Data);
            return JsonConvert.DeserializeObject<JiraConfig>(fileContent);
        }

        public static void SaveConfig(this OwnedItem item, JiraConfig config)
        {
            string fileContent = JsonConvert.SerializeObject(config, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            if (item.AttachedFiles.Any(f => f.Name == Global.JiraConfigAttachmentName))
            {
                var previousConfig = item.AttachedFiles.First(f => f.Name == Global.JiraConfigAttachmentName);
                previousConfig.Delete(MsgBoxResult_OkCancel.Ok, MsgBoxResult_YesNo.Yes);
            }
            string tempFilePath = Environment.ExpandEnvironmentVariables($"%tricentis_projects%\\{Global.JiraConfigAttachmentName}");
            File.WriteAllText(tempFilePath, fileContent);
            item.AttachFile(tempFilePath, "Embedded");
        }
    }
}
