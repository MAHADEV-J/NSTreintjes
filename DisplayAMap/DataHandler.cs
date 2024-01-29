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
using System.Windows.Documents;

namespace DisplayAMap
{
    internal class DataHandler
    {
        internal static FeatureLayer? _trackLayer;
        internal static GeodatabaseFeatureTable? _featureTable;
        internal static Geodatabase? _geodatabase;
        int fetchCounter = 0;
        static int i = 0;

        public async Task KeepUpdatingTrainPosition(object? state, FeatureLayer trainLayer, FeatureQueryResult trainFeatures, FeatureQueryResult trackFeatures, MapView mapView)
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
                        if (featureClicked != null && featureClicked.Attributes["oid"].Equals(feature?.Attributes["oid"]))
                        {
                            await AdjustClickedGraphics(feature, mapView);
                        }
                        feature.Attributes.Remove("color");
                        stopwatch.Restart();
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

        internal async Task ProcessFeature(Feature feature, FeatureLayer trainLayer)
        {
            RootObject? rootObject = JsonConvert.DeserializeObject<RootObject>(await NSAPICalls.GetTimetableData(feature.Attributes["oid"].ToString()));

            // Find the next STOP
            var nextStop = rootObject?.Payload?.Stops?.FirstOrDefault(stop => stop.Status == "STOP");

            int? delayInSeconds = nextStop?.Arrivals?[0]?.DelayInSeconds ?? null;

            if (delayInSeconds == 0)
            {
                await LayerHandler.ChangeFeatureIcon(feature, trainLayer, "Green");
            }
            if (delayInSeconds >= 1 && delayInSeconds <= 300)
            {
                await LayerHandler.ChangeFeatureIcon(feature, trainLayer, "Chartreuse");
            }
            if (delayInSeconds > 300 && delayInSeconds <= 600)
            {
                await LayerHandler.ChangeFeatureIcon(feature, trainLayer, "Yellow");
            }
            if (delayInSeconds > 600 && delayInSeconds <= 1800)
            {
                await LayerHandler.ChangeFeatureIcon(feature, trainLayer, "Orange");
            }
            if (delayInSeconds > 1800)
            {
                await LayerHandler.ChangeFeatureIcon(feature, trainLayer, "Red");
            }
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
                            if (attribute.Key != "color")
                            {
                                feature.Attributes[attribute.Key] = attribute.Value;
                            }
                        }
                        feature.Geometry = pointGeometry;
                        if (featureClicked != null && featureClicked.Attributes["oid"].Equals(feature?.Attributes["oid"]))
                        {
                            await AdjustClickedGraphics(feature, mapView);
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
                .Where(trackFeature => GeometryEngine.Distance(newGeometry, trackFeature.Geometry) <= 15)
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
                ["delayInSeconds"] = nextStop?.Arrivals?[0]?.DelayInSeconds,
                ["plannedTime"] = DateTime.TryParse(nextStop?.Arrivals?[0]?.PlannedTime, out DateTime plannedTime) ? plannedTime.ToString("HH:mm:ss") : null,
                ["actualTime"] = DateTime.TryParse(nextStop?.Arrivals?[0]?.ActualTime, out DateTime actualTime) ? actualTime.ToString("HH:mm:ss") : null,
                ["cancelled"] = nextStop?.Arrivals?[0]?.Cancelled.ToString(),
                ["crowdForecast"] = nextStop?.Arrivals?[0]?.CrowdForecast,
                ["numberOfSeats"] = nextStop?.ActualStock?.NumberOfSeats,
                ["nextStop"] = nextStop?.Arrivals?[0]?.Destination?.Name
            };

            return attributesMapping;
        }

        private async Task AdjustClickedGraphics(Feature feature, MapView? mapView)
        {
            await mapView.Dispatcher.InvokeAsync(async () =>
            {
                var extraInfo = await ProcessTimetableInfo(await NSAPICalls.GetTimetableData(feature.Attributes["oid"].ToString()));

                foreach (var attrib in extraInfo)
                {
                    feature.Attributes[attrib.Key] = attrib.Value;
                }

                GraphicsOverlay? graphicsOverlay = mapView.GraphicsOverlays.FirstOrDefault();
                GraphicCollection graphics = graphicsOverlay.Graphics;

                // Find the TextSymbol and update its text
                var leftTextSymbol = graphics.FirstOrDefault(graphic => graphic.Symbol is TextSymbol && ((TextSymbol)graphic.Symbol).Text.Contains("Richting", StringComparison.OrdinalIgnoreCase));
                if (leftTextSymbol?.Symbol is TextSymbol leftText)
                {
                    leftText.Text = "Richting:\nSnelheid:\nTreinnummer:\n" + TextHandler.PlacenameHandler(extraInfo["nextStop"].ToString(), 0) + "\nVertraging in \nseconden:\nGeplande tijd\naankomst:\nActuele tijd\naankomst:\nDrukte trein:";
                }
                var rightTextSymbol = graphics.FirstOrDefault(graphic => graphic.Symbol is TextSymbol && !((TextSymbol)graphic.Symbol).Text.Contains("richting", StringComparison.OrdinalIgnoreCase));
                if (rightTextSymbol?.Symbol is TextSymbol rightText)
                {
                    rightText.Text = feature.Attributes["richting"] + "\n" + Math.Round((double)feature.Attributes["snelheid"], 2) + "\n" + feature.Attributes["oid"] + "\n" + TextHandler.PlacenameHandler(extraInfo["nextStop"].ToString(), 1) + "\n" + extraInfo["delayInSeconds"] + "\n\n" + extraInfo["plannedTime"] + "\n\n" + extraInfo["actualTime"] + "\n\n" + extraInfo["crowdForecast"];
                }
                foreach (Graphic graphic in graphics)
                {
                    graphic.Geometry = feature.Geometry;
                }
                mapView.UpdateLayout();
            });
        }

        private Feature? ClickedFeature(FeatureQueryResult? features)
        {
            return features.Any(feature => feature.Attributes["clicked"]?.ToString() == "true") ? features.FirstOrDefault(feature => feature.Attributes["clicked"].ToString() == "true") : null;
        }
    }
}


