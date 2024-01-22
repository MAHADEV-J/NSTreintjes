using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.Symbology;

namespace DisplayAMap
{
    internal class ClickHandler
    {
        public async void MyFeatureLayer_GeoViewTapped(object? sender, GeoViewInputEventArgs e, MapView mainMapView, Map map)
        {
            double x;
            double y;
            // Identify features at the clicked location
            IReadOnlyList<IdentifyLayerResult> identifyResults = await mainMapView.IdentifyLayersAsync(e.Position, 5, false);
            IdentifyLayerResult result = identifyResults.FirstOrDefault();
            foreach (Layer layer in mainMapView.Map.AllLayers)
            {
                if (layer.Name == "Treintjes" && layer is FeatureLayer featureLayer)
                {
                    // Check if there are any identification results
                    if (result != null)
                    {
                        var features = await featureLayer.FeatureTable.QueryFeaturesAsync(new QueryParameters() { WhereClause = "1=1" });

                        Feature clickedFeature = result.GeoElements.First() as Feature;
                        foreach (var feature in features)
                        {
                            feature.Attributes["clicked"] = "false";
                            await featureLayer.FeatureTable.UpdateFeatureAsync(feature);
                        }
                        GraphicsOverlay graphicsOverlay = mainMapView.GraphicsOverlays.FirstOrDefault();
                        // Get the clicked feature

                        // Check if the clicked feature belongs to the FeatureLayer
                        if (clickedFeature != null && clickedFeature.FeatureTable == featureLayer.FeatureTable)
                        {
                            CreateNewGraphicsOverlay(clickedFeature, mainMapView);
                            clickedFeature.SetAttributeValue("clicked", "true");
                        }
                        if (mainMapView.GraphicsOverlays.Count > 1)
                        {
                            mainMapView.GraphicsOverlays.RemoveAt(0);
                        }
                        await featureLayer.FeatureTable.UpdateFeatureAsync(clickedFeature);
                    }
                }
            }

        }

        public static void CreateNewGraphicsOverlay(Feature feature, MapView mainMapView) 
        {
            GraphicsOverlay graphicsOverlay = new GraphicsOverlay();

            // Create a text symbol to display attribute information
            IDictionary<string, object?> arr = feature.Attributes;
            TextSymbol textSymbol = new TextSymbol
            {
                Color = System.Drawing.Color.Black,
                Size = 12,
                FontFamily = "Arial",
                Text = "Richting:" + arr["richting"] + "\n Snelheid: " + Math.Round((double)arr["snelheid"], 2) + "\nTreinnummer: " + arr["treinNummer"] + "\n Rit id: " + arr["ritId"],
                OffsetY = 55,
                Angle = 0
            };

            // Create a text graphic with attribute information
            Graphic textGraphic = new Graphic(feature.Geometry, textSymbol);
            SimpleMarkerSymbol markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Square, System.Drawing.Color.FromArgb(160, 255, 0, 255), 110)
            {
                OffsetY = 55,
            };
            Graphic pointGraphic = new Graphic(feature.Geometry, markerSymbol);
            graphicsOverlay.Graphics.Add(pointGraphic);

            // Add the text graphic to the overlay
            graphicsOverlay.Graphics.Add(textGraphic);

            // Add the overlay to the map view
            mainMapView.GraphicsOverlays.Add(graphicsOverlay);
        }
    }
}
