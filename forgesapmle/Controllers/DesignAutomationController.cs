using Autodesk.Forge;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Forge.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using Alias = Autodesk.Forge.DesignAutomation.Model.Alias;
using AppBundle = Autodesk.Forge.DesignAutomation.Model.AppBundle;
using Parameter = Autodesk.Forge.DesignAutomation.Model.Parameter;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;
using WorkItemStatus = Autodesk.Forge.DesignAutomation.Model.WorkItemStatus;

namespace forgeSample.Controllers
{
    [ApiController]
    public class DesignAutomationController : ControllerBase
    {
        private Credentials Credentials { get; set; }
        // Used to access the application folder (temp location for files & bundles)
        private IHostingEnvironment _env;
        // used to access the SignalR Hub
        private IHubContext<DesignAutomationHub> _hubContext;
        // Prefix for AppBundles and Activities
        public static string NickName { get { return Credentials.GetAppSetting("FORGE_NICKNAME"); } }

        DesignAutomationClient _designAutomation;

        public DesignAutomationController(IHostingEnvironment env, IHubContext<DesignAutomationHub> hubContext, DesignAutomationClient api)
        {
            _designAutomation = api;
            _env = env;
            _hubContext = hubContext;

        }

        /// <summary>
        /// Input for StartWorkitem
        /// </summary>
        public class StartWorkitemInput
        {
            public IFormFile inputFile { get; set; }
            public string data { get; set; }
        }

        /// <summary>
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/grid_object_placement")]
        public async Task<IActionResult> StartWorkitem([FromForm]StartWorkitemInput input)
        {
            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (Credentials == null) { return null; }

            // basic input validation
            JObject workItemData = JObject.Parse(input.data);
            string inputRvtFileName = workItemData["inputRvtFileName"].Value<string>();
            string inputRvtFileUrl = workItemData["inputRvtFileUrl"].Value<string>();
            string inputFamilyFileName = workItemData["inputFamilyFileName"].Value<string>();
            string inputFamilyFileUrl = workItemData["inputFamilyFileUrl"].Value<string>();
            string outputFolderUrl = workItemData["outputFolderUrl"].Value<string>();
            string roomUniquId = workItemData["roomUniquId"].Value<string>();
            string gridTypeId = workItemData["gridTypeId"].Value<string>();

            string distanceXMinParam = workItemData["distanceXMinParam"].Value<string>();
            string distanceXMaxParam = workItemData["distanceXMaxParam"].Value<string>();
            string distanceYMinParam = workItemData["distanceYMinParam"].Value<string>();
            string distanceYMaxParam = workItemData["distanceYMaxParam"].Value<string>();
            string distanceWallMinParam = workItemData["distanceWallMinParam"].Value<string>();
            string distanceWallMaxParam = workItemData["distanceWallMaxParam"].Value<string>();

            string browerConnectionId = workItemData["browerConnectionId"].Value<string>();

            string activityName = string.Format("{0}.{1}", NickName, workItemData["activityName"].Value<string>());

            // OAuth token
            dynamic da4rToken = Credentials.DA4RTokenInternal;
            dynamic userToken = Credentials.TokenInternal;

            VersionsApi versionApi = new VersionsApi();
            versionApi.Configuration.AccessToken = Credentials.TokenInternal;

            // Revit file download URL
            string[] revitFileParams = inputRvtFileUrl.Split('/');
            string revitFileProjectIdParam = revitFileParams[1];
            string revitFileVersionIdParam = revitFileParams[2];
            dynamic revitFileVersion = await versionApi.GetVersionAsync(revitFileProjectIdParam, revitFileVersionIdParam);
            string[] revitFileVersionStorageParams = ((string)revitFileVersion.data.relationships.storage.data.id).Split('/');
            string[] revitFileBucketKeyParams = revitFileVersionStorageParams[revitFileVersionStorageParams.Length - 2].Split(':');
            string revitFileBucketKey = revitFileBucketKeyParams[revitFileBucketKeyParams.Length - 1];
            string revitFileObjectName = revitFileVersionStorageParams[revitFileVersionStorageParams.Length - 1];
            string revitFileDownloadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", revitFileBucketKey, revitFileObjectName);

            // Family file download URL
            string[] familyFileParams = inputFamilyFileUrl.Split('/');
            string familyFileProjectIdParam = familyFileParams[1];
            string familyFileVersionIdParam = familyFileParams[2];
            dynamic familyFileVersion = await versionApi.GetVersionAsync(familyFileProjectIdParam, familyFileVersionIdParam);
            string[] familyFileVersionStorageParams = ((string)familyFileVersion.data.relationships.storage.data.id).Split('/');
            string[] familyFileBucketKeyParams = familyFileVersionStorageParams[familyFileVersionStorageParams.Length - 2].Split(':');
            string familyFileBucketKey = familyFileBucketKeyParams[familyFileBucketKeyParams.Length - 1];
            string familyFileObjectName = familyFileVersionStorageParams[familyFileVersionStorageParams.Length - 1];
            string familyFileDownloadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", familyFileBucketKey, familyFileObjectName);

            // prepare workitem arguments
            // 1. input file
            XrefTreeArgument inputRevitFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(revitFileDownloadUrl),
                Verb = Verb.Get,
                Headers = new Dictionary<string, string>()
                {
                    { "Authorization", "Bearer " + userToken }
                }
            };

