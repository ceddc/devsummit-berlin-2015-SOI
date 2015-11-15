# devsummit-berlin-2015-SOI
SOI samples used during the devsummit  *Extending ArcGIS for Server* session in Berlin on November 12, 2015.

They are **Server Object Interceptor** and are written in **C#** using the [ArcObjects .Net SDK](http://resources.arcgis.com/en/help/arcobjects-net/conceptualhelp/index.html#/ArcObjects_Help_for_NET_developers/0001000002zs000000/)

#### What are theses samples ?
The samples that you will find there are *"Proof of concept"* : they are *basic*, *not optimized*, and will not take every cases and possibilities into account.

They should be used as simple samples to get started on :
* **How** you can develop an SOI.
* **What** you can do with them.
* and then of course, build great stuff !

To put things into context, the heatmap was developed during my plane trip to Berlin using literally the first lib that I've found to generate an heatmap in c#.

#### Use cases
- [FilterAccessSOI](FilterAccessSOI/FilterAccessSOI) : Filter **layer visibility** of your services based on the ArcGIS platform **user credentials**
- [heatmapSOI](heatmapSOI/heatmapSOI) : Generate a **server-side heatmap** map, based on your point data.

#### External Resources

(C#) [ArcObjects Help for .NET developers](http://resources.arcgis.com/en/help/arcobjects-net/conceptualhelp/index.html#/ArcObjects_Help_for_NET_developers/0001000002zs000000/)

(C#) [Developing server object interceptors help (with samples)](http://resources.arcgis.com/en/help/arcobjects-net/conceptualhelp/index.html#/Developing_server_object_interceptors/0001000000mw000001/)

(C#) [A blog post from Domenico Ciavarella (NicoGis) with samples about filtering access and map watermark ](http://nicogis.blogspot.it/2015/05/tutti-pazzi-per-il-soi.html)

(Java/Scala) [A project from Mansour Raad about generating on the fly images using MemSQL](https://github.com/mraad/ExportImageSOI/)




### FilterAccessSOI

<a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-rest-not-logged.PNG">
<img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-rest-not-logged.PNG" width="300"></a>
*Rest service without credentials (only 2 layers visible)* <a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-rest-logged.PNG">
<img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-rest-logged.PNG" width="300"></a>
*Rest service with credentials (all layers visible)*


<a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-not-logged.PNG">
<img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-not-logged.PNG" width="300"></a>
*Map visualization without credentials (only 2 layers visible)*   
<a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-logged.PNG">
<img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/filteraccess-logged.PNG" width="300"></a>
*Map visualization with credentials (all layers visible)*  

### heatmapSOI


<a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/50kpoints.png">
<img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/50kpoints.png" width="200"></a>
*50 000 random points without heatmap*   
<a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/50kpoints-heatmap.png">
<img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/50kpoints-heatmap.png" width="200"></a>
*50 000 random points with heatmap*  

 <a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/no-heatmap.PNG"><img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/no-heatmap.PNG" width="280"></a>
*Random points inside a Web AppBuilder application*  <a src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/with-heatmap.PNG"><img src="https://raw.githubusercontent.com/ceddc/devsummit-berlin-2015-SOI/master/images/with-heatmap.PNG" width="280"></a>
*The server side heatmap inside a Web AppBuilder application*


##### TODO :
More explanation on how everything works, should be done in some days.
