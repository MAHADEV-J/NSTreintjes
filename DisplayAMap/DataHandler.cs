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
using static DisplayAMap.ClassAbstractions;
using System.Linq;

namespace DisplayAMap
{
    internal class DataHandler
    {
        internal static FeatureLayer? _trackLayer;
        internal static GeodatabaseFeatureTable? _featureTable;
        internal static Geodatabase? _geodatabase;
        int fetchCounter = 0;

        public async Task KeepUpdatingTrains(object? state, FeatureLayer trainLayer, FeatureQueryResult trainFeatures, FeatureQueryResult trackFeatures, MapView mapView)
        {
            await Task.Run(async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                if (fetchCounter == 5)
                {
                    // Process the train data and update the features
                    await ProcessTrainInfo(NSAPICalls.GetTrainData(), mapView, trainLayer);
                }
                else
                {
                    // Materialize trainFeatures into a list
                    List<Feature> trainFeaturesList = trainFeatures.ToList();

                    Feature? featureClicked = ClickedFeature(trainFeatures);
                    foreach (var feature in trainFeaturesList)
                    {
                        feature.Geometry = AdjustTrainToTrack(trackFeatures, CalculateTrainMovement(feature, stopwatch));

                        if (featureClicked != null && featureClicked == feature)
                        {
                            await AdjustGraphics(feature, mapView);
                        }

                        stopwatch.Restart();
                    }

                    // Use Dispatcher.InvokeAsync to update UI components
                    await trainLayer.FeatureTable.UpdateFeaturesAsync(trainFeaturesList);
                }
                if (fetchCounter == 5)
                {
                    fetchCounter = 0;
                }
            });
            fetchCounter++;
        }

        public async Task<FeatureLayer> ProcessTrainInfo(string trainInfo, MapView? mapView, FeatureLayer? trainLayer)
        {
            Feature? feature;
            FeatureQueryResult? trainFeatures = trainLayer?.FeatureTable?.QueryFeaturesAsync(new QueryParameters() { WhereClause = "1=1" }).Result;


            Feature? featureClicked = (trainFeatures == null) ? null : ClickedFeature(trainFeatures);

            RootObject? rootObject = JsonConvert.DeserializeObject<RootObject>(trainInfo);

            var attributesMapping = new Dictionary<string, Func<int, object?>>
            {
                ["oid"] = i => (Int32?)rootObject.Payload.Treinen[i].TreinNummer,
                ["snelheid"] = i => (double?)rootObject.Payload.Treinen[i].Snelheid,
                ["richting"] = i => rootObject.Payload.Treinen[i].Richting
            };

            for (int i = 0; i < rootObject.Payload.Treinen.Count; i++)
            {
                double lat = Convert.ToDouble(rootObject.Payload.Treinen[i].Lat);
                double lng = Convert.ToDouble(rootObject.Payload.Treinen[i].Lng);
                MapPoint pointGeometry = new MapPoint(lng, lat, SpatialReferences.Wgs84);

                var attributes = attributesMapping.ToDictionary(kv => kv.Key, kv => kv.Value(i));

                if (trainLayer == null)
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
                        WhereClause = $"oid = {rootObject.Payload.Treinen[i].TreinNummer}"
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
                        if (featureClicked != null) ;
                        {
                            if (featureClicked.Attributes["oid"] == feature.Attributes["oid"])
                            {
                                await AdjustGraphics(feature, mapView);
                            }
                        }
                        await trainLayer.FeatureTable.UpdateFeatureAsync(feature);
                    }
                }
            }
            if (trainLayer == null)
            {
                return await LayerHandler.CreateTrainIcons(trainInfo);
            }
            else
            {
                return null;
            }
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

        internal static async Task<Dictionary<string, object?>> ProcessTimetableInfo(string timeTableInfo)
        {
            RootObject? rootObject = JsonConvert.DeserializeObject<RootObject>(timeTableInfo);

            // Find the next STOP
            var nextStop = rootObject?.Payload.Stops.FirstOrDefault(stop => stop.Status == "STOP");

            var attributesMapping = new Dictionary<string, object?>
            {
                ["delayInSeconds"] = nextStop.Arrivals?[0].DelayInSeconds,
                ["plannedTime"] = DateTime.Parse(nextStop.Arrivals?[0].PlannedTime).ToString("HH:mm:ss"),
                ["actualTime"] = DateTime.Parse(nextStop.Arrivals?[0].ActualTime).ToString("HH:mm:ss"),
                ["cancelled"] = nextStop.Arrivals?[0].Cancelled.ToString(),
                ["crowdForecast"] = nextStop.Arrivals?[0].CrowdForecast,
                ["numberOfSeats"] = nextStop.ActualStock?.NumberOfSeats,
                ["nextStop"] = nextStop.Arrivals?[0].Destination.Name
                // Add more attributes as needed
            };

            return attributesMapping;
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
                    textSymbol.Text = "Richting: " + feature.Attributes["richting"].ToString() + "\nSnelheid: " + Math.Round((double)feature.Attributes["snelheid"], 2) + "\noid: " + feature.Attributes["oid"].ToString();
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

        private Feature? ClickedFeature(FeatureQueryResult? features)
        {
            return features.Any(feature => feature.Attributes["clicked"]?.ToString() == "true") ? features.FirstOrDefault(feature => feature.Attributes["clicked"].ToString() == "true") : null;
        }
    }
}


