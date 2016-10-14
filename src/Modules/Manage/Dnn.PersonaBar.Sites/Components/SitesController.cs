﻿#region Copyright
// 
// DotNetNuke® - http://www.dotnetnuke.com
// Copyright (c) 2002-2016
// by DotNetNuke Corporation
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion

#region Usings



#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml;
using Dnn.PersonaBar.Sites.Components.Dto;
using Dnn.PersonaBar.Sites.Services.Dto;
using DotNetNuke.Common;
using DotNetNuke.Common.Internal;
using DotNetNuke.Common.Lists;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Host;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Profile;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Entities.Tabs.TabVersions;
using DotNetNuke.Entities.Urls;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Localization;
using DotNetNuke.Services.Log.EventLog;
using DotNetNuke.Services.Mail;
using ICSharpCode.SharpZipLib.Zip;

namespace Dnn.PersonaBar.Sites.Components
{
    public class SitesController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(SitesController));

        private CultureDropDownTypes DisplayType { get; set; }

        private string IconHome
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_Home.png");
            }
        }

        private string IconPortal
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_Portal.png");
            }
        }

        private string AdminOnlyIcon
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_UserAdmin.png");
            }
        }

        private string RegisteredUsersIcon
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_User.png");
            }
        }

        private string IconPageDisabled
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_Disabled.png");
            }
        }

        private string IconPageHidden
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_Hidden.png");
            }
        }

        private string IconRedirect
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_Redirect.png");
            }
        }

        private string SecuredIcon
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_UserSecure.png");
            }
        }

        private string AllUsersIcon
        {
            get
            {
                return Globals.ResolveUrl("~/DesktopModules/Admin/Tabs/images/Icon_Everyone.png");
            }
        }

        private PortalSettings PortalSettings
        {
            get
            {
                return PortalController.Instance.GetCurrentPortalSettings();
            }
        }

        public string LocalResourcesFile
        {
            get
            {
                return Path.Combine("~/admin/Dnn.PersonaBar/App_LocalResources/Sites.resx");
            }
        }

        public IList<HttpAliasDto> FormatPortalAliases(int portalId)
        {
            var aliases = new List<HttpAliasDto>();

            var arr = PortalAliasController.Instance.GetPortalAliasesByPortalId(portalId).ToList();
            foreach (var portalAliasInfo in arr)
            {
                var httpAlias = Globals.AddHTTP(portalAliasInfo.HTTPAlias);
                var originalUrl = HttpContext.Current.Items["UrlRewrite:OriginalUrl"].ToString().ToLowerInvariant();

                httpAlias = Globals.AddPort(httpAlias, originalUrl);
                aliases.Add(new HttpAliasDto{ Url = portalAliasInfo.HTTPAlias, Link = httpAlias});
            }

            return aliases;
        }

        public string FormatExpiryDate(DateTime dateTime)
        {
            var strDate = string.Empty;
            if (!Null.IsNull(dateTime))
            {
                strDate = dateTime.ToShortDateString();
            }
            return strDate;
        }

        public IList<PortalController.PortalTemplateInfo> GetPortalTemplates()
        {
            var templates = PortalController.Instance.GetAvailablePortalTemplates();
            templates = templates.OrderBy(x => x, new TemplateDisplayComparer()).ToList();
            return templates;
        }

        public PortalController.PortalTemplateInfo GetPortalTemplate(string fileName, string cultureCode)
        {
            return PortalController.Instance.GetPortalTemplate(fileName, cultureCode);
        }

        public ListItem CreateListItem(PortalController.PortalTemplateInfo template)
        {
            string text, value;
            if (string.IsNullOrEmpty(template.CultureCode))
            {
                text = template.Name;
                value = Path.GetFileName(template.TemplateFilePath);
            }
            else
            {
                if (DisplayType == 0)
                {
                    string _ViewType = Convert.ToString(DotNetNuke.Services.Personalization.Personalization.GetProfile("LanguageDisplayMode", "ViewType" + PortalSettings.Current.PortalId));
                    switch (_ViewType)
                    {
                        case "NATIVE":
                            DisplayType = CultureDropDownTypes.NativeName;
                            break;
                        case "ENGLISH":
                            DisplayType = CultureDropDownTypes.EnglishName;
                            break;
                        default:
                            DisplayType = CultureDropDownTypes.DisplayName;
                            break;
                    }
                }

                text = string.Format("{0} - {1}", template.Name, Localization.GetLocaleName(template.CultureCode, DisplayType));
                value = string.Format("{0}|{1}", Path.GetFileName(template.TemplateFilePath), template.CultureCode);
            }

            return new ListItem(text, value);
        }

        public string GetDefaultTemplate()
        {
            var templates = PortalController.Instance.GetAvailablePortalTemplates();
            string currentCulture = Thread.CurrentThread.CurrentUICulture.Name;

            var defaultTemplates =
                templates.Where(x => Path.GetFileNameWithoutExtension(x.TemplateFilePath) == "Default Website").ToList();

            var match = defaultTemplates.FirstOrDefault(x => x.CultureCode == currentCulture);
            if (match == null)
            {
                match = defaultTemplates.FirstOrDefault(x => x.CultureCode.StartsWith(currentCulture.Substring(0, 2)));
            }
            if (match == null)
            {
                match = defaultTemplates.FirstOrDefault(x => string.IsNullOrEmpty(x.CultureCode));
            }

            return match != null ? string.Format("{0}|{1}", Path.GetFileName(match.TemplateFilePath), match.CultureCode) : "";
        }

        public TabDto GetPortalTabs(int portalId, string cultureCode, bool isMultiLanguage, bool excludeAdminTabs = true)
        {
            var rootNode = new TabDto
            {
                Name = PortalSettings.PortalName,
                ImageUrl = IconPortal,
                TabId = Null.NullInteger.ToString(CultureInfo.InvariantCulture),
                ChildTabs = new List<TabDto>(),
                HasChildren = true
            };

            var portalInfo = PortalController.Instance.GetPortal(portalId);
            var tabs =
                TabController.GetPortalTabs(
                    isMultiLanguage
                        ? TabController.GetTabsBySortOrder(portalId, portalInfo.DefaultLanguage, true)
                        : TabController.GetTabsBySortOrder(portalId, cultureCode, true), Null.NullInteger, false,
                    "<" + Localization.GetString("None_Specified") + ">", true, false, true, false, false)
                    .Where(t => !t.IsSystem)
                    .ToList();

            tabs = excludeAdminTabs
                ? tabs.Where(tab => tab.Level == 0 && tab.TabID != portalInfo.AdminTabId).ToList()
                : tabs.Where(tab => tab.Level == 0).ToList();
            rootNode.HasChildren = tabs.Count > 0;
            foreach (var tab in tabs)
            {
                string tooltip;
                var nodeIcon = GetNodeIcon(tab, out tooltip);
                var node = new TabDto
                {
                    Name = tab.TabName, //$"{tab.TabName} {GetNodeStatusIcon(tab)}",
                    TabId = tab.TabID.ToString(CultureInfo.InvariantCulture),
                    ImageUrl = nodeIcon,
                    Tooltip = tooltip,
                    ParentTabId = tab.ParentId,
                    HasChildren = tab.HasChildren,
                    ChildTabs = new List<TabDto>()
                };
                rootNode.ChildTabs.Add(node);
            }

            return rootNode;
        }

        public List<TabDto> GetTabsDescendants(int portalId, int parentId, string cultureCode, bool isMultiLanguage)
        {
            var descendants = new List<TabDto>();
            var tabs =
                GetExportableTabs(TabController.Instance.GetTabsByPortal(portalId)
                    .WithCulture(cultureCode, true)).WithParentId(parentId);

            foreach (var tab in tabs.Where(x => x.ParentId == parentId))
            {
                string tooltip;
                var nodeIcon = GetNodeIcon(tab, out tooltip);
                var node = new TabDto
                {
                    Name = tab.TabName,//$"{tab.TabName} {GetNodeStatusIcon(tab)}",
                    TabId = tab.TabID.ToString(CultureInfo.InvariantCulture),
                    ImageUrl = nodeIcon,
                    Tooltip = tooltip,
                    ParentTabId = tab.ParentId,
                    HasChildren = tab.HasChildren
                };
                descendants.Add(node);
            }
            return descendants;
        }

        public string ExportPortalTemplate(int portalId, string fileName, string description, bool isMultilanguage, IEnumerable<string> locales, bool includeProfile, 
            bool includeModules, bool includeRoles, bool includeFiles, bool includeContent, IEnumerable<TabDto> pages, string localizationCulture, out bool success)
        {
            bool isValid = true;
            success = false;
            // Verify all ancestor pages are selected
            foreach (TabDto page in pages)
            {
                if (page.ParentTabId != Null.NullInteger && !pages.Any(p => p.TabId == page.ParentTabId.ToString(CultureInfo.InvariantCulture)))
                    isValid = false;
            }
            if (!isValid)
            {
                return Localization.GetString("ErrorAncestorPages", LocalResourcesFile);
            }

            if (!pages.Any())
            {
                return Localization.GetString("ErrorPages", LocalResourcesFile);
            }

            var settings = new XmlWriterSettings();
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;

            var filename = Globals.HostMapPath + fileName;
            if (!filename.EndsWith(".template"))
            {
                filename += ".template";
            }
            XmlWriter writer = XmlWriter.Create(filename, settings);

            writer.WriteStartElement("portal");
            writer.WriteAttributeString("version", "5.0");

            //Add template description
            writer.WriteElementString("description", HttpUtility.HtmlEncode(description));

            //Serialize portal settings
            var portal = PortalController.Instance.GetPortal(portalId);

            SerializePortalSettings(writer, portal, isMultilanguage);
            SerializeEnabledLocales(writer, portal, isMultilanguage, locales);
            SerializeExtensionUrlProviders(writer, portalId);

            if (includeProfile)
            {
                //Serialize Profile Definitions
                SerializeProfileDefinitions(writer, portal);
            }

            if (includeModules)
            {
                //Serialize Portal Desktop Modules
                DesktopModuleController.SerializePortalDesktopModules(writer, portalId);
            }

            if (includeRoles)
            {
                //Serialize Roles
                RoleController.SerializeRoleGroups(writer, portalId);
            }

            //Serialize tabs
            SerializeTabs(writer, portal, isMultilanguage, pages, includeContent, locales, localizationCulture);

            if (includeFiles)
            {
                //Create Zip File to hold files
                var resourcesFile = new ZipOutputStream(File.Create(filename + ".resources"));
                resourcesFile.SetLevel(6);

                //Serialize folders (while adding files to zip file)
                SerializeFolders(writer, portal, ref resourcesFile);

                //Finish and Close Zip file
                resourcesFile.Finish();
                resourcesFile.Close();
            }
            writer.WriteEndElement();
            writer.Close();
            success = true;
            return string.Format(Localization.GetString("ExportedMessage", LocalResourcesFile), filename);
        }

        private IEnumerable<TabDto> GetTabsToExport(int portalId, string cultureCode, bool isMultiLanguage,
            IEnumerable<TabDto> userSelection, IList<TabDto> tabsCollection)
        {
            if (tabsCollection == null)
            {
                var tab = GetPortalTabs(portalId, cultureCode, isMultiLanguage);
                tabsCollection = tab.ChildTabs;
                tab.ChildTabs = null;
                tab.HasChildren = false;
                tabsCollection.Add(tab);
            }
            var selectedTabs = userSelection as List<TabDto> ?? userSelection.ToList();
            foreach (var tab in tabsCollection)
            {
                if (selectedTabs.Exists(x => x.TabId == tab.TabId))
                {
                    var existingTab = selectedTabs.First(x => x.TabId == tab.TabId);
                    tab.CheckedState = existingTab.CheckedState;
                    if (string.IsNullOrEmpty(Convert.ToString(existingTab.Name)))
                    {
                        selectedTabs.Remove(existingTab);
                        selectedTabs.Add(tab);
                    }
                }
                else
                {
                    selectedTabs.Add(tab);
                }

                if (tab.HasChildren)
                {
                    var checkedState = NodeCheckedState.UnChecked;
                    if (tab.CheckedState == NodeCheckedState.Checked)
                    {
                        checkedState = NodeCheckedState.Checked;
                    }

                    var descendants = GetTabsDescendants(portalId, Convert.ToInt32(tab.TabId), cultureCode,
                        isMultiLanguage);
                    descendants.ForEach(x => { x.CheckedState = checkedState; });

                    selectedTabs.AddRange(GetTabsToExport(portalId, cultureCode, isMultiLanguage, selectedTabs,
                        descendants).Where(x => !selectedTabs.Exists(y => y.TabId == x.TabId)));
                }
            }
            return selectedTabs;
        }

        public int CreatePortal(ref ArrayList errors, string domainName, string serverPath, string siteTemplate, string siteName, string siteAlias, string siteDescription, string siteKeywords,
            bool isChildSite, string homeDirectory, int siteGroupId, bool useCurrent, string firstname, string lastname, string username, string email, string password,
            string confirm, string question = "", string answer = "")
        {
            PortalController.PortalTemplateInfo template = LoadPortalTemplateInfoForSelectedItem(siteTemplate);

            string strPortalAlias;
            string strChildPath = string.Empty;
            var closePopUpStr = string.Empty;
            int intPortalId = -1;
            //check template validity
            string schemaFilename = HttpContext.Current.Server.MapPath("~/DesktopModules/Admin/Portals/portal.template.xsd");
            string xmlFilename = template.TemplateFilePath;
            var xval = new PortalTemplateValidator();
            if (!xval.Validate(xmlFilename, schemaFilename))
            {
                errors.AddRange(xval.Errors);
                return intPortalId;
            }

            //Set Portal Name
            siteAlias = siteAlias.ToLowerInvariant().Replace("http://", "");

            //Validate Portal Name
            if (!Globals.IsHostTab(PortalSettings.ActiveTab.TabID))
            {
                isChildSite = true;
                strPortalAlias = siteAlias;
            }
            else
            {
                strPortalAlias = isChildSite ? PortalController.GetPortalFolder(siteAlias) : siteAlias;
            }

            string message = string.Empty;
            if (!PortalAliasController.ValidateAlias(strPortalAlias, isChildSite))
            {
                message = Localization.GetString("InvalidName", LocalResourcesFile);
            }

            //check whether have conflict between tab path and portal alias.
            var checkTabPath = string.Format("//{0}", strPortalAlias);
            if (TabController.GetTabByTabPath(PortalSettings.PortalId, checkTabPath, string.Empty) != Null.NullInteger
                || TabController.GetTabByTabPath(Null.NullInteger, checkTabPath, string.Empty) != Null.NullInteger)
            {
                message = Localization.GetString("DuplicateWithTab", LocalResourcesFile);
            }

            //Validate Password
            if (password != confirm)
            {
                if (!string.IsNullOrEmpty(message)) message += "<br/>";
                message += Localization.GetString("InvalidPassword", LocalResourcesFile);
            }

            //Set Portal Alias for Child Portals
            if (string.IsNullOrEmpty(message))
            {
                if (isChildSite)
                {
                    strChildPath = serverPath + strPortalAlias;

                    if (Directory.Exists(strChildPath))
                    {
                        message = Localization.GetString("ChildExists", LocalResourcesFile);
                    }
                    else
                    {
                        if (!Globals.IsHostTab(PortalSettings.ActiveTab.TabID))
                        {
                            //strPortalAlias = Globals.GetDomainName(Request, true) + "/" + strPortalAlias;
                            strPortalAlias = domainName + "/" + strPortalAlias;
                        }
                        else
                        {
                            strPortalAlias = siteAlias;
                        }
                    }
                }
            }

            //Get Home Directory
            string homeDir = homeDirectory != @"Portals/[PortalID]" ? homeDirectory : "";

            //Validate Home Folder
            if (!string.IsNullOrEmpty(homeDir))
            {
                if (string.IsNullOrEmpty(string.Format("{0}\\{1}\\", Globals.ApplicationMapPath, homeDir).Replace("/", "\\")))
                {
                    message = Localization.GetString("InvalidHomeFolder", LocalResourcesFile);
                }
                if (homeDir.Contains("admin") || homeDir.Contains("DesktopModules") || homeDir.ToLowerInvariant() == "portals/")
                {
                    message = Localization.GetString("InvalidHomeFolder", LocalResourcesFile);
                }
            }

            //Validate Portal Alias
            if (!string.IsNullOrEmpty(strPortalAlias))
            {
                PortalAliasInfo portalAlias = null;
                foreach (PortalAliasInfo alias in PortalAliasController.Instance.GetPortalAliases().Values)
                {
                    if (string.Equals(alias.HTTPAlias, strPortalAlias, StringComparison.InvariantCultureIgnoreCase))
                    {
                        portalAlias = alias;
                        break;
                    }
                }

                if (portalAlias != null)
                {
                    message = Localization.GetString("DuplicatePortalAlias", LocalResourcesFile);
                }
            }

            //Create Portal
            if (string.IsNullOrEmpty(message))
            {
                //Attempt to create the portal
                UserInfo adminUser = new UserInfo();
                try
                {
                    if (useCurrent)
                    {
                        adminUser = PortalSettings.Current.UserInfo;
                        intPortalId = PortalController.Instance.CreatePortal(siteName,
                                                                   adminUser.UserID,
                                                                   siteDescription,
                                                                   siteKeywords,
                                                                   template,
                                                                   homeDir,
                                                                   strPortalAlias,
                                                                   serverPath,
                                                                   strChildPath,
                                                                   isChildSite);
                    }
                    else
                    {
                        adminUser = new UserInfo
                        {
                            FirstName = firstname,
                            LastName = lastname,
                            Username = username,
                            DisplayName = firstname + " " + lastname,
                            Email = email,
                            IsSuperUser = false,
                            Membership =
                                                        {
                                                            Approved = true,
                                                            Password = password,
                                                            PasswordQuestion = question,
                                                            PasswordAnswer = answer
                                                        },
                            Profile =
                                                        {
                                                            FirstName = firstname,
                                                            LastName = lastname
                                                        }
                        };

                        intPortalId = PortalController.Instance.CreatePortal(siteName,
                                                                   adminUser,
                                                                   siteDescription,
                                                                   siteKeywords,
                                                                   template,
                                                                   homeDir,
                                                                   strPortalAlias,
                                                                   serverPath,
                                                                   strChildPath,
                                                                   isChildSite);
                    }

                }
                catch (Exception ex)
                {
                    Logger.Error(ex);

                    intPortalId = Null.NullInteger;
                    message = ex.Message;

                    TryDeleteCreatingPortal(serverPath, isChildSite ? strChildPath : string.Empty);
                }

                if (intPortalId != -1)
                {
                    //Add new portal to Site Group
                    if (siteGroupId != Null.NullInteger)
                    {
                        var portal = PortalController.Instance.GetPortal(intPortalId);
                        var portalGroup = PortalGroupController.Instance.GetPortalGroups().SingleOrDefault(g => g.PortalGroupId == siteGroupId);
                        if (portalGroup != null)
                        {
                            PortalGroupController.Instance.AddPortalToGroup(portal, portalGroup, args => { });
                        }
                    }

                    //Create a Portal Settings object for the new Portal
                    PortalInfo objPortal = PortalController.Instance.GetPortal(intPortalId);
                    var newSettings = new PortalSettings { PortalAlias = new PortalAliasInfo { HTTPAlias = strPortalAlias }, PortalId = intPortalId, DefaultLanguage = objPortal.DefaultLanguage };
                    var webUrl = Globals.AddHTTP(strPortalAlias);
                    try
                    {
                        if (!Globals.IsHostTab(PortalSettings.ActiveTab.TabID))
                        {
                            message = (string.IsNullOrEmpty(PortalSettings.Email) &&
                                string.IsNullOrEmpty(Host.HostEmail)) ?
                                string.Format(Localization.GetString("UnknownEmailAddress.Error", LocalResourcesFile), message, webUrl, closePopUpStr) :
                                Mail.SendMail(PortalSettings.Email,
                                                       email,
                                                       string.IsNullOrEmpty(PortalSettings.Email) ? Host.HostEmail : string.IsNullOrEmpty(Host.HostEmail) ? PortalSettings.Email : PortalSettings.Email + ";" + Host.HostEmail,
                                                       Localization.GetSystemMessage(newSettings, "EMAIL_PORTAL_SIGNUP_SUBJECT", adminUser),
                                                       Localization.GetSystemMessage(newSettings, "EMAIL_PORTAL_SIGNUP_BODY", adminUser),
                                                       "",
                                                       "",
                                                       "",
                                                       "",
                                                       "",
                                                       "");

                        }
                        else
                        {
                            message = string.IsNullOrEmpty(Host.HostEmail) ?
                                string.Format(Localization.GetString("UnknownEmailAddress.Error", LocalResourcesFile), message, webUrl, closePopUpStr) :
                                Mail.SendMail(Host.HostEmail,
                                                       email,
                                                       Host.HostEmail,
                                                       Localization.GetSystemMessage(newSettings, "EMAIL_PORTAL_SIGNUP_SUBJECT", adminUser),
                                                       Localization.GetSystemMessage(newSettings, "EMAIL_PORTAL_SIGNUP_BODY", adminUser),
                                                       "",
                                                       "",
                                                       "",
                                                       "",
                                                       "",
                                                       "");
                        }
                    }
                    catch (Exception exc)
                    {
                        Logger.Error(exc);
                        message = string.Format(Localization.GetString("UnknownSendMail.Error", LocalResourcesFile), webUrl, closePopUpStr);
                    }
                    EventLogController.Instance.AddLog(PortalController.Instance.GetPortal(intPortalId), PortalSettings, PortalSettings.UserId, "", EventLogController.EventLogType.PORTAL_CREATED);

                    // mark default language as published if content localization is enabled
                    bool ContentLocalizationEnabled = PortalController.GetPortalSettingAsBoolean("ContentLocalizationEnabled", PortalSettings.PortalId, false);
                    if (ContentLocalizationEnabled)
                    {
                        LocaleController lc = new LocaleController();
                        lc.PublishLanguage(intPortalId, objPortal.DefaultLanguage, true);
                    }

                    //Redirect to this new site
                    if (message != Null.NullString)
                    {
                        message = string.Format(Localization.GetString("SendMail.Error", LocalResourcesFile), message, webUrl, closePopUpStr);
                    }
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                errors.Add(message);
            }
            
            return intPortalId;
        }

        private void AddChildNodes(TabDto parentNode, PortalInfo portal, string cultureCode)
        {
            if (parentNode.ChildTabs != null)
            {
                parentNode.ChildTabs.Clear();

                var parentId = int.Parse(parentNode.TabId);

                var tabs =
                    GetExportableTabs(TabController.Instance.GetTabsByPortal(portal.PortalID)
                        .WithCulture(cultureCode, true)).WithParentId(parentId);

                foreach (var tab in tabs)
                {
                    if (tab.ParentId == parentId)
                    {
                        string tooltip;
                        var nodeIcon = GetNodeIcon(tab, out tooltip);
                        var node = new TabDto
                        {
                            Name = string.Format("{0} {1}", tab.TabName, GetNodeStatusIcon(tab)),
                            TabId = tab.TabID.ToString(CultureInfo.InvariantCulture),
                            ImageUrl = nodeIcon,
                            Tooltip = tooltip,
                            ParentTabId = tab.ParentId
                        };
                        AddChildNodes(node, portal, cultureCode);
                        parentNode.ChildTabs.Add(node);
                    }
                }
            }
        }

        private TabCollection GetExportableTabs(TabCollection tabs)
        {
            var exportableTabs = tabs.Where(kvp => !kvp.Value.IsSystem).Select(kvp => kvp.Value);
            return new TabCollection(exportableTabs);
        }

        private string GetNodeIcon(TabInfo tab, out string toolTip)
        {
            if (PortalSettings.HomeTabId == tab.TabID)
            {
                toolTip = Localization.GetString("lblHome", LocalResourcesFile);
                return IconHome;
            }

            if (IsSecuredTab(tab))
            {
                if (IsAdminTab(tab))
                {
                    toolTip = Localization.GetString("lblAdminOnly", LocalResourcesFile);
                    return AdminOnlyIcon;
                }

                if (IsRegisteredUserTab(tab))
                {
                    toolTip = Localization.GetString("lblRegistered", LocalResourcesFile);
                    return RegisteredUsersIcon;
                }

                toolTip = Localization.GetString("lblSecure", LocalResourcesFile);
                return SecuredIcon;
            }

            toolTip = Localization.GetString("lblEveryone", LocalResourcesFile);
            return AllUsersIcon;
        }

        private bool IsAdminTab(TabInfo tab)
        {
            var perms = tab.TabPermissions;
            return perms.Cast<TabPermissionInfo>().All(perm => perm.RoleName == PortalSettings.AdministratorRoleName || !perm.AllowAccess);
        }

        private bool IsRegisteredUserTab(TabInfo tab)
        {
            var perms = tab.TabPermissions;
            return perms.Cast<TabPermissionInfo>().Any(perm => perm.RoleName == PortalSettings.RegisteredRoleName && perm.AllowAccess);
        }

        private static bool IsSecuredTab(TabInfo tab)
        {
            var perms = tab.TabPermissions;
            return perms.Cast<TabPermissionInfo>().All(perm => perm.RoleName != Globals.glbRoleAllUsersName || !perm.AllowAccess);
        }

        private string GetNodeStatusIcon(TabInfo tab)
        {
            string s = "";
            if (tab.DisableLink)
            {
                s = s + string.Format("<img src=\"{0}\" alt=\"\" title=\"{1}\" class=\"statusicon\" />", IconPageDisabled, Localization.GetString("lblDisabled", LocalResourcesFile));
            }
            if (tab.IsVisible == false)
            {
                s = s + string.Format("<img src=\"{0}\" alt=\"\" title=\"{1}\" class=\"statusicon\" />", IconPageHidden, Localization.GetString("lblHidden", LocalResourcesFile));
            }
            if (tab.Url != "")
            {
                s = s + string.Format("<img src=\"{0}\" alt=\"\" title=\"{1}\" class=\"statusicon\" />", IconRedirect, Localization.GetString("lblRedirect", LocalResourcesFile));
            }
            return s;
        }

        private void SerializePortalSettings(XmlWriter writer, PortalInfo portal, bool isMultilanguage)
        {
            writer.WriteStartElement("settings");

            writer.WriteElementString("logofile", portal.LogoFile);
            writer.WriteElementString("footertext", portal.FooterText);
            writer.WriteElementString("userregistration", portal.UserRegistration.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("banneradvertising", portal.BannerAdvertising.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("defaultlanguage", portal.DefaultLanguage);

            Dictionary<string, string> settingsDictionary = PortalController.Instance.GetPortalSettings(portal.PortalID);

            string setting;
            settingsDictionary.TryGetValue("DefaultPortalSkin", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("skinsrc", setting);
            }
            settingsDictionary.TryGetValue("DefaultAdminSkin", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("skinsrcadmin", setting);
            }
            settingsDictionary.TryGetValue("DefaultPortalContainer", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("containersrc", setting);
            }
            settingsDictionary.TryGetValue("DefaultAdminContainer", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("containersrcadmin", setting);
            }
            settingsDictionary.TryGetValue("EnableSkinWidgets", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("enableskinwidgets", setting);
            }
            settingsDictionary.TryGetValue("portalaliasmapping", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("portalaliasmapping", setting);
            }

            writer.WriteElementString("contentlocalizationenabled", isMultilanguage.ToString());

            settingsDictionary.TryGetValue("TimeZone", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("timezone", setting);
            }

            settingsDictionary.TryGetValue("EnablePopUps", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("enablepopups", setting);
            }

            settingsDictionary.TryGetValue("InlineEditorEnabled", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("inlineeditorenabled", setting);
            }

            settingsDictionary.TryGetValue("HideFoldersEnabled", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("hidefoldersenabled", setting);
            }

            settingsDictionary.TryGetValue("ControlPanelMode", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("controlpanelmode", setting);
            }

            settingsDictionary.TryGetValue("ControlPanelSecurity", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("controlpanelsecurity", setting);
            }

            settingsDictionary.TryGetValue("ControlPanelVisibility", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("controlpanelvisibility", setting);
            }

            writer.WriteElementString("hostspace", portal.HostSpace.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("userquota", portal.UserQuota.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("pagequota", portal.PageQuota.ToString(CultureInfo.InvariantCulture));

            settingsDictionary.TryGetValue("PageHeadText", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("pageheadtext", setting);
            }
            settingsDictionary.TryGetValue("InjectModuleHyperLink", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("injectmodulehyperlink", setting);
            }
            settingsDictionary.TryGetValue("AddCompatibleHttpHeader", out setting);
            if (!string.IsNullOrEmpty(setting))
            {
                writer.WriteElementString("addcompatiblehttpheader", setting);
            }

            //End Portal Settings
            writer.WriteEndElement();
        }
        
        private void SerializeEnabledLocales(XmlWriter writer, PortalInfo portal, bool isMultilanguage, IEnumerable<string> locales)
        {
            var enabledLocales = LocaleController.Instance.GetLocales(portal.PortalID);
            if (enabledLocales.Count > 1)
            {
                writer.WriteStartElement("locales");
                if (isMultilanguage)
                {
                    foreach (string cultureCode in locales)
                    {
                        writer.WriteElementString("locale", cultureCode);
                    }
                }
                else
                {
                    foreach (var enabledLocale in enabledLocales)
                    {
                        writer.WriteElementString("locale", enabledLocale.Value.Code);
                    }
                }

                writer.WriteEndElement();
            }
        }

        private void SerializeExtensionUrlProviders(XmlWriter writer, int portalId)
        {
            var providers = ExtensionUrlProviderController.GetModuleProviders(portalId);
            if (!providers.Any())
            {
                return;
            }

            writer.WriteStartElement("extensionUrlProviders");

            foreach (var provider in providers)
            {
                writer.WriteStartElement("extensionUrlProvider");
                writer.WriteElementString("name", provider.ProviderConfig.ProviderName);
                writer.WriteElementString("active", provider.ProviderConfig.IsActive.ToString());
                var settings = provider.ProviderConfig.Settings;
                if (settings.Any())
                {
                    writer.WriteStartElement("settings");
                    foreach (var setting in settings)
                    {
                        writer.WriteStartElement("setting");
                        writer.WriteAttributeString("name", setting.Key);
                        writer.WriteAttributeString("value", setting.Value);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                ////if (provider.ProviderConfig.TabIds.Any())
                ////{
                ////    writer.WriteStartElement("tabIds");
                ////    foreach (var tabId in provider.ProviderConfig.TabIds)
                ////    {
                ////        // TODO: translate to tab path?
                ////        writer.WriteElementString("tabId", tabId.ToString(CultureInfo.InvariantCulture));
                ////    }
                ////    writer.WriteEndElement();
                ////}

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private void SerializeFolders(XmlWriter writer, PortalInfo objportal, ref ZipOutputStream zipFile)
        {
            //Sync db and filesystem before exporting so all required files are found
            var folderManager = FolderManager.Instance;
            folderManager.Synchronize(objportal.PortalID);
            writer.WriteStartElement("folders");

            foreach (FolderInfo folder in folderManager.GetFolders(objportal.PortalID))
            {
                writer.WriteStartElement("folder");

                writer.WriteElementString("folderpath", folder.FolderPath);
                writer.WriteElementString("storagelocation", folder.StorageLocation.ToString());

                //Serialize Folder Permissions
                SerializeFolderPermissions(writer, objportal, folder.FolderPath);

                //Serialize files
                SerializeFiles(writer, objportal, folder.FolderPath, ref zipFile);

                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void SerializeFiles(XmlWriter writer, PortalInfo objportal, string folderPath, ref ZipOutputStream zipFile)
        {
            var folderManager = FolderManager.Instance;
            var objFolder = folderManager.GetFolder(objportal.PortalID, folderPath);

            writer.WriteStartElement("files");
            foreach (DotNetNuke.Services.FileSystem.FileInfo objFile in folderManager.GetFiles(objFolder))
            {
                //verify that the file exists on the file system
                var filePath = objportal.HomeDirectoryMapPath + folderPath + GetActualFileName(objFile);
                if (File.Exists(filePath))
                {
                    writer.WriteStartElement("file");

                    writer.WriteElementString("contenttype", objFile.ContentType);
                    writer.WriteElementString("extension", objFile.Extension);
                    writer.WriteElementString("filename", objFile.FileName);
                    writer.WriteElementString("height", objFile.Height.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("size", objFile.Size.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("width", objFile.Width.ToString(CultureInfo.InvariantCulture));

                    writer.WriteEndElement();

                    FileSystemUtils.AddToZip(ref zipFile, filePath, GetActualFileName(objFile), folderPath);
                }
            }
            writer.WriteEndElement();
        }

        private string GetActualFileName(DotNetNuke.Services.FileSystem.FileInfo objFile)
        {
            return (objFile.StorageLocation == (int)FolderController.StorageLocationTypes.SecureFileSystem)
                ? objFile.FileName + Globals.glbProtectedExtension
                : objFile.FileName;
        }

        private void SerializeFolderPermissions(XmlWriter writer, PortalInfo objportal, string folderPath)
        {
            FolderPermissionCollection permissions = FolderPermissionController.GetFolderPermissionsCollectionByFolder(objportal.PortalID, folderPath);

            writer.WriteStartElement("folderpermissions");

            foreach (FolderPermissionInfo permission in permissions)
            {
                writer.WriteStartElement("permission");

                writer.WriteElementString("permissioncode", permission.PermissionCode);
                writer.WriteElementString("permissionkey", permission.PermissionKey);
                writer.WriteElementString("rolename", permission.RoleName);
                writer.WriteElementString("allowaccess", permission.AllowAccess.ToString().ToLowerInvariant());

                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void SerializeProfileDefinitions(XmlWriter writer, PortalInfo objportal)
        {
            var objListController = new ListController();

            writer.WriteStartElement("profiledefinitions");
            foreach (ProfilePropertyDefinition objProfileProperty in
                ProfileController.GetPropertyDefinitionsByPortal(objportal.PortalID, false, false))
            {
                writer.WriteStartElement("profiledefinition");

                writer.WriteElementString("propertycategory", objProfileProperty.PropertyCategory);
                writer.WriteElementString("propertyname", objProfileProperty.PropertyName);

                var objList = objListController.GetListEntryInfo("DataType", objProfileProperty.DataType);
                writer.WriteElementString("datatype", objList == null ? "Unknown" : objList.Value);
                writer.WriteElementString("length", objProfileProperty.Length.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("defaultvisibility", Convert.ToInt32(objProfileProperty.DefaultVisibility).ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void SerializeTabs(XmlWriter writer, PortalInfo portal, bool isMultilanguage, IEnumerable<TabDto> pages,
            bool includeContent, IEnumerable<string> locales, string localizationCulture = "")
        {
            //supporting object to build the tab hierarchy
            var tabs = new Hashtable();

            writer.WriteStartElement("tabs");
            var tabsToExport = GetTabsToExport(portal.PortalID, portal.DefaultLanguage, isMultilanguage, pages, null);

            if (isMultilanguage)
            {
                //Process Default Language first
                SerializeTabs(writer, portal, tabs,
                    GetExportableTabs(
                        TabController.Instance.GetTabsByPortal(portal.PortalID)
                            .WithCulture(portal.DefaultLanguage, true)), tabsToExport, includeContent);

                //Process other locales
                foreach (string cultureCode in locales)
                {
                    if (cultureCode != portal.DefaultLanguage)
                    {
                        SerializeTabs(writer, portal, tabs,
                            GetExportableTabs(
                                TabController.Instance.GetTabsByPortal(portal.PortalID).WithCulture(cultureCode, false)),
                            tabsToExport, includeContent);
                    }
                }
            }
            else
            {
                string contentLocalizable;
                if (PortalController.Instance.GetPortalSettings(portal.PortalID)
                    .TryGetValue("ContentLocalizationEnabled", out contentLocalizable) &&
                    Convert.ToBoolean(contentLocalizable))
                {
                    SerializeTabs(writer, portal, tabs,
                     GetExportableTabs(TabController.Instance.GetTabsByPortal(portal.PortalID).WithCulture(localizationCulture, true)), tabsToExport,
                     includeContent);
                }
                else
                {
                    SerializeTabs(writer, portal, tabs,
                        GetExportableTabs(TabController.Instance.GetTabsByPortal(portal.PortalID)), tabsToExport,
                        includeContent);
                }
            }

            writer.WriteEndElement();
        }

        private void SerializeTabs(XmlWriter writer, PortalInfo portal, Hashtable tabs, TabCollection tabCollection, IEnumerable<TabDto> pages, bool chkContent)
        {
            foreach (TabInfo tab in tabCollection.Values.OrderBy(x=>x.TabID))
            {
                //if not deleted
                if (!tab.IsDeleted)
                {
                    XmlNode tabNode = null;
                    if (string.IsNullOrEmpty(tab.CultureCode) || tab.CultureCode == portal.DefaultLanguage)
                    {
                        // page in default culture and checked or page doesn't exist in tree(which should always export).
                        var tabId = tab.TabID.ToString(CultureInfo.InvariantCulture);
                        if (pages.Any(p => p.TabId == tabId && (p.CheckedState == NodeCheckedState.Checked || p.CheckedState == NodeCheckedState.Partial)) ||
                            !pages.Any(p => p.TabId == tabId))
                        {
                            tabNode = TabController.SerializeTab(new XmlDocument(), tabs, tab, portal, chkContent);
                        }
                    }
                    else
                    {
                        // check if default culture page is selected or default page doesn't exist in tree(which should always export).
                        TabInfo defaultTab = tab.DefaultLanguageTab;
                        if (defaultTab == null
                            || !pages.Any(p => p.TabId == defaultTab.TabID.ToString(CultureInfo.InvariantCulture))
                            ||
                            pages.Count(
                                p =>
                                    p.TabId == defaultTab.TabID.ToString(CultureInfo.InvariantCulture) &&
                                    (p.CheckedState == NodeCheckedState.Checked || p.CheckedState == NodeCheckedState.Partial)) > 0)
                        {
                            tabNode = TabController.SerializeTab(new XmlDocument(), tabs, tab, portal, chkContent);
                        }
                    }

                    if (tabNode != null)
                        tabNode.WriteTo(writer);
                }
            }
        }

        private PortalController.PortalTemplateInfo LoadPortalTemplateInfoForSelectedItem(string template)
        {
            var values = template.Split('|');
            return PortalController.Instance.GetPortalTemplate(Path.Combine(TestableGlobals.Instance.HostMapPath, values[0]), values.Length > 1 ? values[1] : null);
        }

        private void TryDeleteCreatingPortal(string serverPath, string childPath)
        {
            try
            {
                if (HttpContext.Current != null && HttpContext.Current.Items.Contains("CreatingPortalId"))
                {
                    var creatingPortalId = Convert.ToInt32(HttpContext.Current.Items["CreatingPortalId"]);
                    var portalInfo = PortalController.Instance.GetPortal(creatingPortalId);
                    PortalController.DeletePortal(portalInfo, serverPath);
                }

                if (!string.IsNullOrEmpty(childPath))
                {
                    PortalController.DeletePortalFolder(string.Empty, childPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        class TemplateDisplayComparer : IComparer<PortalController.PortalTemplateInfo>
        {
            public int Compare(PortalController.PortalTemplateInfo x, PortalController.PortalTemplateInfo y)
            {
                var cultureCompare = string.Compare(x.CultureCode, y.CultureCode, StringComparison.CurrentCulture);
                if (cultureCompare == 0)
                {
                    return string.Compare(x.Name, y.Name, StringComparison.CurrentCulture);
                }

                //put blank cultures last
                if (string.IsNullOrEmpty(x.CultureCode) || string.IsNullOrEmpty(y.CultureCode))
                {
                    cultureCompare *= -1;
                }

                return cultureCompare;
            }
        }
    }
}