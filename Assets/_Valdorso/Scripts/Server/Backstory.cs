using System.Collections.Generic;
using System.Text;

namespace Valdorso.Server
{
    /// <summary>
    /// Le domande del sacerdote nel Registro di Val d'Orso (passo 5.8): le risposte possibili,
    /// il loro piccolo vantaggio (per ora solo descritto: si attiva quando arrivano i sistemi che lo usano)
    /// e le frasi con cui il registro compone il racconto del personaggio.
    /// Serve sia al PC (per le pagine del registro) sia al server (per accettare solo risposte valide).
    /// Nelle frasi: {N} = nome, {o} = o/a, {lo} = lo/la, {gli} = gli/le.
    /// </summary>
    public static class Backstory
    {
        public class Answer
        {
            public string key;
            public string label;        // come si legge nel registro (maschile)
            public string labelFemale;  // se diverso al femminile
            public string hint;         // il piccolo vantaggio, detto in parole semplici
            public string story;        // la frase del racconto

            public string Label(bool female) => female && !string.IsNullOrEmpty(labelFemale) ? labelFemale : label;
        }

        static Answer A(string key, string label, string hint, string story, string labelFemale = null) =>
            new Answer { key = key, label = label, labelFemale = labelFemale, hint = hint, story = story };

        public static readonly Answer[] Homelands =
        {
            A("lago", "Un villaggio di pescatori sul lago", "Nuoti con meno fatica; i pescatori ti trattano da uno di loro.",
                "{N} è nat{o} in un villaggio di pescatori sulle rive del lago, dove si impara a remare prima che a camminare."),
            A("minatori", "Un borgo di minatori sui monti", "Al buio delle grotte vedi un poco meglio e riconosci i filoni di minerale.",
                "{N} è nat{o} in un borgo di minatori sui monti, tra il fumo delle forge e il buio delle gallerie."),
            A("capitale", "I quartieri poveri della capitale", "Riconosci borsaioli e truffatori; la gente di strada si fida di te.",
                "{N} è cresciut{o} nei vicoli poveri della capitale di Aurelia, dove si impara presto a guardarsi le spalle."),
            A("sud", "Le campagne del sud", "Fame e fatica dei lavori nei campi pesano un po' meno.",
                "{N} viene dalle campagne del sud, dove il sole è duro e il lavoro non finisce mai."),
            A("confine", "Le terre di confine, vicino alle rovine", "Conosci una runa degli Antichi.",
                "{N} è nat{o} nelle terre di confine, all'ombra delle rovine degli Antichi, dove i vecchi raccontano storie che nessuno crede più."),
            A("ignoto", "Non lo ricordi: ti sei svegliato nel bosco", "Nessun vantaggio, ma un mistero da scoprire nella valle.",
                "Di {N} nessuno sa da dove venga: è stat{o} trovat{o} nel bosco, senza memoria, con il solo nome sulle labbra.",
                "Non lo ricordi: ti sei svegliata nel bosco")
        };

        public static readonly Answer[] Trades =
        {
            A("contadino", "Contadino", "5 punti nel lavoro dei campi; porti una zappa.", "lavorava la terra come contadin{o}", "Contadina"),
            A("pastore", "Pastore", "5 punti nell'allevamento; porti un bastone da pastore.", "portava le greggi al pascolo sulle colline", "Pastora"),
            A("apprendista", "Apprendista di bottega", "5 punti nell'artigianato; porti un martello da bottega.", "imparava un mestiere come apprendista in una bottega"),
            A("soldato", "Soldato congedato", "5 punti con le armi; porti un vecchio coltello da soldato.", "ha servito nell'esercito della corona, finché non è stat{o} congedat{o}", "Soldatessa congedata"),
            A("servo", "Servo in una casa nobile", "5 punti nel trattare con i nobili; porti una livrea consumata.", "serviva in una casa nobile, imparando i modi dei signori e i loro segreti", "Serva in una casa nobile"),
            A("allievo", "Allievo mancato dell'accademia", "5 punti nella conoscenza arcana; porti un quaderno di appunti.", "studiava all'accademia di magia, finché non ne è stat{o} allontanat{o}", "Allieva mancata dell'accademia"),
            A("orfano", "Orfano cresciuto al tempio", "5 punti nella preghiera; porti un piccolo simbolo sacro.", "è cresciut{o} orfan{o} tra le mura di un tempio", "Orfana cresciuta al tempio"),
            A("mercante", "Mercante caduto in rovina", "5 punti nel commercio; porti una bilancia da mercante.", "commerciava come mercante, finché la sorte non {gli} ha tolto tutto", "Mercante caduta in rovina"),
            A("cacciatore", "Cacciatore", "5 punti nella caccia; porti un coltello da scuoiare.", "viveva di caccia nei boschi", "Cacciatrice"),
            A("marinaio", "Marinaio", "5 punti nella pesca e nella navigazione; porti una canna da pesca.", "navigava sulle navi mercantili del grande mare", "Marinaia")
        };

