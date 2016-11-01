using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Security.AccessControl;
using System.Collections.ObjectModel;
using System.Reflection;

namespace PowerShell.Providers
{
    public class RemoteDriveProxyParameters
    {
        public RemoteDriveProxyParameters() { }
        [Parameter(Mandatory = true)]
        public System.Management.Automation.Runspaces.PSSession Session { get; set; }
        //[Parameter(Mandatory = true)]
        //public String RemoteDriveName { get; set; }
    }

    public class RemoteDriveProxyDriveInfo : PSDriveInfo
    {
        RemoteDriveProxyProvider provider;
        public RemoteDriveProxyDriveInfo(PSDriveInfo driveInfo, RemoteDriveProxyParameters parameters) : base(driveInfo)
        {
            provider = new RemoteDriveProxyProvider();
            provider.RemoteDriveName = driveInfo.Root;
            provider.LocalDriveName = driveInfo.Name;
            provider.Session = parameters.Session;
        }

        public RemoteDriveProxyProvider RemoteDriveProxyProvider
        {
            get { return this.provider; }
        }
    }

    [CmdletProvider("RemoteDriveProxy", ProviderCapabilities.None)]
    public class RemoteDriveProxyProvider : NavigationCmdletProvider, ISecurityDescriptorCmdletProvider, IPropertyCmdletProvider
    {
        private System.Management.Automation.Runspaces.PSSession aSession;
        public System.Management.Automation.Runspaces.PSSession Session {
            get { return aSession; }
            set
            {
                aSession = value;
                ps.Runspace = aSession.Runspace;
            }
        }
        public String RemoteDriveName { get; set; }
        public String LocalDriveName { get; set; }

        private System.Management.Automation.PowerShell ps;

        public RemoteDriveProxyProvider() {
             ps = System.Management.Automation.PowerShell.Create();
        }

        /// <summary>
        /// The Windows PowerShell engine calls this method when the New-Drive 
        /// cmdlet is run. This provider proxies command calls to the specified
        /// Drive (Root) through the PSSession object passed to it.
        /// </summary>
        /// <param name="drive">
        /// Information describing the drive to create.
        /// </param>
        /// <returns>An object that describes the new drive.</returns>
        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            var driveParams = this.DynamicParameters as RemoteDriveProxyParameters;
            return new RemoteDriveProxyDriveInfo(drive, driveParams);
        }

        protected override object NewDriveDynamicParameters()
        {
            return new RemoteDriveProxyParameters();
        }

        // Remove-Drive
        protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
        {
            return base.RemoveDrive(drive);
        }

        protected override void ClearItem(string path)
        {
            base.ClearItem(path);
        }

        protected override void CopyItem(string path, string copyPath, bool recurse)
        {
            base.CopyItem(path, copyPath, recurse);
        }

        protected override string[] ExpandPath(string path)
        {
            return base.ExpandPath(path);
        }

        protected override void GetChildItems(string path, bool recurse)
        {
            ps.Commands.Clear();
            WriteVerbose("GetChildItems - Path: " + path);
            System.Diagnostics.Debug.WriteLine("GetChildItems - Path: " + path);
            ps.AddScript(String.Format("Get-ChildItem -Path '{0}' -Recurse:${1}", path, recurse));
            foreach (var obj in ps.Invoke())
            {
                WriteItemObject(obj, obj.Properties["PSPath"].Value as String, (bool)obj.Properties["PSIsContainer"].Value);
            }
        }

        protected override string GetChildName(string path)
        {
            return base.GetChildName(path);
        }

        protected override void GetChildNames(string path, ReturnContainers returnContainers)
        {
            ps.Commands.Clear();
            WriteVerbose("GetChildNames - Path: " + path);
            ps.AddScript(String.Format("Get-ChildItems -Path '{0}' | select Name,PSPath,PSIsContainer", path));
            foreach (var obj in ps.Invoke())
            {
                WriteItemObject(obj.Properties["Name"].Value, obj.Properties["PSPath"].Value as String, (bool)obj.Properties["PSIsContainer"].Value);
            }
        }

        protected override void GetItem(string path)
        {
            ps.Commands.Clear();
            ps.AddScript("Write-Verbose Path: " + path);
            ps.AddScript(String.Format("Get-Item -Path '{0}'", path));
            foreach (var obj in ps.Invoke())
            {
                WriteItemObject(obj, obj.Properties["PSPath"].Value as String, (bool)obj.Properties["PSIsContainer"].Value);
            }
        }

        protected override string GetParentPath(string path, string root)
        {
            return base.GetParentPath(path, root);
        }

        protected override bool HasChildItems(string path)
        {
            ps.Commands.Clear();
            ps.AddScript(String.Format("(Get-ChildItem -Path '{0}').Count", path));
            foreach (var obj in ps.Invoke())
            {
                if (((int)obj.BaseObject) == 0) {
                    return false;
                }
            }
            return true;
        }

