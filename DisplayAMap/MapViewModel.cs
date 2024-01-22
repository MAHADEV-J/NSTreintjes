using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Esri.ArcGISRuntime.UI.Controls;
using static DisplayAMap.MainWindow;
using Esri.ArcGISRuntime.Data;


namespace DisplayAMap
{
    internal class MapViewModel : INotifyPropertyChanged
    {
        private Timer _repeatingTaskTimer;
        internal static FeatureLayer? _tracks;
        internal static FeatureLayer? _trains;
        public DataHandler _data = new DataHandler();
        public LayerHandler _layer = new LayerHandler();
        public ClickHandler _click = new ClickHandler();


        public MapViewModel()
        {
            SetupMap(NSAPICalls.GetTrainData());
        }

        private async void SetupMap(string trainInfo)
        {
            Map = new Map(BasemapStyle.ArcGISTopographic);
            await _layer.CreateOrPurgeGeodatabase();
            _trains = await _data.ProcessTrainInfo(trainInfo, null);
            _tracks = await _layer.CreateOrFetchTracks();
            Map.OperationalLayers.Add(_tracks);
            Map.OperationalLayers.Add(_trains);
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
            QueryParameters queryParameters = new QueryParameters() { WhereClause = "1=1" };
            _repeatingTaskTimer = new Timer(
                async state =>
                {
                    await _updateSemaphore.WaitAsync();
                    try
                    {
                        await _data.KeepUpdatingTrains(state, _trains ,_trains.FeatureTable.QueryFeaturesAsync(queryParameters).Result, _tracks.FeatureTable.QueryFeaturesAsync(queryParameters).Result, MainMapView);
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
