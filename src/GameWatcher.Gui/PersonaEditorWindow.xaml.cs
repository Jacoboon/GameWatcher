using System.Text.Json;
using System.IO;
using System.Windows;

namespace GameWatcher.Gui;

public partial class PersonaEditorWindow : Window
{
    private readonly string _path;
    public PersonaEditorWindow(string path)
    {
        InitializeComponent();
        _path = path;
        LoadPersona();
    }

    private void LoadPersona()
    {
        try
        {
            if (File.Exists(_path))
            {
                var p = JsonSerializer.Deserialize<Persona>(File.ReadAllText(_path)) ?? new Persona();
                ModelBox.Text = p.Model ?? "";
                VoiceBox.Text = p.Voice ?? "";
                PitchBox.Text = p.Pitch?.ToString() ?? "";
                ReverbBox.Text = p.Reverb?.ToString() ?? "";
            }
        }
        catch { }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var p = new Persona
            {
                Model = ModelBox.Text,
                Voice = VoiceBox.Text,
                Pitch = double.TryParse(PitchBox.Text, out var pitch) ? pitch : null,
                Reverb = double.TryParse(ReverbBox.Text, out var rev) ? rev : null
            };
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to save persona: " + ex.Message);
        }
    }

    private class Persona
    {
        public string? Model { get; set; }
        public string? Voice { get; set; }
        public double? Pitch { get; set; }
        public double? Reverb { get; set; }
    }
}
