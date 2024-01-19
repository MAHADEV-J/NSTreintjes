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
        internal static FeatureLayer _trackLayer;
        internal static FeatureLayer _trainLayer;
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
            _trainLayer = await _data.CreateTrainIcons(trainInfo);
            _trackLayer = await _data.CreateOrFetchTracks();
            Map.OperationalLayers.Add(_trackLayer);
            Map.OperationalLayers.Add(_trainLayer);
            _mainMapView = CopyMainMapView.Copy;
            MainMapView.GeoViewTapped += (sender, e) => _click.MyFeatureLayer_GeoViewTapped(sender, e, MainMapView, Map);
            // To display the map, set the MapViewModel.Map property, which is bound to the map view.
            this.Map = Map;

            // Everything is prepared, time to kick off the main repeating function responsible for making the trains move
            SetupRepeatingTaskTimer();
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

        private readonly SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);

        private void SetupRepeatingTaskTimer()
        {
            _repeatingTaskTimer = new Timer(
                async state =>
                {
                    await _updateSemaphore.WaitAsync();
                    try
                    {
                        await _data.KeepUpdatingTrains(state, _trainLayer, _trackLayer, MainMapView);
                    }
                    finally
                    {
                        _updateSemaphore.Release();
                    }
                },
                null,
                0,
                10000
            );
        }
    }
}