        protected override void InvokeDefaultAction(string path)
        {
            base.InvokeDefaultAction(path);
        }

        // Corresponds to Test-Path
        protected override bool IsValidPath(string path)
        {
            ps.Commands.Clear();
            //WriteVerbose(String.Format("Path: {0} replaced with {1}",path, path.Replace(this.LocalDriveName, this.RemoteDriveName)));
            ps.AddScript(String.Format("Test-Path -Path '{0}'", path));
            return (bool)(ps.Invoke()[0].BaseObject);
        }

        protected override bool IsItemContainer(string path)
        {
            ps.Commands.Clear();
            ps.AddScript(String.Format("(get-item '{0}').PSIsContainer", path));
            return (bool)(ps.Invoke()[0].BaseObject);
        }

        protected override bool ItemExists(string path)
        {
            return this.IsValidPath(path);
        }

        protected override string MakePath(string parent, string child)
        {
            return base.MakePath(parent, child);
        }

        protected override void MoveItem(string path, string destination)
        {
            base.MoveItem(path, destination);
        }

        protected override void NewItem(string path, string itemTypeName, object newItemValue)
        {
            base.NewItem(path, itemTypeName, newItemValue);
        }

        protected override void RemoveItem(string path, bool recurse)
        {
            base.RemoveItem(path, recurse);
        }

        protected override void RenameItem(string path, string newName)
        {
            base.RenameItem(path, newName);
        }

        protected override void SetItem(string path, object value)
        {
            base.SetItem(path, value);
        }

        // Get-Acl
        public void GetSecurityDescriptor(string path, AccessControlSections includeSections)
        {
            ps.Commands.Clear();
            String getAuditStr = "";
            if (includeSections == AccessControlSections.All)
            {
                getAuditStr = "-Audit";
            }
            ps.AddScript(String.Format("Get-Acl '{0}' {1}", path, getAuditStr));
            foreach (var obj in ps.Invoke())
            {
                WriteItemObject(obj, obj.Properties["PSPath"].Value as String, false);
            }
        }

        // Set-Acl
        public void SetSecurityDescriptor(string path, ObjectSecurity securityDescriptor)
        {
            ps.Commands.Clear();
            String variableName = Guid.NewGuid().ToString("n");
            ps.Runspace.SessionStateProxy.SetVariable(variableName, securityDescriptor);
            ps.AddScript(String.Format("Set-Acl -Path '{0}' -AclObject ${1}", path, variableName));
            ps.AddScript(String.Format("${0} = $null", variableName));
            ps.AddScript(String.Format("Remove-Variable {0}", variableName));
            ps.Invoke();
        }


        public ObjectSecurity NewSecurityDescriptorFromPath(string path, AccessControlSections includeSections)
        {
            return aSession.Runspace.SessionStateProxy.InvokeProvider.SecurityDescriptor.NewFromPath(path, includeSections);
        }

        public ObjectSecurity NewSecurityDescriptorOfType(string type, AccessControlSections includeSections)
        {
            throw new NotImplementedException();
        }

        // Get-ItemProperty
        public void GetProperty(string path, Collection<string> providerSpecificPickList)
        {
            ps.Commands.Clear();
            // Can't figure out how to get the GetPropertyDynamicParameters method to work properly,
            // so we are catching when nothing is passed, so we can return all.
            String variableName = Guid.NewGuid().ToString("n");
            ps.Runspace.SessionStateProxy.SetVariable(variableName, providerSpecificPickList);
            if (providerSpecificPickList.Count == 0)
            {
                ps.AddScript(String.Format("${0} = (Get-ItemProperty -Path '{1}' | gm -MemberType *Property | select -Expand Name)", variableName, path));
            }
            ps.AddScript(String.Format("Get-ItemProperty -Path '{0}' -name ${1}", path, variableName));
            ps.AddScript(String.Format("${0} = $null", variableName));
            ps.AddScript(String.Format("Remove-Variable {0}", variableName));
            foreach (var obj in ps.Invoke())
            {
                WriteItemObject(obj, obj.Properties["PSPath"].Value as String, false);
            }
        }

        public object GetPropertyDynamicParameters(string path, Collection<string> providerSpecificPickList)
        {
            ps.Commands.Clear();
            ps.AddScript(String.Format("Get-ItemProperty '{0}'", path));
            return ps.Invoke();
        }

        public void SetProperty(string path, PSObject propertyValue)
        {
            throw new NotImplementedException();
        }

        public object SetPropertyDynamicParameters(string path, PSObject propertyValue)
        {
            throw new NotImplementedException();
        }

        public void ClearProperty(string path, Collection<string> propertyToClear)
        {
            throw new NotImplementedException();
        }

        public object ClearPropertyDynamicParameters(string path, Collection<string> propertyToClear)
        {
            throw new NotImplementedException();
        }
    }
}
