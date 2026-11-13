namespace Klangkiste
{
    public enum MedienformatKategorie
    {
        Audio,
        Video
    }

    public sealed record Medienformat(
        string Endung,
        string Format,
        MedienformatKategorie Kategorie,
        string MetadatenHinweis);

    public static class MedienformatDefinition
    {
        public static IReadOnlyList<Medienformat> Alle { get; } = new[]
        {
            new Medienformat(".mp3", "MPEG Audio Layer III", MedienformatKategorie.Audio, "Titel, Interpret, Album, Jahr, Bitrate und Dauer können verfügbar sein."),
            new Medienformat(".flac", "Free Lossless Audio Codec", MedienformatKategorie.Audio, "Titel, Interpret, Album, Jahr, Bitrate und Dauer können verfügbar sein."),
            new Medienformat(".wav", "Waveform Audio", MedienformatKategorie.Audio, "Metadaten und Dauer hängen vom Inhalt der Datei ab."),
            new Medienformat(".ogg", "Ogg Vorbis", MedienformatKategorie.Audio, "Titel, Interpret, Album, Jahr, Bitrate und Dauer können verfügbar sein."),
            new Medienformat(".wma", "Windows Media Audio", MedienformatKategorie.Audio, "Metadaten und Dauer hängen vom vorhandenen Tag ab."),
            new Medienformat(".mp4", "MPEG-4-Container", MedienformatKategorie.Video, "Video-Metadaten hängen vom enthaltenen Codec und den Tags ab."),
            new Medienformat(".avi", "Audio Video Interleave", MedienformatKategorie.Video, "Video-Metadaten hängen vom enthaltenen Codec und den Tags ab."),
            new Medienformat(".mkv", "Matroska-Container", MedienformatKategorie.Video, "Video-Metadaten hängen vom enthaltenen Codec und den Tags ab."),
            new Medienformat(".mpg", "MPEG-Video", MedienformatKategorie.Video, "Video-Metadaten hängen vom enthaltenen Codec und den Tags ab."),
            new Medienformat(".mpeg", "MPEG-Video", MedienformatKategorie.Video, "Video-Metadaten hängen vom enthaltenen Codec und den Tags ab.")
        };

        public static IReadOnlySet<string> Audioendungen { get; } = Alle
            .Where(Format => Format.Kategorie == MedienformatKategorie.Audio)
            .Select(Format => Format.Endung)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlySet<string> Videoendungen { get; } = Alle
            .Where(Format => Format.Kategorie == MedienformatKategorie.Video)
            .Select(Format => Format.Endung)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
