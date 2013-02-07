using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using drawing = System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing;
using System.Net;
using System.Collections.Specialized;
using ImageResizer;
using ImageResizer.Util;
using System.Web.Hosting;



[assembly: WebResource("Imazen.Crop.jquery.Jcrop.css", "text/css")]
[assembly: WebResource("Imazen.Crop.jquery.Jcrop.js", "text/javascript")]
[assembly: WebResource("Imazen.Crop.jquery.Jcrop.min.js", "text/javascript")]
[assembly: WebResource("Imazen.Crop.jquery-1.8.2.js", "text/javascript")]
[assembly: WebResource("Imazen.Crop.jquery-1.8.2.min.js", "text/javascript")]
[assembly: WebResource("Imazen.Crop.webcropimage.js", "text/javascript")]
[assembly: TagPrefix("Imazen.Crop", "ic")]
namespace Imazen.Crop
{
    /// <summary>
    /// 
    /// </summary>
    [DefaultProperty("Text")]
    [ToolboxBitmap(typeof(CropImage), "CropImage")]
    [Designer(typeof(CropImageDesigner))]
    [ToolboxData("<{0}:CropImage Image=\"image1\" CanvasWidth=\"400\" CanvasHeight=\"400\" runat=\"server\"></{0}:CropImage>")]

