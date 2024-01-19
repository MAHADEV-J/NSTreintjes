using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

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

        public void KeepUpdatingTrains(object? state, FeatureLayer layer)
        {
            //using (Table table)
            //    foreach (Feature feature in layer)
            //    {
            //        // Assuming you have a feature layer named "iconLayer" and a feature with fields "Latitude", "Longitude", "Speed", and "Direction".
            //        // var feature = iconLayer.GetFeature(featureId);
            //        var currentPoint = feature.GetShape() as MapPoint;

            //        // Retrieve speed and direction from the feature's attributes
            //        double speedKmh = Convert.ToDouble(feature.GetAttributeValue("Speed"));
            //        double direction = Convert.ToDouble(feature.GetAttributeValue("Direction"));

            //        // Convert speed from km/h to m/s
            //        double speedMs = speedKmh * 1000 / 3600;

            //        // Calculate the new coordinates based on speed and direction
            //        double distance = speedMs; // Assuming constant speed over a short time interval
            //        double angleInRadians = direction * Math.PI / 180.0; // Convert direction to radians

            //        double deltaX = distance * Math.Sin(angleInRadians);
            //        double deltaY = distance * Math.Cos(angleInRadians);

            //        // Update the current point with the new coordinates
            //        MapPoint newPoint = new MapPoint(currentPoint.X + deltaX, currentPoint.Y + deltaY, currentPoint.SpatialReference);
            //        feature.SetShape(newPoint);

            //        // Refresh the display
            //        //mapView?.Refresh();
            //    }
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
                attributes["lat"] = rootObject.Payload.Treinen[i].Lat;
                attributes["lng"] = rootObject.Payload.Treinen[i].Lng;
                attributes["snelheid"] = (float)rootObject.Payload.Treinen[i].Snelheid; ;
                attributes["richting"] = rootObject.Payload.Treinen[i].Richting;

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
            return _trainLayer = new FeatureLayer(_featureTable) { Name = "Treintjes" };
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

