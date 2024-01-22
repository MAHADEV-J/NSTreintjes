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
        internal static FeatureLayer? _trackLayer;
        internal static GeodatabaseFeatureTable? _featureTable;
        internal static Geodatabase? _geodatabase;
        int fetchCounter = 0;

        public class TrainInfo
        {
            public int? TreinNummer { get; set; }
            public string? RitId { get; set; }
            public double? Lat { get; set; }
            public double? Lng { get; set; }
            public double? Snelheid { get; set; }
            public double? Richting { get; set; }
            public double? HorizontaleNauwkeurigheid { get; set; }
            public string? Type { get; set; }
            public string? Bron { get; set; }
        }

        public class Payload
        {
            public List<TrainInfo>? Treinen { get; set; }
        }
        public class RootObject
        {
            public Payload? Payload { get; set; }
        }

        public async Task KeepUpdatingTrains(object? state, FeatureLayer trainLayer, FeatureQueryResult trainFeatures, FeatureQueryResult trackFeatures, MapView mapView)
        {
            await Task.Run(async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                if (fetchCounter == 5)
                {
                    // Process the train data and update the features
                    await ProcessTrainInfo(NSAPICalls.GetTrainData(), mapView);
                }
                else
                {
                    foreach (var feature in trainFeatures)
                    {
                        feature.Geometry = AdjustTrainToTrack(trackFeatures, CalculateTrainMovement(feature, stopwatch));

                        if (ClickedFeature(feature))
                        {
                            await AdjustGraphics(feature, mapView);
                        }
                        stopwatch.Restart();

                        // Use Dispatcher.InvokeAsync to update UI components
                        await trainLayer.FeatureTable.UpdateFeatureAsync(feature);
                    }
                }
                if (fetchCounter == 5)
                {
                    fetchCounter = 0;
                }
            });
            fetchCounter++;
        }

        private Geometry CalculateTrainMovement(Feature feature, Stopwatch stopwatch)
        {
            double millisecondsElapsed = stopwatch.ElapsedMilliseconds;
            double timeFactor = millisecondsElapsed / 10000.0;

            double speedKmPerHour = (double)feature.Attributes["snelheid"];
            double speedMetersPerSecond = speedKmPerHour / 3.6;

            double angleInRadians = (Math.PI / 180.0) * (double)feature.Attributes["richting"];

            double deltaX = (speedMetersPerSecond * timeFactor) * Math.Cos(angleInRadians);
            double deltaY = (speedMetersPerSecond * timeFactor) * Math.Sin(angleInRadians);

            Geometry? currentGeometry = feature.Geometry;
            return GeometryEngine.Move(currentGeometry, deltaX, deltaY);
        }

        private Geometry AdjustTrainToTrack(FeatureQueryResult features, Geometry newGeometry)
        {
            // Order track features by distance to the new train location
            Feature? nearestTrackFeature = features
                .Where(trackFeature => GeometryEngine.Distance(newGeometry, trackFeature.Geometry) <= 5)
                .OrderBy(trackFeature => GeometryEngine.Distance(newGeometry, trackFeature.Geometry)).First();

            // Find the nearest coordinate on the track to the new train location
            ProximityResult? proximityResult = GeometryEngine.NearestCoordinate(nearestTrackFeature.Geometry, newGeometry as MapPoint);
            MapPoint? nearestPointOnTrack = proximityResult.Coordinate;

            return nearestPointOnTrack;
        }


        public async Task<FeatureLayer> ProcessTrainInfo(string trainInfo, MapView? mapView)
        {
            RootObject? rootObject = JsonConvert.DeserializeObject<RootObject>(trainInfo);

            var attributesMapping = new Dictionary<string, Func<int, object?>>
            {
                ["treinNummer"] = i => (Int32?)rootObject.Payload.Treinen[i].TreinNummer,
                ["ritId"] = i => rootObject.Payload.Treinen[i].RitId,
                ["snelheid"] = i => (double?)rootObject.Payload.Treinen[i].Snelheid,
                ["richting"] = i => rootObject.Payload.Treinen[i].Richting
            };

            FeatureLayer? layer = _featureTable.Layer as FeatureLayer;

            for (int i = 0; i < rootObject.Payload.Treinen.Count; i++)
            {
                var attributes = attributesMapping.ToDictionary(kv => kv.Key, kv => kv.Value(i));

                double lat = Convert.ToDouble(rootObject.Payload.Treinen[i].Lat);
                double lng = Convert.ToDouble(rootObject.Payload.Treinen[i].Lng);
                MapPoint pointGeometry = new MapPoint(lng, lat, SpatialReferences.Wgs84);

                Feature? feature;

                if (layer == null)
                {
                    // Layer doesn't exist, create new features
                    feature = _featureTable.CreateFeature(attributes, pointGeometry);
                    feature.SetAttributeValue("clicked", "false");

                    // Add the feature to the feature table.
                    await _featureTable.AddFeatureAsync(feature);
                }
                else
                {
                    // Layer exists, update existing features
                    QueryParameters query = new QueryParameters
                    {
                        WhereClause = $"treinNummer = {rootObject.Payload.Treinen[i].TreinNummer}"
                    };

                    var result = await _featureTable.QueryFeaturesAsync(query);

                    feature = result.FirstOrDefault();

                    if (feature != null)
                    {
                        foreach (var attribute in attributes)
                        {
                            feature.Attributes[attribute.Key] = attribute.Value;
                        }
                        feature.Geometry = pointGeometry;
                        if (ClickedFeature(feature))
                        {
                            await AdjustGraphics(feature, mapView);
                        }
                        await layer.FeatureTable.UpdateFeatureAsync(feature);
                    }
                }
            }
            if (layer == null)
            {
                return await LayerHandler.CreateTrainIcons(trainInfo);
            }
            else
            {
                return null;
            }
        }

        private async Task AdjustGraphics(Feature feature, MapView? mapView)
        {
            await mapView.Dispatcher.InvokeAsync(async () => 
            {
                GraphicsOverlay? graphicsOverlay = mapView.GraphicsOverlays.FirstOrDefault();
                GraphicCollection graphics = graphicsOverlay.Graphics;

                // Find the TextSymbol and update its text
                var textSymbolGraphic = graphics.FirstOrDefault(graphic => graphic.Symbol is TextSymbol);
                if (textSymbolGraphic?.Symbol is TextSymbol textSymbol)
                {
                    textSymbol.Text = "Richting: " + feature.Attributes["richting"].ToString() + "\nSnelheid: " + Math.Round((double)feature.Attributes["snelheid"], 2) + "\nTreinnummer: " + feature.Attributes["treinNummer"].ToString() + "\n Rit id: " + feature.Attributes["ritId"];
                }
                Graphic table = graphicsOverlay.Graphics.FirstOrDefault(graphic => graphic.Symbol is SimpleMarkerSymbol);
                Graphic textGraphic = graphicsOverlay.Graphics.FirstOrDefault(graphic => graphic.Symbol is TextSymbol);
                (table.Geometry, textGraphic.Geometry) = (feature.Geometry, feature.Geometry);

                // Iterate through all graphics in the overlay

                graphicsOverlay.Graphics.Clear();

                graphicsOverlay.Graphics.Add(table);
                graphicsOverlay.Graphics.Add(textGraphic);
                mapView.UpdateLayout();
            });
        }

        private bool ClickedFeature(Feature feature)
        {
            if (feature.Attributes["clicked"]?.ToString() == "true")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}


