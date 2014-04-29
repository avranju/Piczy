using System;
using System.Collections.Generic;
using System.Data.Services.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piczy.Types
{
    [DataServiceKey("PartitionKey", "RowKey")]
    public class Photo
    {
        /// <summary>
        /// Gets/sets the partition key.  The first letter of the image name is the partition key.
        /// </summary>
        public string PartitionKey { get; set; }

        /// <summary>
        /// Gets/sets the row key.  THe image name is the row key.
        /// </summary>
        public string RowKey { get; set; }

        /// <summary>
        /// Gets/sets the timestamp on this row.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets/sets the file size.
        /// </summary>
        public int FileSize { get; set; }

        /// <summary>
        /// Blob URL for this photo.
        /// </summary>
        public string BlobUrl { get; set; }

        /// <summary>
        /// Gets/sets the status of processing on this file.
        /// </summary>
        public int Status { get; set; }
    }
}
