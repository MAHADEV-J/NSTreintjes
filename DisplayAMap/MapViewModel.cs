using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.Portal;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Tasks.Offline;
using System.IO;
using System.Windows;
using static DisplayAMap.MainWindow;


namespace DisplayAMap
{
    internal class MapViewModel : INotifyPropertyChanged
    {
        private Timer _repeatingTaskTimer;
        internal static FeatureLayer _layer;
        public DataHandler _data = new DataHandler();
        public ClickHandler _click = new ClickHandler();


        public MapViewModel()
        {
            SetupMap(NSAPICalls.GetTrainData());
        }

        private async void SetupMap(string trainInfo)
        {
            Map = new Map(BasemapStyle.ArcGISTopographic);
            await _data.CreateGeodatabase();
            _layer = await _data.CreateTrainIcons(trainInfo);
            Map.OperationalLayers.Add(_layer);
            _mainMapView = CopyMainMapView.Copy;
            MainMapView.GeoViewTapped += (sender, e) => _click.MyFeatureLayer_GeoViewTapped(sender, e, MainMapView, Map);
            // To display the map, set the MapViewModel.Map property, which is bound to the map view.
            this.Map = Map;
            //_repeatingTaskTimer = new Timer(state => handler.KeepUpdatingTrains(state, _layer), null, 0, 5000);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Map? _map;
        public Map? Map
        {
            get { return _map; }
            set
            {
                _map = value;
                OnPropertyChanged();
            }
        }

        private GraphicsOverlayCollection? _graphicsOverlays;
        public GraphicsOverlayCollection? GraphicsOverlays
        {
            get { return _graphicsOverlays; }
            set
            {
                _graphicsOverlays = value;
                OnPropertyChanged();
            }
        }

        private MapView _mainMapView;
        public MapView MainMapView
        {
            get { return _mainMapView; }
            set
            {
                _mainMapView = value;
                // Perform any additional setup or binding logic if needed
            }
        }
    }
}
