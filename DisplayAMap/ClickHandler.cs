using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.Symbology;
using System.Drawing;
using Esri.ArcGISRuntime.Geometry;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace DisplayAMap
{
    internal class ClickHandler
    {
        public async void MyFeatureLayer_GeoViewTapped(object? sender, GeoViewInputEventArgs e, MapView mainMapView, QueryParameters query)
        {
            // Identify features at the clicked location
            IReadOnlyList<IdentifyLayerResult?> identifyResults = await mainMapView.IdentifyLayersAsync(e.Position, 5, false);
            IdentifyLayerResult? result = identifyResults.FirstOrDefault(layer => layer.LayerContent.Name == "Treintjes");
            FeatureLayer? featureLayer = (FeatureLayer?)result?.LayerContent;

            // Check if there are any identification results
            if (result != null)
            {
                var features = await featureLayer.FeatureTable.QueryFeaturesAsync(query);

                Feature? clickedFeature = result.GeoElements.First() as Feature;
                Feature? previouslyClicked = features.FirstOrDefault(feature => feature.Attributes["clicked"] == "true");

                // Check if the clicked feature belongs to the FeatureLayer
                if (clickedFeature != null && clickedFeature.FeatureTable == featureLayer.FeatureTable)
                {
                    await CreateNewGraphicsOverlay(clickedFeature, mainMapView);
                    clickedFeature.SetAttributeValue("clicked", "true");
                }
                if (mainMapView.GraphicsOverlays.Count > 1)
                {
                    mainMapView.GraphicsOverlays.RemoveAt(0);
                }
                await featureLayer.FeatureTable.UpdateFeatureAsync(clickedFeature);
            }
        }

        public async static Task CreateNewGraphicsOverlay(Feature feature, MapView mainMapView)
        {
            var extraInfo = await DataHandler.ProcessTimetableInfo(await NSAPICalls.GetTimetableData(feature.Attributes["oid"].ToString()));

            foreach (var attrib in extraInfo)
            {
                feature.Attributes[attrib.Key] = attrib.Value;
            }

            GraphicsOverlay graphicsOverlay = new GraphicsOverlay();

            SimpleMarkerSymbol squareSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Square, System.Drawing.Color.FromArgb(255, 255, 255, 255), 190)
            {
                OffsetY = 130,
            };

            SimpleMarkerSymbol triangleSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Triangle, System.Drawing.Color.FromArgb(255, 255, 255, 255), 25)
            {
                Angle = 180,
                OffsetY = -30,
            };

            // Create a CompositeSymbol for the main content
            CompositeSymbol compositeSymbol = new CompositeSymbol();
            compositeSymbol.Symbols.Add(squareSymbol);
            compositeSymbol.Symbols.Add(triangleSymbol);

            Graphic boxGraphic = new Graphic(feature.Geometry, compositeSymbol);
            graphicsOverlay.Graphics.Add(boxGraphic);

            // Create a text symbol to display attribute information
            IDictionary<string, object?> arr = feature.Attributes;

            TextSymbol leftTextSymbol = new TextSymbol
            {
                Color = System.Drawing.Color.Black,
                Size = 12,
                FontFamily = "Arial",
                Text = "Richting:\nSnelheid:\nTreinnummer:\n" + TextHandler.PlacenameHandler(feature.Attributes["nextStop"].ToString(), 0) + "\nVertraging in \nseconden:\nGeplande tijd\naankomst:\nActuele tijd\naankomst:\nDrukte trein:",
                Angle = 0,
                OffsetY = 130,
                OffsetX = -80,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            TextSymbol rightTextSymbol = new TextSymbol
            {
                Color = System.Drawing.Color.Black,
                Size = 12,
                FontFamily = "Arial",
                Angle = 0,
                OffsetY = 130,
                OffsetX = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            if (feature.Attributes["nextStop"] != null)
            {
                rightTextSymbol.Text = arr["richting"] + "\n" + Math.Round((double)arr["snelheid"], 2) + "\n" + arr["oid"] + "\n" + TextHandler.PlacenameHandler(feature.Attributes["nextStop"].ToString(), 1) + "\n" + feature.Attributes["delayInSeconds"] + "\n\n" + feature.Attributes["plannedTime"] + "\n\n" + feature.Attributes["actualTime"] + "\n\n" + feature.Attributes["crowdForecast"];
            }
            else
            {
                rightTextSymbol.Text = arr["richting"] + "\n" + Math.Round((double)arr["snelheid"], 2) + "\n" + arr["oid"] + "\nEindhalte bereikt\nN.v.T\n\nN.v.t.\n\nN.v.t.\n\nN.v.t.";
            }

            // Create a text graphic with attribute information
            Graphic leftTextGraphic = new Graphic(feature.Geometry, leftTextSymbol);
            Graphic rightTextGraphic = new Graphic(feature.Geometry, rightTextSymbol);

            // Add the text graphic to the overlay
            rightTextGraphic.Attributes["Type"] = "Text";
            graphicsOverlay.Graphics.Add(leftTextGraphic);
            graphicsOverlay.Graphics.Add(rightTextGraphic);

            mainMapView.GraphicsOverlays.Add(graphicsOverlay);

        }
    }
}


