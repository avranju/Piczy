using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Practices.EnterpriseLibrary.WindowsAzure.TransientFaultHandling;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Piczy.Types;

namespace Piczy.Resizer
{
    public class WorkerRole : RoleEntryPoint
    {
        List<Size> _imageSizes = new List<Size>();
        static RetryManager RetryManager { get; set; }
        static Microsoft.Practices.TransientFaultHandling.RetryPolicy StorageRetryPolicy { get; set; }

        public override void Run()
        {
            var storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));
            CloudBlobClient blobStorage = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobStorage.GetContainerReference("photos");
            CloudQueueClient queueStorage = storageAccount.CreateCloudQueueClient();
            CloudQueue queue = queueStorage.GetQueueReference("resizer");
            CloudTableClient tableStorage = storageAccount.CreateCloudTableClient();
            string tableName = "photos";

            // create container and queue if not created
            StorageRetryPolicy.ExecuteAction(() =>
            {
                container.CreateIfNotExist();
                var permissions = container.GetPermissions();
                permissions.PublicAccess = BlobContainerPublicAccessType.Container;
                container.SetPermissions(permissions);

                queue.CreateIfNotExist();

                tableStorage.CreateTableIfNotExist(tableName);
            });

            var tableContext = tableStorage.GetDataServiceContext();

            // dequeue and process messages from the queue
            while (true)
            {
                try
                {
                    var msg = queue.GetMessage();
                    if (msg != null)
                    {
                        // get the base name of the image
                        string path = msg.AsString;
                        var baseName = Path.GetFileNameWithoutExtension(path);

                        // download image from blob into memory
                        var blob = container.GetBlockBlobReference(path);
                        using (var sourceImageStream = new MemoryStream())
                        {
                            blob.DownloadToStream(sourceImageStream);

                            // build resized images for each image size and upload to blob
                            foreach (var size in _imageSizes)
                            {
                                sourceImageStream.Seek(0, SeekOrigin.Begin);
                                using (var targetImageStream = ResizeImage(sourceImageStream, size.Width, size.Height))
                                {
                                    // upload to blob
                                    var imageName = String.Format("{0}-{1}x{2}.jpg", baseName, size.Width, size.Height);
                                    var resizedBlob = container.GetBlockBlobReference(imageName);
                                    resizedBlob.Properties.ContentType = "image/jpeg";
                                    targetImageStream.Seek(0, SeekOrigin.Begin);
                                    resizedBlob.UploadFromStream(targetImageStream);
                                }
                            }
                        }

                        // get the entity from the table based on file name and upate status
                        var rowKey = Path.GetFileName(path);
                        var partitionKey = rowKey[0].ToString();
                        var query = (from photo in tableContext.CreateQuery<Photo>(tableName)
                                     where photo.PartitionKey == partitionKey && photo.RowKey == rowKey
                                     select photo).AsTableServiceQuery<Photo>();
                        var photoEntity = query.First();
                        photoEntity.Status = (int)FileStatus.Processed;
                        tableContext.UpdateObject(photoEntity);
                        tableContext.SaveChangesWithRetries();

                        queue.DeleteMessage(msg);
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                catch (StorageException ex)
                {
                    System.Threading.Thread.Sleep(5000);
                    Trace.TraceError(string.Format("Exception when processing queue item. Message: '{0}'", ex.Message));
                }
            }
        }

        static WorkerRole()
        {
            InitializeRetryPolicies();
        }

        private static void InitializeRetryPolicies()
        {
 	        if(RetryManager == null)
            {
                RetryManager = EnterpriseLibraryContainer.Current.GetInstance<RetryManager>();
            }

            if (StorageRetryPolicy == null)
            {
                StorageRetryPolicy = RetryManager.GetDefaultAzureStorageRetryPolicy();
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            #region Setup CloudStorageAccount Configuration Setting Publisher

            // This code sets up a handler to update CloudStorageAccount instances when their corresponding
            // configuration settings change in the service configuration file.
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
            {
                // Provide the configSetter with the initial value
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));

                RoleEnvironment.Changed += (sender, arg) =>
                {
                    if (arg.Changes.OfType<RoleEnvironmentConfigurationSettingChange>()
                        .Any((change) => (change.ConfigurationSettingName == configName)))
                    {
                        // The corresponding configuration setting has changed, propagate the value
                        if (!configSetter(RoleEnvironment.GetConfigurationSettingValue(configName)))
                        {
                            // In this case, the change to the storage account credentials in the
                            // service configuration is significant enough that the role needs to be
                            // recycled in order to use the latest settings. (for example, the 
                            // endpoint has changed)
                            RoleEnvironment.RequestRecycle();
                        }
                    }
                };
            });

            #endregion

            // load up the desired image sizes
            var imageSizes = RoleEnvironment.GetConfigurationSettingValue("ImageSizes");
            foreach (var size in imageSizes.Split(','))
            {
                var dims = size.Split('x');
                _imageSizes.Add(new Size(Int32.Parse(dims[0]), Int32.Parse(dims[1])));
            }

            return base.OnStart();
        }

        private Stream ResizeImage(Stream sourceImageStream, int width, int height)
        {
            using (var sourceImage = Image.FromStream(sourceImageStream))
            {
                // compute target dimensions respecting aspect ratio
                if (sourceImage.Width > sourceImage.Height)
                {
                    height = (int)((double)sourceImage.Height * ((double)width / (double)sourceImage.Width));
                }
                else
                {
                    width = (int)((double)sourceImage.Width * ((double)height / (double)sourceImage.Height));
                }

                // resize and render image
                using (Image targetImage = new Bitmap(width, height, sourceImage.PixelFormat))
                {
                    using (Graphics g = Graphics.FromImage(targetImage))
                    {
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        Rectangle rect = new Rectangle(0, 0, width, height);
                        g.DrawImage(sourceImage, rect);

                        var targetImageStream = new MemoryStream();
                        targetImage.Save(targetImageStream, ImageFormat.Jpeg);
                        return targetImageStream;
                    }
                }
            }
        }
    }
}