    public partial class CropImage : CompositeControl, INamingContainer
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            //Updated X,Y, W, H, X2, Y2 values if present in postback
            if (Page.IsPostBack && !string.IsNullOrEmpty(CroppedUrl))
            {
                NameValueCollection s = ImageResizer.Util.PathUtils.ParseQueryStringFriendlyAllowSemicolons(CroppedUrl);
                double[] vals = ParseUtils.ParseList<double>(s["crop"], null, 4);
                if (vals != null)
                {
                    this.X = (int)vals[0];
                    this.Y = (int)vals[1];
                    this.W = (int)vals[2] - (int)vals[0];
                    this.H = (int)vals[3] - (int)vals[1];
                }
            }
            base.OnLoad(e);
        }
        /// <summary>
        /// Gets a stream from the given HTTP URI. No redirect support
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Stream GetUriStream(Uri uri)
        {

            HttpWebResponse response = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.Timeout = 15000; //Default to 15 seconds. Browser timeout is usually 30.

                //This is IDisposable, but only disposes the stream we are returning. So we can't dispose it, and don't need to
                response = request.GetResponse() as HttpWebResponse;
                return response.GetResponseStream();
            }
            catch
            {
                if (response != null) response.Close();
                throw;
            }
        }

        /// <summary>
        /// Crops the original image and saves it to the specified path. If the specified path doesn't contain a valid image extension, it will be appended
        /// </summary>
        /// <param name="dest">The destination Stream, physical path or app-relative virtual path to which the output should be sent</param>
        public void Crop(object dest)
        {
            string destString = dest as string;
            if (destString != null)
            {
                Crop(destString, !ImageResizer.Configuration.Config.Current.Pipeline.IsAcceptedImageType(destString));
            }
            else
            {
                Crop(dest, false);
            }
        }

        /// <summary>
        /// Crops the original image and saves it to the specified path.
        /// </summary>
        /// <param name="dest">The destination Stream, physical path or app-relative virtual path to which the output should be sent</param>
        /// <param name="appendCorrectExtension">If true, the appropriate image extension will be added</param>
        public void Crop(object dest, bool appendCorrectExtension)
        {

            string path = ImagePath != null ? ImagePath : CroppedUrl;

            //Fix relative paths
            if (!path.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("~") && !path.StartsWith("/"))
            {
                path = ResolveUrl(path); //No relative paths here.
            }

            //Fix domain-relative paths into app-relative paths if they're in the same application.
            if (path.StartsWith("/") && path.StartsWith(HostingEnvironment.ApplicationVirtualPath, StringComparison.OrdinalIgnoreCase))
            {
                path = "~/" + path.Substring(HostingEnvironment.ApplicationVirtualPath.Length).TrimStart('/');
            }
            Stream inputStream = null;
            try
            {
                if (path.StartsWith("/"))
                {
                    inputStream =
                        GetUriStream(
                            new Uri(new Uri(Page.Request.Url.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped)),
                                    new Uri(path)));
                }
                else if (!path.StartsWith("~"))
                {
                    //Everything else
                    inputStream = GetUriStream(new Uri(path));
                }

                ExecuteImageJob(dest, appendCorrectExtension, inputStream, path);
            }
            finally
            {
                if (inputStream != null) inputStream.Dispose();
            }
        }

        /// <summary>
        /// Crops the original image and saves it to the specified path.
        /// </summary>
        /// <param name="outputStream">The destination Stream to which the output should be sent</param>
        /// <param name="inputStream">The stream from which the original Image is read</param>
        public void Crop(Stream outputStream, Stream inputStream)
        {
            try
            {
                ExecuteImageJob(outputStream, false, inputStream, null);
            }
            finally
            {
                if (inputStream != null) inputStream.Dispose();
            }
        }

        private void ExecuteImageJob(object dest, bool appendCorrectExtension, Stream inputStream, string path)
        {
            ImageJob j = new ImageJob();
            j.Source = inputStream != null ? (object)inputStream : path;
            j.Dest = dest;
            j.AddFileExtension = appendCorrectExtension;
            j.Settings = new ResizeSettings(CroppedUrlQuerystring);

            NameValueCollection data = CroppedUrlQuerystring;
            j.Settings["cropxunits"] = data["cropxunits"];
            j.Settings["cropyunits"] = data["cropyunits"];
            j.Settings.Quality = this.JpegQuality;
            if (!string.IsNullOrEmpty(this.ForcedImageFormat))
                j.Settings.Format = this.ForcedImageFormat;
            j.Settings["crop"] = "(" + X + ", " + Y + ", " + (X + W) + ", " + (Y + H) + ")";

            j.Build();
        }


        /// <summary>
        /// (readonly) The virtual path or URL to the image we are cropping.
        /// </summary>
        private string ImagePath { get { return ViewState["ImageUrl"].ToString(); } }

        /// <summary>
        /// Returns the name of the hidden field used to store the cropped URL.
        /// </summary>
        public string HiddenFieldClientId { get { return this.ClientID + this.ClientIDSeparator + "url"; } }

        /// <summary>
        /// Returns a URL to a dynamically cropped version of the image. Requires the ImageResizingModule to be enabled in Web.Config.  
        /// </summary>
        public string CroppedUrl
        {
            get { return Page.Request[HiddenFieldClientId]; }
        }
        /// <summary>
        /// Returns a NameValueCollection of the settings used in the cropped url. Doesn't include server-side modified X,Y,W,H values, only client side versions
        /// </summary>
        private NameValueCollection CroppedUrlQuerystring
        {
            get
            {
                return CroppedUrl != null ? ImageResizer.Util.PathUtils.ParseQueryStringFriendlyAllowSemicolons(CroppedUrl) : null;
            }
        }

        /// <summary>
        /// Returns 0 unless ServerSideResize=True and CanvasWidth and/or CanvasHeight are set. 
        /// The width of the display image; the size 'X', 'W', and 'X2' are relative to. 
        /// </summary>
        public double CropXUnits
        {
            get
            {
                return string.IsNullOrEmpty(CroppedUrl) ? 0 :
                    ParseUtils.ParsePrimitive<double>(CroppedUrlQuerystring["cropxunits"], 0);
            }
        }
        /// <summary>
        /// Returns 0 unless ServerSideResize=True and CanvasWidth and/or CanvasHeight are set. 
        /// The height of the display image; the size 'Y', 'H', and 'Y2' are relative to. 
        /// </summary>
        public double CropYUnits
        {
            get
            {
                return string.IsNullOrEmpty(CroppedUrl) ? 0 :
                    ParseUtils.ParsePrimitive<double>(CroppedUrlQuerystring["cropyunits"], 0);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void CreateChildControls()
        {

            //Add the jquery/jcrop includes
            AddFileReferences();

            System.Web.UI.WebControls.Image image =
                Parent.FindControl(this.Image) as System.Web.UI.WebControls.Image;


            string url = image.ImageUrl;
            string resolvedUrl = url.StartsWith("~") ? ResolveUrl(url) : url;
            ViewState["ImageUrl"] = resolvedUrl;


            //image - '<img />' tag ID, DOM, or jquery reference
            //settings - object

            /* 
            settings = {keepAspectRatio:false,
                        keepAspectRatioCheckbox: '#id' or reference,
                        aspectRatio: null or value,
                        minSize: [w,h],
                        maxSize: [w,h],
                        previewDiv: '#id' or reference or null,
                        previewWidth: (previewDiv original width, or 100),
                        previewHeight: (previewDiv original height, or 100),
                        boxWidth: null or value,
                        boxHeight: null or value,
                        setSelect: [x1,y1,x2,y2],
                        updateLinks: "#id1, #id2" or null,
                        updateFields: "#id1, #id2" or null,
                        updateImages: "#id1, #id2" or null
            */

            NameValueCollection s = new NameValueCollection();

            if (!string.IsNullOrEmpty(this.FixedAspectRatioCheckboxID))
            {
                Control c = Parent.FindControl(FixedAspectRatioCheckboxID);
                if (c != null) s["keepAspectRatioCheckbox"] = "'#" + c.ClientID + "'";
            }
            s["keepAspectRatio"] = FixedAspectRatio.ToString().ToLower();

            if (!string.IsNullOrEmpty(this.Ratio))
                s["aspectRatio"] = Ratio;

            //If the image URL is a virtual path within the application, and ServerSideResize=true, then use URL resizing; otherwise, use boxWidth/boxHeight
            if (CanvasHeight > 0 | CanvasWidth > 0)
            {
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && this.ServerSizeResize)
                {
                    NameValueCollection max = new NameValueCollection();
                    if (CanvasHeight > 0) max["maxheight"] = CanvasHeight.ToString();
                    if (CanvasWidth > 0) max["maxwidth"] = CanvasWidth.ToString();
                    url = ImageResizer.Util.PathUtils.MergeOverwriteQueryString(url, max);
                    image.ImageUrl = url;
                }
                else
                {
                    if (CanvasHeight > 0) s["boxHeight"] = CanvasHeight.ToString();
                    if (CanvasWidth > 0) s["boxWidth"] = CanvasWidth.ToString();
                }
            }


            //If there has been a crop rectangle specified, use it
            if (W > 0 || H > 0) s["setSelect"] =
                "[" + X.ToString() + "," +
                Y.ToString() + "," +
                X2.ToString() + ", " +
                Y2.ToString() + "]";



            if (!string.IsNullOrEmpty(this.MaxSize))
                s["maxSize"] = "[" + this.MaxSize + "]";

            if (!string.IsNullOrEmpty(this.MinSize))
                s["minSize"] = "[" + this.MinSize + "]";

            if (this.PreviewWidth > 0)
                s["previewWidth"] = this.PreviewWidth.ToString();

            if (this.PreviewHeight > 0)
                s["previewHeight"] = this.PreviewHeight.ToString();

            if (!string.IsNullOrEmpty(this.PreviewImageID))
            {
                Control c = Parent.FindControl(PreviewImageID);
                if (c != null) s["previewDiv"] = "'#" + c.ClientID + "'";
            }

            s["updateFields"] = "'#" + HiddenFieldClientId + "'";



            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<script>$(function(){ ");
            sb.AppendLine("webcropimage('#" + image.ClientID + "', {");
            foreach (string key in s)
            {
                sb.AppendLine(key + ": " + s[key] + ",");
            }
            sb.Append("});});");
            sb.Append(@"</script>");


            if (isInUpdatePanel)
            {
                ScriptManager.RegisterClientScriptBlock(this.Page, this.Page.GetType()
                    , "cropInit" + this.ClientID
                    , sb.ToString(), false);
                ScriptManager.RegisterHiddenField(this.Page, HiddenFieldClientId, resolvedUrl);

            }
            else
            {
                Page.ClientScript.RegisterClientScriptBlock(this.GetType()
                    , "cropInit" + this.ClientID
                    , sb.ToString(), false);
                Page.ClientScript.RegisterHiddenField(HiddenFieldClientId, resolvedUrl);
            }

            base.CreateChildControls();
        }

        /// <summary>
        /// We don't render anything for this control, it just ties to other controls.
        /// </summary>
        /// <param name="writer"></param>
        public override void RenderEndTag(HtmlTextWriter writer)
        {
            //base.RenderEndTag(writer);
        }


        /// <summary>
        /// We don't render anything for this control, it just ties to other controls.
        /// </summary>
        /// <param name="writer"></param>
        public override void RenderBeginTag(HtmlTextWriter writer)
        {
            //base.RenderBeginTag(writer);
        }


        /// <summary>
        /// Adds the jQuery, jCrop, webcropimage.js and jCrop CSS references as specified in the matching properties.
        /// </summary>
        protected void AddFileReferences()
        {

            ClientScriptManager cs = Page.ClientScript;

            string jQueryVer = "1.8.2";
            string ext = (DebugMode ? ".min.js" : ".js");

            if (string.IsNullOrEmpty(this.ScriptPath)) this.ScriptPath = "~/scripts/";


            string jQueryGoogle = "<script src=\"//ajax.googleapis.com/ajax/libs/jquery/" + jQueryVer + "/jquery" + ext + "\" type=\"text/javascript\"></script>\n";
            string jQueryFolder = "<script src=\"" + ResolveClientUrl(this.ScriptPath.TrimEnd('/') + "/jquery-" + jQueryVer + ext) + "\" type=\"text/javascript\"></script>\n";
            string jQueryResource = "<script src=\"" + cs.GetWebResourceUrl(this.GetType(), "Imazen.Crop.jquery-" + jQueryVer + ext) + "\" type=\"text/javascript\"></script>\n";

            string jQueryFallbackResource = "\n<script>!window.jQuery && document.write(unescape('%3Cscript src=\"" +
                        cs.GetWebResourceUrl(this.GetType(), "Imazen.Crop.jquery-" + jQueryVer + ext) +
                            "\"%3E%3C/script%3E'))</script>\n";

            string jCropFolder = "<script src=\"" + ResolveClientUrl(this.ScriptPath.TrimEnd('/') + "/jquery.Jcrop" + ext) + "\" type=\"text/javascript\"></script>\n";
            string jCropResource = "<script src=\"" + cs.GetWebResourceUrl(this.GetType(), "Imazen.Crop.jquery.Jcrop" + ext) + "\" type=\"text/javascript\"></script>\n";

            string jCropFallbackResource = "\n<script>!$.Jcrop && document.write(unescape('%3Cscript src=\"" +
                        cs.GetWebResourceUrl(this.GetType(), "Imazen.Crop.jquery.Jcrop" + ext) +
                            "\"%3E%3C/script%3E'))</script>\n";

            string webCropFolder = "<script src=\"" + ResolveClientUrl(this.ScriptPath.TrimEnd('/') + "/webcropimage.js") + "\" type=\"text/javascript\"></script>\n";
            string webCropResource = "<script src=\"" + cs.GetWebResourceUrl(this.GetType(), "Imazen.Crop.webcropimage.js") + "\" type=\"text/javascript\"></script>\n";

            string webCropFallbackResource = "\n<script>!window.webcropimage && document.write(unescape('%3Cscript src=\"" +
                        cs.GetWebResourceUrl(this.GetType(), "Imazen.Crop.webcropimage.js") +
                            "\"%3E%3C/script%3E'))</script>\n";

            string jCropCssResource = cs.GetWebResourceUrl(this.GetType(), "Imazen.Crop.jquery.Jcrop.css");
            string jCropCssFolder = ResolveClientUrl(this.ScriptPath.TrimEnd('/') + "/jquery.Jcrop.css");

            string jQueryBlock = null;
            if (JQueryInclude == JQueryIncludeMode.Embedded) jQueryBlock = jQueryResource;
            if (JQueryInclude == JQueryIncludeMode.Google) jQueryBlock = jQueryGoogle + jQueryFallbackResource;
            if (JQueryInclude == JQueryIncludeMode.ScriptFolder) jQueryBlock = jQueryFolder + jQueryFallbackResource;

            string jCropBlock = null;

            if (JCropInclude == JCropIncludeMode.Embedded)
            {
                jCropBlock = jCropResource;
            }
            else if (JCropInclude == JCropIncludeMode.ScriptFolder)
            {
                jCropBlock = jCropFolder + jCropFallbackResource;
            }

            string webBlock = null;
            if (this.WebCropInclude == WebCropImageIncludeMode.Embedded)
                webBlock = webCropResource;
            else if (this.webCropInclude == WebCropImageIncludeMode.ScriptFolder)
                webBlock = webCropFolder + webCropFallbackResource;

            string jCropCss = null;
            if (JCropCssInclude == JCropCssIncludeMode.Embedded)
            {
                jCropCss = jCropCssResource;
            }
            else if (JCropCssInclude == JCropCssIncludeMode.ScriptFolder)
            {
                jCropCss = jCropCssFolder;
            }


            if (IsInUpdatePanel)
            {
                if (jQueryBlock != null && !cs.IsClientScriptBlockRegistered("jquery"))
                    ScriptManager.RegisterClientScriptBlock(this.Page, this.Page.GetType(), "jquery", jQueryBlock, false);

                if (jCropBlock != null && !cs.IsClientScriptBlockRegistered("cropJS"))
                    ScriptManager.RegisterClientScriptBlock(this.Page, this.Page.GetType(), "cropJS", jCropBlock, false);

                if (webBlock != null && !cs.IsClientScriptBlockRegistered("webcropimage"))
                    ScriptManager.RegisterClientScriptBlock(this.Page, this.Page.GetType(), "webcropimage", webBlock, false);
            }
            else
            {
                if (jQueryBlock != null && !cs.IsClientScriptBlockRegistered("jquery"))
                    cs.RegisterClientScriptBlock(this.GetType(), "jquery", jQueryBlock, false);

                if (jCropBlock != null && !cs.IsClientScriptBlockRegistered("cropJS"))
                    cs.RegisterClientScriptBlock(this.GetType(), "cropJS", jCropBlock);

                if (webBlock != null && !cs.IsClientScriptBlockRegistered("webcropimage"))
                    cs.RegisterClientScriptBlock(this.GetType(), "webcropimage", webBlock);
            }


            if (jCropCss != null)
            {
                Page.Header.Controls.Add(new LiteralControl("<link href=\"" + jCropCss + "\" type=\"text/css\" rel=\"stylesheet\" />\r\n"));
            }


        }


    }
}
