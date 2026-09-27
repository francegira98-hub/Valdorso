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
    }

    /// <summary>Server → PC: com'è andata la creazione.</summary>
    public struct CreateCharacterResponse : NetworkMessage
    {
        public bool success;
        public string message;
        public string characterId;
        public string characterName;
    }

    /// <summary>PC → server: "entro nel mondo con questo personaggio".</summary>
    public struct EnterWorldRequest : NetworkMessage
    {
        public string characterId;
    }
}