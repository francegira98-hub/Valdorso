using UnityEngine;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Una bacheca, un albo o un cartello: con E si legge. Leggere non cambia il mondo, quindi non passa dal server
    /// e ognuno legge per conto suo. Il testo per ora si scrive nell'Inspector; più avanti la bacheca della Gilda
    /// mostrerà gli incarichi veri e quella della piazza i proclami del Balivo.
    /// </summary>
    public class Bacheca : Interagibile
    {
        [Header("Bacheca")]
        [Tooltip("Il titolo in cima al foglio (es. Bacheca della Gilda)")]
        [SerializeField] string titolo = "Bacheca";

        [Tooltip("Il testo degli avvisi. Una riga vuota separa un avviso dall'altro")]
        [TextArea(6, 20)]
        [SerializeField] string testo = "";

        protected override void Awake()
        {
            base.Awake();
            if (nome == "l'oggetto") nome = "la bacheca";
        }

        public override string Azione(GameObject chi) => "Leggi " + nome;

        public override bool Locale => true;

        public override void UsaLocale(GameObject chi) => PannelloLettura.Apri(titolo, testo, transform);

        // Leggere non chiede niente al server
        public override void Interagisci(GameObject chi) { }

        /// <summary>Per chi scrive gli avvisi da codice (incarichi, proclami): cambia il testo.</summary>
        public void ImpostaTesto(string nuovoTitolo, string nuovoTesto)
        {
            titolo = nuovoTitolo;
            testo = nuovoTesto;
        }
    }
}
