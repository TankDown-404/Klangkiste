namespace Klangkiste
{
    public enum PlayerProfil
    {
        WindowsStandard,
        Benutzerdefiniert,
        VLC,
        Winamp,
        Foobar2000
    }

    // Art der Übergabe an den Player (befüllt das Argument-Template als Vorlage).
    public enum PlayerUebergabe
    {
        Playlist,
        DateienDirekt,
        Einzeldatei
    }

    public class PlayerEinstellung
    {
        public PlayerProfil Profil { get; set; } = PlayerProfil.WindowsStandard;
        public string ExePfad { get; set; } = string.Empty;

        // Argument-Template mit Platzhaltern: {playlist}, {file}, {first}.
        // Leer = Standardvorlage des jeweiligen Profils verwenden.
        public string Argumente { get; set; } = string.Empty;

        public PlayerUebergabe Uebergabe { get; set; } = PlayerUebergabe.Playlist;

                                public static string StandardVorlage(PlayerProfil profil, PlayerUebergabe uebergabe)
        {
            return uebergabe switch
            {
                // Dateien direkt: je Profil passender Schalter, sonst nur die Dateien.
                PlayerUebergabe.DateienDirekt => profil switch
                {
                    // /PLAY startet die Wiedergabe der übergebenen Dateien.
                    PlayerProfil.Winamp => "/PLAY {file}",
                    PlayerProfil.Foobar2000 => "{file}",
                    _ => "{file}"
                },
                PlayerUebergabe.Einzeldatei => profil switch
                {
                    PlayerProfil.Winamp => "/PLAY {first}",
                    PlayerProfil.Foobar2000 => "{first}",
                    _ => "{first}"
                },
                                _ => profil switch
                {
                    // --started-from-file sorgt dafür, dass eine bereits laufende VLC-Instanz
                    // die Playlist übernimmt UND die .m3u in ihre Einzeltitel auflöst. Ohne
                    // diesen Schalter landet bei laufendem VLC nur die .m3u-Datei als EIN
                    // Eintrag in der Playlist.
                    PlayerProfil.VLC => "--started-from-file --playlist-autostart \"{playlist}\"",
                    PlayerProfil.Winamp => "\"{playlist}\"",
                    PlayerProfil.Foobar2000 => "\"{playlist}\"",
                    _ => "\"{playlist}\""
                }
            };
        }

        // Vorlage zum ANHÄNGEN an die laufende Playlist (ohne die Wiedergabe zu starten).
        // Je Profil der passende Enqueue-Schalter; greift nur bei "Dateien direkt",
        // da Playlists nicht zuverlässig angehängt werden können.
                public static string AnhaengeVorlage(PlayerProfil profil)
        {
            return profil switch
            {
                // Winamp 2.x kennt kein /ENQUEUE (das gibt es erst ab Winamp 5).
                // Anhängen ohne Abspielen erfolgt dort über /ADD. Bei Winamp 5 ist
                // /ADD ebenfalls gültig, daher ist es für beide Versionen passend.
                PlayerProfil.Winamp => "/ADD {file}",
                                PlayerProfil.Foobar2000 => "/add {file}",
                // --started-from-file löst VLCs Einzelinstanz-/IPC-Logik aus. Ohne diesen
                // Schalter startet VLC beim direkten Process.Start oft eine ZWEITE Instanz,
                // statt an die laufende anzudocken. --playlist-enqueue hängt dann an.
                PlayerProfil.VLC => "--started-from-file --playlist-enqueue {file}",
                _ => "{file}"
            };
        }

        // Liefert die tatsächlich zu verwendende Vorlage (gesetzt oder Standard).
        public string ErmittleVorlage()
        {
            return string.IsNullOrWhiteSpace(Argumente)
                ? StandardVorlage(Profil, Uebergabe)
                : Argumente;
        }

        // Liefert die Vorlage für eine Übergabe; beim Anhängen wird die Enqueue-Vorlage
        // verwendet (eigene Vorlage greift hier nicht, da sie meist auf Playlist basiert).
        public string ErmittleVorlage(bool anhaengen)
        {
            return anhaengen ? AnhaengeVorlage(Profil) : ErmittleVorlage();
        }

        public PlayerEinstellung Kopie()
        {
            return new PlayerEinstellung
            {
                Profil = Profil,
                ExePfad = ExePfad,
                Argumente = Argumente,
                Uebergabe = Uebergabe
            };
        }
    }
}
