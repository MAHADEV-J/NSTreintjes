using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Mapping.Popups;
using Esri.ArcGISRuntime.UI.Controls;
using ArcGIS.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DisplayAMap
{
    internal class ClickHandler
    {
        public async void MyFeatureLayer_GeoViewTapped(object? sender, GeoViewInputEventArgs e, MapView mainMapView, Map map)
        {
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
                        // Get the clicked feature
                        Feature clickedFeature = result.GeoElements.First() as Feature;

                        // Check if the clicked feature belongs to the FeatureLayer
                        if (clickedFeature != null && clickedFeature.FeatureTable == featureLayer.FeatureTable)
                        {
                            IDictionary<string, object?> attributes = clickedFeature.Attributes;

                            // Display attribute information (you can use a dialog, tooltip, etc.)
                            DisplayAttributeInformation(attributes, mainMapView, map);
                        }
                    }
                }
            }
        }

        private void DisplayAttributeInformation(IDictionary<string, object> attributes, MapView mainMapView, Map map)
        {
            // Implement the logic to display attribute information (e.g., update UI elements)
            // You can use WPF controls, Windows Forms, or any other UI framework you are using
            // For simplicity, you can use Console.WriteLine to print the information in the console.
            foreach (var attribute in attributes)
            {
                // Create a PopupDefinition object
                var popupDef = new PopupDefinition() {
                    Append = true;
                    FieldName = attribute.Key,
                    Label = attribute.Value.ToString(),
                    IsVisible = true

                };
                // Get the command to open the attribute table
                var openTableBtnCmd = FrameworkApplication.GetPlugInWrapper("esri_editing_table_openTablePaneButton") as ICommand;
                var test = map.OfType<MapMember>().
                if (openTableBtnCmd != null)
                {
                    // Let ArcGIS Pro do the work for us
                    if (openTableBtnCmd.CanExecute(null))
                    {
                        openTableBtnCmd.Execute(null);
                    }
                }
            }
        }
    }
}
