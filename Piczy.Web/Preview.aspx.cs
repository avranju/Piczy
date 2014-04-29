using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Piczy.Web
{
    public partial class Preview : System.Web.UI.Page
    {
        static Size[] ImageSizes = new Size[] {
            new Size(640, 480),
            new Size(1024, 768),
            new Size(1920, 1080),
            new Size(1600, 900)
        };

        protected void Page_Load(object sender, EventArgs e)
        {
            var blobUrl = Request.QueryString["BlobUrl"];

            // build list of URLs
            var urlsList = new List<string>();
            foreach (var size in ImageSizes)
            {
                var url = blobUrl.Insert(blobUrl.LastIndexOf('.'),
                    String.Format("-{0}x{1}", size.Width, size.Height));
                imageList.Controls.Add(new LiteralControl(String.Format("<li><img src='{0}' /></li>",
                    url)));
            }
        }
    }
}