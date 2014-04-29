using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.WindowsAzure.TransientFaultHandling;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using Piczy.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;

namespace Piczy.Web
{
    /// <summary>
    /// Summary description for PiczyService
    /// </summary>
    public class PiczyService : IHttpHandler
    {
        static RetryManager RetryManager { get; set; }
        static Microsoft.Practices.TransientFaultHandling.RetryPolicy StorageRetryPolicy { get; set; }

        public void ProcessRequest(HttpContext context)
        {
            var storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));
            CloudTableClient tableStorage = storageAccount.CreateCloudTableClient();
            string tableName = "photos";

            // create container and queue if not created
            StorageRetryPolicy.ExecuteAction(() =>
            {
                tableStorage.CreateTableIfNotExist(tableName);
            });

            // retrieve all rows and return in JSON format
            var tableContext = tableStorage.GetDataServiceContext();
            var query = (from photo in tableContext.CreateQuery<Photo>(tableName)
                         select photo).AsTableServiceQuery<Photo>();
            var items = query.ToList();
            var serializer = new JavaScriptSerializer();

            context.Response.ContentType = "application/json";
            context.Response.Write(serializer.Serialize(items));
        }

        static PiczyService()
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

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}