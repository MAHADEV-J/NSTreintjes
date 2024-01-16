using System.Configuration;
using System.Data;
using System.Windows;
using Windows.Services.Maps;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Esri.ArcGISRuntime.UI.Controls;

namespace DisplayAMap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Note: it is not best practice to store API keys in source code.
            // The API key is referenced here for the convenience of this tutorial.
            Esri.ArcGISRuntime.ArcGISRuntimeEnvironment.ApiKey = "AAPKce63513b93824b5dba36b320adccb33c4vElsRF0Pt4dsPsp2KlO4SnAS2WPdiXELtiEmsb6q9dOEyD4oIIsUSBzHph9nZRu";
        }
    }

}
