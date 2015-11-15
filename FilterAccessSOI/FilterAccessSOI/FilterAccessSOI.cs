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
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.IO;



//TODO: sign the project (project properties > signing tab > sign the assembly)
//      this is strongly suggested if the dll will be registered using regasm.exe <your>.dll /codebase


namespace FilterAccessSOI
{
    [ComVisible(true)]
    [Guid("5a74d634-3fe1-49aa-b2a3-0e93d583785d")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectInterceptor("MapServer",//use "MapServer" if SOI extends a Map service and "ImageServer" if it extends an Image service.
        Description = "",
        DisplayName = "FilterAccessSOI",
        Properties = "")]
    public class FilterAccessSOI : IServerObjectExtension, IRESTRequestHandler, IWebRequestHandler, IRequestHandler2, IRequestHandler
    {
        private string _soiName;
        private IServerObjectHelper _soHelper;
        private ServerLogger _serverLog;
        private Dictionary<String, IServerObjectExtension> _extensionCache = new Dictionary<String, IServerObjectExtension>();
        IServerEnvironment2 _serverEnvironment;
        // path can be accessed by ags
        private string _permissonPath = "C:\\arcgisserver\\permissions.json";
        private PermissionsList _permList = null;




        public FilterAccessSOI()
        {
            _soiName = this.GetType().Name;
        }

