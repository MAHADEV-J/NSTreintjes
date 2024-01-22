using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using Esri.ArcGISRuntime.UI;
using System.Diagnostics;
using Esri.ArcGISRuntime.Symbology;

namespace DisplayAMap
{
    internal class DataHandler
    {
        internal static FeatureLayer _trainLayer;
        internal static FeatureLayer _trackLayer;
        private GeodatabaseFeatureTable? _featureTable;
        private Geodatabase? _geodatabase;
        private string? _gdbPath;
        private string? _directoryPath;
        private string? _tracklayerPath;
        private string? _trackFeatureTable;
        private System.Timers.Timer _timer = new System.Timers.Timer();
        int fetchCounter = 0;

        public class TrainInfo
        {
            public int TreinNummer { get; set; }
            public string RitId { get; set; }
            public double Lat { get; set; }
            public double Lng { get; set; }
            public double Snelheid { get; set; }
            public double Richting { get; set; }
            public double HorizontaleNauwkeurigheid { get; set; }
            public string Type { get; set; }
            public string Bron { get; set; }
        }

        public class Payload
        {
            public List<TrainInfo> Treinen { get; set; }
        }
        public class RootObject
        {
            public Payload Payload { get; set; }
        }

        public async Task KeepUpdatingTrains(object? state, FeatureLayer layer, FeatureLayer trackLayer, MapView mainMapView)
        {
            await Task.Run(async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                if (fetchCounter == 5)
                {
                    // Process the train data and update the features
                    await ProcessAndDisplayTrainData(layer);
                }

                // Create a query to get all features
                QueryParameters queryParameters = new QueryParameters();
                queryParameters.WhereClause = "1=1"; // Retrieve all features

                // Get features in the layer
                var features = await _trainLayer.FeatureTable.QueryFeaturesAsync(queryParameters);

                foreach (var feature in features)
                {
                    double millisecondsElapsed = stopwatch.ElapsedMilliseconds;
                    double timeFactor = millisecondsElapsed / 10000.0;

                    double speedKmPerHour = (double)feature.Attributes["snelheid"];
                    double speedMetersPerSecond = speedKmPerHour / 3.6;

                    double angleInRadians = (Math.PI / 180.0) * (double)feature.Attributes["richting"];

                    double deltaX = (speedMetersPerSecond * timeFactor) * Math.Cos(angleInRadians);
                    double deltaY = (speedMetersPerSecond * timeFactor) * Math.Sin(angleInRadians);

                    Geometry currentGeometry = feature.Geometry;
                    Geometry newGeometry = GeometryEngine.Move(currentGeometry, deltaX, deltaY);

                    // Query features from the track layer directly without an additional function
                    var trackFeatures = await trackLayer.FeatureTable.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" });

                    MapPoint nearestPointOnTracks = null;
                    double minDistance = double.MaxValue;

                    // Assuming tracks are represented as Polyline geometries
                    foreach (var trackFeature in trackFeatures)
                    {
                        if (trackFeature.Geometry is Polyline polyline)
                        {
                            foreach (var vertex in polyline.Parts.SelectMany(part => part.Points))
                            {
                                double distance = GeometryEngine.Distance(newGeometry as MapPoint, vertex);

                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    nearestPointOnTracks = vertex;
                                }
                            }
                        }
                    }
                    feature.Geometry = nearestPointOnTracks;
                    if (feature.Attributes["clicked"]?.ToString() == "true")
                    {
                        await mainMapView.Dispatcher.InvokeAsync(async () =>
                        {
                            GraphicsOverlay? graphicsOverlay = mainMapView.GraphicsOverlays.FirstOrDefault();
                            GraphicCollection graphics = graphicsOverlay.Graphics;
                            // Iterate through all graphics in the overlay
                            for (int i = 0; i < graphics.Count + 1; i++)
                            {
                                graphics[i].Geometry = feature.Geometry;
                            }
                            graphicsOverlay.Graphics.Remove(graphics[1]);
                            graphicsOverlay.Graphics.Remove(graphics[0]);
                            graphicsOverlay.Graphics.Add(graphics[0]);
                            graphicsOverlay.Graphics.Add(graphics[1]);
                        });
                    }
                    stopwatch.Restart();

                    // Use Dispatcher.InvokeAsync to update UI components
                    await layer.FeatureTable.UpdateFeatureAsync(feature);
                }
                if (fetchCounter == 5)
                {
                    fetchCounter = 0;
                }
            });
            fetchCounter++;
        }

        public async Task ProcessAndDisplayTrainData(FeatureLayer layer)
        {
            string trainInfo = NSAPICalls.GetTrainData();
            RootObject? rootObject = JsonConvert.DeserializeObject<RootObject>(trainInfo);

            FeatureTable featureTable = layer.FeatureTable;

            for (int i = 0; i < rootObject.Payload.Treinen.Count; i++)
            {
                var attributes = new Dictionary<string, object?>();
                attributes["treinNummer"] = (Int32)rootObject.Payload.Treinen[i].TreinNummer;
                attributes["ritId"] = rootObject.Payload.Treinen[i].RitId;
                attributes["snelheid"] = (float)rootObject.Payload.Treinen[i].Snelheid; ;
                attributes["richting"] = rootObject.Payload.Treinen[i].Richting;
                attributes["clicked"] = "false";

                double lat = Convert.ToDouble(rootObject.Payload.Treinen[i].Lat);
                double lng = Convert.ToDouble(rootObject.Payload.Treinen[i].Lng);
                MapPoint pointGeometry = new MapPoint(lng, lat, SpatialReferences.Wgs84);

                QueryParameters query = new QueryParameters
                {
                    WhereClause = $"treinNummer = " + rootObject.Payload.Treinen[i].TreinNummer
                };

                var result = await featureTable.QueryFeaturesAsync(query);
                if (result != null)
                {
                    foreach (var feature in result)
                    {
                        feature.Geometry = pointGeometry;
                        foreach (var attribute in attributes)
                        {
                            feature.SetAttributeValue(attribute.Key, attribute.Value);
                        }
                        await layer.FeatureTable.UpdateFeatureAsync(feature);
                    }
                }
            }
        }


        public async Task CreateGeodatabase()
        {
            // Create a new randomly named directory for the geodatabase.
            _directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CreateMobileGeodatabase");
            if (!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }

            // Create the geodatabase file.
            _gdbPath = Path.Combine(_directoryPath, "LocationHistory.geodatabase");

            // Delete existing file if present from previous sample run.
            // Check if the geodatabase file exists
            if (File.Exists(_gdbPath))
            {
                _geodatabase = await Geodatabase.OpenAsync(_gdbPath);

                // Check if the feature table "locationhistory" exists
                GeodatabaseFeatureTable existingTable = _geodatabase.GeodatabaseFeatureTables.FirstOrDefault(table => table.TableName == "LocationHistory");

                if (existingTable != null)
                {
                    // Delete the feature table if it exists
                    await _geodatabase.DeleteTableAsync(existingTable.TableName);
                }
            }
            else
            {
                _geodatabase = await Geodatabase.CreateAsync(_gdbPath);
            }

            // Construct a table description which stores features as points on a map.
            var tableDescription = new TableDescription("LocationHistory", SpatialReferences.Wgs84, GeometryType.Point)
            {
                HasAttachments = false,
                HasM = false,
                HasZ = false
            };

            // Set up the fields for the table:
            // FieldType.OID is the primary key of the SQLite table.
            // FieldType.Date is a date column used to store a Calendar date.
            // FieldDescriptions can be a SHORT, INTEGER, GUID, FLOAT, DOUBLE, DATE, TEXT, OID, GLOBALID, BLOB, GEOMETRY, RASTER, or XML.
            tableDescription.FieldDescriptions.Add(new FieldDescription("oid", FieldType.OID));
            tableDescription.FieldDescriptions.Add(new FieldDescription("collection_timestamp", FieldType.Date));
            tableDescription.FieldDescriptions.Add(new FieldDescription("treinNummer", FieldType.Int32));
            tableDescription.FieldDescriptions.Add(new FieldDescription("ritId", FieldType.Text));
            tableDescription.FieldDescriptions.Add(new FieldDescription("snelheid", FieldType.Float64));
            tableDescription.FieldDescriptions.Add(new FieldDescription("richting", FieldType.Float64));
            tableDescription.FieldDescriptions.Add(new FieldDescription("clicked", FieldType.Text));

            // Add a new table to the geodatabase by creating one from the table description.
            _featureTable = await _geodatabase.CreateTableAsync(tableDescription);

        }

        public async Task<FeatureLayer> CreateOrFetchTracks()
        {
            FeatureTable featureTable;

            _directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CreateMobileGeodatabase", "locationhistory.geodatabase");

            // Open the geodatabase
            Geodatabase geodatabase = await Geodatabase.OpenAsync(_directoryPath);

            // Retrieve the FeatureLayer based on the table name or create a new one;
            if ((featureTable = geodatabase.GeodatabaseFeatureTables.FirstOrDefault(table => table.TableName == "TrainTracks")) == null)
            {

                // Construct a table description which stores features as points on a map.
                var tableDescription = new TableDescription("TrainTracks", SpatialReferences.Wgs84, GeometryType.Polyline)
                {
                    HasAttachments = false,
                    HasM = false,
                    HasZ = false
                };

                // Set up the fields for the table:
                // FieldType.OID is the primary key of the SQLite table.
                // FieldType.Date is a date column used to store a Calendar date.
                // FieldDescriptions can be a SHORT, INTEGER, GUID, FLOAT, DOUBLE, DATE, TEXT, OID, GLOBALID, BLOB, GEOMETRY, RASTER, or XML.
                tableDescription.FieldDescriptions.Add(new FieldDescription("oid", FieldType.OID));
                tableDescription.FieldDescriptions.Add(new FieldDescription("from", FieldType.Text));
                tableDescription.FieldDescriptions.Add(new FieldDescription("to", FieldType.Text));

                FeatureTable trackFeatureTable = await _geodatabase.CreateTableAsync(tableDescription);

                string geoJsonString = await NSAPICalls.GetTrackData();

                // Deserialize the GeoJSON string to a JObject
                JObject geoJsonObject = JsonConvert.DeserializeObject<JObject>(geoJsonString);

                // Check if 'features' exists within the 'payload' object
                if (geoJsonObject["payload"]?["features"] is JArray features)
                {
                    // Do something with the features...
                    foreach (JObject feature in features)
                    {
                        // Access properties, geometry, etc.
                        JObject properties = (JObject)feature["properties"];
                        JObject geometry = (JObject)feature["geometry"];

                        // Get the geometry type
                        string geometryType = geometry["type"].ToString();

                        // Check if the geometry type is LineString
                        if (geometryType.Equals("LineString", StringComparison.OrdinalIgnoreCase))
                        {
                            // Handle LineString geometry
                            JArray coordinates = (JArray)geometry["coordinates"];
                            Polyline polyline = ConvertGeoJsonLineStringToEsriPolyline(coordinates);
                            // Now you can use 'polyline' as your geometry

                            // Create a new feature and set its attributes and geometry
                            Feature newFeature = trackFeatureTable.CreateFeature();

                            // Set geometry
                            newFeature.Geometry = polyline;

                            // Set attributes
                            foreach (var property in properties)
                            {
                                newFeature.SetAttributeValue(property.Key, property.Value.ToString());
                            }
                            // Add the feature to the feature table
                            await trackFeatureTable.AddFeatureAsync(newFeature);
                        }
                    }
                }
                return _trackLayer = new FeatureLayer(trackFeatureTable) { Name = "Tracks" };
            }
            else
            {
                featureTable = geodatabase.GeodatabaseFeatureTables.FirstOrDefault(table => table.TableName == "TrainTracks");

                return new FeatureLayer(featureTable);
            }
        }

        public async Task<FeatureLayer> CreateTrainIcons(string trainInfo)
        {

            RootObject? rootObject = JsonConvert.DeserializeObject<RootObject>(trainInfo);
            for (int i = 0; i < rootObject.Payload.Treinen.Count; i++)
            {
                var attributes = new Dictionary<string, object?>();
                attributes["treinNummer"] = (Int32)rootObject.Payload.Treinen[i].TreinNummer;
                attributes["ritId"] = rootObject.Payload.Treinen[i].RitId;
                attributes["snelheid"] = (float)rootObject.Payload.Treinen[i].Snelheid; ;
                attributes["richting"] = rootObject.Payload.Treinen[i].Richting;
                attributes["clicked"] = "false";

                double lat = Convert.ToDouble(rootObject.Payload.Treinen[i].Lat);
                double lng = Convert.ToDouble(rootObject.Payload.Treinen[i].Lng);
                MapPoint pointGeometry = new MapPoint(lng, lat, SpatialReferences.Wgs84);
                Feature feature = _featureTable.CreateFeature(attributes, pointGeometry);
                try
                {
                    // Add the feature to the feature table.
                    await _featureTable.AddFeatureAsync(feature);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Get the base directory of the application
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Combine the base directory with the relative path to your custom icon
            string iconRelativePath = "TrainIcon.png";
            string iconPath = Path.Combine(baseDirectory, iconRelativePath);

            PictureMarkerSymbol customSymbol = new PictureMarkerSymbol(new Uri(iconPath));
            // Create a SimpleRenderer with the custom symbol
            SimpleRenderer renderer = new SimpleRenderer(customSymbol);
            return _trainLayer = new FeatureLayer(_featureTable) { Name = "Treintjes", Renderer = renderer };
        }

        private Polyline ConvertGeoJsonLineStringToEsriPolyline(JArray coordinates)
        {
            var pointList = new List<MapPoint>();

            foreach (var coord in coordinates)
            {
                double x = (double)coord[0];
                double y = (double)coord[1];
                MapPoint mapPoint = new MapPoint(x, y, SpatialReferences.Wgs84);
                pointList.Add(mapPoint);
            }

            return new Polyline(pointList);
        }
    }
}