        public static readonly Answer[] Reasons =
        {
            A("terra", "La terra promessa dalla corona", "Un piccolo aiuto del balivo e il diritto a un pezzo di terra.",
                "È venut{o} nella valle per la terra promessa dalla corona a chi ha il coraggio di restare."),
            A("fuga", "Fuggi da qualcosa, o da qualcuno", "La tua fama, buona o cattiva, cresce più piano. Qualcuno ti cerca.",
                "È venut{o} nella valle per fuggire da qualcosa, o da qualcuno: non ha voluto dire da cosa."),
            A("scomparso", "Cerchi una persona scomparsa", "Una storia da seguire, nascosta nella valle.",
                "È venut{o} nella valle per cercare una persona scomparsa, di cui porta ancora il ricordo."),
            A("sogno", "Un sogno, o una visione", "A volte i tuoi sogni anticipano qualcosa di vero.",
                "È venut{o} nella valle seguendo un sogno: un'ombra enorme tra gli alberi, e un cuore che batte."),
            A("debito", "Un debito da saldare", "Più Aureus all'inizio, ma un creditore che viene a riscuotere.",
                "È venut{o} nella valle per saldare un debito, e il suo creditore non è tipo da dimenticare."),
            A("fede", "La fede ti ha chiamato", "Parti con un po' di favore della tua divinità.",
                "È venut{o} nella valle perché la fede l'ha chiamat{o}.", "La fede ti ha chiamata"),
            A("avventura", "Cerchi avventura", "Scoprire luoghi nuovi ti dà un po' di più.",
                "È venut{o} nella valle in cerca di avventura, come nelle storie che si raccontano davanti al fuoco.")
        };

        public static readonly Answer[] Keepsakes =
        {
            A("anello", "L'anello di tua madre", "Si può dare in pegno per un prestito, e poi riscattare.",
                "Porta con sé l'anello di sua madre, e non lo toglie mai."),
            A("lettera", "Una lettera mai aperta", "Una storia da scoprire, quando avrai il coraggio di aprirla.",
                "Porta con sé una lettera sigillata che non ha mai avuto il coraggio di aprire."),
            A("spada", "La spada spezzata di tuo padre", "Un fabbro può riforgiarla: diventerà un'arma con una storia.",
                "Porta con sé la spada spezzata di suo padre, avvolta in un panno."),
            A("amuleto", "Un amuleto di legno", "Una volta al giorno ti aiuta contro la paura.",
                "Porta al collo un amuleto di legno intagliato a forma d'orso."),
            A("mappa", "Una mappa strappata", "Indica un luogo nascosto della valle; l'altra metà è da trovare.",
                "Porta con sé metà di una vecchia mappa della valle; l'altra metà è perduta."),
            A("libro", "Un libro di preghiere", "Chi lo porta prega meglio; è il primo passo verso la magia della luce.",
                "Porta con sé un libro di preghiere consumato dalle mani.")
        };

        public static readonly Answer[] Fears =
        {
            A("fuoco", "Il fuoco", "Vicino alle fiamme sei un poco più lent{o}, finché non vinci la paura.", "il fuoco"),
            A("acqua", "L'acqua profonda", "Nell'acqua profonda nuoti più piano, finché non vinci la paura.", "l'acqua profonda"),
            A("buio", "Il buio", "Al buio ti stanchi prima, finché non vinci la paura.", "il buio"),
            A("solitudine", "La solitudine", "Da sol{o} recuperi le forze più lentamente, finché non vinci la paura.", "la solitudine"),
            A("morti", "I morti", "Davanti ai non morti tremi un poco, finché non vinci la paura.", "i morti"),
            A("magia", "La magia", "Gli incantesimi ti spaventano un poco, finché non vinci la paura.", "la magia")
        };

