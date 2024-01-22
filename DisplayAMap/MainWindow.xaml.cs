using System.Windows;
using Esri.ArcGISRuntime.UI.Controls;

namespace DisplayAMap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class CopyMainMapView
        {
            public static MapView? Copy;
        }
        public MainWindow()
        {
            InitializeComponent();
            CopyMainMapView.Copy = this.MainMapView;
        }
    }
}