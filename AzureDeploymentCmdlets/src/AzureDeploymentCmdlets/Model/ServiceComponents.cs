﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AzureDeploymentCmdlets.ServiceDefinitionSchema;
using AzureDeploymentCmdlets.ServiceConfigurationSchema;
using AzureDeploymentCmdlets.Utilities;
using System.Reflection;
using AzureDeploymentCmdlets.Properties;
using System.IO;

namespace AzureDeploymentCmdlets.Model
{
    public class ServiceComponents
    {
        public ServiceDefinition Definition { get; private set; }
        public ServiceConfiguration CloudConfig { get; private set; }
        public ServiceConfiguration LocalConfig { get; private set; }
        public ServiceSettings Settings { get; private set; }

        public ServiceComponents(ServicePathInfo paths)
        {
            LoadComponents(paths);
        }

        private void LoadComponents(ServicePathInfo paths)
        {
            Validate.ValidateNullArgument(paths, string.Format(Resources.NullObjectMessage, "paths"));
            Validate.ValidateFileFull(paths.CloudConfiguration, Resources.ServiceConfiguration);
            Validate.ValidateFileFull(paths.LocalConfiguration, Resources.ServiceConfiguration);
            Validate.ValidateFileFull(paths.Definition, Resources.ServiceDefinition);
            Validate.ValidateFileFull(paths.Settings, Resources.ServiceSettings);

            Definition = General.DeserializeXmlFile<ServiceDefinition>(paths.Definition);
            CloudConfig = General.DeserializeXmlFile<ServiceConfiguration>(paths.CloudConfiguration);
            LocalConfig = General.DeserializeXmlFile<ServiceConfiguration>(paths.LocalConfiguration);
            Settings = ServiceSettings.Load(paths.Settings);
        }

        public void Save(ServicePathInfo paths)
        {
            if (paths == null) throw new ArgumentNullException("paths");
            // Validate directory exists and it's valid

            General.SerializeXmlFile<ServiceDefinition>(Definition, paths.Definition);
            General.SerializeXmlFile<ServiceConfiguration>(CloudConfig, paths.CloudConfiguration);
            General.SerializeXmlFile<ServiceConfiguration>(LocalConfig, paths.LocalConfiguration);
            Settings.Save(paths.Settings);
        }

        public void SetRoleInstances(string roleName, int instances)
        {
            Validate.ValidateStringIsNullOrEmpty(roleName, Resources.RoleName);
            if (instances <= 0 || instances > int.Parse(Resources.RoleMaxInstances))
            {
                throw new ArgumentException(string.Format(Resources.InvalidInstancesCount, roleName));
            }

            if (!RoleExists(roleName))
            {
                throw new ArgumentException(string.Format(Resources.RoleNotFoundMessage, roleName));
            }

            CloudConfig.Role.First<RoleSettings>(r => r.name.Equals(roleName)).Instances.count = instances;
            LocalConfig.Role.First<RoleSettings>(r => r.name.Equals(roleName)).Instances.count = instances;
        }

        public int GetNextPort()
        {
            if (Definition.WebRole == null && Definition.WorkerRole == null)
            {
                // First role will have port #80
                //
                return int.Parse(Resources.DefaultWebPort);
            }
            else
            {
                int maxWeb = 0;
                int maxWorker = 0;

                if (Definition.WebRole != null)
                {
                    maxWeb = Definition.WebRole.Max(wr => wr.Endpoints.InputEndpoint.Max(ie => ie.port));
                }

                if (Definition.WorkerRole != null)
                {
                    maxWorker = Definition.WorkerRole.Max(wr => wr.Endpoints.InputEndpoint.Max(ie => ie.port));
                }

                int maxPort = Math.Max(maxWeb, maxWorker);

                if (maxPort == int.Parse(Resources.DefaultWebPort))
                {
                    // This is second role to be added
                    //
                    return int.Parse(Resources.DefaultPort);
                }
                else
                {
                    // Increase max port and return it
                    //
                    return (maxPort + 1);
                }
            }
        }

        public void AddRoleToConfiguration(RoleSettings role, DevEnv env)
        {
            Validate.ValidateNullArgument(role, string.Format(Resources.NullRoleSettingsMessage, "ServiceConfiguration"));

            ServiceConfiguration config = (env == DevEnv.Cloud) ? CloudConfig : LocalConfig;

            if (config.Role == null)
            {
                config.Role = new RoleSettings[] { role };
            }
            else
            {
                config.Role = config.Role.Concat(new RoleSettings[] { role }).ToArray();
            }
        }

        /// <summary>
        /// Determines if a specified role exists in service components (*.csdef, local and cloud *cscfg) or not.
        /// </summary>
        /// <param name="roleName">Role name</param>
        /// <returns>bool value indicating whether this role is found or not </returns>
        public bool RoleExists(string roleName)
        {
            // If any one of these fields doesn't have elements then this means no roles added at all or
            // there's inconsistency between service components.
            //
            if ((Definition.WebRole == null && Definition.WorkerRole == null) || CloudConfig.Role == null || LocalConfig.Role == null)
                return false;
            else
            {
                return
                    (Definition.WebRole.Any<WebRole>(wr => wr.name.Equals(roleName)) || Definition.WorkerRole.Any<WorkerRole>(wr => wr.name.Equals(roleName))) &&
                    CloudConfig.Role.Any<RoleSettings>(rs => rs.name.Equals(roleName)) &&
                    LocalConfig.Role.Any<RoleSettings>(rs => rs.name.Equals(roleName));
            }
        }

        /// <summary>
        /// Validates if given role name is valid or not
        /// </summary>
        /// <param name="roleName">Role name to be checked</param>
        /// <returns></returns>
        /// <remarks>This method doesn't check if the role exists in service components or not. To check for role existence use RoleExists</remarks>
        public static bool ValidRoleName(string roleName)
        {
            try
            {
                Validate.ValidateFileName(roleName);
                Validate.HasWhiteCharacter(roleName);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}