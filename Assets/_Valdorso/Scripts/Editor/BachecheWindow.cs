using System;
using System.Collections.Generic;
using Mirror;
using UnityEditor;
using UnityEngine;
using Valdorso.Interazione;
using Valdorso.Server;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Scrivere e togliere gli avvisi delle bacheche (per ora a mano dal server; più avanti lo farà l'addetto della Gilda).
    /// Menu: Valdorso → Server → Bacheche. Si sceglie l'archivio (Gilda o Balivo), si scrive e si salva.
    /// Se il gioco è in Play come Host o server, "Salva" aggiorna subito le bacheche nel mondo.
    /// </summary>
    public class BachecheWindow : EditorWindow
    {
        static readonly string[] Archivi = { "Gilda", "Balivo" };
        static readonly string[] Gradi = { "Comune (ceralacca rossa)", "Bronzo", "Argento", "Oro" };

        int scelta;
        ArchivioAvvisi archivio;
        Vector2 scorri;
        bool modificato;

        [MenuItem("Valdorso/Server/Bacheche")]
        static void Apri() => GetWindow<BachecheWindow>("Bacheche").Show();

        void OnEnable() => Carica();

        void Carica()
        {
            archivio = AvvisiStore.Carica(Archivi[scelta]);
            modificato = false;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4);
            int nuova = GUILayout.Toolbar(scelta, new[] { "Gilda: incarichi", "Balivo: proclami" });
            if (nuova != scelta)
            {
                if (modificato && !EditorUtility.DisplayDialog("Bacheche", "Ci sono modifiche non salvate. Cambiare bacheca lo stesso?", "Sì", "No"))
                    return;
                scelta = nuova;
                Carica();
            }
            bool incarichi = scelta == 0;

            EditorGUILayout.HelpBox(incarichi
                ? "Ogni incarico è un foglio sulla bacheca della Gilda. Quando i posti sono pieni (o è scaduto) il foglio non si vede più, ma resta qui."
                : "Ogni proclama è un foglio sulla bacheca davanti alla casa del Balivo.", MessageType.None);

            scorri = EditorGUILayout.BeginScrollView(scorri);
            AvvisoRecord daTogliere = null;
            foreach (AvvisoRecord a in archivio.avvisi)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUI.BeginChangeCheck();
                a.titolo = EditorGUILayout.TextField("Titolo", a.titolo);
                a.firma = EditorGUILayout.TextField(incarichi ? "Chi lo chiede" : "Firma", a.firma);
                EditorGUILayout.LabelField("Testo");
                a.testo = EditorGUILayout.TextArea(a.testo, GUILayout.MinHeight(60));
                if (incarichi)
                {
                    a.ricompensa = EditorGUILayout.TextField("Ricompensa", a.ricompensa);
                    a.grado = EditorGUILayout.Popup("Grado", a.grado, Gradi);
                    a.posti = Mathf.Max(1, EditorGUILayout.IntField("Posti", a.posti));
                    EditorGUILayout.LabelField("Accettato da", $"{a.accettatoDa.Count} personaggi" + (a.Pieno ? " (pieno: non si vede più)" : ""));
                }
                int giorni = GiorniAllaScadenza(a);
                int nuoviGiorni = EditorGUILayout.IntField("Scade tra (giorni, 0 = mai)", giorni);
                if (nuoviGiorni != giorni)
                    a.scadenza = nuoviGiorni <= 0 ? "" : DateTime.UtcNow.AddDays(nuoviGiorni).ToString("o");
                if (EditorGUI.EndChangeCheck()) modificato = true;

                EditorGUILayout.BeginHorizontal();
                if (incarichi && a.accettatoDa.Count > 0 && GUILayout.Button("Svuota chi l'ha accettato", GUILayout.Width(190)))
                {
                    a.accettatoDa.Clear();
                    modificato = true;
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Togli dalla bacheca", GUILayout.Width(150))) daTogliere = a;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            if (daTogliere != null)
            {
                archivio.avvisi.Remove(daTogliere);
                modificato = true;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(incarichi ? "Nuovo incarico" : "Nuovo proclama"))
            {
                archivio.avvisi.Add(new AvvisoRecord
                {
                    id = AvvisiStore.NuovoId(),
                    titolo = incarichi ? "Nuovo incarico" : "Nuovo proclama",
                    firma = incarichi ? "" : "Il Balivo di Val d'Orso",
                    testo = "",
                    creato = DateTime.UtcNow.ToString("o"),
                    posti = incarichi ? 1 : 0,
                });
                modificato = true;
            }
            if (GUILayout.Button("Aggiungi esempi")) { AggiungiEsempi(incarichi); modificato = true; }
            GUI.enabled = modificato;
            if (GUILayout.Button(modificato ? "Salva *" : "Salva"))
            {
                AvvisiStore.Salva(Archivi[scelta], archivio);
                modificato = false;
                if (Application.isPlaying && NetworkServer.active) BachecaAvvisi.ServerRicaricaTutte();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        static int GiorniAllaScadenza(AvvisoRecord a)
        {
            if (string.IsNullOrEmpty(a.scadenza) || !DateTime.TryParse(a.scadenza, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime s))
                return 0;
            return Mathf.Max(1, Mathf.CeilToInt((float)(s.ToUniversalTime() - DateTime.UtcNow).TotalDays));
        }

        void AggiungiEsempi(bool incarichi)
        {
            string ora = DateTime.UtcNow.ToString("o");
            string tra(int g) => DateTime.UtcNow.AddDays(g).ToString("o");
            var esempi = incarichi
                ? new List<AvvisoRecord>
                {
                    new AvvisoRecord { titolo = "Lupi al mulino", firma = "Berto, il mugnaio",
                        testo = "Da tre notti un branco di lupi gira intorno al mulino sulla riva est. Hanno già preso due galline e il cane non smette di abbaiare. Cerco gente coraggiosa che li allontani dal fiume.",
                        ricompensa = "15 monete d'argento e un sacco di farina", grado = 1, posti = 3, scadenza = tra(10) },
                    new AvvisoRecord { titolo = "Scorta al carro del fabbro", firma = "Mastro Aldo, fabbro",
                        testo = "Il mio carro deve portare il ferro fino al posto di guardia del passo. La strada è tranquilla, ma preferisco non andarci da solo.",
                        ricompensa = "8 monete d'argento", grado = 0, posti = 2, scadenza = tra(5) },
                    new AvvisoRecord { titolo = "L'anello perduto", firma = "Marta, la locandiera",
                        testo = "Ho perso l'anello di mia madre lavando i panni al fiume, vicino al ponte. Chi lo ritrova avrà la mia riconoscenza e una cena calda.",
                        ricompensa = "Una cena e una camera per tre notti", grado = 0, posti = 1, scadenza = tra(14) },
                    new AvvisoRecord { titolo = "Le Rovine degli Antichi", firma = "La Gilda degli avventurieri",
                        testo = "Qualcuno ha visto luci tra le Rovine degli Antichi, di notte. La Gilda cerca un gruppo esperto che vada a vedere e riferisca. Non toccate nulla che porti rune.",
                        ricompensa = "40 monete d'argento, divise tra il gruppo", grado = 2, posti = 4, scadenza = tra(20) },
                }
                : new List<AvvisoRecord>
                {
                    new AvvisoRecord { titolo = "Benvenuti, coloni", firma = "Il Balivo di Val d'Orso, per la Corona di Aurelia",
                        testo = "La Corona accoglie chi ha firmato il Registro. Ricordate il Patto: un anno e un giorno nella valle. Chi lavora avrà terra e protezione; chi porta disordine risponderà al Balivo.",
                        posti = 0 },
                    new AvvisoRecord { titolo = "Il fiume attende un nome", firma = "Il Balivo di Val d'Orso",
                        testo = "Il fiume che scende dai monti fino al lago non ha ancora un nome. Il Balivo raccoglie le proposte dei coloni: il nome scelto sarà scritto nelle carte della Corona.",
                        posti = 0, scadenza = tra(10) },
                };
            foreach (AvvisoRecord e in esempi)
            {
                e.id = AvvisiStore.NuovoId();
                e.creato = ora;
                archivio.avvisi.Add(e);
            }
        }
    }
}
