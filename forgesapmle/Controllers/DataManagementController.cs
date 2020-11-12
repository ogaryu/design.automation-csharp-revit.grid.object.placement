﻿/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using Autodesk.Forge;
using Autodesk.Forge.Model;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace forgeSample.Controllers
{
    public class DataManagementController : ControllerBase
    {
        /// <summary>
        /// Credentials on this request
        /// </summary>
        private Credentials Credentials { get; set; }

        /// <summary>
        /// GET TreeNode passing the ID
        /// </summary>
        [HttpGet]
        [Route("api/forge/datamanagement")]
        public async Task<IList<jsTreeNode>> GetTreeNodeAsync(string id)
        {
            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (Credentials == null) { return null; }

            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            if (id == "#") // root
                return await GetHubsAsync();
            else
            {
                string[] idParams = id.Split('/');
                string resource = idParams[idParams.Length - 2];
                switch (resource)
                {
                    case "hubs": // hubs node selected/expanded, show projects
                        return await GetProjectsAsync(id);
                    case "projects": // projects node selected/expanded, show root folder contents
                        return await GetProjectContents(id);
                    case "folders": // folders node selected/expanded, show folder contents
                        return await GetFolderContents(id);
                    case "items":
                        return await GetItemVersions(id);
                }
            }

            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetHubsAsync()
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            HubsApi hubsApi = new HubsApi();
            hubsApi.Configuration.AccessToken = Credentials.TokenInternal;

            var hubs = await hubsApi.GetHubsAsync();
            foreach (KeyValuePair<string, dynamic> hubInfo in new DynamicDictionaryItems(hubs.data))
            {
                // check the type of the hub to show an icon
                string nodeType = "hubs";
                switch ((string)hubInfo.Value.attributes.extension.type)
                {
                    case "hubs:autodesk.core:Hub":
                        nodeType = "hubs"; // if showing only BIM 360, mark this as 'unsupported'
                        break;
                    case "hubs:autodesk.a360:PersonalHub":
                        nodeType = "personalHub"; // if showing only BIM 360, mark this as 'unsupported'
                        break;
                    case "hubs:autodesk.bim360:Account":
                        nodeType = "bim360Hubs";
                        break;
                }

                // create a treenode with the values
                jsTreeNode hubNode = new jsTreeNode(hubInfo.Value.links.self.href, hubInfo.Value.attributes.name, nodeType, !(nodeType == "unsupported"));
                nodes.Add(hubNode);
            }

            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetProjectsAsync(string href)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            ProjectsApi projectsApi = new ProjectsApi();
            projectsApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the hubId from the href
            string[] idParams = href.Split('/');
            string hubId = idParams[idParams.Length - 1];

            var projects = await projectsApi.GetHubProjectsAsync(hubId);
            foreach (KeyValuePair<string, dynamic> projectInfo in new DynamicDictionaryItems(projects.data))
            {
                // check the type of the project to show an icon
                string nodeType = "projects";
                switch ((string)projectInfo.Value.attributes.extension.type)
                {
                    case "projects:autodesk.core:Project":
                        nodeType = "a360projects";
                        break;
                    case "projects:autodesk.bim360:Project":
                        nodeType = "bim360projects";
                        break;
                }

                // create a treenode with the values
                jsTreeNode projectNode = new jsTreeNode(projectInfo.Value.links.self.href, projectInfo.Value.attributes.name, nodeType, true);
                nodes.Add(projectNode);
            }

            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetProjectContents(string href)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            ProjectsApi projectApi = new ProjectsApi();
            projectApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the hubId & projectId from the href
            string[] idParams = href.Split('/');
            string hubId = idParams[idParams.Length - 3];
            string projectId = idParams[idParams.Length - 1];

            var project = await projectApi.GetProjectAsync(hubId, projectId);
            var rootFolderHref = project.data.relationships.rootFolder.meta.link.href;

            return await GetFolderContents(rootFolderHref);
        }

        private async Task<IList<jsTreeNode>> GetFolderContents(string href)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            FoldersApi folderApi = new FoldersApi();
            folderApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the projectId & folderId from the href
            string[] idParams = href.Split('/');
            string folderId = idParams[idParams.Length - 1];
            string projectId = idParams[idParams.Length - 3];

            // check if folder specifies visible types
            JArray visibleTypes = null;
            dynamic folder = (await folderApi.GetFolderAsync(projectId, folderId)).ToJson();
            if (folder.data.attributes != null && folder.data.attributes.extension != null && folder.data.attributes.extension.data != null && !(folder.data.attributes.extension.data is JArray) && folder.data.attributes.extension.data.visibleTypes != null)
                visibleTypes = folder.data.attributes.extension.data.visibleTypes;

            var folderContents = await folderApi.GetFolderContentsAsync(projectId, folderId);
            // the GET Folder Contents has 2 main properties: data & included (not always available)
            var folderData = new DynamicDictionaryItems(folderContents.data);
            var folderIncluded = (folderContents.Dictionary.ContainsKey("included") ? new DynamicDictionaryItems(folderContents.included) : null);

            // let's start iterating the FOLDER DATA
            foreach (KeyValuePair<string, dynamic> folderContentItem in folderData)
            {
                // do we need to skip some items? based on the visibleTypes of this folder
                string extension = folderContentItem.Value.attributes.extension.type;
                if (extension.IndexOf("Folder") /*any folder*/ == -1 && visibleTypes != null && !visibleTypes.ToString().Contains(extension)) continue;

                // if the type is items:autodesk.bim360:Document we need some manipulation...
                if (extension.Equals("items:autodesk.bim360:Document"))
                {
                    // as this is a DOCUMENT, lets interate the FOLDER INCLUDED to get the name (known issue)
                    foreach (KeyValuePair<string, dynamic> includedItem in folderIncluded)
                    {
                        // check if the id match...
                        if (includedItem.Value.relationships.item.data.id.IndexOf(folderContentItem.Value.id) != -1)
                        {
                            // found it! now we need to go back on the FOLDER DATA to get the respective FILE for this DOCUMENT
                            foreach (KeyValuePair<string, dynamic> folderContentItem1 in folderData)
                            {
                                if (folderContentItem1.Value.attributes.extension.type.IndexOf("File") == -1) continue; // skip if type is NOT File

                                // check if the sourceFileName match...
                                if (folderContentItem1.Value.attributes.extension.data.sourceFileName == includedItem.Value.attributes.extension.data.sourceFileName)
                                {
                                    // ready!

                                    // let's return for the jsTree with a special id:
                                    // itemUrn|versionUrn|viewableId
                                    // itemUrn: used as target_urn to get document issues
                                    // versionUrn: used to launch the Viewer
                                    // viewableId: which viewable should be loaded on the Viewer
                                    // this information will be extracted when the user click on the tree node, see ForgeTree.js:136 (activate_node.jstree event handler)
                                    string treeId = string.Format("{0}|{1}|{2}",
                                        folderContentItem.Value.id, // item urn
                                        Base64Encode(folderContentItem1.Value.relationships.tip.data.id), // version urn
                                        includedItem.Value.attributes.extension.data.viewableId // viewableID
                                    );
                                    nodes.Add(new jsTreeNode(treeId, WebUtility.UrlDecode(includedItem.Value.attributes.name), "bim360documents", false));
                                }
                            }
                        }
                    }
                }
                else
                {
                    // non-Plans folder items
                    nodes.Add(new jsTreeNode(folderContentItem.Value.links.self.href, folderContentItem.Value.attributes.displayName, (string)folderContentItem.Value.type, true));
                }
            }

            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetItemVersions(string href)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            ItemsApi itemApi = new ItemsApi();
            itemApi.Configuration.AccessToken = Credentials.TokenInternal;

            DerivativesApi derivativesApi = new DerivativesApi();
            derivativesApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the projectId & itemId from the href
            string[] idParams = href.Split('/');
            string itemId = idParams[idParams.Length - 1];
            string projectId = idParams[idParams.Length - 3];

            var versions = await itemApi.GetItemVersionsAsync(projectId, itemId);

            foreach (KeyValuePair<string, dynamic> version in new DynamicDictionaryItems(versions.data))
            {
                DateTime versionDate = version.Value.attributes.lastModifiedTime;
                string verId = version.Value.id;
                string verNum = version.Value.id.Split("=")[1];
                string userName = version.Value.attributes.lastModifiedUserName;

                string urn = string.Empty;
                try { 
                    urn = (string)version.Value.relationships.derivatives.data.id;

                    dynamic manifestData = await derivativesApi.GetManifestAsync(urn);

                    foreach (KeyValuePair<string, dynamic> manifestParam in new DynamicDictionaryItems(manifestData))
                    {
                        if(manifestParam.Key == "derivatives")
                        {
                            foreach (KeyValuePair<string, dynamic> manifestDerivativeParam in new DynamicDictionaryItems(manifestParam.Value[0]))
                            {
                                if (manifestDerivativeParam.Key == "children")
                                {
                                    foreach (KeyValuePair<string, dynamic> manifestDerivativeChildrenParam in new DynamicDictionaryItems(manifestDerivativeParam.Value))
                                    {
                                        string roomManifestStr = Convert.ToString(manifestDerivativeChildrenParam.Value);

                                        //string roomManifestStrJson = roomManifestStr.Substring(1, roomManifestStr.Length - 2);

                                        RoomManifestChildren deserializedRoomManifest = JsonConvert.DeserializeObject<RoomManifestChildren>(roomManifestStr);

                                        if (deserializedRoomManifest.Role == "3d" && deserializedRoomManifest.Status == ManifestChildren.StatusEnum.Success)
                                        {
                                            if (deserializedRoomManifest.PhaseNames != null)
                                            {
                                                jsTreeNode phaseNode = new jsTreeNode(
                                                        urn + '|' + deserializedRoomManifest.ViewableId + '/' + projectId + '/' + verId,
                                                        string.Format("v{0}: phase: {1}", verNum, deserializedRoomManifest.PhaseNames),
                                                        "versions",
                                                        false);

                                                nodes.Add(phaseNode);
                                            }
                                        }

                                        //foreach(RoomManifestChildren roomManifestChild in deserializedManifestChildren)
                                        //{
                                        //    if (roomManifestChild.Role == ManifestChildren.RoleEnum._3d && roomManifestChild.Status == ManifestChildren.StatusEnum.Success)
                                        //    {
                                        //        if (roomManifestChild.PhaseNames != null)
                                        //        {
                                        //            jsTreeNode phaseNode = new jsTreeNode(
                                        //                    urn + '|' + roomManifestChild.ViewableId + '/' + projectId + '/' + verId,
                                        //                    string.Format("v{0}: {1}", verNum, roomManifestChild.PhaseNames),
                                        //                    "versions",
                                        //                    false);

                                        //            nodes.Add(phaseNode);
                                        //        }
                                        //    }
                                        //}

                                        //if (manifestDerivativeChildParam.Key == "role" && manifestDerivativeChildParam.Value == "3d")
                                        //{
                                        //    if(manifestDerivativeChildParam.Key == "status" && manifestDerivativeChildParam.Value == "success")
                                        //    {
                                        //        if(manifestDerivativeChildParam.Key == "phaseNames")
                                        //        {
                                        //            jsTreeNode phaseNode = new jsTreeNode(
                                        //                urn + '|' + manifestDerivativeChildParam.Value.viewableID + '/' + projectId + '/' + verId,
                                        //                string.Format("v{0}: {1}", verNum, manifestDerivativeChildParam.Value),
                                        //                "versions",
                                        //                false);

                                        //            nodes.Add(phaseNode);
                                        //        }
                                        //    }
                                        //}
                                    }
                                }
                            }
                        }
                        
                    } 
                }
                catch (Exception ex) {
                    string message = ex.Message;
                    urn = Base64Encode(version.Value.id); 
                } // some BIM 360 versions don't have viewable

                jsTreeNode node = new jsTreeNode(
                    urn + '/' + projectId + '/' + verId,
                    string.Format("v{0}: {1} by {2}", verNum, versionDate.ToString("dd/MM/yy HH:mm:ss"), userName),
                    "versions",
                    false);
                nodes.Add(node);
            }



            return nodes;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("/", "_");
        }

        public class jsTreeNode
        {
            public jsTreeNode(string id, string text, string type, bool children)
            {
                this.id = id;
                this.text = text;
                this.type = type;
                this.children = children;
            }

            public string id { get; set; }
            public string text { get; set; }
            public string type { get; set; }
            public bool children { get; set; }
        }

        public class RoomManifestChildren : Autodesk.Forge.Model.ManifestChildren
        {
            [DataMember(Name = "viewableId", EmitDefaultValue = false)]
            public string ViewableId { get; set; }

            [DataMember(Name = "phaseNames", EmitDefaultValue = false)]
            public string PhaseNames { get; set; }

            [DataMember(Name = "role", EmitDefaultValue = false)]
            public new string Role { get; set; }
        }

    }
}