namespace Klangkiste
{
    public sealed class HilfeThema
    {
        public string Titel { get; }
        public string Inhalt { get; }

        public HilfeThema(string titel, string inhalt)
        {
            Titel = titel;
            Inhalt = inhalt;
        }
    }
}