            XrefTreeArgument inputFamilyFileArgument = new XrefTreeArgument()
            {
                Url = string.Format(familyFileDownloadUrl),
                Verb = Verb.Get,
                Headers = new Dictionary<string, string>()
                {
                    { "Authorization", "Bearer " + userToken }
                }
            };

            // 2. input json
            dynamic inputJson = new JObject();
            inputJson.RoomUniqueId = roomUniquId;
            inputJson.GridTypeId = gridTypeId;
            inputJson.FamilyFileName = inputFamilyFileName;
            inputJson.OutputZipFileName = "exportedDwgs";
            inputJson.DistanceXMinParam = distanceXMinParam;
            inputJson.DistanceXMaxParam = distanceXMaxParam;
            inputJson.DistanceYMinParam = distanceYMinParam;
            inputJson.DistanceYMaxParam = distanceYMaxParam;
            inputJson.DistanceWallMinParam = distanceWallMinParam;
            inputJson.DistanceWallMaxParam = distanceWallMaxParam;
            XrefTreeArgument inputJsonArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)inputJson).ToString(Formatting.None).Replace("\"", "'")
            };

            // 3. output file
            string[] folderParams = outputFolderUrl.Split('/');

            string outputFolderId = folderParams[folderParams.Length - 1];
            string outputProjectId = folderParams[folderParams.Length - 3];

            dynamic storageCreatedZip = await this.CreateStorage(outputFolderId, outputProjectId, "exportedDwgs" + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip");

            string[] zipFileStorageIdParams = ((string)storageCreatedZip.data.id).Split('/');
            string[] zipFileBucketKeyParams = zipFileStorageIdParams[zipFileStorageIdParams.Length - 2].Split(':');
            string zipFileBucketKey = zipFileBucketKeyParams[zipFileBucketKeyParams.Length - 1];
            string zipFileObjectName = zipFileStorageIdParams[zipFileStorageIdParams.Length - 1];

            string uploadZipFileUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", zipFileBucketKey, zipFileObjectName);


            XrefTreeArgument outputZipFileArgument = new XrefTreeArgument()
            {
                Url = uploadZipFileUrl,
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                {
                    {"Authorization", "Bearer " + userToken }
                }
            };

            // prepare & submit workitem
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/grid_object_placement?id={1}&zipFileName={2}&zipFileBucketKey={3}&zipFileObjectName={4}&zipStorageId={5}&projectId={6}&folderId={7}", Credentials.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, "exportedDwgs.zip", zipFileBucketKey, zipFileObjectName, (string)storageCreatedZip.data.id, outputProjectId, outputFolderId);
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = activityName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputRvtFile", inputRevitFileArgument },
                    { "inputFamilyFile", inputFamilyFileArgument },
                    { "inputJsonFile",  inputJsonArgument },
                    { "resultZipFile", outputZipFileArgument },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };

            WorkItemStatus workItemStatus = null;

            try
            {
                workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
            }
            catch (Exception e)
            {
                string message = e.Message;
            }

            return Ok(new { WorkItemId = workItemStatus.Id });
        }

        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation/grid_object_placement")]
        public async Task<IActionResult> OnCallbackGridObjectPlacement(string id, string zipFileName, string zipFileBucketKey, string zipFileObjectName, string zipStorageId, string projectId, string folderId, [FromBody]dynamic body)
        {
            try
            {
                await CreateItemVersion(projectId, folderId, zipFileName, zipStorageId);

                // your webhook should return immediately! we can use Hangfire to schedule a job
                JObject bodyJson = JObject.Parse((string)body.ToString());
                //await _hubContext.Clients.Client(id).SendAsync("onComplete", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);

                OutputData outputData = new OutputData();
                outputData.ReportLog = report;
                outputData.ZipFileName = zipFileName;
                outputData.ZipFileBucketKey = zipFileBucketKey;
                outputData.ZipFileObjectName = zipFileObjectName;

                var jsonData = JsonConvert.SerializeObject(outputData);

                await _hubContext.Clients.Client(id).SendAsync("onCompleteGridObjectPlacement", jsonData);

            }
            catch (Exception e) { }

            // ALWAYS return ok (200)
            return Ok();
        }

        private async Task CreateItemVersion(string projectId, string folderId, string fileName, string storageId)
        {
            FoldersApi foldersApi = new FoldersApi();
            var folderContents = await foldersApi.GetFolderContentsAsync(projectId, folderId);
            var folderData = new DynamicDictionaryItems(folderContents.data);

            string itemId = string.Empty;
            foreach (KeyValuePair<string, dynamic> item in folderData)
                if (item.Value.attributes.displayName == fileName)
                    itemId = item.Value.id; // this means a file with same name is already there, so we'll create a new version

            if (string.IsNullOrWhiteSpace(itemId))
            {
                // create a new item
                BaseAttributesExtensionObject baseAttribute = new BaseAttributesExtensionObject(projectId.StartsWith("a.") ? "items:autodesk.core:File" : "items:autodesk.bim360:File", "1.0");
                CreateItemDataAttributes createItemAttributes = new CreateItemDataAttributes(fileName, baseAttribute);
                CreateItemDataRelationshipsTipData createItemRelationshipsTipData = new CreateItemDataRelationshipsTipData(CreateItemDataRelationshipsTipData.TypeEnum.Versions, CreateItemDataRelationshipsTipData.IdEnum._1);
                CreateItemDataRelationshipsTip createItemRelationshipsTip = new CreateItemDataRelationshipsTip(createItemRelationshipsTipData);
                StorageRelationshipsTargetData storageTargetData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, folderId);
                CreateStorageDataRelationshipsTarget createStorageRelationshipTarget = new CreateStorageDataRelationshipsTarget(storageTargetData);
                CreateItemDataRelationships createItemDataRelationhips = new CreateItemDataRelationships(createItemRelationshipsTip, createStorageRelationshipTarget);
                CreateItemData createItemData = new CreateItemData(CreateItemData.TypeEnum.Items, createItemAttributes, createItemDataRelationhips);
                BaseAttributesExtensionObject baseAttExtensionObj = new BaseAttributesExtensionObject(projectId.StartsWith("a.") ? "versions:autodesk.core:File" : "versions:autodesk.bim360:File", "1.0");
                CreateStorageDataAttributes storageDataAtt = new CreateStorageDataAttributes(fileName, baseAttExtensionObj);
                CreateItemRelationshipsStorageData createItemRelationshipsStorageData = new CreateItemRelationshipsStorageData(CreateItemRelationshipsStorageData.TypeEnum.Objects, storageId);
                CreateItemRelationshipsStorage createItemRelationshipsStorage = new CreateItemRelationshipsStorage(createItemRelationshipsStorageData);
                CreateItemRelationships createItemRelationship = new CreateItemRelationships(createItemRelationshipsStorage);
                CreateItemIncluded includedVersion = new CreateItemIncluded(CreateItemIncluded.TypeEnum.Versions, CreateItemIncluded.IdEnum._1, storageDataAtt, createItemRelationship);
                CreateItem createItem = new CreateItem(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), createItemData, new List<CreateItemIncluded>() { includedVersion });

                ItemsApi itemsApi = new ItemsApi();
                var newItem = await itemsApi.PostItemAsync(projectId, createItem);
            }
            else
            {
                // create a new version
                BaseAttributesExtensionObject attExtensionObj = new BaseAttributesExtensionObject(projectId.StartsWith("a.") ? "versions:autodesk.core:File" : "versions:autodesk.bim360:File", "1.0");
                CreateStorageDataAttributes storageDataAtt = new CreateStorageDataAttributes(fileName, attExtensionObj);
                CreateVersionDataRelationshipsItemData dataRelationshipsItemData = new CreateVersionDataRelationshipsItemData(CreateVersionDataRelationshipsItemData.TypeEnum.Items, itemId);
                CreateVersionDataRelationshipsItem dataRelationshipsItem = new CreateVersionDataRelationshipsItem(dataRelationshipsItemData);
                CreateItemRelationshipsStorageData itemRelationshipsStorageData = new CreateItemRelationshipsStorageData(CreateItemRelationshipsStorageData.TypeEnum.Objects, storageId);
                CreateItemRelationshipsStorage itemRelationshipsStorage = new CreateItemRelationshipsStorage(itemRelationshipsStorageData);
                CreateVersionDataRelationships dataRelationships = new CreateVersionDataRelationships(dataRelationshipsItem, itemRelationshipsStorage);
                CreateVersionData versionData = new CreateVersionData(CreateVersionData.TypeEnum.Versions, storageDataAtt, dataRelationships);
                CreateVersion newVersionData = new CreateVersion(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), versionData);

                VersionsApi versionsApis = new VersionsApi();
                dynamic newVersion = await versionsApis.PostVersionAsync(projectId, newVersionData);

            }
        }

        [HttpPost]
        [Route("api/forge/designautomation/createstorage")]
        public async Task<dynamic> CreateStorage(string folderId, string projectId, string filename)
        {

            StorageRelationshipsTargetData storageRelData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, folderId);
            CreateStorageDataRelationshipsTarget storageTarget = new CreateStorageDataRelationshipsTarget(storageRelData);
            CreateStorageDataRelationships storageRel = new CreateStorageDataRelationships(storageTarget);
            BaseAttributesExtensionObject attributes = new BaseAttributesExtensionObject(string.Empty, string.Empty, new JsonApiLink(string.Empty), null);
            CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes(filename, attributes);
            CreateStorageData storageData = new CreateStorageData(CreateStorageData.TypeEnum.Objects, storageAtt, storageRel);
            CreateStorage storage = new CreateStorage(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), storageData);

            ProjectsApi ProjectsApi = new ProjectsApi();
            ProjectsApi.Configuration.AccessToken = Credentials.TokenInternal;
            dynamic response = await ProjectsApi.PostStorageAsync(projectId, storage);

            return response;
        }

        [HttpGet]
        [Route("api/forge/designautomation/download")]
        public async Task<IActionResult> DownloadFile([FromQuery]string fileName, string fileBucketKey, string fileObjectName)
        {
            try
            {
                ObjectsApi objectsApi = new ObjectsApi();

                Stream file = await objectsApi.GetObjectAsync(fileBucketKey, fileObjectName);

                var cd = new System.Net.Mime.ContentDisposition
                {
                    FileName = fileName,
                    // always prompt the user for downloading, set to true if you want 
                    // the browser to try to show the file inline
                    Inline = false,
                };
                Response.Headers.Add("Content-Disposition", cd.ToString());

                return File(file, "application/octet-stream", fileName);

            }
            catch (Exception e)
            {
                string message = e.Message;

                return Ok();
            }
        }
    }

    public class DesignAutomationHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public string GetConnectionId() { return Context.ConnectionId; }
    }

    [JsonObject("OutputData")]
    class OutputData
    {
        [JsonProperty("reportLog")]
        public string ReportLog { get; set; }

        [JsonProperty("zipFileName")]
        public string ZipFileName { get; set; }

        [JsonProperty("zipFileBucketKey")]
        public string ZipFileBucketKey { get; set; }

        [JsonProperty("zipFileObjectName")]
        public string ZipFileObjectName { get; set; }
    }
}
