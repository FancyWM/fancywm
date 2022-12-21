using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FancyWM.Models
{
    public class AppState
    {
        public IObservableFileEntity<Settings> Settings { get; }

        public AppState()
        {
            Settings = new ObservableJsonEntity<Settings>(Path.GetFullPath("settings.json"), 
                () => new Settings
                {
                    AutoCollapsePanels = true,
                }, 
                new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    WriteIndented = true,
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Converters =
                    {
                        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                    }
                });
        }
    }
}
