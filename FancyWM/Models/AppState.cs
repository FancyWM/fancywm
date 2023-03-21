using System;
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
            string SettingsDirectory;
            string SettingsFile;

#pragma warning disable CS8600
            SettingsDirectory = Environment.GetEnvironmentVariable("FANCYWM_CONF_DIR");
#pragma warning restore CS8600

            if (SettingsDirectory == null)
                SettingsFile = Path.GetFullPath("settings.json");
            else
                SettingsFile = Path.GetFullPath("settings.json", SettingsDirectory);

            Settings = new ObservableJsonEntity<Settings>(SettingsFile,
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
