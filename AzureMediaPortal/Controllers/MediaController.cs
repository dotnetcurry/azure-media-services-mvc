using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using AzureMediaPortal.Models;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using System.Globalization;
using System.Text;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Auth;

namespace AzureMediaPortal.Controllers
{
    [Authorize]
    public class MediaController : Controller
    {
        private AzureMediaPortalContext db = new AzureMediaPortalContext();

        //
        // GET: /Media/

        public ActionResult Index()
        {
            return View(db.MediaElements.Where(m => m.UserId == User.Identity.Name).ToList());
        }

        //
        // GET: /Media/Details/5

        public ActionResult Details(int id = 0)
        {
            MediaElement mediaelement = db.MediaElements.FirstOrDefault(m => m.UserId == User.Identity.Name && m.Id == id);
            if (mediaelement == null)
            {
                return HttpNotFound();
            }
            return View(mediaelement);
        }

        //
        // GET: /Media/Create

        public ActionResult Create()
        {
            return View();
        }

        //
        // POST: /Media/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(MediaElement mediaelement)
        {
            if (ModelState.IsValid)
            {
                mediaelement.UserId = User.Identity.Name;
                db.MediaElements.Add(mediaelement);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(mediaelement);
        }

        [HttpPost]
        public JsonResult Save(MediaElement mediaelement)
        {
            try
            {
                mediaelement.UserId = User.Identity.Name;
                mediaelement.FileUrl = GetStreamingUrl(mediaelement.AssetId);
                db.MediaElements.Add(mediaelement);
                db.SaveChanges();
                return Json(new { Saved = true, StreamingUrl =  mediaelement.FileUrl});
            }
            catch (Exception ex)
            {
                return Json(new { Saved = false });
            }
        }

        private string GetStreamingUrl(string assetId)
        {
            CloudMediaContext context = new CloudMediaContext(ConfigurationManager.AppSettings["MediaAccountName"],
                ConfigurationManager.AppSettings["MediaAccountKey"]);

            var daysForWhichStreamingUrlIsActive = 365;
            var streamingAsset = context.Assets.Where(a => a.Id == assetId).FirstOrDefault();

            IAccessPolicy accessPolicy =
                accessPolicy = context.AccessPolicies.Create(streamingAsset.Name, TimeSpan.FromDays(daysForWhichStreamingUrlIsActive),
                                     AccessPermissions.Read | AccessPermissions.List);
            
            string streamingUrl = string.Empty;
            var assetFiles = streamingAsset.AssetFiles.ToList();
            var streamingAssetFile = assetFiles.Where(f => f.Name.ToLower().EndsWith("m3u8-aapl.ism")).FirstOrDefault();
            if (streamingAssetFile != null)
            {
                var locator = context.Locators.CreateLocator(LocatorType.OnDemandOrigin, streamingAsset, accessPolicy);
                Uri hlsUri = new Uri(locator.Path + streamingAssetFile.Name + "/manifest(format=m3u8-aapl)");
                streamingUrl = hlsUri.ToString();
            }
            streamingAssetFile = assetFiles.Where(f => f.Name.ToLower().EndsWith(".ism")).FirstOrDefault();
            if (string.IsNullOrEmpty(streamingUrl) && streamingAssetFile != null)
            {
                var locator = context.Locators.CreateLocator(LocatorType.OnDemandOrigin, streamingAsset, accessPolicy);
                Uri smoothUri = new Uri(locator.Path + streamingAssetFile.Name + "/manifest");
                streamingUrl = smoothUri.ToString();
            }
            streamingAssetFile = assetFiles.Where(f => f.Name.ToLower().EndsWith(".mp4")).FirstOrDefault();
            if (string.IsNullOrEmpty(streamingUrl) && streamingAssetFile != null)
            {
                var locator = context.Locators.CreateLocator(LocatorType.Sas, streamingAsset, accessPolicy);
                var mp4Uri = new UriBuilder(locator.Path);
                mp4Uri.Path += "/" + streamingAssetFile.Name;
                streamingUrl = mp4Uri.ToString();
            }
            return streamingUrl;
        }

        //
        // GET: /Media/Edit/5

        public ActionResult Edit(int id = 0)
        {
            MediaElement mediaelement = db.MediaElements.Find(id);
            if (mediaelement == null)
            {
                return HttpNotFound();
            }
            if (string.IsNullOrEmpty(mediaelement.FileUrl))
            {
                mediaelement.FileUrl = GetStreamingUrl(mediaelement.AssetId);
                db.Entry(mediaelement).State = EntityState.Modified;
                db.SaveChanges();
            }
            return View(mediaelement);
        }

        //
        // POST: /Media/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(MediaElement mediaelement)
        {
            if (ModelState.IsValid)
            {
                db.Entry(mediaelement).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(mediaelement);
        }

        //
        // GET: /Media/Delete/5

        public ActionResult Delete(int id = 0)
        {
            MediaElement mediaelement = db.MediaElements.Find(id);
            if (mediaelement == null)
            {
                return HttpNotFound();
            }
            
            return View(mediaelement);
        }

        //
        // POST: /Media/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            MediaElement mediaelement = db.MediaElements.Find(id);
            DeleteMedia(mediaelement.AssetId);
            db.MediaElements.Remove(mediaelement);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        private void DeleteMedia(string assetId)
        {
            string mediaAccountName = ConfigurationManager.AppSettings["MediaAccountName"];
            string mediaAccountKey = ConfigurationManager.AppSettings["MediaAccountKey"];
            CloudMediaContext context = new CloudMediaContext(mediaAccountName, mediaAccountKey);
            var streamingAsset = context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
            if (streamingAsset != null)
            {
                streamingAsset.Delete();
            }
        }

        [HttpGet]
        public ActionResult Upload()
        {
            return View();
        }

        //[HttpPost]
        //public ActionResult Upload(MediaElement mediaelement, string fileName)
        //{
        //    try
        //    {
        //        var uploadFilePath = fileName;
        //        CloudMediaContext context = new CloudMediaContext(ConfigurationManager.AppSettings["MediaAccountName"],
        //            ConfigurationManager.AppSettings["MediaAccountKey"]);
        //        IAsset uploadAsset = context.Assets.Create(Path.GetFileNameWithoutExtension(uploadFilePath), AssetCreationOptions.None);
        //        var assetFile = uploadAsset.AssetFiles.Create(Path.GetFileName(uploadFilePath));
        //        assetFile.Upload(uploadFilePath);
        //        ViewBag.UploadMessage = "Uploaded Success";
        //        return View();
        //    }
        //    catch (Exception ex)
        //    {
        //        ViewBag.UploadMessage = "Uploaded Failed";
        //        return View(mediaelement);
        //    }

        //}

        [HttpPost]
        public ActionResult SetMetadata(int blocksCount, string fileName, long fileSize)
        {
            var container = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]).CreateCloudBlobClient()
                .GetContainerReference(ConfigurationManager.AppSettings["StorageContainerReference"]);
            container.CreateIfNotExists();
            var fileToUpload = new CloudFile()
            {
                BlockCount = blocksCount,
                FileName = fileName,
                Size = fileSize,
                BlockBlob = container.GetBlockBlobReference(fileName),
                StartTime = DateTime.Now,
                IsUploadCompleted = false,
                UploadStatusMessage = string.Empty
            };
            Session.Add("CurrentFile", fileToUpload);
            return Json(true);
        }

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult UploadChunk(int id)
        {
            HttpPostedFileBase request = Request.Files["Slice"];
            byte[] chunk = new byte[request.ContentLength];
            request.InputStream.Read(chunk, 0, Convert.ToInt32(request.ContentLength));
            JsonResult returnData = null;
            string fileSession = "CurrentFile";
            if (Session[fileSession] != null)
            {
                CloudFile model = (CloudFile)Session[fileSession];
                returnData = UploadCurrentChunk(model, chunk, id);
                if (returnData != null)
                {
                    return returnData;
                }
                if (id == model.BlockCount)
                {
                    return CommitAllChunks(model);
                }
            }
            else
            {
                returnData = Json(new
                {
                    error = true,
                    isLastBlock = false,
                    message = string.Format(CultureInfo.CurrentCulture,
                        "Failed to Upload file.", "Session Timed out")
                });
                return returnData;
            }
            return Json(new { error = false, isLastBlock = false, message = string.Empty });
        }

