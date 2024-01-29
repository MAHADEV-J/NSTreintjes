using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Symbology;
using System.Drawing;
using System.Diagnostics;

namespace DisplayAMap
{
    internal class LayerHandler : DataHandler
    {
        private string? _gdbPath;
        private string? _directoryPath;
        static int i = 0;

        internal static async Task ChangeFeatureIcon(Feature feature, FeatureLayer layer, string color)
        {
            try
            {
                // Get the base directory of the application
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Combine the base directory with the relative path to your custom icon
                string iconRelativePath = "TrainIcon" + color + ".png";
                string iconPath = Path.Combine(baseDirectory, iconRelativePath);
                feature.Attributes["color"] = color;
                PictureMarkerSymbol customSymbol = new PictureMarkerSymbol(new Uri(iconPath));

                UniqueValueRenderer renderer = (UniqueValueRenderer)layer.Renderer;

                lock (renderer.UniqueValues)
                {
                    if (!renderer.UniqueValues.Any(uv => uv.Label == color))
                    {
                        renderer.UniqueValues.Add(new UniqueValue("color", color, customSymbol, color));
                    }
                }
                Debug.WriteLine(i + "Aantal wel goed");
                layer.FeatureTable.UpdateFeatureAsync(feature);
                i++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        internal static async Task<FeatureLayer> CreateTrainIcons(string trainInfo)
        {
            // Get the base directory of the application
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Combine the base directory with the relative path to your custom icon
            string iconRelativePath = "TrainIcon.png";
            string iconPath = Path.Combine(baseDirectory, iconRelativePath);

            PictureMarkerSymbol customSymbol = new PictureMarkerSymbol(new Uri(iconPath));
            // Create a SimpleRenderer with the custom symbol
            UniqueValueRenderer renderer = new UniqueValueRenderer();
            renderer.DefaultSymbol = customSymbol;
            renderer.FieldNames.Add("color");
            return new FeatureLayer(_featureTable) { Name = "Treintjes", Renderer = renderer };
        }

        public async Task<FeatureLayer> CreateOrFetchTracks()
        {
            FeatureTable featureTable;
            // Retrieve the FeatureLayer based on the table name or create a new one;
            if ((featureTable = _geodatabase.GeodatabaseFeatureTables.FirstOrDefault(table => table.TableName == "TrainTracks")) == null)
            {
                return _trackLayer = await CreateTracklayer();
            }
            else
            {
                featureTable = _geodatabase.GeodatabaseFeatureTables.FirstOrDefault(table => table.TableName == "TrainTracks");

                return new FeatureLayer(featureTable);
            }
        }

        internal async Task CreateOrPurgeGeodatabase()
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
            tableDescription.FieldDescriptions.Add(new FieldDescription("oid", FieldType.Int32));
            tableDescription.FieldDescriptions.Add(new FieldDescription("color", FieldType.Text));
            tableDescription.FieldDescriptions.Add(new FieldDescription("snelheid", FieldType.Float64));
            tableDescription.FieldDescriptions.Add(new FieldDescription("richting", FieldType.Float64));
            tableDescription.FieldDescriptions.Add(new FieldDescription("clicked", FieldType.Text));
            tableDescription.FieldDescriptions.Add(new FieldDescription("delayInSeconds", FieldType.Int32));
            tableDescription.FieldDescriptions.Add(new FieldDescription("plannedTime", FieldType.Text));  // Assuming PlannedTime is a date
            tableDescription.FieldDescriptions.Add(new FieldDescription("actualTime", FieldType.Text));   // Assuming ActualTime is a date
            tableDescription.FieldDescriptions.Add(new FieldDescription("cancelled", FieldType.Text)); // Assuming Cancelled is a boolean
            tableDescription.FieldDescriptions.Add(new FieldDescription("crowdForecast", FieldType.Text));
            tableDescription.FieldDescriptions.Add(new FieldDescription("numberOfSeats", FieldType.Int32));
            tableDescription.FieldDescriptions.Add(new FieldDescription("nextStop", FieldType.Text));

            // Add a new table to the geodatabase by creating one from the table description.
            _featureTable = await _geodatabase.CreateTableAsync(tableDescription);
        }

        internal static async Task<FeatureLayer> CreateTracklayer()
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

        internal static Polyline ConvertGeoJsonLineStringToEsriPolyline(JArray coordinates)
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
