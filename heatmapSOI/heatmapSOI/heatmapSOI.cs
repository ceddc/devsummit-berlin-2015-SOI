using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.Specialized;

using System.Runtime.InteropServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.SOESupport;
using System.Drawing;
using System.Drawing.Imaging;


/***
 * 
 * Heamap generation code from https://github.com/jondot/heatmapdotnet
 *  Dotan J. Nahum
 * 
 * 
 * */


namespace heatmapSOI
{
    [ComVisible(true)]
    [Guid("f5456cac-5499-4384-9760-95615f11867e")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectInterceptor("MapServer",//use "MapServer" if SOI extends a Map service and "ImageServer" if it extends an Image service.
        Description = "",
        DisplayName = "heatmapSOI",
        Properties = "")]
    public class heatmapSOI : IServerObjectExtension, IRESTRequestHandler, IWebRequestHandler, IRequestHandler2, IRequestHandler
    {
        private string _soiName;
        private IServerObjectHelper _soHelper;
        private ServerLogger _serverLog;
        private Dictionary<String, IServerObjectExtension> _extensionCache = new Dictionary<String, IServerObjectExtension>();
        IServerEnvironment2 _serverEnvironment;

        private IFeatureClass myPointsClass;

        public heatmapSOI()
        {
            _soiName = this.GetType().Name;
        }

        public void Init(IServerObjectHelper pSOH)
        {
            _soHelper = pSOH;
            _serverLog = new ServerLogger();

            // set the name of the point layer in the mapservice
            string mapLayerToFind = "points";
            //System.Diagnostics.Debugger.Launch();
            //Access the map service and its layer infos
            ESRI.ArcGIS.Carto.IMapServer3 mapServer = (ESRI.ArcGIS.Carto.IMapServer3)_soHelper.ServerObject;
            string mapName = mapServer.DefaultMapName;
            IMapLayerInfos layerInfos = mapServer.GetServerInfo(mapName).MapLayerInfos;
            IMapLayerInfo layerInfo;

            // Find the index of the layer of interest
            int c = layerInfos.Count;
            int layerIndex = 0;
            for (int i = 0; i < c; i++)
            {
                layerInfo = layerInfos.get_Element(i);
                if (layerInfo.Name == mapLayerToFind)
                {
                    layerIndex = i;
                    break;
                }
            }

            // Access the source feature class of the layer data source (to request it on map export :-))
            IMapServerDataAccess dataAccess = (IMapServerDataAccess)mapServer;
            myPointsClass = (IFeatureClass)dataAccess.GetDataSource(mapName, layerIndex);

            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".init()", 200, "Initialized " + _soiName + " SOI.");
        }