        private ActionResult CommitAllChunks(CloudFile model)
        {
            model.IsUploadCompleted = true;
            bool errorInOperation = false;
            try
            {
                var blockList = Enumerable.Range(1, (int)model.BlockCount).ToList<int>().ConvertAll(rangeElement =>
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                                string.Format(CultureInfo.InvariantCulture, "{0:D4}", rangeElement))));
                model.BlockBlob.PutBlockList(blockList);
                var duration = DateTime.Now - model.StartTime;
                float fileSizeInKb = model.Size / 1024;
                string fileSizeMessage = fileSizeInKb > 1024 ?
                    string.Concat((fileSizeInKb / 1024).ToString(CultureInfo.CurrentCulture), " MB") :
                    string.Concat(fileSizeInKb.ToString(CultureInfo.CurrentCulture), " KB");
                model.UploadStatusMessage = string.Format(CultureInfo.CurrentCulture,
                    "File uploaded successfully. {0} took {1} seconds to upload",
                    fileSizeMessage, duration.TotalSeconds);
                CreateMediaAsset(model);
            }
            catch (StorageException e)
            {
                model.UploadStatusMessage = "Failed to Upload file. Exception - " + e.Message;
                errorInOperation = true;
            }
            finally
            {
                Session.Remove("CurrentFile");
            }
            return Json(new
            {
                error = errorInOperation,
                isLastBlock = model.IsUploadCompleted,
                message = model.UploadStatusMessage,
                assetId = model.AssetId
            });
        }

        private JsonResult UploadCurrentChunk(CloudFile model, byte[] chunk, int id)
        {
            using (var chunkStream = new MemoryStream(chunk))
            {
                var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                        string.Format(CultureInfo.InvariantCulture, "{0:D4}", id)));
                try
                {
                    model.BlockBlob.PutBlock(
                        blockId,
                        chunkStream, null, null,
                        new BlobRequestOptions()
                        {
                            RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(10), 3)
                        },
                        null);
                    return null;
                }
                catch (StorageException e)
                {
                    Session.Remove("CurrentFile");
                    model.IsUploadCompleted = true;
                    model.UploadStatusMessage = "Failed to Upload file. Exception - " + e.Message;
                    return Json(new { error = true, isLastBlock = false, message = model.UploadStatusMessage });
                }
            }
        }

        private void CreateMediaAsset(CloudFile model)
        {
            string mediaAccountName = ConfigurationManager.AppSettings["MediaAccountName"];
            string mediaAccountKey = ConfigurationManager.AppSettings["MediaAccountKey"];
            string storageAccountName = ConfigurationManager.AppSettings["StorageAccountName"];
            string storageAccountKey = ConfigurationManager.AppSettings["StorageAccountKey"];

            CloudMediaContext context = new CloudMediaContext(mediaAccountName, mediaAccountKey);
            var storageAccount = new CloudStorageAccount(new StorageCredentials(storageAccountName, storageAccountKey), true);
            var cloudBlobClient = storageAccount.CreateCloudBlobClient();
            var mediaBlobContainer = cloudBlobClient.GetContainerReference(cloudBlobClient.BaseUri + "temporary-media");

            mediaBlobContainer.CreateIfNotExists();

            // Create a new asset.
            IAsset asset = context.Assets.Create("NewAsset_" + Guid.NewGuid(), AssetCreationOptions.None);
            IAccessPolicy writePolicy = context.AccessPolicies.Create("writePolicy",
                TimeSpan.FromMinutes(120), AccessPermissions.Write);
            ILocator destinationLocator = context.Locators.CreateLocator(LocatorType.Sas, asset, writePolicy);

            // Get the asset container URI and copy blobs from mediaContainer to assetContainer.
            Uri uploadUri = new Uri(destinationLocator.Path);
            string assetContainerName = uploadUri.Segments[1];
            CloudBlobContainer assetContainer =
                cloudBlobClient.GetContainerReference(assetContainerName);
            string fileName = HttpUtility.UrlDecode(Path.GetFileName(model.BlockBlob.Uri.AbsoluteUri));

            var sourceCloudBlob = mediaBlobContainer.GetBlockBlobReference(fileName);
            sourceCloudBlob.FetchAttributes();

            if (sourceCloudBlob.Properties.Length > 0)
            {
                IAssetFile assetFile = asset.AssetFiles.Create(fileName);
                var destinationBlob = assetContainer.GetBlockBlobReference(fileName);

                destinationBlob.DeleteIfExists();
                destinationBlob.StartCopyFromBlob(sourceCloudBlob);
                destinationBlob.FetchAttributes();
                if (sourceCloudBlob.Properties.Length != destinationBlob.Properties.Length)
                    model.UploadStatusMessage += "Failed to copy as Media Asset!";
            }
            destinationLocator.Delete();
            writePolicy.Delete();

            // Refresh the asset.
            asset = context.Assets.Where(a => a.Id == asset.Id).FirstOrDefault();

            var ismAssetFiles = asset.AssetFiles.ToList().
            Where(f => f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            .ToArray();

            if (ismAssetFiles.Count() != 1)
                throw new ArgumentException("The asset should have only one, .ism file");

            ismAssetFiles.First().IsPrimary = true;
            ismAssetFiles.First().Update();


            
           
            model.UploadStatusMessage += " Created Media Asset '" + asset.Name + "' successfully.";
            model.AssetId = asset.Id;
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}