        /// <summary>
        /// Function called when the service is init.
        /// We read persmissions there.
        /// </summary>
        /// <param name="pSOH"></param>
        public void Init(IServerObjectHelper pSOH)
        {
            _soHelper = pSOH;
            _serverLog = new ServerLogger();

            // we get the permissions (a user can see a specific set of layers)
            _permList = new PermissionsList();
            if (File.Exists(_permissonPath))
            {
                String permListString = File.ReadAllText(_permissonPath);
                JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
                // default maxsize = 4mo of data.
                // deserialize and set a list.
                var permListJson = jsSerializer.DeserializeObject(permListString) as IDictionary<string, object>;
                var permArray = permListJson["permissions"] as object[];
                foreach (var perm in permArray)
                {
                    var permJson = perm as IDictionary<string, object>;
                    Permission myPermission = new Permission();
                    myPermission.user = permJson["user"].ToString();
                    myPermission.layers = permJson["layers"].ToString();
                    _permList.permissions.Add(myPermission);
                }
            }


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

        /// <summary>
        /// Every rest request to the mapservice goes through there.
        /// We handle or specific use cases by pre-filtering and post-filtering
        /// Important to check doc for "standard requests" and parameters :
        /// http://resources.arcgis.com/en/help/arcgis-rest-api/index.html#/Map_Service/02r3000000w2000000/
        /// </summary>
        /// <param name="Capabilities"></param>
        /// <param name="resourceName"></param>
        /// <param name="operationName"></param>
        /// <param name="operationInput"></param>
        /// <param name="outputFormat"></param>
        /// <param name="requestProperties"></param>
        /// <param name="responseProperties"></param>
        /// <returns></returns>
        public byte[] HandleRESTRequest(string Capabilities, string resourceName, string operationName,
            string operationInput, string outputFormat, string requestProperties, out string responseProperties)
        {
            responseProperties = null;

            _serverLog.LogMessage(ServerLogger.msgType.infoStandard, _soiName + ".HandleRESTRequest()",
                200, "Request received in Sample Object Interceptor for handleRESTRequest");


            // Find the correct delegate to forward the request too
            IRESTRequestHandler restRequestHandler = FindRequestHandlerDelegate<IRESTRequestHandler>();
            if (restRequestHandler == null)
                return null;

            // get the current logged user name
            var userName = ServerEnvironment.UserInfo.Name;
            // no name user == public
            if (userName == null)
            {
                userName = "";
            }



            #region Pre-filter

            


            string opInput = operationInput;
            /***
             * Prefilter part
             * request some operation on the /mapserver/ :
             * we "prefilter" only the export, find and identify parts
             * */
            if (resourceName == "")
            {
                switch (operationName)
                {
                        // when we call the export map request
                    case "export":
                        opInput = filterExport(opInput, userName);
                        break;
                    // all other operations just do the normal stuff :-)
                    default:
                        break;
                }

            }

            /***
             *  when we request some operation on the /mapserver/<layerID>
             *  http://vmarcgis.local.int/arcgis/rest/services/devsummit/MapServer/0 > resourceName = "/layers/0"
             *  http://vmarcgis.local.int/arcgis/rest/services/devsummit/MapServer/0/query > resourceName = "layers/0"
             * */
            else if (resourceName.StartsWith("/layers/") || resourceName.StartsWith("layers/"))
            {
                // need to check the layerid
                var layerId = Regex.Match(resourceName, @"layers/(\d+)").Groups[1].ToString();
                
                // if we have no right on the layer, send directly a not authorized error.
                // will work on everything like query, applyedit, add, update, delete, ...

                if (!_permList.getPermissionFromUser(userName).layerIsAuth(layerId))
                {

                    responseProperties = "{\"Content-Type\":\"text/plain;charset=utf-8\"}";
                    return System.Text.Encoding.UTF8.GetBytes("{\"error\":{\"code\": 403,\"message\":\"Access Forbidden\"}}");
                }
            }
            #endregion

            var response = restRequestHandler.HandleRESTRequest(
                    Capabilities, resourceName, operationName, opInput,
                    outputFormat, requestProperties, out responseProperties);

            #region Post-Filter
            /***
            * Postfilter part
            * We need to filter layers from the service "root", layers and legend call.
             * There is no rest parameters to do that before the request
            * 
            * */
            if (resourceName == "")
            {
                switch (operationName)
                {
                    // if we call the root request url : http://vmarcgis.local.int/arcgis/rest/services/devsummit/MapServer
                    case "":
                        response = postFilterRoot(response, userName);
                        break;
                    // all other operations just do the normal stuff :-)
                    default:
                        break;
                }
            }
            // if we call the root legend url    http://vmarcgis.local.int/arcgis/rest/services/devsummit/MapServer/legend
            else if (resourceName == "/legend")
            {
                // we filter the response to show only authorized layers
                // for legend, the layer id key is layerId
                response = postFilterLayersLegend(response, userName, "layerId");
            }
            // if we call the root list of layers and tables url    http://vmarcgis.local.int/arcgis/rest/services/devsummit/MapServer/layers
            else if (resourceName == "/layers")
            {
                // we filter the response to show only authorized layers
                // for aller layers and table part, the layer id key is id
                response = postFilterLayersLegend(response, userName, "id");
            }
            #endregion
            return response;
        }

        #endregion
        #region Filter specific functions
        #region Pre-filter functions (filter request)
        /// <summary>
        /// Filter input rest request to show only the layers authorized
        /// </summary>
        /// <param name="opInput"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public string filterExport(string opInput, string userName)
        {
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            // default maxsize = 4mo of data.

            var opInputJson = jsSerializer.DeserializeObject(opInput) as IDictionary<string, object>;

            // well, this is the easy way
            // should normaly take into account if there is already a layer params, and all the possible ways (include,exclude,show,hide) 

            var perm = _permList.getPermissionFromUser(userName);

            opInputJson["layers"] = "show:" + perm.layers;

            opInput = jsSerializer.Serialize(opInputJson);
            return opInput;
        }
        #endregion
        #region Post-filter functions (filter response)
        /// <summary>
        /// Filter the infos (list of layers) present in the rest endpoint
        /// Syntax not the same as in rest directory (http://vmarcgis.local.int/arcgis/rest/services/devsummit/MapServer?f=pjson)
        ///  check rootRequestSample for complete response.
        ///   For this demo we just filter the content part used by rest endpoint.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public byte[] postFilterRoot(byte[] response, string userName)
        {
            var responseString = System.Text.Encoding.UTF8.GetString(response);
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            // default maxsize = 4mo of data.

            var responseJson = jsSerializer.DeserializeObject(responseString) as IDictionary<string, object>;
            var contentsJson = responseJson["contents"] as IDictionary<string, object>;
            var contentsLayersArr = contentsJson["layers"] as object[];
            var contentLayerListFiltered = new List<object>();
            foreach (var layer in contentsLayersArr)
            {
                var layerJson = layer as IDictionary<string, object>;
                var layerId = layerJson["id"].ToString();
                // if authorized, we add it.
                if (_permList.getPermissionFromUser(userName).layerIsAuth(layerId))
                {
                    contentLayerListFiltered.Add(layer);
                }
            }
            contentsJson["layers"] = contentLayerListFiltered.ToArray();
            responseJson["contents"] = contentsJson;

            // should do the same for resources/layers and resource/legend/layers
            //var resourcesJson = responseJson["resources"] as IDictionary<string, object>;


            string newResponse = jsSerializer.Serialize(responseJson);
            return System.Text.Encoding.UTF8.GetBytes(newResponse);
        }
        /// <summary>
        /// Filter function to show authorized layers during call to the legend operation endpoint, and the "all layers and table" operation endpoint
        /// </summary>
        /// <param name="response"></param>
        /// <param name="userName"></param>
        /// <param name="layerKey"></param>
        /// <returns></returns>
        public byte[] postFilterLayersLegend(byte[] response, string userName, string layerKey)
        {
            var responseString = System.Text.Encoding.UTF8.GetString(response);
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            // default maxsize = 4mo of data.

            var responseJson = jsSerializer.DeserializeObject(responseString) as IDictionary<string, object>;
            var layersArr = responseJson["layers"] as object[];
            var layerListFiltered = new List<object>();
            foreach (var layer in layersArr)
            {
                var layerJson = layer as IDictionary<string, object>;
                var layerId = layerJson[layerKey].ToString();
                // if authorized, we add it.
                if (_permList.getPermissionFromUser(userName).layerIsAuth(layerId))
                {
                    layerListFiltered.Add(layer);
                }
            }
            responseJson["layers"] = layerListFiltered.ToArray();
            //responseJson["contents"] = contentsJson;



            string newResponse = jsSerializer.Serialize(responseJson);
            return System.Text.Encoding.UTF8.GetBytes(newResponse);
        }
        #endregion
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
    #region permission  classes
    /** 
    * These classe are here, so you can just copy this file on github and have everything.
    {
   "permissions": [
       {
           "user": "",
           "layers": "6"
       },
       {
           "user": "agsportal",
           "layers": "0,1,2,3,4,5,6"
       }
   ]
   }
    * */
    public class Permission
    {
        public string user { get; set; }
        public string layers { get; set; }


        public bool layerIsAuth(string layerId)
        {
            return layers.Split(',').Contains(layerId);
        }
        public string[] layerArray()
        {
            return layers.Split(',');
        }
    }

    public class PermissionsList
    {
        public List<Permission> permissions { get; set; }
        public PermissionsList()
        {
            permissions = new List<Permission>();
        }
        public Permission getPermissionFromUser(string userName)
        {
            var perm = permissions.FirstOrDefault(x => x.user == userName);
            if (perm == null)
            {
                perm = new Permission();
                // if we don't specify layers to show in export map, so an empty list of layers in permission files
                // everything is visible. (show:<empty>)
                // we put a number which I hope is not a layerId for your service (otherwise you should really change how many layers you have in your MXD)
                perm.layers = "999";
            }
            return perm;
        }
    }
    #endregion
}
