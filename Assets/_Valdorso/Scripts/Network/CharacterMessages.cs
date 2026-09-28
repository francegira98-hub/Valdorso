using Mirror;

namespace Valdorso.Network
{
    // I messaggi dell'anticamera: le "lettere" che PC e server si scambiano
    // dopo l'accesso e prima di entrare nel mondo.

    /// <summary>Un personaggio dell'account, come lo vede l'anticamera.</summary>
    public struct CharacterSummary
    {
        public string id;
        public string name;
        public string faith;            // vuoto = nessuna fede
        public string lastPlayedAt;     // data e ora UTC (formato ISO)
        public string appearanceRecipe; // per mostrare il personaggio sul palco
        public byte[] portrait;         // il ritratto JPG (può mancare)
    }

    /// <summary>PC → server: "quali personaggi ho?"</summary>
    public struct CharacterListRequest : NetworkMessage { }

    /// <summary>Server → PC: l'elenco dei personaggi dell'account.</summary>
    public struct CharacterListResponse : NetworkMessage
    {
        public CharacterSummary[] characters;
        public int maxCharacters;
    }

    /// <summary>PC → server: "scrivi questo personaggio nel registro".</summary>
    public struct CreateCharacterRequest : NetworkMessage
    {
        public string name;             // vuoto = nome provvisorio (finché non c'è la pagina del nome)
        public string faith;            // vuoto = nessuna fede
        public string appearanceRecipe; // vuoto = aspetto casuale al primo ingresso
        public byte[] portrait;         // il ritratto del viso in JPG (può mancare)

        // Le risposte al sacerdote (chiavi di Backstory) e i due testi del registro.
        public string homeland;
        public string formerTrade;
        public string reason;
        public string keepsake;
        public string fear;
        public string[] traits;
        public string story;            // il racconto composto dal registro (ritoccabile)
        public string freeStory;        // "La tua storia", scritta dal giocatore
    }

    /// <summary>Server → PC: com'è andata la creazione.</summary>
    public struct CreateCharacterResponse : NetworkMessage
    {
        public bool success;
        public string message;
        public string characterId;
        public string characterName;
    }

    /// <summary>PC → server: "cancella questo personaggio" (bisogna riscrivere il suo nome).</summary>
    public struct DeleteCharacterRequest : NetworkMessage
    {
        public string characterId;
        public string confirmName;
    }

    /// <summary>Server → PC: com'è andata la cancellazione.</summary>
    public struct DeleteCharacterResponse : NetworkMessage
    {
        public bool success;
        public string message;
    }

    /// <summary>Dove si va lasciando il mondo.</summary>
    public enum LeaveMode : byte
    {
        Characters = 0, // torna ai personaggi (resta collegato)
        Menu = 1,       // torna al menu principale
        Quit = 2        // esce dal gioco
    }

    /// <summary>PC → server: "voglio lasciare la valle" (parte l'attesa, o si esce subito nei luoghi sicuri).</summary>
    public struct LeaveWorldRequest : NetworkMessage
    {
        public LeaveMode mode;
    }

    /// <summary>PC → server: "ci ho ripensato, resto".</summary>
    public struct LeaveWorldCancel : NetworkMessage { }

    /// <summary>Server → PC: a che punto è l'uscita dal mondo.</summary>
    public struct LeaveWorldStatus : NetworkMessage
    {
        public LeaveMode mode;
        public int secondsLeft;
        public bool cancelled;  // annullata: il messaggio dice perché
        public bool done;       // il personaggio è uscito ed è salvato
        public string message;
    }

    /// <summary>PC → server: "entro nel mondo con questo personaggio".</summary>
    public struct EnterWorldRequest : NetworkMessage
    {
        public string characterId;
    }
}