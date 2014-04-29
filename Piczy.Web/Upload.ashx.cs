using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.WindowsAzure.TransientFaultHandling;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using Piczy.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Piczy.Web
{
    public class Upload : IHttpHandler
    {
        static RetryManager RetryManager { get; set; }
        static Microsoft.Practices.TransientFaultHandling.RetryPolicy StorageRetryPolicy { get; set; }

        static Upload()
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

        public void ProcessRequest(HttpContext context)
        {
            try
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

                string fileName = context.Request.QueryString["fileName"];
                int fileSize = Int32.Parse(context.Request.QueryString["fileSize"]);

                // insert a row for this image into the table
                Photo photo = new Photo()
                {
                    PartitionKey = fileName[0].ToString(),
                    RowKey = fileName,
                    Timestamp = DateTime.Now,
                    FileSize = fileSize,
                    Status = (int)FileStatus.Uploading,
                    BlobUrl = string.Empty
                };
                var tableContext = tableStorage.GetDataServiceContext();
                tableContext.AddObject(tableName, photo);
                tableContext.SaveChangesWithRetries();

                // create a blob with the image file name and upload file to blob
                var imageBlob = container.GetBlockBlobReference(fileName);
                imageBlob.Properties.ContentType = context.Request.QueryString["contentType"];
                imageBlob.UploadFromStream(context.Request.InputStream);

                // update entity as being processed
                photo.Status = (int)FileStatus.Processing;
                photo.BlobUrl = imageBlob.Uri.AbsoluteUri;
                tableContext.UpdateObject(photo);
                tableContext.SaveChangesWithRetries();

                // add a new message to the queue
                queue.AddMessage(new CloudQueueMessage(System.Text.Encoding.UTF8.GetBytes(imageBlob.Uri.AbsoluteUri)));

                context.Response.ContentType = "text/plain";
                context.Response.Write("All good.");
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "text/plain";
                context.Response.Write("Error: " + ex.ToString());
            }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}