        /// <summary>Le quattro coppie del carattere: si sceglie uno dei due lati per ogni coppia.</summary>
        public static readonly Answer[][] Traits =
        {
            new[] { A("onesto", "Onesto", "Ti credono più facilmente.", "onest{o}", "Onesta"),
                    A("furbo", "Furbo", "Mercanteggi e inganni meglio.", "furb{o}", "Furba") },
            new[] { A("coraggioso", "Coraggioso", "L'intimidazione ha meno effetto su di te.", "coraggios{o}", "Coraggiosa"),
                    A("prudente", "Prudente", "Noti prima trappole e pericoli.", "prudente") },
            new[] { A("devoto", "Devoto", "Il favore divino cresce prima.", "devot{o}", "Devota"),
                    A("scettico", "Scettico", "Illusioni e inganni magici fanno meno presa.", "scettic{o}", "Scettica") },
            new[] { A("gentile", "Gentile", "Animali e abitanti timidi si avvicinano.", "gentile"),
                    A("duro", "Duro", "Intimidisci meglio.", "dur{o}", "Dura") }
        };

        public const int MaxStoryLength = 1500;
        public const int MaxFreeStoryLength = 2500;

        public static Answer Find(Answer[] list, string key)
        {
            foreach (Answer a in list)
                if (a.key == key) return a;
            return null;
        }

        /// <summary>Il racconto che il registro scrive con le risposte date.</summary>
        public static string Compose(string name, bool female, string homeland, string trade, string reason,
            string keepsake, string fear, string[] traits)
        {
            var text = new StringBuilder();
            Answer h = Find(Homelands, homeland), t = Find(Trades, trade), r = Find(Reasons, reason),
                k = Find(Keepsakes, keepsake), f = Find(Fears, fear);
            if (h != null) text.Append(h.story).Append(' ');
            if (t != null) text.Append("Prima di venire nella valle ").Append(t.story).Append(". ");
            if (r != null) text.Append(r.story).Append(' ');
            if (k != null) text.Append(k.story).Append(' ');
            if (f != null) text.Append("Il sacerdote annota, a margine, che teme ").Append(f.story).Append(" più di ogni altra cosa. ");

            var adjectives = new List<string>();
            if (traits != null)
                for (int i = 0; i < Traits.Length && i < traits.Length; i++)
                {
                    Answer a = Find(Traits[i], traits[i]);
                    if (a != null) adjectives.Add(a.story);
                }
            if (adjectives.Count > 0)
            {
                string list = adjectives.Count == 1
                    ? adjectives[0]
                    : string.Join(", ", adjectives.GetRange(0, adjectives.Count - 1)) + " e " + adjectives[adjectives.Count - 1];
                text.Append("Chi l'ha conosciut{o} {lo} descrive ").Append(list).Append('.');
            }
            text.Append("\n\nAnno 312 dopo il Crepuscolo: il Cuore ha battuto, e il nome di {N} è scritto nel registro.");
            return Fill(text.ToString(), name, female);
        }

        /// <summary>Sostituisce nome e desinenze nelle frasi e nei vantaggi.</summary>
        public static string Fill(string s, string name, bool female)
        {
            return s.Replace("{N}", string.IsNullOrWhiteSpace(name) ? "Il nuovo colono" : name.Trim())
                    .Replace("{o}", female ? "a" : "o")
                    .Replace("{lo}", female ? "la" : "lo")
                    .Replace("{gli}", female ? "le" : "gli");
        }

        /// <summary>Controlla le risposte arrivate dal PC. Restituisce il motivo del rifiuto, oppure null se vanno bene.</summary>
        public static string Validate(string homeland, string trade, string reason, string keepsake, string fear,
            string[] traits, string story, string freeStory)
        {
            if (Find(Homelands, homeland) == null || Find(Trades, trade) == null || Find(Reasons, reason) == null ||
                Find(Keepsakes, keepsake) == null || Find(Fears, fear) == null)
                return "Il sacerdote non capisce una delle tue risposte.";
            if (traits == null || traits.Length != Traits.Length) return "Il sacerdote non capisce il tuo carattere.";
            for (int i = 0; i < Traits.Length; i++)
                if (Find(Traits[i], traits[i]) == null) return "Il sacerdote non capisce il tuo carattere.";
            if ((story ?? string.Empty).Length > MaxStoryLength) return "Il racconto è troppo lungo per una pagina del registro.";
            if ((freeStory ?? string.Empty).Length > MaxFreeStoryLength) return "La tua storia è troppo lunga per il registro.";
            return null;
        }

        /// <summary>Toglie i caratteri invisibili o di controllo (tranne gli a capo) da un testo scritto dal giocatore.</summary>
        public static string CleanText(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var clean = new StringBuilder(s.Length);
            foreach (char c in s)
                if (c == '\n' || !char.IsControl(c)) clean.Append(c);
            return clean.ToString().Replace("<", "‹").Replace(">", "›").Trim(); // niente comandi di formattazione nascosti
        }
    }
}