        public void Shutdown()
        {
            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".init()", 200, "Shutting down " + _soiName + " SOI.");
        }

        #region REST interceptors

        public string GetSchema()
        {
            IRESTRequestHandler restRequestHandler = FindRequestHandlerDelegate<IRESTRequestHandler>();
            if (restRequestHandler == null)
                return null;

            return restRequestHandler.GetSchema();
        }

        public byte[] HandleRESTRequest(string Capabilities, string resourceName, string operationName,
            string operationInput, string outputFormat, string requestProperties, out string responseProperties)
        {
            responseProperties = null;
            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleRESTRequest()",
                200, "Request received in Sample Object Interceptor for handleRESTRequest");

            /*
            * Add code to manipulate REST requests here
            */
            // when we call export map operation
            if (operationName.Equals("export", StringComparison.CurrentCultureIgnoreCase))
            {
                
                // will generate heatmap only on rest image export that request an image. (could also request json, and then we need to save image on disk and send json response)
                if (outputFormat.Equals("image", StringComparison.CurrentCultureIgnoreCase))
                {
                    var json = new JsonObject(operationInput);
                    // get export map parameters
                    string bbox;
                    string size;
                    json.TryGetString("bbox", out bbox);
                    json.TryGetString("size", out size);
                    string[] extentString = bbox.Split(',');
                    var width = int.Parse(size.Split(',')[0]);
                    var height = int.Parse(size.Split(',')[1]);

                    // save values
                    var xmin = double.Parse(extentString[0]);
                    var ymin = double.Parse(extentString[1]);
                    var xmax = double.Parse(extentString[2]);
                    var ymax = double.Parse(extentString[3]);

                    var xVal = xmax - xmin;
                    var yVal = ymax - ymin;


                    //create envelope
                    IEnvelope envelope = new EnvelopeClass();
                    envelope.XMin = xmin;
                    envelope.YMin = ymin;
                    envelope.XMax = xmax;
                    envelope.YMax = ymax;

                    //query points into enveloppe and iterate.
                    _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleRESTRequest()",
                          200, "startquery");
                    ISpatialFilter filter = new SpatialFilter();
                    filter.Geometry = envelope;
                    filter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                    IFeatureCursor featureCursor = myPointsClass.Search(filter, true);
                    IFeature feature;
                    List<float> xList = new List<float>();
                    List<float> yList = new List<float>();
                    int i = 0;
                    
                    while ((feature = featureCursor.NextFeature()) != null)
                    {
                        // code to "project points" from real life coordinates inside the envelope, to relative image pixels coordinates.
                        IPoint point = feature.Shape as IPoint;
                        var baseX = point.X - xmin;
                        var baseY = point.Y - ymin;

                        var relativeX = ((baseX * width) / xVal);
                        xList.Add((float)relativeX);
                        // damn, envelope start left bottom, and image left top ! :-(
                        var relativeY = height -((baseY * height) / yVal);
                        yList.Add((float)relativeY);
                    }
                    Marshal.ReleaseComObject(featureCursor);
                    _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleRESTRequest()",
                        200, "endquery // i=" + i);
                    
                    // then call an external lib, to generate an image.
                    // the image returned is 32 bpp, should find a way to get it to indexed 8 bpp for file size
                    Image heatmap = Heatmap.GenerateHeatMap(width, height, xList.ToArray(), yList.ToArray());
                    var newResponse = new System.IO.MemoryStream();
                    heatmap.Save(newResponse, ImageFormat.Png);
                    heatmap.Dispose();
                    // then we send our own response without calling the standard "export map".

                    return newResponse.GetBuffer();



                }
            }


            // Find the correct delegate to forward the request too
            IRESTRequestHandler restRequestHandler = FindRequestHandlerDelegate<IRESTRequestHandler>();
            if (restRequestHandler == null)
                return null;

            var response = restRequestHandler.HandleRESTRequest(
                    Capabilities, resourceName, operationName, operationInput,
                    outputFormat, requestProperties, out responseProperties);
            return response;
        }

        #endregion

        #region SOAP interceptors

        public byte[] HandleStringWebRequest(esriHttpMethod httpMethod, string requestURL,
            string queryString, string Capabilities, string requestData,
            out string responseContentType, out esriWebResponseDataType respDataType)
        {
            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleStringWebRequest()",
                200, "Request received in Sample Object Interceptor for HandleStringWebRequest");

            /*
             * Add code to manipulate requests here
             */

            IWebRequestHandler webRequestHandler = FindRequestHandlerDelegate<IWebRequestHandler>();
            if (webRequestHandler != null)
            {
                return webRequestHandler.HandleStringWebRequest(
                        httpMethod, requestURL, queryString, Capabilities, requestData, out responseContentType, out respDataType);
            }

            responseContentType = null;
            respDataType = esriWebResponseDataType.esriWRDTPayload;
            //Insert error response here.
            return null;
        }

        public byte[] HandleBinaryRequest(ref byte[] request)
        {
            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleBinaryRequest()",
                  200, "Request received in Sample Object Interceptor for HandleBinaryRequest");

            /*
             * Add code to manipulate requests here
             */

            IRequestHandler requestHandler = FindRequestHandlerDelegate<IRequestHandler>();
            if (requestHandler != null)
            {
                return requestHandler.HandleBinaryRequest(request);
            }

            //Insert error response here.
            return null;
        }

        public byte[] HandleBinaryRequest2(string Capabilities, ref byte[] request)
        {
            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleBinaryRequest2()",
                  200, "Request received in Sample Object Interceptor for HandleBinaryRequest2");

            /*
             * Add code to manipulate requests here
             */

            IRequestHandler2 requestHandler = FindRequestHandlerDelegate<IRequestHandler2>();
            if (requestHandler != null)
            {
                return requestHandler.HandleBinaryRequest2(Capabilities, request);
            }

            //Insert error response here.
            return null;
        }

        public string HandleStringRequest(string Capabilities, string request)
        {
            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleStringRequest()",
                   200, "Request received in Sample Object Interceptor for HandleStringRequest");

            /*
             * Add code to manipulate requests here
             */

            IRequestHandler requestHandler = FindRequestHandlerDelegate<IRequestHandler>();
            if (requestHandler != null)
            {
                return requestHandler.HandleStringRequest(Capabilities, request);
            }

            //Insert error response here.
            return null;
        }

        #endregion

        #region Utility code

        private IServerEnvironment2 ServerEnvironment
        {
            get
            {
                if (_serverEnvironment == null)
                {
                    UID uid = new UIDClass();
                    uid.Value = "{32D4C328-E473-4615-922C-63C108F55E60}";

                    // CoCreate an EnvironmentManager and retrieve the IServerEnvironment
                    IEnvironmentManager environmentManager = new EnvironmentManager() as IEnvironmentManager;
                    _serverEnvironment = environmentManager.GetEnvironment(uid) as IServerEnvironment2;
                }

                return _serverEnvironment;
            }
        }

        private THandlerInterface FindRequestHandlerDelegate<THandlerInterface>() where THandlerInterface : class
        {
            try
            {
                IPropertySet props = ServerEnvironment.Properties;
                String extensionName;
                try
                {
                    extensionName = (String)props.GetProperty("ExtensionName");
                }
                catch (Exception /*e*/)
                {
                    extensionName = null;
                }

                if (String.IsNullOrEmpty(extensionName))
                {
                    return (_soHelper.ServerObject as THandlerInterface);
                }

                // Get the extension reference from cache if available
                if (_extensionCache.ContainsKey(extensionName))
                {
                    return (_extensionCache[extensionName] as THandlerInterface);
                }

                // This request is to be made on a specific extension
                // so we find the extension from the extension manager
                IServerObjectExtensionManager extnMgr = _soHelper.ServerObject as IServerObjectExtensionManager;
                IServerObjectExtension soe = extnMgr.FindExtensionByTypeName(extensionName);
                return (soe as THandlerInterface);
            }
            catch (Exception e)
            {
                _serverLog.LogMessage(ServerLogger.msgType.error,
                                    _soiName + ".FindRequestHandlerDelegate()", 500, e.ToString());
                throw;
            }
        }
        #endregion
    }
}
