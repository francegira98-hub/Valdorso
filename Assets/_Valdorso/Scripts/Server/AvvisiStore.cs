using System;
using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.Server
{
    /// <summary>Un avviso appeso a una bacheca: un incarico della Gilda o un proclama del Balivo.</summary>
    [Serializable]
    public class AvvisoRecord
    {
        public string id;
        public string titolo;
        public string firma;          // chi lo chiede o chi lo proclama (es. "Il mugnaio Berto", "Il Balivo")
        public string testo;
        public string ricompensa;     // solo per gli incarichi (per ora un testo: "20 monete d'argento")
        public string creato;         // data ISO
        public string scadenza;       // data ISO, vuota = nessuna scadenza
        public int grado;             // 0 comune (ceralacca rossa), 1 bronzo, 2 argento, 3 oro
        public int posti = 1;         // quante persone possono accettarlo (0 = nessun limite)
        public List<string> accettatoDa = new List<string>(); // id dei personaggi

        public bool Scaduto => !string.IsNullOrEmpty(scadenza) &&
                               DateTime.TryParse(scadenza, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime s) &&
                               DateTime.UtcNow > s.ToUniversalTime();

        public bool Pieno => posti > 0 && accettatoDa.Count >= posti;
    }

    [Serializable]
    public class ArchivioAvvisi
    {
        public List<AvvisoRecord> avvisi = new List<AvvisoRecord>();
    }

    /// <summary>
    /// Gli avvisi di ogni bacheca, salvati sul server in Bacheche/&lt;nome&gt;.json (es. Gilda, Balivo).
    /// Li scrive per ora la finestra del server (Valdorso → Server → Bacheche); più avanti l'addetto della Gilda.
    /// </summary>
    public static class AvvisiStore
    {
        static string Percorso(string archivio) => System.IO.Path.Combine("Bacheche", archivio + ".json");

        public static ArchivioAvvisi Carica(string archivio)
        {
            return ServerStorage.TryReadJson(Percorso(archivio), out ArchivioAvvisi a) && a != null ? a : new ArchivioAvvisi();
        }

        public static void Salva(string archivio, ArchivioAvvisi a) => ServerStorage.WriteJson(Percorso(archivio), a);

        public static string NuovoId() => Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}
